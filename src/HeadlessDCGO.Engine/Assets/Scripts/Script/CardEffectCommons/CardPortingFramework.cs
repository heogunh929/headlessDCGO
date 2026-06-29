namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;

// (Phase 1) Card-porting recipe foundation.
//
// The original DCGO authors each card as `public class <Id> : CEntity_Effect` overriding
// `CardEffects(EffectTiming timing, CardSource card)` which returns the `ICardEffect`s active for that
// timing (see DCGO/Assets/Scripts/CardEffect/<set>/<color>/<id>.cs). To keep ported card files a 1:1
// mirror of that source (AS-IS structure-mirror rule), this file provides the headless equivalents of
// the Unity authoring surface — `CEntity_Effect`, `CardSource`, `EffectTiming`, `ICardEffect`, a
// `CardEffectFactory` whose method names match the original, and `CardEffectCommons` condition predicates
// — so a ported card body reads identically to the original and compiles against the headless engine.
//
// Each `ICardEffect` lowers to an `EffectBinding` that the existing continuous / keyword gates already
// consume (no new resolution plumbing). The original evaluates conditions against global singletons; the
// headless threads the live `EngineContext` through `CardSource` so a `condition` lambda evaluates against
// real turn / zone / digivolution state at read time. `CardEffectRegistrar` materialises a card's
// bindings into the EffectRegistry when it enters play.

/// <summary>
/// Headless mirror of the original (large) <c>EffectTiming</c> enum. Only the timings used by ported
/// cards are listed; grow this as cards require new ones. <see cref="None"/> is the original's marker for
/// always-on continuous / static effects (registered once while the card is in play).
/// </summary>
public enum EffectTiming
{
    None = 0,
    OnEnterFieldAnyone,
    OnDetermineDoSecurityCheck,
    OnUseAttack,
    WhenDigivolving,
    OnDestroyedAnyone,
    OnAllyAttack,
    OnBlockAnyone,

    // Player-activated abilities (NOT auto-registered on enter-play; activation flow is Wave 3).
    OptionSkill,
    SecuritySkill,
}

/// <summary>The headless <see cref="EffectTiming"/> mirror values are named after the engine trigger
/// strings (the "...Anyone" forms used by <c>TriggerTimings</c> / <c>GetEffectsForTiming</c>), so the
/// engine timing string is just the enum name.</summary>
public static class EffectTimings
{
    public static string ToTriggerName(EffectTiming timing) => timing.ToString();
}

/// <summary>A read-only view of the permanent (digivolution stack) a card belongs to — the headless
/// stand-in for the original <c>Permanent</c> accessed via <c>CardSource.PermanentOfThisCard()</c>.</summary>
public sealed class PermanentView
{
    public PermanentView(DigivolutionStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        Stack = stack;
    }

    public DigivolutionStack Stack { get; }

    /// <summary>The under-cards (digivolution sources) of the permanent — mirrors
    /// <c>Permanent.DigivolutionCards</c>. <c>.Count</c> is the source count.</summary>
    public IReadOnlyList<StackedCard> DigivolutionCards => Stack.UnderCards;

    public bool IsEmpty => Stack.IsEmpty;
}

/// <summary>
/// Headless mirror of the original <c>CardSource</c> — the handle a card-effect builder receives. Carries
/// the live instance id, the controlling / owning player, and the live <see cref="EngineContext"/> so
/// condition predicates can read turn / zone / stack state.
/// </summary>
public sealed class CardSource
{
    public CardSource(EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId controller, HeadlessPlayerId? owner = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (instanceId.IsEmpty)
        {
            throw new ArgumentException("Card source instance id must not be empty.", nameof(instanceId));
        }

        if (controller.IsEmpty)
        {
            throw new ArgumentException("Card source controller id must not be empty.", nameof(controller));
        }

        Context = context;
        InstanceId = instanceId;
        Controller = controller;
        Owner = owner ?? controller;
    }

    public EngineContext Context { get; }

    public HeadlessEntityId InstanceId { get; }

    public HeadlessPlayerId Controller { get; }

    public HeadlessPlayerId Owner { get; }

    /// <summary>Mirror of <c>CardSource.PermanentOfThisCard()</c>: the permanent (stack) this card is part
    /// of, whether it is the top card or a buried digivolution source. Empty if the card is not in a
    /// battle-area permanent.</summary>
    public PermanentView PermanentOfThisCard()
    {
        var zones = (IZoneStateReader)Context.ZoneMover;
        foreach (HeadlessEntityId top in zones.GetCards(Owner, ChoiceZone.BattleArea))
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(Context.CardInstanceRepository, Context.CardRepository, top);
            if (top == InstanceId || stack.UnderCards.Any(under => under.InstanceId == InstanceId))
            {
                return new PermanentView(stack);
            }
        }

        return new PermanentView(DigivolutionStack.Empty);
    }
}

/// <summary>
/// Headless mirror of the original <c>ICardEffect</c>. A ported card returns these; the registrar lowers
/// each to an <see cref="EffectBinding"/> using the supplied unique effect id.
/// </summary>
public interface ICardEffect
{
    EffectBinding ToBinding(string effectId);
}

/// <summary>Marker for effects resolved via the activation / choice flow (Option / Security skills,
/// select-and-act, triggered-with-choice) rather than auto-registered continuous/trigger bindings.
/// <see cref="CardEffectRegistrar"/> skips these on enter-play; they are resolved imperatively until the
/// interactive activation path is wired.</summary>
public interface IActivatedCardEffect : ICardEffect
{
}

/// <summary>Headless mirror of the original card-effect base class <c>CEntity_Effect</c>.</summary>
public abstract class CEntity_Effect
{
    /// <summary>Returns the effects active for <paramref name="timing"/> (mirrors the original override).</summary>
    public abstract IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card);
}

/// <summary>
/// A continuous numeric self-modifier (DP / security attack / cost). Lowers to a continuous-role binding
/// targeting the source card, carrying the delta under the matching <see cref="ModifierHelpers"/> key plus
/// optional inherited / condition markers, so <see cref="ContinuousDpGate"/> /
/// <see cref="ContinuousModifierGate"/> fold it in automatically (with inherited / condition gating
/// applied by <see cref="ContinuousScopeEvaluation"/>).
/// </summary>
public sealed class ContinuousSelfModifierEffect : ICardEffect
{
    /// <summary>Marks a continuous binding as an inherited (digivolution-source) effect: it applies to the
    /// TOP card of the stack the source is buried in, never to the source as a stand-alone permanent.</summary>
    public const string InheritedEffectKey = "continuous.isInherited";

    /// <summary>Carries the card-authored <c>condition</c> predicate (a <c>Func&lt;bool&gt;</c>) evaluated
    /// at read time by <see cref="ContinuousScopeEvaluation"/>.</summary>
    public const string ConditionKey = "continuous.condition";

    /// <summary>Carries a card-authored dynamic delta (<c>Func&lt;int&gt;</c>, e.g. "+X where X = sources / 2")
    /// evaluated at read time; the resolved int is written under <see cref="DynamicMetricKey"/>'s metric.</summary>
    public const string DynamicValueKey = "continuous.dynamicValue";

    /// <summary>The metric delta key a resolved <see cref="DynamicValueKey"/> should be written under.</summary>
    public const string DynamicMetricKey = "continuous.dynamicMetric";

    public ContinuousSelfModifierEffect(CardSource card, string deltaKey, int changeValue, bool isInheritedEffect, Func<bool>? condition, Func<int>? dynamicValue = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(deltaKey);
        Card = card;
        DeltaKey = deltaKey;
        ChangeValue = changeValue;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        DynamicValue = dynamicValue;
    }

    public CardSource Card { get; }

    public string DeltaKey { get; }

    public int ChangeValue { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public Func<int>? DynamicValue { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (DynamicValue is not null)
        {
            // Resolved to a concrete int under DeltaKey at read time by ContinuousScopeEvaluation.
            values[DynamicValueKey] = DynamicValue;
            values[DynamicMetricKey] = DeltaKey;
        }
        else
        {
            values[DeltaKey] = ChangeValue;
        }

        if (IsInheritedEffect)
        {
            values[InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: new[] { Card.InstanceId },
            values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null,
            EffectQueryRole.Continuous,
            new[] { ContinuousModifierGate.Scope },
            effect: null,
            duration: null);
    }
}

/// <summary>A self keyword grant (Blocker / Jamming / Reboot / Piercing) reusing the existing
/// <see cref="KeywordBaseBatch1Effect"/> resolution + gate wiring.</summary>
public sealed class SelfKeywordEffect : ICardEffect
{
    public SelfKeywordEffect(CardSource card, KeywordBaseBatch1Kind kind, bool isInheritedEffect, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
        Kind = kind;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
    }

    public CardSource Card { get; }

    public KeywordBaseBatch1Kind Kind { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        // The keyword factory derives its own deterministic effect id from (kind, source); effectId is
        // accepted for signature uniformity with ICardEffect but not needed here.
        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: new[] { Card.InstanceId });
        KeywordBaseBatch1Effect effect = KeywordBaseBatch1Factory.Create(
            Kind,
            Card.InstanceId,
            targetEntityId: Card.InstanceId,
            isInherited: IsInheritedEffect,
            isLinked: false);
        return KeywordBaseBatch1Factory.ToBinding(effect, Card.Controller, context);
    }
}

/// <summary>Minimal headless mirror of the original <c>Permanent</c> — used only for the signature of
/// card <c>permanentCondition</c> predicates. Player-scope effects scope to the owner's cards directly, so
/// the predicate body is not invoked by the headless evaluation (it exists for 1:1 source fidelity).</summary>
public sealed class Permanent
{
    public Permanent(HeadlessEntityId instanceId, HeadlessPlayerId ownerId)
    {
        InstanceId = instanceId;
        OwnerId = ownerId;
    }

    public HeadlessEntityId InstanceId { get; }

    public HeadlessPlayerId OwnerId { get; }
}

/// <summary>
/// A continuous player-scope numeric modifier ("your Digimon get +X DP"). Lowers to a continuous-role
/// binding carrying the player-scope markers (<see cref="PlayerScopeContinuousHelpers"/>) so it reaches
/// every applicable card the owner controls via <see cref="ContinuousScopeEvaluation"/>.
/// </summary>
public sealed class PlayerScopeModifierEffect : ICardEffect
{
    public PlayerScopeModifierEffect(CardSource card, string deltaKey, int changeValue, string? scopeCardType, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(deltaKey);
        Card = card;
        DeltaKey = deltaKey;
        ChangeValue = changeValue;
        ScopeCardType = scopeCardType;
        Condition = condition;
    }

    public CardSource Card { get; }

    public string DeltaKey { get; }

    public int ChangeValue { get; }

    public string? ScopeCardType { get; }

    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
            [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = Card.Owner.Value,
            [DeltaKey] = ChangeValue,
        };
        if (!string.IsNullOrWhiteSpace(ScopeCardType))
        {
            values[PlayerScopeContinuousHelpers.ScopeCardTypeKey] = ScopeCardType;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>(),
            values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null,
            EffectQueryRole.Continuous,
            new[] { ContinuousModifierGate.Scope },
            effect: null,
            duration: null);
    }
}

/// <summary>
/// A triggered effect that gains / loses memory when its timing fires (the common ActivateClass form
/// "[When ...] gain/lose N memory", e.g. ST1_06 / ST1_09). Carries the effect body itself so the existing
/// scheduler / resolver pipeline (TriggerEventEmitter -> AutoProcessingTriggerCollector -> EffectScheduler
/// -> CardEffectSchedulerResolver) resolves it into an AddMemory mutation on the
/// <see cref="MatchStateMutationSink"/>. The original coroutine becomes an emitted mutation (1:1 relaxed
/// for trigger plumbing).
/// </summary>
public sealed class TriggeredMemoryEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly Func<bool>? _condition;

    public TriggeredMemoryEffect(CardSource card, EffectTiming timing, int amount, bool isInheritedEffect, Func<bool>? condition, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Amount = amount;
        IsInheritedEffect = isInheritedEffect;
        _condition = condition;
        string trigger = EffectTimings.ToTriggerName(timing);
        var effectId = new HeadlessEntityId($"{card.InstanceId.Value}:mem:{trigger}:{amount}");
        Definition = new CardEffectDefinition(effectId, card.InstanceId, description, trigger, isOptional: amount > 0);
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public bool IsInheritedEffect { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_condition is not null && !_condition())
        {
            return CardEffectCanResolveResult.Failure("Trigger condition not met.");
        }

        return CardEffectCanResolveResult.Success();
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
            return ValueTask.FromResult(EffectResult.Failure(check.Message ?? "Cannot resolve.", check.Values));
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount };
        mutations.Apply(new EffectMutation(MatchStateMutationSink.AddMemoryKind, Definition.SourceEntityId, values));
        return ValueTask.FromResult(EffectResult.Success($"Memory {(Amount >= 0 ? "+" : string.Empty)}{Amount}."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null,
            EffectQueryRole.None,
            Array.Empty<string>(),
            effect: this,
            duration: null);
    }
}

/// <summary>Placeholder for an original effect whose subsystem is not yet ported. Returned so a ported
/// card body compiles 1:1; never registered (its timing is excluded from
/// <see cref="CardEffectRegistrar.AllTimings"/>). If ever lowered, it fails loudly.</summary>
public sealed class DeferredCardEffect : IActivatedCardEffect
{
    public DeferredCardEffect(string reason)
    {
        Reason = reason;
    }

    public string Reason { get; }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Card effect not yet ported: {Reason}");
}

/// <summary>
/// An activated targeted effect (an Option [Main] / [Security] skill that selects permanents and acts on
/// them, e.g. "delete up to 2 of your opponent's Digimon"). Wraps the <see cref="SelectPermanentEffect"/>
/// helper: <see cref="BuildRequest"/> enumerates candidates into a <c>ChoiceRequest</c> and
/// <see cref="Apply"/> applies the Mode's mutation to the chosen targets.
///
/// NOTE: the interactive activation path (Option/Security action -> resolve this effect with a live choice
/// provider) is NOT yet wired (IHeadlessCardEffect.ResolveAsync has no choice provider). These effects are
/// therefore resolved imperatively (build request -> answer -> apply), exactly as the
/// SelectPermanentEffect tests do, until that integration lands. They are not auto-registered (their
/// OptionSkill / SecuritySkill timing is excluded from <see cref="CardEffectRegistrar.AllTimings"/>).
/// </summary>
public sealed class ActivatedSelectEffect : IActivatedCardEffect
{
    private readonly SelectPermanentEffect _select = new();

    public ActivatedSelectEffect(
        CardSource card,
        Func<HeadlessEntityId, bool> canTarget,
        int maxCount,
        bool canNoSelect,
        bool canEndNotMax,
        SelectPermanentEffect.Mode mode,
        string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
        _select.SetUp(card.Owner, canTarget, maxCount, canNoSelect, canEndNotMax, mode, card.InstanceId);
        _select.SetUpCustomMessage(description);
    }

    public CardSource Card { get; }

    public string Description { get; }

    /// <summary>Enumerate the candidates into a Permanent ChoiceRequest the driver/agent answers.</summary>
    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players) =>
        _select.BuildRequest((IZoneStateReader)Card.Context.ZoneMover, players);

    /// <summary>Apply the Mode's mutation to the chosen targets.</summary>
    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected) =>
        _select.Apply(sink, selected);

    // Activated effects are not auto-registered; lowering one to a binding is a wiring error.
    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated select effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated effect that SELECTS targets and grants each a continuous numeric modifier for a
/// <see cref="EffectDuration"/> (e.g. ST1_13 [Main] "1 of your Digimon gets +3000 DP for the turn").
/// <see cref="ApplyBuff"/> registers a duration-tagged continuous binding per chosen target, so the
/// existing gate folds it in and <see cref="EffectDurationExpiry"/> removes it on expiry.
/// </summary>
public sealed class ActivatedTargetBuffEffect : IActivatedCardEffect
{
    private readonly SelectPermanentEffect _select = new();

    public ActivatedTargetBuffEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, string deltaKey, int changeValue, EffectDuration duration, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(deltaKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        DeltaKey = deltaKey;
        ChangeValue = changeValue;
        Duration = duration;
        Description = description;
        _select.SetUp(card.Owner, canTarget, maxCount, canNoSelect: false, canEndNotMax: maxCount > 1, SelectPermanentEffect.Mode.Custom, card.InstanceId);
        _select.SetUpCustomMessage(description);
    }

    public CardSource Card { get; }

    public string DeltaKey { get; }

    public int ChangeValue { get; }

    public EffectDuration Duration { get; }

    public string Description { get; }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players) =>
        _select.BuildRequest((IZoneStateReader)Card.Context.ZoneMover, players);

    /// <summary>Register a duration-tagged continuous modifier on each chosen target.</summary>
    public void ApplyBuff(IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        int index = 0;
        foreach (HeadlessEntityId target in selected)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [DeltaKey] = ChangeValue };
            var context = new EffectContext(
                Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { target }, values: values);
            var binding = new EffectBinding(
                new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:buff:{target.Value}:{DeltaKey}:{index++}"), Card.Controller, "Continuous", context),
                keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope }, effect: null, duration: Duration);
            Card.Context.EffectRegistry.Register(binding);
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated buff effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated PLAYER-SCOPE timed buff ("all your Digimon gain +X for a duration", e.g. ST1_13 [Security]
/// "all your Digimon gain Security Attack +1 until your next turn end"). <see cref="ApplyBuff"/> registers
/// one duration-tagged player-scope continuous binding.
/// </summary>
public sealed class ActivatedPlayerScopeBuffEffect : IActivatedCardEffect
{
    public ActivatedPlayerScopeBuffEffect(CardSource card, string deltaKey, int changeValue, EffectDuration duration, string scopeCardType, string description, string? scopeZone = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(deltaKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        DeltaKey = deltaKey;
        ChangeValue = changeValue;
        Duration = duration;
        ScopeCardType = scopeCardType;
        ScopeZone = scopeZone;
        Description = description;
    }

    public CardSource Card { get; }

    public string DeltaKey { get; }

    public int ChangeValue { get; }

    public EffectDuration Duration { get; }

    public string? ScopeCardType { get; }

    public string? ScopeZone { get; }

    public string Description { get; }

    public void ApplyBuff()
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
            [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = Card.Owner.Value,
            [DeltaKey] = ChangeValue,
        };
        if (!string.IsNullOrWhiteSpace(ScopeCardType))
        {
            values[PlayerScopeContinuousHelpers.ScopeCardTypeKey] = ScopeCardType;
        }

        if (!string.IsNullOrWhiteSpace(ScopeZone))
        {
            values[PlayerScopeContinuousHelpers.ScopeZoneKey] = ScopeZone;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
        var binding = new EffectBinding(
            new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:pscopebuff:{DeltaKey}"), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope }, effect: null, duration: Duration);
        Card.Context.EffectRegistry.Register(binding);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated player-scope buff is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// Headless mirror of the original <c>CardEffectFactory</c>. Method names match the original so ported
/// card bodies read 1:1. Each returns an <see cref="ICardEffect"/> the registrar lowers to a binding.
/// </summary>
public static class CardEffectFactory
{
    /// <summary>Original: <c>ChangeSelfSAttackStaticEffect</c> — continuous ±security attack on self.</summary>
    public static ICardEffect ChangeSelfSAttackStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfModifierEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue, isInheritedEffect, condition);

    /// <summary>Original: <c>ChangeSelfSAttackStaticEffect&lt;Func&lt;int&gt;&gt;</c> — continuous ±security
    /// attack on self with a dynamic (read-time) value.</summary>
    public static ICardEffect ChangeSelfSAttackStaticEffect(Func<int> changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(changeValue);
        return new ContinuousSelfModifierEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue: 0, isInheritedEffect, condition, dynamicValue: changeValue);
    }

    /// <summary>Original: <c>ChangeSelfDPStaticEffect</c> — continuous ±DP on self.</summary>
    public static ICardEffect ChangeSelfDPStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfModifierEffect(card, ModifierHelpers.DpDeltaKey, changeValue, isInheritedEffect, condition);

    /// <summary>Original: <c>PierceSelfEffect</c> — grants Piercing to self.</summary>
    public static ICardEffect PierceSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordEffect(card, KeywordBaseBatch1Kind.Piercing, isInheritedEffect, condition);

    /// <summary>Original: <c>BlockerSelfStaticEffect</c> — grants Blocker to self.</summary>
    public static ICardEffect BlockerSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordEffect(card, KeywordBaseBatch1Kind.Blocker, isInheritedEffect, condition);

    /// <summary>Original: <c>JammingSelfStaticEffect</c> — grants Jamming to self.</summary>
    public static ICardEffect JammingSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordEffect(card, KeywordBaseBatch1Kind.Jamming, isInheritedEffect, condition);

    /// <summary>Original: <c>ChangeDPStaticEffect</c> — continuous ±DP on a set of permanents. Here scoped
    /// to the owner's Digimon (the common "your Digimon get +X DP" form); <paramref name="permanentCondition"/>
    /// is accepted for source fidelity but the owner-scope handles targeting.</summary>
    public static ICardEffect ChangeDPStaticEffect(
        Func<Permanent, bool> permanentCondition,
        int changeValue,
        bool isInheritedEffect,
        CardSource card,
        Func<bool>? condition,
        Func<string>? effectName = null) =>
        new PlayerScopeModifierEffect(card, ModifierHelpers.DpDeltaKey, changeValue, scopeCardType: "Digimon", condition);

    /// <summary>A triggered "[When ...] gain/lose N memory" effect (the common ActivateClass memory form).
    /// <paramref name="timing"/> is the branch timing the card declared it under.</summary>
    public static ICardEffect AddMemoryTriggerEffect(EffectTiming timing, int amount, bool isInheritedEffect, CardSource card, Func<bool>? condition, string description) =>
        new TriggeredMemoryEffect(card, timing, amount, isInheritedEffect, condition, description);

    /// <summary>Original: <c>PlaySelfTamerSecurityEffect</c> — the security-skill of a Tamer. The
    /// security-skill activation flow is not yet ported (Wave 3); kept for 1:1 source fidelity.</summary>
    public static ICardEffect PlaySelfTamerSecurityEffect(CardSource card) =>
        new DeferredCardEffect("PlaySelfTamerSecurityEffect (security-skill activation flow, Wave 3)");

    /// <summary>An activated "select up to <paramref name="maxCount"/> matching permanents and delete them"
    /// effect (Option [Main] delete skill, e.g. ST1_16 / ST1_15).</summary>
    public static ICardEffect SelectAndDestroyEffect(
        CardSource card,
        Func<HeadlessEntityId, bool> canTarget,
        int maxCount,
        bool canEndNotMax,
        string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Destroy, description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> matching Digimon and give each
    /// +<paramref name="changeValue"/> DP for <paramref name="duration"/>" effect (e.g. ST1_13 [Main]).</summary>
    public static ICardEffect SelectAndBuffDpEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int changeValue, EffectDuration duration, string description) =>
        new ActivatedTargetBuffEffect(card, canTarget, maxCount, ModifierHelpers.DpDeltaKey, changeValue, duration, description);

    /// <summary>An activated "all your Digimon gain +<paramref name="changeValue"/> Security Attack for
    /// <paramref name="duration"/>" player-scope effect (e.g. ST1_13 [Security]).</summary>
    public static ICardEffect PlayerScopeBuffSAttackEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, scopeCardType: "Digimon", description);

    /// <summary>An activated "all your Security Digimon get +<paramref name="changeValue"/> DP for
    /// <paramref name="duration"/>" player-scope effect, scoped to the owner's Security-zone Digimon
    /// (e.g. ST1_14).</summary>
    public static ICardEffect PlayerScopeBuffSecurityDpEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.DpDeltaKey, changeValue, duration, scopeCardType: "Digimon", description, scopeZone: "Security");
}

/// <summary>
/// Headless mirror of the original <c>CardEffectCommons</c> condition predicates used inside card
/// <c>condition</c> lambdas. Each reads live state from the <see cref="CardSource"/>'s engine context.
/// </summary>
public static class CardEffectCommons
{
    /// <summary>It is the card owner's turn.</summary>
    public static bool IsOwnerTurn(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return TurnOwnershipHelpers.IsOwnerTurn(card.Context.TurnController.Current.TurnPlayerId, card.Owner);
    }

    /// <summary>The card is part of a battle-area permanent (as the top card or a buried source).</summary>
    public static bool IsExistOnBattleArea(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return !card.PermanentOfThisCard().IsEmpty;
    }

    /// <summary>Mirror of the original predicate: <paramref name="permanent"/> is one of <paramref name="card"/>'s
    /// owner's battle-area Digimon. Player-scope effects scope to the owner directly, so this is used only
    /// inside card <c>permanentCondition</c> lambdas for source fidelity.</summary>
    public static bool IsPermanentExistsOnOwnerBattleAreaDigimon(Permanent permanent, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(permanent);
        ArgumentNullException.ThrowIfNull(card);
        return permanent.OwnerId == card.Owner;
    }

    /// <summary>The opponent owns <paramref name="permanent"/> (mirror of the opponent battle-area predicate).</summary>
    public static bool IsPermanentExistsOnOpponentBattleAreaDigimon(Permanent permanent, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(permanent);
        ArgumentNullException.ThrowIfNull(card);
        return permanent.OwnerId != card.Owner;
    }

    /// <summary><paramref name="id"/> is an opponent's battle-area Digimon (entity-id predicate form used
    /// by SelectPermanentEffect target conditions).</summary>
    public static bool IsOpponentBattleAreaDigimon(CardSource card, HeadlessEntityId id) =>
        IsBattleAreaDigimon(card, id, opponent: true);

    /// <summary><paramref name="id"/> is one of the card owner's battle-area Digimon.</summary>
    public static bool IsOwnerBattleAreaDigimon(CardSource card, HeadlessEntityId id) =>
        IsBattleAreaDigimon(card, id, opponent: false);

    private static bool IsBattleAreaDigimon(CardSource card, HeadlessEntityId id, bool opponent)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty || !card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) || instance is null)
        {
            return false;
        }

        bool isOpponentOwned = instance.OwnerId != card.Owner;
        if (isOpponentOwned != opponent)
        {
            return false;
        }

        var zones = (IZoneStateReader)card.Context.ZoneMover;
        if (!zones.GetCards(instance.OwnerId, ChoiceZone.BattleArea).Contains(id))
        {
            return false;
        }

        return card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            && def is not null
            && string.Equals(def.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resolved current DP of a battle-area card (base printed DP folded with continuous DP
    /// modifiers via <see cref="ContinuousDpGate"/>). Used by DP-threshold target predicates (e.g. ST1_15
    /// "Digimon with 4000 DP or less").</summary>
    public static int CurrentDp(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        int baseDp = 0;
        if (card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) && instance is not null)
        {
            baseDp = ReadDp(instance.Metadata)
                ?? (card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) && def is not null
                    ? ReadDp(def.Metadata) ?? 0
                    : 0);
        }

        return ContinuousDpGate.ResolveDp(card.Context, id, baseDp);
    }

    private static int? ReadDp(IReadOnlyDictionary<string, object?> metadata)
    {
        foreach (string key in new[] { "dp", "DP" })
        {
            if (metadata.TryGetValue(key, out object? raw))
            {
                if (raw is int i) return i;
                if (raw is long l) return (int)l;
                if (raw is string s && int.TryParse(s, out int p)) return p;
            }
        }

        return null;
    }

    /// <summary>Mirror of the original <c>AddActivateMainOptionSecurityEffect</c>: reuse the Option's [Main]
    /// skill from security. The security-skill activation flow is not yet ported (kept for source fidelity,
    /// not auto-registered).</summary>
    public static void AddActivateMainOptionSecurityEffect(CardSource card, ref List<ICardEffect> cardEffects, string effectName)
    {
        ArgumentNullException.ThrowIfNull(cardEffects);
        cardEffects.Add(new DeferredCardEffect($"AddActivateMainOptionSecurityEffect: {effectName} (security activation flow)"));
    }
}

/// <summary>
/// The runtime seam: builds a card's effect bindings (across the given timings) and registers them into
/// the EffectRegistry. Call when a card enters play. Returns the registered bindings for inspection.
/// </summary>
public static class CardEffectRegistrar
{
    /// <summary>The timings auto-registered when a card enters play (continuous + passive triggers).
    /// Player-activated abilities (<see cref="EffectTiming.OptionSkill"/> / <see cref="EffectTiming.SecuritySkill"/>)
    /// are intentionally excluded — their activation flow is built in a later wave.</summary>
    public static readonly IReadOnlyList<EffectTiming> AllTimings = Array.AsReadOnly(new[]
    {
        EffectTiming.None,
        EffectTiming.OnEnterFieldAnyone,
        EffectTiming.OnDetermineDoSecurityCheck,
        EffectTiming.OnUseAttack,
        EffectTiming.WhenDigivolving,
        EffectTiming.OnDestroyedAnyone,
        EffectTiming.OnAllyAttack,
        EffectTiming.OnBlockAnyone,
    });

    public static IReadOnlyList<EffectBinding> RegisterOnEnterPlay(
        EngineContext context,
        CEntity_Effect effect,
        string cardNumber,
        CardSource card)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardNumber);
        ArgumentNullException.ThrowIfNull(card);

        var registered = new List<EffectBinding>();
        int index = 0;
        foreach (EffectTiming timing in AllTimings)
        {
            foreach (ICardEffect cardEffect in effect.CardEffects(timing, card))
            {
                // Activated / choice effects are resolved via the activation flow, not auto-registered.
                if (cardEffect is IActivatedCardEffect)
                {
                    continue;
                }

                EffectBinding binding = cardEffect.ToBinding($"{card.InstanceId.Value}:{cardNumber}:{timing}:{index++}");
                context.EffectRegistry.Register(binding);
                registered.Add(binding);
            }
        }

        return registered;
    }
}
