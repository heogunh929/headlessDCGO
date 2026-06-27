namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public enum KeywordBaseBatch1Kind
{
    Blocker = 0,
    Jamming = 1,
    Reboot = 2,
    Piercing = 3,
}

public static class KeywordBaseBatch1Timings
{
    public const string BlockTiming = "BlockTiming";
    public const string BattleDeletionCheck = "BattleDeletionCheck";
    public const string OpponentUnsuspend = "OpponentUnsuspend";
    public const string DetermineSecurityCheck = "OnDetermineDoSecurityCheck";
}

public static class KeywordBaseBatch1Scopes
{
    public const string CanBlock = "CanBlock";
    public const string SecurityBattleDeletion = "SecurityBattleDeletion";
    public const string RebootUnsuspend = "RebootUnsuspend";
    public const string SecurityCheck = "SecurityCheck";
}

public static class KeywordBaseBatch1ContextKeys
{
    public const string MatchState = "matchState";
    public const string TargetEntityId = "targetEntityId";
    public const string AttackingCardId = "attackingCardId";
    public const string DefendingCardIsSecurity = "defendingCardIsSecurity";
    public const string BattleDeletedByBattle = "battleDeletedByBattle";
    public const string BattleWinnerCardId = "battleWinnerCardId";
    public const string BattleLoserCardId = "battleLoserCardId";
    public const string OpponentSecurityCount = "opponentSecurityCount";
    public const string DoSecurityCheck = "doSecurityCheck";
}

// N-: per-keyword resolution logic is split across partial files (Blocker.cs / Jamming.cs / Reboot.cs /
// Pierce.cs) to mirror the original DCGO CardEffectCommons/KeyWordEffects/<Name>.cs structure (which is
// itself a partial-class-per-keyword layout). Shared scaffolding (enum, timings, dispatch, factory) stays
// here.
public sealed partial class KeywordBaseBatch1Effect : IHeadlessCardEffect
{
    public KeywordBaseBatch1Effect(
        KeywordBaseBatch1Kind kind,
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null,
        bool isInherited = false,
        bool isLinked = false)
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
        Keyword = KeywordBaseBatch1Factory.KeywordName(kind);
        Definition = new CardEffectDefinition(
            KeywordBaseBatch1Factory.EffectId(kind, sourceEntityId),
            sourceEntityId,
            Keyword,
            KeywordBaseBatch1Factory.Timing(kind),
            isOptional: kind == KeywordBaseBatch1Kind.Piercing,
            hash: KeywordBaseBatch1Factory.Hash(kind, sourceEntityId, targetEntityId, isInherited, isLinked));
    }

    public KeywordBaseBatch1Kind Kind { get; }

    public string Keyword { get; }

    public HeadlessEntityId? TargetEntityId { get; }

    public bool IsInherited { get; }

    public bool IsLinked { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.EffectContext.TryGetValue(KeywordBaseBatch1ContextKeys.MatchState, out MatchState? state)
            || state is null)
        {
            return Failure("match state is required", "matchState", context);
        }

        HeadlessEntityId targetId = ResolveTargetId(context);
        if (!TryGetBattleCard(state, targetId, out CardInstanceState? target)
            || target is null)
        {
            return Failure("target must be on battle area", "targetEntityId", context, targetId);
        }

        CardInstanceState battleTarget = target;
        return Kind switch
        {
            KeywordBaseBatch1Kind.Blocker => CanResolveBlocker(context, battleTarget),
            KeywordBaseBatch1Kind.Reboot => CanResolveReboot(context, battleTarget),
            KeywordBaseBatch1Kind.Jamming => CanResolveJamming(context, battleTarget),
            KeywordBaseBatch1Kind.Piercing => CanResolvePiercing(context, state, battleTarget),
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
            KeywordBaseBatch1Kind.Blocker => "GrantBlocker",
            KeywordBaseBatch1Kind.Jamming => "PreventBattleDeletion",
            KeywordBaseBatch1Kind.Reboot => "ScheduleRebootUnsuspend",
            KeywordBaseBatch1Kind.Piercing => "SetSecurityCheck",
            _ => "KeywordBaseBatch1",
        };

        Dictionary<string, object?> values = BaseValues(context, targetId);
        values["mutationKind"] = mutationKind;
        if (Kind == KeywordBaseBatch1Kind.Piercing)
        {
            values["doSecurityCheck"] = true;
        }

        mutations.Apply(new EffectMutation(mutationKind, Definition.SourceEntityId, values));
        return ValueTask.FromResult(EffectResult.Success($"{Keyword} resolved.", values));
    }

    public EffectBinding ToBinding(
        HeadlessPlayerId controllerId,
        EffectContext context)
    {
        return KeywordBaseBatch1Factory.ToBinding(this, controllerId, context);
    }

    private HeadlessEntityId ResolveTargetId(CardEffectResolveContext context)
    {
        if (TargetEntityId is HeadlessEntityId configuredTarget)
        {
            return configuredTarget;
        }

        if (context.EffectContext.TryGetValue(KeywordBaseBatch1ContextKeys.TargetEntityId, out HeadlessEntityId targetFromValues))
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
        return CardEffectCanResolveResult.Failure($"{Keyword} {message}.", values);
    }

    private Dictionary<string, object?> BaseValues(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        Dictionary<string, object?> values = BaseValues(context, target.InstanceId);
        values["targetOwnerId"] = target.OwnerId.Value;
        values["targetIsSuspended"] = target.IsSuspended;
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
            ["controllerId"] = context.Request.ControllerId.Value,
        };
    }
}

public static class KeywordBaseBatch1Factory
{
    public static KeywordBaseBatch1Effect Create(
        KeywordBaseBatch1Kind kind,
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null,
        bool isInherited = false,
        bool isLinked = false)
    {
        return new KeywordBaseBatch1Effect(kind, sourceEntityId, targetEntityId, isInherited, isLinked);
    }

    public static IReadOnlyList<KeywordBaseBatch1Effect> CreateAll(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null,
        bool isInherited = false,
        bool isLinked = false)
    {
        return Array.AsReadOnly(Enum
            .GetValues<KeywordBaseBatch1Kind>()
            .Select(kind => Create(kind, sourceEntityId, targetEntityId, isInherited, isLinked))
            .ToArray());
    }

    public static EffectBinding ToBinding(
        KeywordBaseBatch1Effect effect,
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

    public static IReadOnlyList<EffectBinding> RegisterBaseBatch1(
        EffectRegistry registry,
        HeadlessEntityId sourceEntityId,
        HeadlessPlayerId controllerId,
        EffectContext context,
        HeadlessEntityId? targetEntityId = null,
        bool isInherited = false,
        bool isLinked = false)
    {
        ArgumentNullException.ThrowIfNull(registry);
        KeywordBaseBatch1Effect[] effects = CreateAll(sourceEntityId, targetEntityId, isInherited, isLinked).ToArray();
        EffectBinding[] bindings = effects
            .Select(effect => ToBinding(effect, controllerId, context))
            .ToArray();

        foreach (EffectBinding binding in bindings)
        {
            registry.Register(binding);
        }

        return Array.AsReadOnly(bindings);
    }

    public static HeadlessEntityId EffectId(KeywordBaseBatch1Kind kind, HeadlessEntityId sourceEntityId)
    {
        return new HeadlessEntityId($"{sourceEntityId.Value}:{KeywordName(kind)}:base1");
    }

    public static string KeywordName(KeywordBaseBatch1Kind kind)
    {
        return kind switch
        {
            KeywordBaseBatch1Kind.Blocker => "Blocker",
            KeywordBaseBatch1Kind.Jamming => "Jamming",
            KeywordBaseBatch1Kind.Reboot => "Reboot",
            KeywordBaseBatch1Kind.Piercing => "Piercing",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Keyword kind must be known."),
        };
    }

    public static string Timing(KeywordBaseBatch1Kind kind)
    {
        return kind switch
        {
            KeywordBaseBatch1Kind.Blocker => KeywordBaseBatch1Timings.BlockTiming,
            KeywordBaseBatch1Kind.Jamming => KeywordBaseBatch1Timings.BattleDeletionCheck,
            KeywordBaseBatch1Kind.Reboot => KeywordBaseBatch1Timings.OpponentUnsuspend,
            KeywordBaseBatch1Kind.Piercing => KeywordBaseBatch1Timings.DetermineSecurityCheck,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Keyword kind must be known."),
        };
    }

    public static EffectQueryRole QueryRole(KeywordBaseBatch1Kind kind)
    {
        return kind switch
        {
            KeywordBaseBatch1Kind.Blocker => EffectQueryRole.Restriction,
            KeywordBaseBatch1Kind.Jamming => EffectQueryRole.Replacement,
            KeywordBaseBatch1Kind.Reboot => EffectQueryRole.Modifier,
            KeywordBaseBatch1Kind.Piercing => EffectQueryRole.Continuous,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Keyword kind must be known."),
        };
    }

    public static IReadOnlyList<string> QueryScopes(KeywordBaseBatch1Kind kind)
    {
        string scope = kind switch
        {
            KeywordBaseBatch1Kind.Blocker => KeywordBaseBatch1Scopes.CanBlock,
            KeywordBaseBatch1Kind.Jamming => KeywordBaseBatch1Scopes.SecurityBattleDeletion,
            KeywordBaseBatch1Kind.Reboot => KeywordBaseBatch1Scopes.RebootUnsuspend,
            KeywordBaseBatch1Kind.Piercing => KeywordBaseBatch1Scopes.SecurityCheck,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), "Keyword kind must be known."),
        };
        return Array.AsReadOnly(new[] { scope });
    }

    public static IReadOnlyList<string> KeywordAliases(KeywordBaseBatch1Kind kind)
    {
        string keyword = KeywordName(kind);
        return kind == KeywordBaseBatch1Kind.Piercing
            ? Array.AsReadOnly(new[] { keyword, "Pierce" })
            : Array.AsReadOnly(new[] { keyword });
    }

    public static string Hash(
        KeywordBaseBatch1Kind kind,
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId,
        bool isInherited,
        bool isLinked)
    {
        string target = targetEntityId?.Value ?? "<self>";
        return string.Join(
            "|",
            "keyword-base1",
            KeywordName(kind),
            sourceEntityId.Value,
            target,
            isInherited,
            isLinked);
    }
}
