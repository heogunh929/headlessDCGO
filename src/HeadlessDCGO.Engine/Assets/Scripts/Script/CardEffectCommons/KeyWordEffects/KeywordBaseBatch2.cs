namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public enum KeywordBaseBatch2Kind
{
    Rush = 0,
    Blitz = 1,
    Retaliation = 2,
    ArmorPurge = 3,
}

public static class KeywordBaseBatch2Timings
{
    public const string ImmediateAttackPermission = "ImmediateAttackPermission";
    public const string OnEnterFieldAnyone = "OnEnterFieldAnyone";
    public const string OnDestroyedAnyone = "OnDestroyedAnyone";
    public const string WhenRemoveField = "WhenRemoveField";
}

public static class KeywordBaseBatch2Scopes
{
    public const string CanAttackImmediately = "CanAttackImmediately";
    public const string BlitzAttack = "BlitzAttack";
    public const string RetaliationBattleDeletion = "RetaliationBattleDeletion";
    public const string ArmorPurgeReplacement = "ArmorPurgeReplacement";
}

public static class KeywordBaseBatch2ContextKeys
{
    public const string MatchState = "matchState";
    public const string TargetEntityId = "targetEntityId";
    public const string TriggerReason = "triggerReason";
    public const string CanAttack = "canAttack";
    public const string OpponentMemory = "opponentMemory";
    public const string IsAttacking = "isAttacking";
    public const string DeletedByBattle = "deletedByBattle";
    public const string DeletedCardId = "deletedCardId";
    public const string OpponentBattleCardId = "opponentBattleCardId";
    public const string RemovedFromField = "removedFromField";
    public const string RemovedCardId = "removedCardId";
}

// Per-keyword resolution logic is split across partial files (Rush.cs / Blitz.cs / Retaliation.cs /
// ArmorPurge.cs) to mirror the original DCGO CardEffectCommons/KeyWordEffects/<Name>.cs layout. Shared
// scaffolding (enum, timings, dispatch, factory) stays here.
public sealed partial class KeywordBaseBatch2Effect : IHeadlessCardEffect
{
    public KeywordBaseBatch2Effect(
        KeywordBaseBatch2Kind kind,
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null,
        bool isInherited = false,
        bool isLinked = false,
        string? triggerReason = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Keyword kind must be known.");
        }

        if (sourceEntityId.IsEmpty)
        {
            throw new ArgumentException("Source entity id must not be empty.", nameof(sourceEntityId));
        }

        if (targetEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Target entity id must not be empty.", nameof(targetEntityId));
        }

        Kind = kind;
        TargetEntityId = targetEntityId;
        IsInherited = isInherited;
        IsLinked = isLinked;
        TriggerReason = string.IsNullOrWhiteSpace(triggerReason) ? null : triggerReason.Trim();
        Keyword = KeywordBaseBatch2Factory.KeywordName(kind);
        Definition = new CardEffectDefinition(
            KeywordBaseBatch2Factory.EffectId(kind, sourceEntityId),
            sourceEntityId,
            Keyword,
            KeywordBaseBatch2Factory.Timing(kind),
            isOptional: kind is KeywordBaseBatch2Kind.Blitz or KeywordBaseBatch2Kind.Retaliation or KeywordBaseBatch2Kind.ArmorPurge,
            hash: KeywordBaseBatch2Factory.Hash(kind, sourceEntityId, targetEntityId, isInherited, isLinked, TriggerReason));
    }

    public KeywordBaseBatch2Kind Kind { get; }

    public string Keyword { get; }

    public HeadlessEntityId? TargetEntityId { get; }

    public bool IsInherited { get; }

    public bool IsLinked { get; }

    public string? TriggerReason { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.MatchState, out MatchState? state)
            || state is null)
        {
            return Failure("match state is required", "matchState", context);
        }

        HeadlessEntityId targetId = ResolveTargetId(context);
        if (Kind == KeywordBaseBatch2Kind.Retaliation)
        {
            if (!state.CardInstances.TryGetValue(targetId, out CardInstanceState? retaliationTarget))
            {
                return Failure("target was not found", "targetEntityId", context, targetId);
            }

            return CanResolveRetaliation(context, state, retaliationTarget);
        }

        if (!TryGetBattleCard(state, targetId, out CardInstanceState? target)
            || target is null)
        {
            return Failure("target must be on battle area", "targetEntityId", context, targetId);
        }

        CardInstanceState battleTarget = target;
        return Kind switch
        {
            KeywordBaseBatch2Kind.Rush => CanResolveRush(context, battleTarget),
            KeywordBaseBatch2Kind.Blitz => CanResolveBlitz(context, battleTarget),
            KeywordBaseBatch2Kind.ArmorPurge => CanResolveArmorPurge(context, battleTarget),
            _ => Failure("unknown keyword", "keyword", context, targetId),
        };
    }

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();

        CardEffectCanResolveResult check = CanResolve(context);
        if (!check.CanResolve)
        {
            return ValueTask.FromResult(EffectResult.Failure(
                check.Message ?? "Keyword effect cannot resolve.",
                check.Values));
        }

        HeadlessEntityId targetId = ResolveTargetId(context);
        string mutationKind = Kind switch
        {
            KeywordBaseBatch2Kind.Rush => "GrantRush",
            KeywordBaseBatch2Kind.Blitz => "RequestBlitzAttack",
            KeywordBaseBatch2Kind.Retaliation => "DeleteRetaliationTarget",
            KeywordBaseBatch2Kind.ArmorPurge => "ApplyArmorPurge",
            _ => "KeywordBaseBatch2",
        };

        Dictionary<string, object?> values = BaseValues(context, targetId);
        values["mutationKind"] = mutationKind;
        if (Kind == KeywordBaseBatch2Kind.ArmorPurge
            && context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.MatchState, out MatchState? state)
            && state is not null
            && state.CardInstances.TryGetValue(targetId, out CardInstanceState? armorTarget))
        {
            values["purgedSourceId"] = armorTarget.SourceIds.LastOrDefault().Value;
        }

        mutations.Apply(new EffectMutation(mutationKind, Definition.SourceEntityId, values));
        return ValueTask.FromResult(EffectResult.Success($"{Keyword} resolved.", values));
    }

    public EffectBinding ToBinding(
        HeadlessPlayerId controllerId,
        EffectContext context)
    {
        return KeywordBaseBatch2Factory.ToBinding(this, controllerId, context);
    }

    private HeadlessEntityId ResolveTargetId(CardEffectResolveContext context)
    {
        if (TargetEntityId is HeadlessEntityId configuredTarget)
        {
            return configuredTarget;
        }

        if (context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.TargetEntityId, out HeadlessEntityId targetFromValues))
        {
            return targetFromValues;
        }

        return context.EffectContext.TargetEntityIds.Count > 0
            ? context.EffectContext.TargetEntityIds[0]
            : Definition.SourceEntityId;
    }

    private static bool TryGetBattleCard(
        MatchState state,
        HeadlessEntityId cardId,
        out CardInstanceState? card)
    {
        card = null;
        if (!state.CardInstances.TryGetValue(cardId, out CardInstanceState? instance))
        {
            return false;
        }

        PlayerState player = state.GetPlayer(instance.OwnerId);
        if (!player.GetZone(ChoiceZone.BattleArea).Contains(cardId))
        {
            return false;
        }

        card = instance;
        return true;
    }

    private CardEffectCanResolveResult Failure(
        string message,
        string field,
        CardEffectResolveContext context,
        HeadlessEntityId? targetId = null)
    {
        Dictionary<string, object?> values = targetId.HasValue
            ? BaseValues(context, targetId.Value)
            : BaseValues(context, ResolveTargetId(context));
        values["field"] = field;
        return CardEffectCanResolveResult.Failure($"{Keyword} {message}", values);
    }

    private Dictionary<string, object?> BaseValues(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        Dictionary<string, object?> values = BaseValues(context, target.InstanceId);
        values["targetOwnerId"] = target.OwnerId.Value;
        values["targetSourceCount"] = target.SourceIds.Count;
        return values;
    }

    private Dictionary<string, object?> BaseValues(
        CardEffectResolveContext context,
        HeadlessEntityId targetId)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["keyword"] = Keyword,
            ["kind"] = Kind.ToString(),
            ["effectId"] = Definition.EffectId.Value,
            ["sourceEntityId"] = Definition.SourceEntityId.Value,
            ["targetEntityId"] = targetId.Value,
            ["timing"] = Definition.Timing,
            ["isInherited"] = IsInherited,
            ["isLinked"] = IsLinked,
            ["triggerReason"] = TriggerReason,
            ["controllerId"] = context.Request.ControllerId.Value,
        };
    }
}

public static class KeywordBaseBatch2Factory
{
    public static KeywordBaseBatch2Effect Create(
        KeywordBaseBatch2Kind kind,
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null,
        bool isInherited = false,
        bool isLinked = false,
        string? triggerReason = null)
    {
        return new KeywordBaseBatch2Effect(kind, sourceEntityId, targetEntityId, isInherited, isLinked, triggerReason);
    }

    public static IReadOnlyList<KeywordBaseBatch2Effect> CreateAll(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null,
        bool isInherited = false,
        bool isLinked = false)
    {
        return Array.AsReadOnly(Enum
            .GetValues<KeywordBaseBatch2Kind>()
            .Select(kind => Create(kind, sourceEntityId, targetEntityId, isInherited, isLinked, kind == KeywordBaseBatch2Kind.Blitz ? "OnPlay" : null))
            .ToArray());
    }

    public static EffectBinding ToBinding(
        KeywordBaseBatch2Effect effect,
        HeadlessPlayerId controllerId,
        EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(context);
        if (controllerId.IsEmpty)
        {
            throw new ArgumentException("Controller id must not be empty.", nameof(controllerId));
        }

        if (context.SourceEntityId != effect.Definition.SourceEntityId)
        {
            throw new ArgumentException("Effect context source must match keyword source entity.", nameof(context));
        }

        EffectRequest request = new(effect.Definition.EffectId, controllerId, effect.Definition.Timing, context);
        return new EffectBinding(
            request,
            KeywordAliases(effect.Kind),
            QueryRole(effect.Kind),
            QueryScopes(effect.Kind),
            effect);
    }

    public static IReadOnlyList<EffectBinding> RegisterBaseBatch2(
        EffectRegistry registry,
        HeadlessEntityId sourceEntityId,
        HeadlessPlayerId controllerId,
        EffectContext context,
        HeadlessEntityId? targetEntityId = null,
        bool isInherited = false,
        bool isLinked = false)
    {
        ArgumentNullException.ThrowIfNull(registry);
        EffectBinding[] bindings = CreateAll(sourceEntityId, targetEntityId, isInherited, isLinked)
            .Select(effect => ToBinding(effect, controllerId, context))
            .ToArray();

        foreach (EffectBinding binding in bindings)
        {
            registry.Register(binding);
        }

        return Array.AsReadOnly(bindings);
    }

    public static HeadlessEntityId EffectId(KeywordBaseBatch2Kind kind, HeadlessEntityId sourceEntityId)
    {
        return new HeadlessEntityId($"{sourceEntityId.Value}:{KeywordName(kind).Replace(" ", string.Empty, StringComparison.Ordinal)}:base2");
    }

    public static string KeywordName(KeywordBaseBatch2Kind kind)
    {
        return kind switch
        {
            KeywordBaseBatch2Kind.Rush => "Rush",
            KeywordBaseBatch2Kind.Blitz => "Blitz",
            KeywordBaseBatch2Kind.Retaliation => "Retaliation",
            KeywordBaseBatch2Kind.ArmorPurge => "Armor Purge",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Keyword kind must be known."),
        };
    }

    public static string Timing(KeywordBaseBatch2Kind kind)
    {
        return kind switch
        {
            KeywordBaseBatch2Kind.Rush => KeywordBaseBatch2Timings.ImmediateAttackPermission,
            KeywordBaseBatch2Kind.Blitz => KeywordBaseBatch2Timings.OnEnterFieldAnyone,
            KeywordBaseBatch2Kind.Retaliation => KeywordBaseBatch2Timings.OnDestroyedAnyone,
            KeywordBaseBatch2Kind.ArmorPurge => KeywordBaseBatch2Timings.WhenRemoveField,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Keyword kind must be known."),
        };
    }

    public static EffectQueryRole QueryRole(KeywordBaseBatch2Kind kind)
    {
        return kind switch
        {
            KeywordBaseBatch2Kind.Rush => EffectQueryRole.Restriction,
            KeywordBaseBatch2Kind.Blitz => EffectQueryRole.Continuous,
            KeywordBaseBatch2Kind.Retaliation => EffectQueryRole.Continuous,
            KeywordBaseBatch2Kind.ArmorPurge => EffectQueryRole.Replacement,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Keyword kind must be known."),
        };
    }

    public static IReadOnlyList<string> QueryScopes(KeywordBaseBatch2Kind kind)
    {
        string scope = kind switch
        {
            KeywordBaseBatch2Kind.Rush => KeywordBaseBatch2Scopes.CanAttackImmediately,
            KeywordBaseBatch2Kind.Blitz => KeywordBaseBatch2Scopes.BlitzAttack,
            KeywordBaseBatch2Kind.Retaliation => KeywordBaseBatch2Scopes.RetaliationBattleDeletion,
            KeywordBaseBatch2Kind.ArmorPurge => KeywordBaseBatch2Scopes.ArmorPurgeReplacement,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Keyword kind must be known."),
        };
        return Array.AsReadOnly(new[] { scope });
    }

    public static IReadOnlyList<string> KeywordAliases(KeywordBaseBatch2Kind kind)
    {
        string keyword = KeywordName(kind);
        return kind == KeywordBaseBatch2Kind.ArmorPurge
            ? Array.AsReadOnly(new[] { keyword, "ArmorPurge" })
            : Array.AsReadOnly(new[] { keyword });
    }

    public static string Hash(
        KeywordBaseBatch2Kind kind,
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId,
        bool isInherited,
        bool isLinked,
        string? triggerReason)
    {
        string target = targetEntityId?.Value ?? "<self>";
        return string.Join(
            "|",
            "keyword-base2",
            KeywordName(kind),
            sourceEntityId.Value,
            target,
            isInherited,
            isLinked,
            triggerReason ?? "<none>");
    }
}
