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
    OnEndTurn,
    OnStartTurn,

    // Player-activated abilities (NOT auto-registered on enter-play; activation flow is Wave 3).
    OptionSkill,
    SecuritySkill,

    // (EX8_074 Stage 1) "When this card would be played" — the original BeforePayCost timing. Engine-level
    // string trigger `TriggerTimings.BeforePayCost` already fires in PlayCardAction; this enum value lets a
    // ported card return BeforePayCost effects. The interactive pre-payment cost-reduction WINDOW that
    // consumes them is a later stage (PlayCardAction's cost is currently locked at action-generation time).
    BeforePayCost,
    // (PRIM-W4 WhenMovingClass) mirrors the original EffectTiming.OnMove — fires when a Digimon is promoted
    // out of the breeding area (CV-A4). ToTriggerName -> "OnMove" matches the engine's TriggerTimings.OnMove
    // emit. Appended at the end to keep existing enum ordinals stable.
    OnMove,
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

    /// <summary>The top card's instance id (the battling Digimon) — mirrors <c>Permanent.TopCard</c>.</summary>
    public HeadlessEntityId TopInstanceId => Stack.Cards.Count > 0 ? Stack.Cards[^1].InstanceId : default;
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

    // ===== (PRIM-W5-0) card-query view — the member surface card predicates read =====================
    // Backed by the definition CardRecord (colors/level/traits/type) + instance metadata. Enables 1:1
    // mirror of the original `cardSource.<X>` / `permanent.TopCard.<X>` predicates.

    private CardRecord? Definition =>
        Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? inst) && inst is not null
            && Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) ? def
            : (Context.CardRepository.TryGetCard(InstanceId, out CardRecord? self) ? self : null);

    private static IReadOnlyList<string> ReadStrings(IReadOnlyDictionary<string, object?>? meta, string key)
    {
        if (meta is null || !meta.TryGetValue(key, out object? raw) || raw is null) return Array.Empty<string>();
        return raw switch
        {
            IEnumerable<string> ss => ss.ToArray(),
            string s => s.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _ => Array.Empty<string>(),
        };
    }

    /// <summary>The card's colors (mirror of <c>CardColors</c>).</summary>
    public IReadOnlyList<string> CardColors => ReadStrings(Definition?.Metadata, "colors");

    /// <summary>The card's traits (mirror of <c>CardTraits</c>).</summary>
    public IReadOnlyList<string> CardTraits => ReadStrings(Definition?.Metadata, "traits");

    /// <summary>Continuous-binding key for an added card name (AS-IS ChangeCardNamesClass).</summary>
    public const string AddedCardNameKey = "addedCardName";

    /// <summary>The card's name(s) (mirror of <c>CardNames</c>) — the printed name plus any names granted by
    /// active continuous effects (ChangeCardNames).</summary>
    public IReadOnlyList<string> CardNames
    {
        get
        {
            var names = new List<string>();
            if (Definition is { } d)
            {
                names.Add(d.Name);
            }

            foreach (EffectRequest effect in Context.EffectRegistry.GetContinuousEffects(
                new EffectQueryContext(ContinuousRestrictionGate.Scope, targetEntityId: InstanceId)))
            {
                if (effect.Context.Values.TryGetValue(AddedCardNameKey, out object? raw) && raw is string added && !string.IsNullOrWhiteSpace(added))
                {
                    names.Add(added);
                }
            }

            return names;
        }
    }

    /// <summary>The card's level, or -1 (mirror of <c>Level</c>).</summary>
    public int Level => Definition?.Metadata is { } m && m.TryGetValue("level", out object? raw) && raw is int lv ? lv : -1;

    /// <summary>The card's printed number (e.g. "BT10-012"), used as the SpecialPlayRecipe key.</summary>
    public string CardNumber => Definition?.CardNumber ?? string.Empty;

    public bool IsDigimon => string.Equals(Definition?.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);
    public bool IsTamer => string.Equals(Definition?.CardType, "Tamer", StringComparison.OrdinalIgnoreCase);
    public bool IsOption => string.Equals(Definition?.CardType, "Option", StringComparison.OrdinalIgnoreCase);
    public bool IsToken => Context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
        && i.Metadata.TryGetValue("isToken", out object? t) && t is bool b && b;

    public bool HasLevel => Level >= 0;
    public bool IsLevel(int level) => Level == level;
    public bool HasCardColor(string color) => CardColors.Any(c => string.Equals(c, color, StringComparison.OrdinalIgnoreCase));
    public bool EqualsCardName(string name) => CardNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    public bool ContainsCardName(string fragment) => CardNames.Any(n => n.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    public bool EqualsTraits(string trait) => CardTraits.Any(t => string.Equals(t, trait, StringComparison.OrdinalIgnoreCase));
    public bool ContainsTraits(string fragment) => CardTraits.Any(t => t.Contains(fragment, StringComparison.OrdinalIgnoreCase));
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

/// <summary>(PRIM-W1) A continuous SELF restriction (cannot digivolve / attack / block / suspend / …) — the
/// restriction analogue of <see cref="ContinuousSelfModifierEffect"/>. Registers a <c>Restriction</c>-role
/// binding under <see cref="ContinuousRestrictionGate.Scope"/> carrying the given restriction flag, targeting
/// this card; the various actions (DigivolveAction / AttackPermanentAction / BlockTiming / …) already consult
/// <see cref="ContinuousRestrictionGate"/>. Condition / inherited-effect are honoured (same
/// <c>ContinuousScopeEvaluation</c> as the modifier gate). Reused across the CanNot* self-static primitives.</summary>
public sealed class ContinuousSelfRestrictionEffect : ICardEffect
{
    public ContinuousSelfRestrictionEffect(CardSource card, string restrictionKey, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? causingEffectPredicate = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(restrictionKey);
        Card = card;
        RestrictionKey = restrictionKey;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        CausingEffectPredicate = causingEffectPredicate;
    }

    public CardSource Card { get; }

    public string RestrictionKey { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    /// <summary>(FR2/M-2) AS-IS cardEffectCondition — the restriction only blocks effects whose causing effect's
    /// SOURCE card matches this. Null = blocks any effect.</summary>
    public Func<CardSource, bool>? CausingEffectPredicate { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [RestrictionHelpers.RestrictionTargetEntityIdKey] = Card.InstanceId.Value,
            [RestrictionHelpers.RestrictionSourceEntityIdKey] = Card.InstanceId.Value,
            [RestrictionKey] = true,
        };
        if (CausingEffectPredicate is not null)
        {
            values[RestrictionHelpers.CausingEffectPredicateKey] = CausingEffectPredicate;
        }

        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        // Role Continuous (not Restriction): ContinuousRestrictionGate.Evaluate reads restrictions off the
        // CONTINUOUS-role effects (ContinuousScopeEvaluation.EvaluateForCard -> GetContinuousEffects ->
        // RestrictionHelpers.ReadRestrictions on their values), the same seam ContinuousSelfModifierEffect
        // rides. This also gets condition / inherited honouring for free.
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(PRIM-W1) A continuous PLAYER-SCOPE restriction — the restriction analogue of the player-scope
/// buff. Registers a <c>Restriction</c> flag over a player's cards (optionally narrowed by CardType) under
/// <see cref="ContinuousRestrictionGate.Scope"/>, collected by <c>ContinuousScopeEvaluation</c>'s player-scope
/// path. Covers the structured "your opponent's Digimon cannot digivolve" style; arbitrary per-permanent
/// predicates (the original's <c>Func&lt;Permanent,bool&gt;</c>) beyond CardType/meta scoping are per-card.</summary>
public sealed class ContinuousPlayerScopeRestrictionEffect : ICardEffect
{
    private readonly HeadlessPlayerId _scopePlayerId;

    public ContinuousPlayerScopeRestrictionEffect(CardSource card, HeadlessPlayerId scopePlayerId, string restrictionKey, string? scopeCardType, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? scopePredicate = null, Func<CardSource, bool>? causingEffectPredicate = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(restrictionKey);
        Card = card;
        _scopePlayerId = scopePlayerId;
        RestrictionKey = restrictionKey;
        ScopeCardType = scopeCardType;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        ScopePredicate = scopePredicate;
        CausingEffectPredicate = causingEffectPredicate;
    }

    public CardSource Card { get; }

    public string RestrictionKey { get; }

    public string? ScopeCardType { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public Func<CardSource, bool>? ScopePredicate { get; }

    public Func<CardSource, bool>? CausingEffectPredicate { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
            [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = _scopePlayerId.Value,
            [RestrictionKey] = true,
        };
        if (!string.IsNullOrWhiteSpace(ScopeCardType))
        {
            values[PlayerScopeContinuousHelpers.ScopeCardTypeKey] = ScopeCardType;
        }

        if (ScopePredicate is not null)
        {
            values[PlayerScopeContinuousHelpers.ScopePredicateKey] = ScopePredicate;
        }

        if (CausingEffectPredicate is not null)
        {
            values[RestrictionHelpers.CausingEffectPredicateKey] = CausingEffectPredicate;
        }

        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(PRIM-W2) Mirror of the original <c>&lt;Link&gt;</c> activation (<c>CardEffectFactory.LinkEffect</c>):
/// attach THIS card as a link card to a chosen own battle-area Digimon, paying the link cost. Drives the
/// host choice through the activation <c>ChoiceProvider</c> and attaches via
/// <see cref="Runtime.LinkHelpers.AddLinkCardAsync"/> (which emits the WhenLinked window / trims the host's
/// link max). Bounded to the self-play synchronous flow; the link CONDITION (which hosts are valid) is a
/// per-card predicate.</summary>
public sealed class LinkSelfEffect : IActivatedCardEffect
{
    public LinkSelfEffect(CardSource card, int linkCost, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        LinkCost = linkCost;
        Description = description;
    }

    public CardSource Card { get; }

    public int LinkCost { get; }

    public string Description { get; }

    public async Task ResolveAsync(CancellationToken cancellationToken)
    {
        EngineContext context = Card.Context;
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        List<ChoiceCandidate> candidates = zones.GetCards(Card.Owner, ChoiceZone.BattleArea)
            .Where(id => id != Card.InstanceId && CardEffectCommons.IsOwnerBattleAreaDigimon(Card, id))
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: Card.Owner))
            .ToList();
        if (candidates.Count == 0)
        {
            return; // no valid host.
        }

        var request = new ChoiceRequest(
            ChoiceType.Card, Card.Owner, Description, minCount: 0, maxCount: 1, canSkip: true, ChoiceZone.BattleArea, candidates);
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return;
        }

        // (M-4) fold continuous linkCostDelta reductions (GrantedReduceLinkCost) into the paid cost.
        int effectiveLinkCost = LinkHelpers.ResolveLinkCost(context, Card.InstanceId, LinkCost);
        if (effectiveLinkCost > 0)
        {
            context.MemoryController.Pay(effectiveLinkCost);
        }

        ChoiceZone from = zones.GetCards(Card.Owner, ChoiceZone.Hand).Contains(Card.InstanceId) ? ChoiceZone.Hand : ChoiceZone.BattleArea;
        await LinkHelpers.AddLinkCardAsync(
            context.CardInstanceRepository, context.ZoneMover, result.SelectedIds[0], Card.InstanceId, from, context.GameEventQueue, cancellationToken, context)
            .ConfigureAwait(false);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Link effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W1-6/9) A continuous "added digivolution requirement" on self — grants this card an
/// ADDITIONAL "Color@Level" from-condition (AS-IS AddDigivolutionRequirementStaticEffect /
/// AddDigivolutionRequirementClass). Registered under <see cref="ContinuousRestrictionGate.Scope"/> carrying
/// <see cref="DigivolveAction.AddedEvolutionConditionKey"/>; DigivolveAction consults it when the printed
/// condition fails. Condition / inherited honoured. (Per-path cost is composed via
/// <c>ChangeDigivolutionCostStaticEffect</c> or handled per-card.)</summary>
public sealed class AddedDigivolutionRequirementEffect : ICardEffect
{
    public AddedDigivolutionRequirementEffect(CardSource card, string fromCondition, bool isInheritedEffect, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromCondition);
        Card = card;
        FromCondition = fromCondition;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
    }

    public CardSource Card { get; }

    public string FromCondition { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [DigivolveAction.AddedEvolutionConditionKey] = FromCondition,
        };
        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(PRIM-W5) Predicate-based added digivolution source (AS-IS
/// <c>AddSelfDigivolutionRequirementStaticEffect</c>): "you can also digivolve this card from any Digimon
/// matching <see cref="Predicate"/> (for <see cref="DigivolutionCost"/> memory)". Registers the predicate on a
/// continuous binding that <c>DigivolveAction</c> evaluates by building the under-card as a <see cref="Permanent"/>.
/// Cost/ignore-requirement are retained for fidelity; the primary behavior is enabling the digivolve.</summary>
public sealed class AddedDigivolutionRequirementPredicateEffect : ICardEffect
{
    public AddedDigivolutionRequirementPredicateEffect(CardSource card, Func<Permanent, bool> predicate, int digivolutionCost, bool ignoreDigivolutionRequirement, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? targetCardCondition = null, Func<int>? costEquation = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(predicate);
        Card = card;
        Predicate = predicate;
        DigivolutionCost = digivolutionCost;
        IgnoreDigivolutionRequirement = ignoreDigivolutionRequirement;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        TargetCardCondition = targetCardCondition;
        CostEquation = costEquation;
    }

    public CardSource Card { get; }
    public Func<Permanent, bool> Predicate { get; }
    public int DigivolutionCost { get; }

    /// <summary>(FR2/M-3) AS-IS costEquation — a DYNAMIC digivolution cost for this added path, evaluated at read
    /// time (<c>costEquation() ?? digivolutionCost</c>). Null = the fixed <see cref="DigivolutionCost"/>.</summary>
    public Func<int>? CostEquation { get; }

    public bool IgnoreDigivolutionRequirement { get; }
    public bool IsInheritedEffect { get; }
    public Func<bool>? Condition { get; }

    /// <summary>(FR2/M-1) AS-IS cardCondition — WHICH cards receive this added digivolution requirement. Null =
    /// self only (default <c>cs => cs == card</c>). Non-null = any owner's card matching it (player-scope +
    /// predicate), e.g. "your UlforceVeedramon cards in hand".</summary>
    public Func<CardSource, bool>? TargetCardCondition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [DigivolveAction.AddedEvolutionPredicateKey] = Predicate,
            [DigivolveAction.AddedEvolutionCostKey] = DigivolutionCost,
        };
        if (CostEquation is not null)
        {
            values[DigivolveAction.AddedEvolutionCostEquationKey] = CostEquation;
        }

        if (TargetCardCondition is not null)
        {
            // Player-scope so the requirement reaches every owner's card the predicate selects (not just self).
            values[PlayerScopeContinuousHelpers.PlayerScopeKey] = true;
            values[PlayerScopeContinuousHelpers.ScopePlayerIdKey] = Card.Owner.Value;
            values[PlayerScopeContinuousHelpers.ScopePredicateKey] = TargetCardCondition;
        }

        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
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

/// <summary>
/// Self-static keyword grant for the <see cref="KeywordBaseBatch2Kind"/> family (Vortex / Alliance /
/// Overclock / …). Structural twin of <see cref="SelfKeywordEffect"/> (which covers Batch1) — the original
/// <c>CardEffectFactory</c> exposes a per-keyword <c>&lt;Keyword&gt;SelfEffect</c> for each of these, so the
/// headless mirror provides the same entry points lowering to a Batch2 binding. The "this Digimon is on the
/// battle area" guard the original <c>SelfEffect</c> wraps around <paramref name="condition"/> is enforced
/// here by the binding lifecycle (registered on enter-play, unregistered on leave) + the read-time
/// <see cref="ContinuousKeywordGate"/> query, matching how the existing Batch1 self-statics behave.
/// </summary>
public sealed class SelfKeywordBatch2Effect : ICardEffect
{
    public SelfKeywordBatch2Effect(CardSource card, KeywordBaseBatch2Kind kind, bool isInheritedEffect, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
        Kind = kind;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
    }

    public CardSource Card { get; }

    public KeywordBaseBatch2Kind Kind { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: new[] { Card.InstanceId });
        KeywordBaseBatch2Effect effect = KeywordBaseBatch2Factory.Create(
            Kind,
            Card.InstanceId,
            targetEntityId: Card.InstanceId,
            isInherited: IsInheritedEffect,
            isLinked: false);
        return KeywordBaseBatch2Factory.ToBinding(effect, Card.Controller, context);
    }
}

/// <summary>(PRIM-W2) A self-static keyword grant BY NAME — for keywords outside the Batch1/Batch2 enums
/// (Raid / Barrier / Collision / Fortitude / Evade) whose behaviour gates read a metadata flag. Registers a
/// keyword binding (keywords = [name], target self) so <see cref="ContinuousKeywordGate.HasKeyword"/> reports
/// it live; the same bar as the Batch2 self-statics. Condition / inherited carried on the binding values.</summary>
public sealed class SelfKeywordByNameEffect : ICardEffect
{
    public SelfKeywordByNameEffect(CardSource card, string keywordName, bool isInheritedEffect, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(keywordName);
        Card = card;
        KeywordName = keywordName;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
    }

    public CardSource Card { get; }

    public string KeywordName { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: new[] { KeywordName }, EffectQueryRole.Continuous, queryScopes: null, effect: null, duration: null);
    }
}

/// <summary>(PRIM-W2) A continuous PLAYER-SCOPE keyword grant — grants a keyword to a player's cards
/// (optionally narrowed by CardType), e.g. "your Digimon gain &lt;Blocker&gt;". Registers a keyword binding
/// (keywords = [name]) carrying the player-scope markers; <see cref="ContinuousKeywordGate.HasKeyword"/>
/// (context overload) resolves it for any of the scoped player's cards.</summary>
public sealed class ContinuousPlayerScopeKeywordEffect : ICardEffect
{
    private readonly HeadlessPlayerId _scopePlayerId;

    public ContinuousPlayerScopeKeywordEffect(CardSource card, HeadlessPlayerId scopePlayerId, string keywordName, string? scopeCardType, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? scopePredicate = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(keywordName);
        Card = card;
        _scopePlayerId = scopePlayerId;
        KeywordName = keywordName;
        ScopeCardType = scopeCardType;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        ScopePredicate = scopePredicate;
    }

    public CardSource Card { get; }

    public string KeywordName { get; }

    public string? ScopeCardType { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    public Func<CardSource, bool>? ScopePredicate { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
            [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = _scopePlayerId.Value,
        };
        if (!string.IsNullOrWhiteSpace(ScopeCardType))
        {
            values[PlayerScopeContinuousHelpers.ScopeCardTypeKey] = ScopeCardType;
        }

        if (ScopePredicate is not null)
        {
            values[PlayerScopeContinuousHelpers.ScopePredicateKey] = ScopePredicate;
        }

        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: new[] { KeywordName }, EffectQueryRole.Continuous, queryScopes: null, effect: null, duration: null);
    }
}

/// <summary>Minimal headless mirror of the original <c>Permanent</c> — used only for the signature of
/// card <c>permanentCondition</c> predicates. Player-scope effects scope to the owner's cards directly, so
/// the predicate body is not invoked by the headless evaluation (it exists for 1:1 source fidelity).</summary>
/// <summary>(PRIM-W5-0) A battle-area permanent view — the member surface card predicates read off
/// <c>permanent.*</c>. Backed by the engine: <see cref="TopCard"/> reuses <see cref="CardSource"/> for the
/// card-view members, DP folds continuous modifiers, and digivolution sources come from the stack.</summary>
public sealed class Permanent
{
    private readonly EngineContext _context;

    public Permanent(EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId ownerId)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        InstanceId = instanceId;
        OwnerId = ownerId;
    }

    public HeadlessEntityId InstanceId { get; }

    public HeadlessPlayerId OwnerId { get; }

    /// <summary>The top (battling) card of this permanent as a <see cref="CardSource"/>.</summary>
    public CardSource TopCard => new(_context, InstanceId, OwnerId);

    /// <summary>Effective DP (base + continuous modifiers), or 0.</summary>
    public int DP => ContinuousDpGate.ResolveDp(_context, InstanceId, BaseDp());

    public int Level => TopCard.Level;
    public bool HasNoDigivolutionCards => DigivolutionCards.Count == 0;
    public bool IsDigimon => TopCard.IsDigimon;
    public bool IsTamer => TopCard.IsTamer;
    public bool IsToken => TopCard.IsToken;

    public bool IsSuspended =>
        _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
        && i.Metadata.TryGetValue("isSuspended", out object? raw) && raw is bool b && b;

    /// <summary>The digivolution (under-)cards of this permanent (mirror of <c>DigivolutionCards</c>).</summary>
    public IReadOnlyList<CardSource> DigivolutionCards
    {
        get
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(_context.CardInstanceRepository, _context.CardRepository, InstanceId);
            return stack.UnderCards.Select(u => new CardSource(_context, u.InstanceId, OwnerId)).ToArray();
        }
    }

    private int BaseDp() =>
        _context.CardInstanceRepository.TryGetInstance(InstanceId, out CardInstanceRecord? i) && i is not null
        && i.Metadata.TryGetValue("dp", out object? raw) && raw is int dp ? dp : 0;
}

/// <summary>
/// A continuous player-scope numeric modifier ("your Digimon get +X DP"). Lowers to a continuous-role
/// binding carrying the player-scope markers (<see cref="PlayerScopeContinuousHelpers"/>) so it reaches
/// every applicable card the owner controls via <see cref="ContinuousScopeEvaluation"/>.
/// </summary>
public sealed class PlayerScopeModifierEffect : ICardEffect
{
    public PlayerScopeModifierEffect(CardSource card, string deltaKey, int changeValue, string? scopeCardType, Func<bool>? condition, string? scopeZone = null, Func<CardSource, bool>? scopePredicate = null, bool scopeAnyPlayer = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(deltaKey);
        Card = card;
        DeltaKey = deltaKey;
        ChangeValue = changeValue;
        ScopeCardType = scopeCardType;
        Condition = condition;
        ScopeZone = scopeZone;
        ScopePredicate = scopePredicate;
        ScopeAnyPlayer = scopeAnyPlayer;
    }

    public CardSource Card { get; }

    public string DeltaKey { get; }

    public int ChangeValue { get; }

    public string? ScopeCardType { get; }

    public Func<bool>? Condition { get; }

    public string? ScopeZone { get; }

    public Func<CardSource, bool>? ScopePredicate { get; }

    public bool ScopeAnyPlayer { get; }

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

        if (!string.IsNullOrWhiteSpace(ScopeZone))
        {
            values[PlayerScopeContinuousHelpers.ScopeZoneKey] = ScopeZone;
        }

        if (ScopePredicate is not null)
        {
            values[PlayerScopeContinuousHelpers.ScopePredicateKey] = ScopePredicate;
        }

        if (ScopeAnyPlayer)
        {
            values[PlayerScopeContinuousHelpers.ScopeAnyPlayerKey] = true;
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
    private readonly Func<CardEffectResolveContext, bool>? _triggerGate;

    public TriggeredMemoryEffect(
        CardSource card, EffectTiming timing, int amount, bool isInheritedEffect, Func<bool>? condition, string description,
        Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null, bool? isOptional = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Amount = amount;
        IsInheritedEffect = isInheritedEffect;
        _condition = condition;
        _triggerGate = triggerGate;
        string trigger = EffectTimings.ToTriggerName(timing);
        var effectId = new HeadlessEntityId($"{card.InstanceId.Value}:mem:{trigger}:{amount}");
        // Gaining memory defaults to an optional "you may" prompt; a card whose trigger is mandatory passes
        // isOptional: false explicitly (e.g. ST3_04 "gain 1 memory").
        Definition = new CardEffectDefinition(effectId, card.InstanceId, description, trigger, isOptional: isOptional ?? (amount > 0), maxCountPerTurn: maxCountPerTurn, hash: hash);
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

        if (_triggerGate is not null && !_triggerGate(context))
        {
            return CardEffectCanResolveResult.Failure("Trigger event condition not met.");
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

/// <summary>(G8-004) "[Security] activate this card's [Main] effect" — a security skill that re-runs the
/// card's Option [Main] activated effects. Resolved by <see cref="ActivatedEffectResolver"/>; not
/// auto-registered (security timing is excluded from <see cref="CardEffectRegistrar.AllTimings"/>).</summary>
public sealed class ReuseMainOptionEffect : IActivatedCardEffect
{
    public ReuseMainOptionEffect(string description)
    {
        Description = description;
    }

    public string Description { get; }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Reuse-main security effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (EX8-2 brick) Re-activates THIS card's <see cref="EffectTiming.WhenDigivolving"/> effects through the
/// choice flow — the headless analog of the original "[All Turns] you may activate 1 of this Digimon's
/// [When Digivolving] effects" (EX8_074 region "All Turns"). Structural twin of
/// <see cref="ReuseMainOptionEffect"/> (which re-runs [Main]/OptionSkill): when resolved,
/// <see cref="ActivatedEffectResolver"/> recursively resolves <c>CardEffects(WhenDigivolving)</c> on the same
/// sink / choice provider. The once-per-turn, "when any Digimon is played" TRIGGER that OFFERS this effect
/// is the remaining EX8-2 integration (see docs/audit/ex8_074_remaining_goals.md §EX8-2).
/// </summary>
public sealed class ReuseWhenDigivolvingEffect : IActivatedCardEffect
{
    public ReuseWhenDigivolvingEffect(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description;
    }

    public string Description { get; }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Reuse-when-digivolving effect is resolved via the activation flow, not registered: {Description}");
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
/// (EX8_074 Stage 3 brick) An activated "suspend N of your Digimon to reduce THIS card's play cost by M"
/// effect — the headless composite of the original <c>SuspendPermanentsClass.Tap()</c> +
/// <c>ChangeCostClass</c> added to <c>Player.UntilCalculateFixedCostEffect</c>. Selecting EXACTLY
/// <see cref="SuspendCount"/> own Digimon suspends them (<see cref="SelectPermanentEffect.Mode.Tap"/> →
/// <c>SuspendKind</c>) and registers a one-shot self play-cost reduction binding
/// (<see cref="EffectDuration.UntilCalculateFixedCost"/> — cleared by PlayCardAction's
/// <c>ExpireFixedCostCalc</c> once the play's cost is locked, mirroring the original's one-shot lifetime).
/// Selecting fewer (declined / insufficient) applies nothing — the original adds the ChangeCostClass only
/// inside the "permanents.Count == 2" branch. Resolved via the choice flow (<see cref="ActivatedEffectResolver"/>),
/// not auto-registered. This brick is engine-side only; wiring it into the BeforePayCost pre-payment window
/// of PlayCardAction is a later stage.
/// </summary>
public sealed class SuspendCostReductionEffect : IActivatedCardEffect
{
    private readonly SelectPermanentEffect _select = new();

    private readonly Func<HeadlessEntityId, bool> _canSuspendTarget;

    public SuspendCostReductionEffect(
        CardSource card,
        Func<HeadlessEntityId, bool> canSuspendTarget,
        int suspendCount,
        int costReduction,
        string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canSuspendTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (suspendCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(suspendCount), "Suspend count must be positive.");
        }

        if (costReduction <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costReduction), "Cost reduction must be positive.");
        }

        Card = card;
        SuspendCount = suspendCount;
        CostReduction = costReduction;
        Description = description;
        _canSuspendTarget = canSuspendTarget;
        // Configure the suspend selection (Mode.Tap); canNoSelect is recomputed per BuildRequest from the
        // owner's affordability (see Configure). The ctor setup also keeps Apply safe if called without a
        // prior BuildRequest (mode must be Tap).
        Configure(canNoSelect: true);
        _select.SetUpCustomMessage(description);
    }

    public CardSource Card { get; }

    public int SuspendCount { get; }

    public int CostReduction { get; }

    public string Description { get; }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        // (#1↔#2 coupling) The original sets canNoSelect:false when the player cannot otherwise afford the
        // card (PayingCost > MaxMemoryCost) — the suspend is FORCED when the reduction is the only way to
        // pay; optional (canNoSelect:true) when the full cost is affordable without it.
        Configure(canNoSelect: CanAffordFullCost());
        return _select.BuildRequest((IZoneStateReader)Card.Context.ZoneMover, players);
    }

    private void Configure(bool canNoSelect) =>
        _select.SetUp(Card.Owner, _canSuspendTarget, maxCount: SuspendCount, canNoSelect, canEndNotMax: false, SelectPermanentEffect.Mode.Tap, Card.InstanceId);

    /// <summary>Whether the owner can pay this card's FULL play cost (without this reduction, which is only
    /// registered in <see cref="Apply"/>). When false, the suspend is mandatory.</summary>
    private bool CanAffordFullCost()
    {
        if (!Card.Context.CardInstanceRepository.TryGetInstance(Card.InstanceId, out CardInstanceRecord? instance) || instance is null
            || !Card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) || def is null
            || !PlayCostHelpers.TryResolveCost(def, instance, out int baseCost, out _))
        {
            return true; // unknown cost → don't force the suspend
        }

        int fullCost = ContinuousModifierGate.ResolvePlayCost(Card.Context, Card.InstanceId, baseCost);
        return Card.Context.MemoryController.CanPay(fullCost);
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        List<HeadlessEntityId> ids = selected?.ToList() ?? new List<HeadlessEntityId>();
        if (ids.Count != SuspendCount)
        {
            // Declined or short — mirror the original: the ChangeCostClass is only added when exactly N
            // Digimon were suspended. No suspend, no reduction.
            return;
        }

        _select.Apply(sink, ids);
        Card.Context.EffectRegistry.Register(BuildReductionBinding());
    }

    /// <summary>The one-shot self play-cost reduction the suspend pays for — a <c>playCostDelta = -M</c>
    /// continuous self modifier scoped/keyed exactly like <see cref="ContinuousSelfModifierEffect"/>, but
    /// tagged <see cref="EffectDuration.UntilCalculateFixedCost"/> so it lasts only until this play's cost is
    /// locked in (mirrors <c>Player.UntilCalculateFixedCostEffect.Add(_ => changeCostClass)</c>).</summary>
    private EffectBinding BuildReductionBinding()
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ModifierHelpers.PlayCostDeltaKey] = -CostReduction,
        };
        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: new[] { Card.InstanceId },
            values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:beforePayCostReduction"), Card.Controller, "Continuous", context),
            keywords: null,
            EffectQueryRole.Continuous,
            new[] { ContinuousModifierGate.Scope },
            effect: null,
            duration: EffectDuration.UntilCalculateFixedCost);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Suspend-cost-reduction effect is resolved via the activation flow, not registered: {Description}");
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
    private readonly HeadlessPlayerId _scopePlayerId;

    public ActivatedPlayerScopeBuffEffect(CardSource card, string deltaKey, int changeValue, EffectDuration duration, string scopeCardType, string description, string? scopeZone = null, HeadlessPlayerId? scopePlayerId = null)
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
        _scopePlayerId = scopePlayerId ?? card.Owner;
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
            [PlayerScopeContinuousHelpers.ScopePlayerIdKey] = _scopePlayerId.Value,
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
            new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:pscopebuff:{DeltaKey}:{ScopeZone ?? "battle"}:{_scopePlayerId.Value}"), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope }, effect: null, duration: Duration);
        Card.Context.EffectRegistry.Register(binding);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated player-scope buff is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// A triggered "[When ...] unsuspend this Digimon" effect (the common ActivateClass IUnsuspendPermanents
/// form, e.g. ST2_11). Auto-registered under its trigger timing; on resolution emits an Unsuspend mutation
/// on the source card. (The original's [Once Per Turn] gate maps to the once-flag subsystem; the headless
/// emission is unconditional for now — a 1:1 relaxation, like the threshold relaxations in ST1.)
/// </summary>
public sealed class TriggeredUnsuspendSelfEffect : ICardEffect, IHeadlessCardEffect
{
    public TriggeredUnsuspendSelfEffect(CardSource card, EffectTiming timing, string description, int? maxCountPerTurn = null, string? hash = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:unsuspendself:{trigger}"), card.InstanceId, description, trigger,
            isOptional: true, maxCountPerTurn: maxCountPerTurn, hash: hash);
    }

    public CardSource Card { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();
        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.UnsuspendKind,
            Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value }));
        return ValueTask.FromResult(EffectResult.Success("Unsuspend this Digimon."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}

/// <summary>(PRIM-W2) A triggered "set your memory to <see cref="TargetMemory"/> if it is
/// &lt;= <see cref="Threshold"/>" effect — the Tamer memory-setter family (AS-IS SetMemoryTo3TamerEffect:
/// "[Start of Your Turn] If you have 2 or less memory, set your memory to 3."). Auto-registered under its
/// timing (OnStartTurn); resolves only on the owner's turn (mirrors IsOwnerTurn) and only when the current
/// memory is at or below the threshold, emitting a SetMemory mutation.</summary>
public sealed class TriggeredSetMemoryEffect : ICardEffect, IHeadlessCardEffect
{
    public TriggeredSetMemoryEffect(CardSource card, EffectTiming timing, int targetMemory, int threshold, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        TargetMemory = targetMemory;
        Threshold = threshold;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:setmemory:{trigger}"), card.InstanceId, description, trigger, isOptional: false);
    }

    public CardSource Card { get; }

    public int TargetMemory { get; }

    public int Threshold { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();

        // AS-IS IsOwnerTurn + "2 or less memory" gate.
        if (Card.Context.TurnController.Current.TurnPlayerId != Card.Owner
            || Card.Context.MemoryController.Current.Current > Threshold)
        {
            return ValueTask.FromResult(EffectResult.Success("Set-memory condition not met; no change."));
        }

        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.SetMemoryKind,
            Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = TargetMemory }));
        return ValueTask.FromResult(EffectResult.Success($"Set memory to {TargetMemory}."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}

/// <summary>(PRIM-W3) A triggered "gain <see cref="Amount"/> memory (if <see cref="ExtraCondition"/> holds)"
/// effect — the Tamer memory-gain family (AS-IS Gain1MemoryTamerOpponentDigimonEffect etc.). Auto-registered
/// under its timing; resolves only on the owner's turn (and when the extra condition passes), emitting an
/// AddMemory mutation.</summary>
public sealed class TriggeredGainMemoryEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly Func<bool>? _extraCondition;

    public TriggeredGainMemoryEffect(CardSource card, EffectTiming timing, int amount, string description, Func<bool>? extraCondition = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Amount = amount;
        _extraCondition = extraCondition;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:gainmemory:{trigger}"), card.InstanceId, description, trigger, isOptional: false);
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();

        if (Card.Context.TurnController.Current.TurnPlayerId != Card.Owner || (_extraCondition is not null && !_extraCondition()))
        {
            return ValueTask.FromResult(EffectResult.Success("Gain-memory condition not met; no change."));
        }

        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind,
            Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount }));
        return ValueTask.FromResult(EffectResult.Success($"Gain {Amount} memory."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}

/// <summary>
/// An activated "select up to <paramref name="maxCount"/> opponent Digimon and trash
/// <paramref name="trashCount"/> of each host's digivolution cards" effect (e.g. ST2_03 / ST2_06 / ST2_09).
/// Resolved imperatively (BuildRequest → answer → Apply); Apply emits a TrashDigivolutionCards mutation
/// (host = selected target) for each chosen host.
/// </summary>
public sealed class ActivatedSelectTrashDigivolutionEffect : IActivatedCardEffect
{
    private readonly SelectPermanentEffect _select = new();
    private readonly int _trashCount;
    private readonly bool _fromBottom;

    public ActivatedSelectTrashDigivolutionEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int trashCount, bool fromBottom, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
        _trashCount = trashCount;
        _fromBottom = fromBottom;
        _select.SetUp(card.Owner, canTarget, maxCount, canNoSelect: false, canEndNotMax: maxCount > 1, SelectPermanentEffect.Mode.Custom, card.InstanceId);
        _select.SetUpCustomMessage(description);
    }

    public CardSource Card { get; }

    public string Description { get; }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players) =>
        _select.BuildRequest((IZoneStateReader)Card.Context.ZoneMover, players);

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(selected);
        foreach (HeadlessEntityId host in selected)
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.TrashDigivolutionCardsKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = host.Value,
                    [MatchStateMutationSink.CountKey] = _trashCount,
                    [MatchStateMutationSink.FromBottomKey] = _fromBottom,
                }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated trash-digivolution effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated "gain/lose N memory" effect for player-activated skills (Option [Main] / [Security], e.g.
/// ST2_13). Resolved imperatively; <see cref="Apply"/> emits an AddMemory mutation.
/// </summary>
public sealed class ActivatedMemoryEffect : IActivatedCardEffect
{
    public ActivatedMemoryEffect(CardSource card, int amount, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Amount = amount;
        Description = description;
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated memory effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (BT-PRE-A1) Mirror of the original <c>DrawClass</c> (DCGO/Assets/Scripts/Script/CardController.cs):
/// "draw <see cref="DrawCount"/> cards" from the top of the controller's library to their hand. The AS-IS
/// <c>Draw()</c> guards drawCount &gt; 0 and an empty library (no-op), and draws min(count, available); those
/// guards live in <c>ZoneMover.DrawAsync</c>, which this stages via the sink's <c>DrawCards</c> mutation so
/// it flushes once with the rest of the activation (re-run safe under the deferred-choice cycle — a later
/// effect suspending will NOT double-draw, since nothing flushes until resolution completes).
/// </summary>
public sealed class DrawEffect : IActivatedCardEffect
{
    public DrawEffect(CardSource card, int drawCount, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        DrawCount = drawCount;
        Description = description;
    }

    public CardSource Card { get; }

    public int DrawCount { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        // AS-IS DrawClass.Draw(): `if (_drawCount <= 0) yield break;` — emit nothing for a non-positive count.
        if (DrawCount <= 0)
        {
            return;
        }

        sink.Apply(new EffectMutation(
            MatchStateMutationSink.DrawCardsKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = Card.Owner,
                [MatchStateMutationSink.CountKey] = DrawCount,
            }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Draw effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (BT-PRE-A2) Mirror of the original <c>SimplifiedSelectCardConditionClass</c>
/// (DCGO/Assets/Scripts/Script/CardEffectCommons/RevealLibrary.cs): a single "select up to
/// <see cref="MaxCount"/> revealed cards matching <see cref="CanTargetCondition"/>, sending the chosen ones
/// to <see cref="SelectedTo"/>" condition, used inside <see cref="SimplifiedRevealAndSelectEffect"/>. The
/// AS-IS <c>mode</c> (<c>SelectCardEffect.Mode</c>) is represented by a <see cref="RevealDestination"/> — the
/// dominant BT usage is <c>Mode.AddHand</c> → <see cref="RevealDestination.Hand"/> (tutor). The AS-IS
/// <c>selectCardCoroutine</c> (Mode.Custom) per-card action is a per-card follow-up (not modeled here).
/// </summary>
public sealed class SimplifiedSelectCardConditionClass
{
    public SimplifiedSelectCardConditionClass(
        Func<HeadlessEntityId, bool> canTargetCondition,
        string message,
        RevealDestination selectedTo,
        int maxCount)
    {
        ArgumentNullException.ThrowIfNull(canTargetCondition);
        CanTargetCondition = canTargetCondition;
        Message = message ?? string.Empty;
        SelectedTo = selectedTo;
        MaxCount = maxCount;
    }

    public Func<HeadlessEntityId, bool> CanTargetCondition { get; }

    public string Message { get; }

    public RevealDestination SelectedTo { get; }

    public int MaxCount { get; }
}

/// <summary>(PRIM-W2) Mirror of the original <c>SelectCardConditionClass</c>
/// (CardEffectCommons/RevealLibrary.cs) — the fuller reveal-select descriptor
/// (<see cref="SimplifiedSelectCardConditionClass"/> is the simplified twin, which the original maps onto
/// this). Consumed by the same reveal-select mechanism (<see cref="SimplifiedRevealAndSelectEffect"/>) via
/// <see cref="ToSimplified"/>. The advanced predicates (by-pre-selected-list / can-end-select) and the
/// <c>Mode.Custom</c> per-card select action are accepted for 1:1 source fidelity but resolved per-card.</summary>
public sealed class SelectCardConditionClass
{
    public SelectCardConditionClass(
        Func<HeadlessEntityId, bool> canTargetCondition,
        Func<IReadOnlyList<HeadlessEntityId>, HeadlessEntityId, bool>? canTargetConditionByPreSelectedList,
        Func<IReadOnlyList<HeadlessEntityId>, bool>? canEndSelectCondition,
        bool canNoSelect,
        string message,
        int maxCount,
        bool canEndNotMax,
        RevealDestination selectedTo)
    {
        ArgumentNullException.ThrowIfNull(canTargetCondition);
        CanTargetCondition = canTargetCondition;
        CanTargetConditionByPreSelectedList = canTargetConditionByPreSelectedList;
        CanEndSelectCondition = canEndSelectCondition;
        CanNoSelect = canNoSelect;
        Message = message ?? string.Empty;
        MaxCount = maxCount;
        CanEndNotMax = canEndNotMax;
        SelectedTo = selectedTo;
    }

    public Func<HeadlessEntityId, bool> CanTargetCondition { get; }

    public Func<IReadOnlyList<HeadlessEntityId>, HeadlessEntityId, bool>? CanTargetConditionByPreSelectedList { get; }

    public Func<IReadOnlyList<HeadlessEntityId>, bool>? CanEndSelectCondition { get; }

    public bool CanNoSelect { get; }

    public string Message { get; }

    public int MaxCount { get; }

    public bool CanEndNotMax { get; }

    public RevealDestination SelectedTo { get; }

    /// <summary>The core (condition/message/destination/maxCount) as the simplified twin the reveal-select
    /// mechanism consumes.</summary>
    public SimplifiedSelectCardConditionClass ToSimplified() =>
        new(CanTargetCondition, Message, SelectedTo, MaxCount);
}

/// <summary>
/// (BT-PRE-A2) Mirror of the original <c>CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect</c>: reveal
/// the top <see cref="RevealCount"/> library cards, run each <see cref="SimplifiedSelectCardConditionClass"/>
/// in turn (select condition-matching revealed cards → that condition's destination), then send every
/// still-unselected revealed card to <see cref="RemainingTo"/>. Choices flow through the activation
/// <c>ChoiceProvider</c> (re-run safe: choose-then-stage, all moves staged on the sink and flushed once).
/// </summary>
public sealed class SimplifiedRevealAndSelectEffect : IActivatedCardEffect
{
    private readonly IReadOnlyList<SimplifiedSelectCardConditionClass> _conditions;

    public SimplifiedRevealAndSelectEffect(
        CardSource card,
        int revealCount,
        IReadOnlyList<SimplifiedSelectCardConditionClass> conditions,
        RevealDestination remainingTo,
        string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        RevealCount = revealCount;
        _conditions = conditions;
        RemainingTo = remainingTo;
        Description = description;
    }

    public CardSource Card { get; }

    public int RevealCount { get; }

    public RevealDestination RemainingTo { get; }

    public string Description { get; }

    /// <summary>Reveal + per-condition select + destination routing. Driven by <see cref="ActivatedEffectResolver"/>
    /// (which has the live ChoiceProvider); all moves are staged on <paramref name="sink"/> for one flush.</summary>
    public async Task ResolveAsync(MatchStateMutationSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        EngineContext context = Card.Context;
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        HeadlessPlayerId player = Card.Owner;
        List<HeadlessEntityId> revealed = zones.GetCards(player, ChoiceZone.Library)
            .Take(Math.Max(0, RevealCount)).ToList();
        if (revealed.Count == 0)
        {
            return; // AS-IS: nothing to reveal -> no-op.
        }

        var picked = new HashSet<HeadlessEntityId>();
        foreach (SimplifiedSelectCardConditionClass condition in _conditions)
        {
            List<ChoiceCandidate> candidates = revealed
                .Where(id => !picked.Contains(id) && condition.CanTargetCondition(id))
                .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Library, IsSelectable: true, ownerId: player))
                .ToList();
            if (candidates.Count == 0)
            {
                continue; // no match for this condition -> skip it (AS-IS surfaces an empty/auto selection).
            }

            int max = Math.Min(condition.MaxCount, candidates.Count);
            var request = new ChoiceRequest(
                ChoiceType.Card, player, string.IsNullOrEmpty(condition.Message) ? Description : condition.Message,
                minCount: 0, maxCount: Math.Max(1, max), canSkip: true, ChoiceZone.Library, candidates);

            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.IsSkipped)
            {
                continue;
            }

            foreach (HeadlessEntityId id in result.SelectedIds)
            {
                if (picked.Add(id))
                {
                    StageMove(sink, id, condition.SelectedTo);
                }
            }
        }

        foreach (HeadlessEntityId id in revealed)
        {
            if (!picked.Contains(id))
            {
                StageMove(sink, id, RemainingTo);
            }
        }
    }

    private void StageMove(MatchStateMutationSink sink, HeadlessEntityId cardId, RevealDestination destination)
    {
        string kind = destination switch
        {
            RevealDestination.Hand => MatchStateMutationSink.ReturnToHandKind,
            RevealDestination.DeckTop => MatchStateMutationSink.ReturnToDeckTopKind,
            RevealDestination.DeckBottom => MatchStateMutationSink.ReturnToDeckBottomKind,
            RevealDestination.Trash => MatchStateMutationSink.TrashCardKind,
            _ => MatchStateMutationSink.ReturnToDeckBottomKind,
        };
        sink.Apply(new EffectMutation(
            kind, Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Reveal-and-select effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (BT-PRE-A3) Mirror of the original <c>DestroyPermanentsClass</c>
/// (DCGO/Assets/Scripts/Script/CardController.cs): DIRECTLY delete a pre-computed list of permanents (no
/// selection — the card already filtered them, e.g. "all enemy Digimon with the same name"). Each target is
/// staged as a <c>Delete</c> sink mutation whose source is this card, so the sink's CENTRALISED gates apply:
/// opponent-effect immunity (<see cref="HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate"/> — AS-IS <c>CanNotBeAffected</c>) and
/// deletion-prevention (<c>cannotBeDeleted</c> / continuous prevent) / the optional would-be-deleted window.
/// The AS-IS filter is NOT re-implemented here (EX8_074 lesson). NOTE: the AS-IS <c>CanBeDestroyedBySkill</c>
/// (skill-destroy immunity) is not modeled engine-wide (<c>CanNotBeDestroyedBySkillClass</c> is an unported
/// skeleton — no card sets it), so it is a documented engine gap, not re-implemented in this predicate.
/// </summary>
public sealed class DestroyPermanentsEffect : IActivatedCardEffect
{
    private readonly IReadOnlyList<HeadlessEntityId> _targets;

    public DestroyPermanentsEffect(CardSource card, IReadOnlyList<HeadlessEntityId> targets, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _targets = targets;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        foreach (HeadlessEntityId target in _targets)
        {
            if (target.IsEmpty)
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.DeleteKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Destroy-permanents effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W2) Mirror of the original <c>DeckBottomBounceClass</c> (CardController.cs): return a
/// pre-computed list of permanents to the bottom of their owners' decks. Each target is staged as a
/// <c>ReturnToDeckBottom</c> sink mutation; the sink's centralised immunity gate filters (source = this
/// card), mirroring <see cref="DestroyPermanentsEffect"/> for the delete case.</summary>
public sealed class DeckBottomBounceEffect : IActivatedCardEffect
{
    private readonly IReadOnlyList<HeadlessEntityId> _targets;

    public DeckBottomBounceEffect(CardSource card, IReadOnlyList<HeadlessEntityId> targets, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _targets = targets;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        foreach (HeadlessEntityId target in _targets)
        {
            if (target.IsEmpty)
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.ReturnToDeckBottomKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Deck-bottom-bounce effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W3) Mirror of AS-IS <c>ReturnToLibraryBottomDigivolutionCardsClass</c> — returns the host's
/// own digivolution (under-)cards to the bottom of the deck. Emits the engine's existing
/// <see cref="MatchStateMutationSink.ReturnDigivolutionCardsKind"/> (toDeck) on the host.</summary>
public sealed class ReturnSelfDigivolutionCardsToDeckEffect : IActivatedCardEffect
{
    private readonly int _count;

    public ReturnSelfDigivolutionCardsToDeckEffect(CardSource card, int count, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _count = count;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnDigivolutionCardsKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value,
                [MatchStateMutationSink.CountKey] = _count,
                [MatchStateMutationSink.ToDeckKey] = true,
                [MatchStateMutationSink.FromBottomKey] = true,
            }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Return-digivolution-to-deck effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W3) Mirror of AS-IS <c>ReplaceBottomSecurityWithFaceUpOption(Main)Effect</c> — "add your
/// bottom security card to the hand, then place this card face up as the bottom security card." Emits
/// ReturnToHand on the current bottom security card, then AddToSecurity (face up, bottom) for the host.</summary>
public sealed class ReplaceBottomSecurityWithFaceUpEffect : IActivatedCardEffect
{
    private readonly bool _top;

    public ReplaceBottomSecurityWithFaceUpEffect(CardSource card, string description, bool top = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
        _top = top;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (Card.Context.ZoneMover is IZoneStateReader reader)
        {
            IReadOnlyList<HeadlessEntityId> security = reader.GetCards(Card.Owner, ChoiceZone.Security);
            if (security.Count > 0)
            {
                // Top security = index 0; bottom = last of the ordered stack.
                HeadlessEntityId target = _top ? security[0] : security[^1];
                sink.Apply(new EffectMutation(
                    MatchStateMutationSink.ReturnToHandKind,
                    Card.InstanceId,
                    new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
            }
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value,
            [MatchStateMutationSink.FaceUpKey] = true,
        };
        if (!_top)
        {
            values[MatchStateMutationSink.ToBottomKey] = true;
        }

        sink.Apply(new EffectMutation(MatchStateMutationSink.AddToSecurityKind, Card.InstanceId, values));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Replace-bottom-security effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W4) Mirror of AS-IS <c>RevealLibraryClass</c> — reveals the top N cards of the owner's
/// deck. The full-information headless model has no hidden state to expose, so this carries no mutation; it
/// exists so a card that reveals-then-acts can declare the reveal step (the follow-up act is authored per
/// card). The reveal count is retained for logging / any card-facing consumer.</summary>
public sealed class InformationalRevealEffect : IActivatedCardEffect
{
    public InformationalRevealEffect(CardSource card, int revealCount, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        RevealCount = revealCount;
        Description = description;
    }

    public CardSource Card { get; }

    public int RevealCount { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        // No state change: a reveal exposes cards to the opponent, which the full-information model already has.
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Informational reveal effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W3, C-24) Mirror of AS-IS <c>TrainingEffect</c> — an activated [Breeding] effect: suspend
/// self (cost) and place the top card of the owner's deck at the bottom of self's digivolution stack. Wraps
/// the engine's <see cref="DigivolutionStackHelpers.TrainAsync"/> primitive via the Train mutation.</summary>
public sealed class TrainingActivatedEffect : IActivatedCardEffect
{
    public TrainingActivatedEffect(CardSource card, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.TrainKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Training effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W3, C-23) Mirror of AS-IS <c>MaterialSaveEffect</c> — re-parents <c>count</c> of this
/// Digimon's digivolution cards to another of the owner's Digimon (<paramref name="destinationId"/>, selected
/// at porting time). Wraps the engine's <see cref="DigivolutionStackHelpers.MoveSourcesBottom"/> primitive
/// via the MaterialSave mutation.</summary>
public sealed class MaterialSaveActivatedEffect : IActivatedCardEffect
{
    private readonly HeadlessEntityId _destinationId;
    private readonly int _count;

    public MaterialSaveActivatedEffect(CardSource card, HeadlessEntityId destinationId, int count, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _destinationId = destinationId;
        _count = count;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (_destinationId.IsEmpty)
        {
            return;
        }

        sink.Apply(new EffectMutation(
            MatchStateMutationSink.MaterialSaveKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.ToEntityIdKey] = _destinationId.Value,
                [MatchStateMutationSink.CountKey] = _count,
            }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Material-save effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (BT-PRE-A4) Mirror of the original <c>HatchDigiEggClass</c>
/// (DCGO/Assets/Scripts/Script/CardController.cs): if the controller <c>CanHatch</c> (an empty breeding area
/// and an available digi-egg), move the top digi-egg from the digitama library into the breeding area. The
/// AS-IS <c>CanHatch</c> guard is mirrored explicitly here — the raw <c>ZoneMover.HatchDigitamaAsync</c> only
/// checks for an available egg, NOT the empty-breeding-area rule (that lives on the legal-action dispatcher),
/// so the effect re-checks it (also keeping this re-run safe: a second pass finds the breeding area occupied
/// and no-ops).
/// </summary>
public sealed class HatchDigiEggEffect : IActivatedCardEffect
{
    public HatchDigiEggEffect(CardSource card, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public async Task ResolveAsync(CancellationToken cancellationToken)
    {
        EngineContext context = Card.Context;
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        HeadlessPlayerId player = Card.Owner;
        // AS-IS CanHatch: an empty breeding area AND an available digi-egg.
        if (zones.GetCards(player, ChoiceZone.BreedingArea).Count > 0
            || zones.GetCards(player, ChoiceZone.DigitamaLibrary).Count == 0)
        {
            return;
        }

        await context.ZoneMover.HatchDigitamaAsync(player, cancellationToken).ConfigureAwait(false);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Hatch effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// (BT-PRE-A5) Mirror of the original <c>PlayCardClass</c>
/// (DCGO/Assets/Scripts/Script/CardController.cs) for the SIMPLE cost-free play the BT sets actually use
/// (BT1_078: <c>payCost: false, root: Library</c>): play <see cref="TargetCardId"/> from <see cref="FromZone"/>
/// onto the battle area at no cost. Staged as a <c>PlayCard</c> sink mutation (same seam as
/// <see cref="PlayThisCardToBattleEffect"/>, generalised to an arbitrary target). The original's
/// jogress / burst / app-fusion / targetPermanent / isTapped / <c>payCost:true</c> branches are NOT modeled
/// here (out of BT-PRE scope — no such mechanism is invented until a card needs it).
/// </summary>
public sealed class PlayCardEffect : IActivatedCardEffect
{
    public PlayCardEffect(CardSource card, HeadlessEntityId targetCardId, ChoiceZone fromZone, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        TargetCardId = targetCardId;
        FromZone = fromZone;
        Description = description;
    }

    public CardSource Card { get; }

    public HeadlessEntityId TargetCardId { get; }

    public ChoiceZone FromZone { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (TargetCardId.IsEmpty)
        {
            return;
        }

        sink.Apply(new EffectMutation(
            MatchStateMutationSink.PlayCardKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = TargetCardId.Value,
                [MatchStateMutationSink.FromZoneKey] = FromZone.ToString(),
            }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Play-card effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W5) A material condition for a Blast-DNA digivolution (AS-IS <c>BlastDNACondition</c>) —
/// the material card names that fuse. Card-facing shim so ported cards compile.</summary>
/// <summary>(S2) A continuous effect-immunity registered under <see cref="HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate"/>
/// (AS-IS <c>CanNotAffectedClass</c>). Carries the per-card <c>SkillCondition</c> (over the causing effect's
/// source) so the immunity gate evaluates it 1:1. Null skill → opponent-only fallback.</summary>
public sealed class ContinuousImmunityEffect : ICardEffect
{
    public ContinuousImmunityEffect(CardSource card, Func<CardSource, bool>? skillCondition, bool isInheritedEffect, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
        SkillCondition = skillCondition;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
    }

    public CardSource Card { get; }
    public Func<CardSource, bool>? SkillCondition { get; }
    public bool IsInheritedEffect { get; }
    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (SkillCondition is not null)
        {
            values[HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate.SkillPredicateKey] = SkillCondition;
        }
        else
        {
            values[HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate.ImmunityFromOpponentOnlyKey] = true;
        }

        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(FR-P3) A defender-conditional "cannot attack" restriction (AS-IS
/// <c>CanNotAttackTargetDefendingPermanentClass</c> with a <c>defenderCondition</c>): the attacker may not
/// attack defenders matching <see cref="DefenderPredicate"/>, but MAY attack others. Registers a self
/// CannotAttack binding carrying the defender predicate, which ContinuousRestrictionGate.EvaluateAttack
/// evaluates against the chosen defender.</summary>
public sealed class CanNotAttackDefenderConditionEffect : ICardEffect
{
    public CanNotAttackDefenderConditionEffect(CardSource card, Func<CardSource, bool> defenderPredicate, bool isInheritedEffect, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(defenderPredicate);
        Card = card;
        DefenderPredicate = defenderPredicate;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
    }

    public CardSource Card { get; }
    public Func<CardSource, bool> DefenderPredicate { get; }
    public bool IsInheritedEffect { get; }
    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [RestrictionHelpers.RestrictionTargetEntityIdKey] = Card.InstanceId.Value,
            [RestrictionHelpers.RestrictionSourceEntityIdKey] = Card.InstanceId.Value,
            [RestrictionHelpers.CannotAttackKey] = true,
            [RestrictionHelpers.DefenderPredicateKey] = DefenderPredicate,
        };
        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(PRIM-W5) A material condition for a Blast-DNA digivolution (AS-IS <c>BlastDNACondition</c>).
/// <see cref="Matches"/> preserves the original's per-material predicate 1:1; use <see cref="ByName"/> for the
/// name-equality subset.</summary>
public sealed record BlastDNACondition(Func<CardSource, bool> Matches, string Label)
{
    public static BlastDNACondition ByName(string name) => new(cs => cs.EqualsCardName(name), name);
}

/// <summary>(PRIM-W5) A no-op effect returned by the special-play factories. The real work (registering the
/// card's SpecialPlayRecipe) happens in the factory; this marker just occupies the card's effect list and is
/// never consumed (role None).</summary>
public sealed class SpecialPlayRecipeMarkerEffect : ICardEffect
{
    public SpecialPlayRecipeMarkerEffect(CardSource card) => Card = card;

    public CardSource Card { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var context = new EffectContext(Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "None", context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: null, duration: null);
    }
}

/// <summary>(PRIM-W5) Grants this card an additional card name (AS-IS <c>ChangeCardNamesClass</c>). Registers
/// a continuous binding carrying <see cref="CardSource.AddedCardNameKey"/>, which <see cref="CardSource.CardNames"/>
/// folds in — so name-based predicates (EqualsCardName / ContainsCardName) see it.</summary>
public sealed class ChangeCardNamesEffect : ICardEffect
{
    public ChangeCardNamesEffect(CardSource card, string addedName, bool isInheritedEffect, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(addedName);
        Card = card;
        AddedName = addedName;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
    }

    public CardSource Card { get; }
    public string AddedName { get; }
    public bool IsInheritedEffect { get; }
    public Func<bool>? Condition { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [CardSource.AddedCardNameKey] = AddedName,
        };
        if (IsInheritedEffect)
        {
            values[ContinuousSelfModifierEffect.InheritedEffectKey] = true;
        }

        if (Condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = Condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}

/// <summary>(PRIM-W5) Return this card to the owner's hand (AS-IS <c>AddThisCardToHand</c>).</summary>
public sealed class ReturnThisCardToHandEffect : IActivatedCardEffect
{
    public ReturnThisCardToHandEffect(CardSource card, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnToHandKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Return-to-hand effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.DigivolveIntoHandOrTrashCard(..)</c>
/// coroutine: select up to <paramref name="maxCount"/> battle-area Digimon matching <c>canTarget</c> and
/// de-digivolve each by <c>count</c> (remove its top digivolution cards). Wraps the engine's
/// <see cref="DeDigivolveHelpers"/> primitive via the DeDigivolve mutation.</summary>
public sealed class ActivatedSelectAndDeDigivolveEffect : IActivatedCardEffect
{
    private readonly Func<HeadlessEntityId, bool> _canTarget;
    private readonly int _maxCount;
    private readonly int _count;
    private readonly bool _canEndNotMax;

    public ActivatedSelectAndDeDigivolveEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int count, bool canEndNotMax, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _canTarget = canTarget;
        _maxCount = maxCount;
        _count = count;
        _canEndNotMax = canEndNotMax;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    private IEnumerable<HeadlessEntityId> Candidates()
    {
        var zones = (IZoneStateReader)Card.Context.ZoneMover;
        foreach (HeadlessPlayerId player in Card.Context.TurnController.Current.PlayerOrder)
        {
            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                if (_canTarget(id))
                {
                    yield return id;
                }
            }
        }
    }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        var candidates = Candidates()
            .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, ChoiceZone.BattleArea, isSelectable: true, Card.Owner))
            .ToList();
        int max = Math.Min(_maxCount, candidates.Count);
        return EffectChoiceHelpers.CreatePermanentRequest(Card.Owner, Description, minCount: _canEndNotMax ? 0 : max, maxCount: max, canSkip: _canEndNotMax, candidates);
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(selected);
        foreach (HeadlessEntityId id in selected)
        {
            if (id.IsEmpty)
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.DeDigivolveKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = id.Value,
                    [MatchStateMutationSink.CountKey] = _count,
                }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Select-and-de-digivolve effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.PlayPermanentCards(..., root)</c>
/// coroutine: select up to <paramref name="maxCount"/> of the owner's cards in <paramref name="fromZone"/>
/// (Trash / Hand) matching <paramref name="canTarget"/>, then play each onto the battle area (cost-free).</summary>
public sealed class ActivatedSelectAndPlayEffect : IActivatedCardEffect
{
    private readonly ChoiceZone _fromZone;
    private readonly Func<HeadlessEntityId, bool> _canTarget;
    private readonly int _maxCount;
    private readonly bool _canEndNotMax;

    public ActivatedSelectAndPlayEffect(CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _fromZone = fromZone;
        _canTarget = canTarget;
        _maxCount = maxCount;
        _canEndNotMax = canEndNotMax;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    private IEnumerable<HeadlessEntityId> Candidates() =>
        ((IZoneStateReader)Card.Context.ZoneMover).GetCards(Card.Owner, _fromZone).Where(_canTarget);

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        var candidates = Candidates()
            .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, _fromZone, isSelectable: true, Card.Owner))
            .ToList();
        int max = Math.Min(_maxCount, candidates.Count);
        return EffectChoiceHelpers.CreatePermanentRequest(Card.Owner, Description, minCount: _canEndNotMax ? 0 : max, maxCount: max, canSkip: _canEndNotMax, candidates);
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(selected);
        foreach (HeadlessEntityId id in selected)
        {
            if (id.IsEmpty)
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.PlayCardKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = id.Value,
                    [MatchStateMutationSink.FromZoneKey] = _fromZone.ToString(),
                }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Select-and-play effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated "select up to <paramref name="maxCount"/> Digimon and make each unable to attack and/or
/// block for a <see cref="EffectDuration"/>" effect (e.g. ST2_14). <see cref="ApplyRestriction"/> registers
/// one duration-tagged restriction binding per chosen target, queried by <c>RestrictionHelpers</c> via the
/// continuous-restriction scope, so <see cref="EffectDurationExpiry"/> removes it on expiry.
/// </summary>
public sealed class ActivatedTargetRestrictionEffect : IActivatedCardEffect
{
    private readonly SelectPermanentEffect _select = new();
    private readonly EffectDuration _duration;
    private readonly bool _cannotAttack;
    private readonly bool _cannotBlock;

    public ActivatedTargetRestrictionEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, EffectDuration duration, bool cannotAttack, bool cannotBlock, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
        _duration = duration;
        _cannotAttack = cannotAttack;
        _cannotBlock = cannotBlock;
        _select.SetUp(card.Owner, canTarget, maxCount, canNoSelect: false, canEndNotMax: maxCount > 1, SelectPermanentEffect.Mode.Custom, card.InstanceId);
        _select.SetUpCustomMessage(description);
    }

    public CardSource Card { get; }

    public string Description { get; }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players) =>
        _select.BuildRequest((IZoneStateReader)Card.Context.ZoneMover, players);

    public void ApplyRestriction(IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        int index = 0;
        foreach (HeadlessEntityId target in selected)
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [RestrictionHelpers.RestrictionTargetEntityIdKey] = target.Value,
                [RestrictionHelpers.RestrictionSourceEntityIdKey] = Card.InstanceId.Value,
            };
            if (_cannotAttack)
            {
                values[RestrictionHelpers.CannotAttackKey] = true;
            }

            if (_cannotBlock)
            {
                values[RestrictionHelpers.CannotBlockKey] = true;
            }

            var context = new EffectContext(
                Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { target }, values: values);
            var binding = new EffectBinding(
                new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:restrict:{target.Value}:{index++}"), Card.Controller, "Continuous", context),
                keywords: null, EffectQueryRole.Restriction, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: _duration);
            Card.Context.EffectRegistry.Register(binding);
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Activated restriction effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// A triggered "[When ...] this Digimon gets +X DP for a <see cref="EffectDuration"/>" effect (e.g. ST3_01
/// "when an opponent's Digimon is deleted by 0 DP, this Digimon gets +1000 DP for the turn"). On resolution
/// it registers one duration-tagged self DP-modifier binding, folded in by the continuous gate and removed
/// by <see cref="EffectDurationExpiry"/>. Auto-registered under its trigger timing. (The original's
/// [Once Per Turn] / 0-DP-delete gates map to the once-flag / trigger subsystems — relaxed here, like ST2_11.)
/// </summary>
public sealed class TriggeredSelfDpBuffEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly int _changeValue;
    private readonly EffectDuration _duration;
    private readonly Func<bool>? _condition;
    private readonly Func<CardEffectResolveContext, bool>? _triggerGate;

    public TriggeredSelfDpBuffEffect(
        CardSource card, EffectTiming timing, int changeValue, EffectDuration duration, Func<bool>? condition, string description,
        Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _changeValue = changeValue;
        _duration = duration;
        _condition = condition;
        _triggerGate = triggerGate;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:selfdpbuff:{trigger}"), card.InstanceId, description, trigger,
            isOptional: false, maxCountPerTurn: maxCountPerTurn, hash: hash);
    }

    public CardSource Card { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        if (_condition is not null && !_condition())
        {
            return CardEffectCanResolveResult.Failure("Trigger condition not met.");
        }

        if (_triggerGate is not null && !_triggerGate(context))
        {
            return CardEffectCanResolveResult.Failure("Trigger event condition not met.");
        }

        return CardEffectCanResolveResult.Success();
    }

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanResolve(context).CanResolve)
        {
            return ValueTask.FromResult(EffectResult.Failure("Cannot resolve."));
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [ModifierHelpers.DpDeltaKey] = _changeValue };
        var bindingContext = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        // Unique per application (the triggering subject) so repeated firings across turns don't collide;
        // the duration expiry removes each at turn end.
        string applied = context.Request.Context.TriggerEntityId?.Value ?? "self";
        var binding = new EffectBinding(
            new EffectRequest(new HeadlessEntityId($"{Card.InstanceId.Value}:selfdpbuff:applied:{_changeValue}:{applied}"), Card.Controller, "Continuous", bindingContext),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousModifierGate.Scope }, effect: null, duration: _duration);
        Card.Context.EffectRegistry.Register(binding);
        return ValueTask.FromResult(EffectResult.Success($"This Digimon gets {(_changeValue >= 0 ? "+" : string.Empty)}{_changeValue} DP."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}

/// <summary>
/// A triggered "[When ...] &lt;Recovery +N (Deck)&gt;" effect (e.g. ST3_09): on resolution emits a Recover
/// mutation moving the top <paramref name="amount"/> deck card(s) onto the owner's security stack.
/// </summary>
public sealed class RecoverTriggerEffect : ICardEffect, IHeadlessCardEffect
{
    private readonly int _amount;
    private readonly Func<bool>? _condition;

    public RecoverTriggerEffect(CardSource card, EffectTiming timing, int amount, Func<bool>? condition, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _amount = amount;
        _condition = condition;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:recover:{trigger}"), card.InstanceId, description, trigger, isOptional: true);
    }

    public CardSource Card { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context)
    {
        if (_condition is not null && !_condition())
        {
            return CardEffectCanResolveResult.Failure("Trigger condition not met.");
        }

        return CardEffectCanResolveResult.Success();
    }

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanResolve(context).CanResolve)
        {
            return ValueTask.FromResult(EffectResult.Failure("Cannot resolve."));
        }

        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.RecoverKind,
            Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = Card.Owner.Value,
                [MatchStateMutationSink.CountKey] = _amount,
            }));
        return ValueTask.FromResult(EffectResult.Success($"Recovery +{_amount}."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, context),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}

/// <summary>
/// An activated "add this card to its owner's hand" effect (Option/Security self-bounce, e.g. ST3_13 /
/// ST3_14 [Security]). <see cref="Apply"/> emits a ReturnToHand mutation on the source card.
/// </summary>
public sealed class AddThisCardToHandEffect : IActivatedCardEffect
{
    public AddThisCardToHandEffect(CardSource card, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnToHandKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Add-to-hand effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated "play THIS card onto the battle area (without paying its cost)" effect — the headless
/// realization of a Tamer's <c>PlaySelfTamerSecurityEffect</c> security skill (e.g. ST1_12 / ST2_12 /
/// ST3_12 [Security] "Play this Tamer"). The security loop reveals the card (to the trash) before resolving
/// its SecuritySkill, so <see cref="Apply"/> plays it from whatever zone it currently sits in to the battle
/// area via a PlayCard mutation, which also auto-registers its effects (G6-001 / G8-002).
/// </summary>
public sealed class PlayThisCardToBattleEffect : IActivatedCardEffect
{
    public PlayThisCardToBattleEffect(CardSource card, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public void Apply(MatchStateMutationSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ChoiceZone from = CurrentZone() ?? ChoiceZone.Trash;
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.PlayCardKind,
            Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = Card.InstanceId.Value,
                [MatchStateMutationSink.FromZoneKey] = from.ToString(),
            }));
    }

    private ChoiceZone? CurrentZone()
    {
        var zones = (IZoneStateReader)Card.Context.ZoneMover;
        foreach (ChoiceZone zone in new[] { ChoiceZone.Security, ChoiceZone.Trash, ChoiceZone.Hand, ChoiceZone.BattleArea })
        {
            if (zones.GetCards(Card.Owner, zone).Contains(Card.InstanceId))
            {
                return zone;
            }
        }

        return null;
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Play-this-card effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// An activated "choose a Digimon digivolution card under one of your Digimon and play it as another Digimon
/// without paying its cost" effect (e.g. ST2_15 [Main], the play-from-under flow). Candidates are the
/// Digimon under-cards of the owner's battle-area Digimon; <see cref="Apply"/> emits a
/// PlayDigivolutionAsDigimon mutation that moves the chosen under-card out of its host onto the battle area
/// (cost-free) and auto-registers it.
/// </summary>
public sealed class ActivatedPlayFromUnderEffect : IActivatedCardEffect
{
    public ActivatedPlayFromUnderEffect(CardSource card, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        var candidates = new List<ChoiceCandidate>();
        foreach ((HeadlessEntityId under, HeadlessEntityId _) in OwnerDigimonUnderCards())
        {
            candidates.Add(EffectChoiceHelpers.Candidate(under, under.Value, ChoiceZone.BattleArea, isSelectable: true, Card.Owner));
        }

        int max = Math.Min(1, candidates.Count);
        return EffectChoiceHelpers.CreatePermanentRequest(Card.Owner, Description, minCount: max, maxCount: max, canSkip: false, candidates);
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(selected);
        var selectedSet = new HashSet<string>(selected.Select(s => s.Value), StringComparer.Ordinal);
        foreach ((HeadlessEntityId under, HeadlessEntityId host) in OwnerDigimonUnderCards())
        {
            if (!selectedSet.Contains(under.Value))
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.PlayDigivolutionAsDigimonKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = under.Value,
                    [MatchStateMutationSink.HostEntityIdKey] = host.Value,
                }));
        }
    }

    private IEnumerable<(HeadlessEntityId Under, HeadlessEntityId Host)> OwnerDigimonUnderCards()
    {
        var zones = (IZoneStateReader)Card.Context.ZoneMover;
        foreach (HeadlessEntityId top in zones.GetCards(Card.Owner, ChoiceZone.BattleArea))
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(Card.Context.CardInstanceRepository, Card.Context.CardRepository, top);
            foreach (StackedCard under in stack.UnderCards)
            {
                if (IsDigimonCard(under.InstanceId))
                {
                    yield return (under.InstanceId, top);
                }
            }
        }
    }

    private bool IsDigimonCard(HeadlessEntityId id)
    {
        return Card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) && instance is not null
            && Card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) && def is not null
            && string.Equals(def.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Play-from-under effect is resolved via the activation flow, not registered: {Description}");
}

/// <summary>
/// Headless mirror of the original <c>CardEffectFactory</c>. Method names match the original so ported
/// card bodies read 1:1. Each returns an <see cref="ICardEffect"/> the registrar lowers to a binding.
/// </summary>
public static partial class CardEffectFactory
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

    /// <summary>(PRIM-W1-3) Original: <c>ChangeDigivolutionCostStaticEffect</c> — continuous ±digivolution
    /// cost on self (delta). Registers a <see cref="DigivolutionCostHelpers.DigivolutionCostDeltaKey"/> modifier
    /// under the continuous-modifier scope, which <c>ContinuousModifierGate.ResolveDigivolutionCost</c> folds
    /// into this card's evolution cost (D-8; "cannot be reduced" replacement honoured). <paramref name="changeValue"/>
    /// is signed (negative = reduction). The original's <c>setFixedCost</c> (SET rather than ±) and per-target
    /// permanent/root conditions are out of this delta primitive's scope (per-card follow-up).</summary>
    public static ICardEffect ChangeDigivolutionCostStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfModifierEffect(card, DigivolutionCostHelpers.DigivolutionCostDeltaKey, changeValue, isInheritedEffect, condition);

    /// <summary>(PRIM-W1-3) Dynamic (<c>Func&lt;int&gt;</c>) variant of <see cref="ChangeDigivolutionCostStaticEffect(int,bool,CardSource,Func{bool})"/>.</summary>
    public static ICardEffect ChangeDigivolutionCostStaticEffect(Func<int> changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfModifierEffect(card, DigivolutionCostHelpers.DigivolutionCostDeltaKey, changeValue: 0, isInheritedEffect, condition, dynamicValue: changeValue);

    /// <summary>(PRIM-W1-5) Original: <c>CanNotDigivolveStaticSelfEffect</c> — a continuous "this card cannot
    /// be digivolved (as the digivolution source)" restriction on self. Registers a
    /// <see cref="RestrictionHelpers.CannotDigivolveKey"/> restriction that <c>DigivolveAction</c> already
    /// consults (<c>ContinuousRestrictionGate.EvaluateDigivolve</c> on the target under-card).</summary>
    public static ICardEffect CanNotDigivolveStaticSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotDigivolveKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W1-8) Original: <c>CanNotDigivolveStaticEffect</c> — a continuous "the scoped player's
    /// Digimon (optionally of <paramref name="scopeCardType"/>) cannot digivolve" restriction. Covers the
    /// structured scope (e.g. "your opponent's Digimon cannot digivolve" — <paramref name="scopePlayerId"/> =
    /// the opponent); the original's arbitrary per-permanent predicate beyond CardType is a per-card concern.</summary>
    public static ICardEffect CanNotDigivolveStaticEffect(HeadlessPlayerId scopePlayerId, string? scopeCardType, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeRestrictionEffect(card, scopePlayerId, RestrictionHelpers.CannotDigivolveKey, scopeCardType, isInheritedEffect, condition);

    /// <summary>(PRIM-W1-6/9) Original: <c>AddDigivolutionRequirementStaticEffect</c> — grant this card an
    /// ADDITIONAL digivolution path "from <paramref name="fromColor"/> Lv<paramref name="fromLevel"/>". When
    /// the printed condition fails but this added condition matches the target, DigivolveAction allows the
    /// digivolve. (Per-path cost via <see cref="ChangeDigivolutionCostStaticEffect(int,bool,CardSource,Func{bool})"/>
    /// or per-card; arbitrary per-permanent predicates beyond Color@Level are per-card.)</summary>
    public static ICardEffect AddDigivolutionRequirementStaticEffect(string fromColor, int fromLevel, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new AddedDigivolutionRequirementEffect(card, $"{fromColor}@{fromLevel}", isInheritedEffect, condition);

    /// <summary>(PRIM-W5) <c>AddSelfDigivolutionRequirementStaticEffect</c> — adds an alternative digivolution
    /// source for THIS card: it may digivolve from any under-card matching <paramref name="permanentCondition"/>
    /// (for <paramref name="digivolutionCost"/> memory). DigivolveAction evaluates the predicate against the
    /// target. Extra AS-IS args (effectName/cardCondition/costEquation/level ranges) accepted for fidelity;
    /// the original <c>CardColor cardColor</c> param is omitted (no headless CardColor — express color via
    /// the predicate).</summary>
    public static ICardEffect AddSelfDigivolutionRequirementStaticEffect(
        Func<Permanent, bool> permanentCondition, int digivolutionCost, bool ignoreDigivolutionRequirement,
        CardSource card, Func<bool>? condition, string? effectName = null, Func<CardSource, bool>? cardCondition = null,
        Func<int>? costEquation = null, int level = -1, int minLevel = -1, int maxLevel = -1) =>
        // (FR2/M-1) cardCondition = which cards receive the added requirement; null → self only (AS-IS default
        // cs => cs == card), non-null → any owner's card matching it (e.g. ST8_04 UlforceVeedramon in hand).
        new AddedDigivolutionRequirementPredicateEffect(card, permanentCondition, digivolutionCost, ignoreDigivolutionRequirement, isInheritedEffect: false, condition, targetCardCondition: cardCondition, costEquation: costEquation);

    /// <summary>(PRIM-W5) <c>DrawCardsEffect</c> — the declarative form of the AS-IS
    /// <c>new DrawClass(owner, count, ...).Draw()</c> coroutine: the owner draws <paramref name="count"/>
    /// cards. Use this in place of the original draw coroutine.</summary>
    public static IActivatedCardEffect DrawCardsEffect(CardSource card, int count) =>
        new DrawEffect(card, count, $"Draw {count} card(s).");

    /// <summary>Original: <c>PierceSelfEffect</c> — grants Piercing to self.</summary>
    public static ICardEffect PierceSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordEffect(card, KeywordBaseBatch1Kind.Piercing, isInheritedEffect, condition);

    /// <summary>Original: <c>BlockerSelfStaticEffect</c> — grants Blocker to self.</summary>
    public static ICardEffect BlockerSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordEffect(card, KeywordBaseBatch1Kind.Blocker, isInheritedEffect, condition);

    /// <summary>Original: <c>JammingSelfStaticEffect</c> — grants Jamming to self.</summary>
    public static ICardEffect JammingSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordEffect(card, KeywordBaseBatch1Kind.Jamming, isInheritedEffect, condition);

    /// <summary>Original: <c>RebootSelfStaticEffect</c> — grants Reboot to self (Batch1).</summary>
    public static ICardEffect RebootSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordEffect(card, KeywordBaseBatch1Kind.Reboot, isInheritedEffect, condition);

    /// <summary>Original: <c>AllianceSelfEffect</c> — grants Alliance to self (Batch2). The original wraps
    /// <paramref name="condition"/> with <c>IsExistOnBattleAreaDigimon(card)</c>; here that battle-area guard
    /// is the binding lifecycle (see <see cref="SelfKeywordBatch2Effect"/>).</summary>
    public static ICardEffect AllianceSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Alliance, isInheritedEffect, condition);

    /// <summary>Original: <c>OverclockSelfEffect</c> — grants Overclock to self (Batch2).</summary>
    public static ICardEffect OverclockSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Overclock, isInheritedEffect, condition);

    /// <summary>Original: <c>VortexSelfEffect(isInheritedEffect, card, condition, rootCardEffect = null)</c> —
    /// grants Vortex to self (Batch2). <paramref name="rootCardEffect"/> is accepted for 1:1 source-signature
    /// fidelity (the original threads it to the underlying <c>VortexEffect</c>); the headless grant layer
    /// derives its binding from the card source, so it is not otherwise needed.</summary>
    public static ICardEffect VortexSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, ICardEffect? rootCardEffect = null) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Vortex, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>RushSelfStaticEffect</c> — grants Rush to self (Batch2).</summary>
    public static ICardEffect RushSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Rush, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>RetaliationSelfEffect(isInheritedEffect, card, condition, isLinkedEffect = false)</c>
    /// — grants Retaliation to self (Batch2). <paramref name="isLinkedEffect"/> is accepted for source-signature
    /// fidelity; the headless grant derives from the card source.</summary>
    public static ICardEffect RetaliationSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Retaliation, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>RaidSelfEffect</c> — grants Raid (attack-switch) to self.
    /// <paramref name="rootCardEffect"/>/<paramref name="isLinkedEffect"/> accepted for source fidelity.</summary>
    public static ICardEffect RaidSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, ICardEffect? rootCardEffect = null, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Raid, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>BarrierSelfEffect</c> — grants Barrier (deletion-replacement) to self.</summary>
    public static ICardEffect BarrierSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Barrier, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>CollisionSelfStaticEffect</c> — grants Collision (forced-block) to self.</summary>
    public static ICardEffect CollisionSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Collision, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>FortitudeSelfEffect</c> — grants Fortitude (post-deletion replay) to self.</summary>
    public static ICardEffect FortitudeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Fortitude, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>EvadeSelfEffect</c> — grants Evade (deletion-replacement) to self.</summary>
    public static ICardEffect EvadeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Evade, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>SaveEffect(card)</c> — grants Save (deletion-replacement: place under a
    /// Tamer instead of trashing) to self.</summary>
    public static ICardEffect SaveEffect(CardSource card) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Save, isInheritedEffect: false, condition: null);

    // --- (PRIM-W3) keyword self-static grants -----------------------------------------------------------
    /// <summary>(PRIM-W3) <c>BlitzSelfEffect</c> — grants Blitz to self (Batch2).</summary>
    public static ICardEffect BlitzSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Blitz, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>DecodeSelfEffect</c> — grants Decode to self (Batch2).</summary>
    public static ICardEffect DecodeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Decode, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>ProgressSelfStaticEffect</c> — grants Progress to self (Batch2).</summary>
    public static ICardEffect ProgressSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Progress, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>PartitionSelfEffect</c> — grants Partition to self (Batch2). The per-card
    /// <c>cardSourceConditions</c> list is accepted for source fidelity (per-card).</summary>
    public static ICardEffect PartitionSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, object? cardSourceConditions = null) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Partition, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>IcecladSelfStaticEffect</c> — grants Iceclad to self.</summary>
    public static ICardEffect IcecladSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Iceclad, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>DecoySelfEffect</c> — grants Decoy (deletion-replacement) to self. Extra
    /// per-card args accepted for fidelity.</summary>
    public static ICardEffect DecoySelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, Func<Permanent, bool>? permanentCondition = null, string? effectName = null, string? effectDescription = null) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Decoy, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>FragmentSelfEffect</c> — grants Fragment (deletion-replacement) to self.</summary>
    public static ICardEffect FragmentSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, int trashValue = 0, string? effectName = null, string? effectDescription = null) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Fragment, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>ExecuteSelfEffect</c> — grants Execute to self.</summary>
    public static ICardEffect ExecuteSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Execute, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>ScapegoatSelfEffect</c> — grants Scapegoat (deletion-replacement) to self.</summary>
    public static ICardEffect ScapegoatSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null, string? effectDescription = null, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Scapegoat, isInheritedEffect, condition);

    /// <summary>(FR-P2) Adapts a ported card's <c>Func&lt;Permanent,bool&gt; permanentCondition</c> into the
    /// player-scope predicate (evaluated against each candidate 1:1). Null → no predicate (whole scope).</summary>
    internal static Func<CardSource, bool>? ScopePred(Func<Permanent, bool>? permanentCondition) =>
        permanentCondition is null ? null : cs => permanentCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner));

    /// <summary>(PRIM-W3/FR-P2) <c>RushStaticEffect(permanentCondition, ...)</c> — grants Rush to the owner's
    /// Digimon matching <paramref name="permanentCondition"/> (evaluated 1:1; null = all owner's Digimon).</summary>
    public static ICardEffect RushStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Rush, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W3) <c>RebootStaticEffect(permanentCondition, ...)</c> — grants Reboot to the owner's
    /// Digimon (player-scope). <paramref name="permanentCondition"/>/<paramref name="isLinkedEffect"/> per-card.</summary>
    public static ICardEffect RebootStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Reboot, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W3) <c>CanNotAttackStaticEffect(...)</c> — the scoped player's Digimon cannot attack
    /// (player-scope CannotAttack restriction consulted by AttackPermanentAction). Per-permanent predicate is
    /// per-card.</summary>
    public static ICardEffect CanNotAttackStaticEffect(HeadlessPlayerId scopePlayerId, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        new ContinuousPlayerScopeRestrictionEffect(card, scopePlayerId, RestrictionHelpers.CannotAttackKey, scopeCardType: null, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>Gain1MemoryTamerOpponentDigimonEffect(card)</c> — "[Start of Your Turn] if your
    /// opponent has a Digimon, gain 1 memory." (main-phase timing mapped to OnStartTurn).</summary>
    public static ICardEffect Gain1MemoryTamerOpponentDigimonEffect(CardSource card) =>
        new TriggeredGainMemoryEffect(card, EffectTiming.OnStartTurn, amount: 1,
            "[Start of Your Turn] If your opponent has a Digimon, gain 1 memory.",
            extraCondition: () => CardEffectCommons.MatchConditionPermanentCount(card, id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)) > 0);

    /// <summary>(PRIM-W3) <c>Gain2MemoryOptionDelayEffect(card)</c> — a delayed "gain 2 memory" (resolves at
    /// the next start of the owner's turn). The Option-delay timing is mapped to OnStartTurn.</summary>
    public static ICardEffect Gain2MemoryOptionDelayEffect(CardSource card) =>
        new TriggeredGainMemoryEffect(card, EffectTiming.OnStartTurn, amount: 2, "Gain 2 memory (delayed to the start of your turn).");

    /// <summary>(PRIM-W3) <c>CanNotBeBlockedStaticSelfEffect</c> — this Digimon cannot be blocked (unblockable);
    /// consulted by BlockTiming when enumerating blocker candidates.</summary>
    public static ICardEffect CanNotBeBlockedStaticSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotBeBlockedKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>CantUnsuspendStaticEffect</c> — this Digimon does not unsuspend; consulted by the
    /// Unsuspend step.</summary>
    public static ICardEffect CantUnsuspendStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotUnsuspendKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>CanNotBeDestroyedBySkillStaticEffect</c> — this Digimon cannot be deleted by
    /// effects/skills (battle deletion still applies); consulted by the effect-sourced delete path.</summary>
    public static ICardEffect CanNotBeDestroyedBySkillStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotBeDeletedBySkillKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>ChangeSAttackStaticEffect</c> — continuous ±security attack on the owner's Digimon
    /// (player-scope SA modifier consulted by ContinuousModifierGate.ResolveSecurityAttack). Mirrors the SA
    /// analogue of <see cref="ChangeDPStaticEffect"/>; <paramref name="permanentCondition"/> per-card.</summary>
    public static ICardEffect ChangeSAttackStaticEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new PlayerScopeModifierEffect(card, ModifierHelpers.SAttackDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopePredicate: ScopePred(permanentCondition));

    /// <summary>(PRIM-W3) <c>ReturnToLibraryBottomDigivolutionCardsClass</c> — returns the host's own
    /// digivolution (under-)cards to the bottom of the deck (activated).</summary>
    public static IActivatedCardEffect ReturnToLibraryBottomDigivolutionCardsClass(CardSource card, int count) =>
        new ReturnSelfDigivolutionCardsToDeckEffect(card, count, "Return this Digimon's digivolution cards to the bottom of the deck.");

    /// <summary>(PRIM-W3) <c>ReplaceBottomSecurityWithFaceUpOptionEffect</c> — Option [Main]: add the bottom
    /// security card to hand, then place this card face up as the bottom security card.</summary>
    public static IActivatedCardEffect ReplaceBottomSecurityWithFaceUpOptionEffect(CardSource card) =>
        new ReplaceBottomSecurityWithFaceUpEffect(card, "[Main] Add your bottom security card to the hand. Then, place this card face up as the bottom security card.");

    /// <summary>(PRIM-W3) <c>ReplaceBottomSecurityWithFaceUpOptionMainEffect</c> — Main-phase variant of
    /// <see cref="ReplaceBottomSecurityWithFaceUpOptionEffect"/>.</summary>
    public static IActivatedCardEffect ReplaceBottomSecurityWithFaceUpOptionMainEffect(CardSource card) =>
        new ReplaceBottomSecurityWithFaceUpEffect(card, "[Main] Add your bottom security card to the hand. Then, place this card face up as the bottom security card.");

    /// <summary>(PRIM-W3, C-24) <c>TrainingEffect</c> — activated [Breeding]: suspend self, place the top deck
    /// card at the bottom of self's digivolution stack.</summary>
    public static IActivatedCardEffect TrainingEffect(CardSource card) =>
        new TrainingActivatedEffect(card, "[Breeding] Suspend this Digimon: place the top card of your deck under it as its bottom digivolution card.");

    /// <summary>(PRIM-W3, C-23) <c>MaterialSaveEffect</c> — move <paramref name="count"/> of this Digimon's
    /// digivolution cards under another of your Digimon (<paramref name="destinationId"/>, chosen at port time).</summary>
    public static IActivatedCardEffect MaterialSaveEffect(CardSource card, HeadlessEntityId destinationId, int count) =>
        new MaterialSaveActivatedEffect(card, destinationId, count, "Place this Digimon's digivolution cards under another of your Digimon.");

    /// <summary>(PRIM-W3) <c>ChangeSelfLinkMaxStaticEffect</c> — continuous ±link-maximum on self. Registers a
    /// LinkedMaxDelta continuous modifier (queryable via ContinuousModifierGate). Grant is live; the link
    /// enforcement consumer migrates to consult it separately (preemptive seal).</summary>
    public static ICardEffect ChangeSelfLinkMaxStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfModifierEffect(card, ModifierHelpers.LinkedMaxDeltaKey, changeValue, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>GrantedReduceLinkCostClass</c> — continuous link-cost reduction. Registers a
    /// LinkCostDelta continuous modifier (queryable via ContinuousModifierGate); the link-cost payment consumer
    /// migrates to consult it separately (preemptive seal). Per-card conditions accepted for fidelity.</summary>
    public static ICardEffect GrantedReduceLinkCostClass(CardSource card, int reducedCost, bool isInheritedEffect = false, Func<bool>? condition = null) =>
        new ContinuousSelfModifierEffect(card, ModifierHelpers.LinkCostDeltaKey, -Math.Abs(reducedCost), isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>MindLink</c> — grants the MindLink keyword (Tamer↔Digimon link). Grant is live via
    /// HasKeyword; the tamer-as-Digimon behavior consumer migrates separately (preemptive seal).</summary>
    public static ICardEffect MindLinkSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.MindLink, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>UseRequirements</c> (AS-IS <c>IgnoreColorConditionClass</c>) — lets this card
    /// digivolve ignoring the COLOR part of the printed requirement (level still enforced). Registers a
    /// continuous ignore-color flag consulted by DigivolveAction. <paramref name="cardCondition"/> per-card.</summary>
    public static ICardEffect UseRequirements(CardSource card, Func<CardSource, bool>? cardCondition = null, bool isInheritedEffect = false, Func<bool>? condition = null)
    {
        // (FR2/M-1) AS-IS UseRequirements' CanUseCondition: the ignore-color is ACTIVE only while the owner
        // controls a battle-area OR breeding-area Digimon/Tamer whose top card matches cardCondition. Fold that
        // into the effect's condition so it is not granted unconditionally.
        Func<bool>? gate = cardCondition is null
            ? condition
            : () => (condition is null || condition())
                && OwnerControlsMatchingDigimon(card, p => (p.IsDigimon || p.IsTamer) && cardCondition(p.TopCard), ChoiceZone.BattleArea, ChoiceZone.BreedingArea);
        return new ContinuousSelfRestrictionEffect(card, DigivolveAction.IgnoreColorRequirementKey, isInheritedEffect, gate);
    }

    // ===== PRIM-W4 (low-frequency tail) =================================================================

    /// <summary>(PRIM-W4) <c>CanNotBlockStaticSelfEffect</c> — this Digimon cannot block (self CannotBlock
    /// restriction consulted by ContinuousRestrictionGate.EvaluateBlock).</summary>
    public static ICardEffect CanNotBlockStaticSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotBlockKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W4) <c>CanNotBlockStaticEffect</c> — the scoped player's Digimon cannot block
    /// (player-scope CannotBlock restriction).</summary>
    public static ICardEffect CanNotBlockStaticEffect(HeadlessPlayerId scopePlayerId, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeRestrictionEffect(card, scopePlayerId, RestrictionHelpers.CannotBlockKey, scopeCardType: null, isInheritedEffect, condition);

    /// <summary>(PRIM-W4) <c>CanNotBeDestroyedStaticEffect</c> — registers a continuous Delete/Prevent
    /// replacement on the HOST (battle + effect deletion), honoured by BattleDeletionGate and the effect-delete
    /// path. **FIDELITY: SELF-only.** <paramref name="permanentCondition"/> is currently NOT honoured — the
    /// prevent is a self replacement, so this is 1:1 only for the "THIS Digimon cannot be deleted" form. The
    /// SET form ("your &lt;X&gt; Digimon cannot be deleted") needs a player-scope prevent (not built) → STOP to
    /// the strong model for that form. See fidelity_debt.md.</summary>
    public static ICardEffect CanNotBeDestroyedStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, ReplacementHelpers.PreventDeletionKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, ReplacementHelpers.PreventDeletionKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>ImmuneFromDPMinusStaticEffect</c> — this Digimon is immune to DP-reducing effects
    /// (D-A3). Registers a continuous DpReduction/Immune replacement honoured by ContinuousDpGate.</summary>
    public static ICardEffect ImmuneFromDPMinusStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, ReplacementHelpers.ImmuneFromDpMinusKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, ReplacementHelpers.ImmuneFromDpMinusKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>AllianceStaticEffect</c> — grants Alliance to the owner's Digimon (player-scope
    /// keyword). <paramref name="permanentCondition"/> per-card.</summary>
    public static ICardEffect AllianceStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Alliance, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>JammingStaticEffect</c> — grants Jamming to the owner's Digimon (player-scope
    /// keyword). <paramref name="permanentCondition"/>/<paramref name="isLinkedEffect"/> per-card.</summary>
    public static ICardEffect JammingStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Jamming, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>AscensionSelfEffect</c> — grants the Ascension keyword (post-deletion → security).
    /// Grant live via HasKeyword; DeletionReplacementGate's hasAscension consumer migrates separately.</summary>
    public static ICardEffect AscensionSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Ascension, isInheritedEffect, condition);

    /// <summary>(PRIM-W4) <c>ChangeBaseDPGlobalEffect</c> — continuous ±base-DP on the owner's Digimon
    /// (player-scope BaseDp modifier consulted by ContinuousDpGate). <paramref name="permanentCondition"/>
    /// per-card; the opponent-side "global" reach is a per-card scope concern.</summary>
    public static ICardEffect ChangeBaseDPGlobalEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        // (M-5) AS-IS "Global" = BOTH players' Digimon matching permanentCondition (no owner scope). scopeAnyPlayer
        // + the predicate select the set across both players (the owner-only scope missed the opponent's).
        new PlayerScopeModifierEffect(card, ModifierHelpers.BaseDpDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopePredicate: ScopePred(permanentCondition), scopeAnyPlayer: true);

    /// <summary>(PRIM-W4) <c>InvertSAttackStaticEffect</c> — continuous invert-security-attack on self
    /// (consumed by ContinuousModifierGate.ResolveSecurityAttack).</summary>
    public static ICardEffect InvertSAttackStaticEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        permanentCondition is null
            ? new ContinuousSelfModifierEffect(card, ModifierHelpers.InvertSecurityAttackDeltaKey, changeValue, isInheritedEffect, condition)
            : new PlayerScopeModifierEffect(card, ModifierHelpers.InvertSecurityAttackDeltaKey, changeValue, scopeCardType: null, condition, scopeZone: null, scopePredicate: ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>CollisionStaticEffect</c> — grants Collision to the owner's Digimon (player-scope
    /// keyword). Grant live via HasKeyword; BlockTiming's hasCollision consumer migrates separately.</summary>
    public static ICardEffect CollisionStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Collision, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>VortexCanAttackPlayersStaticEffect</c> — grants Vortex to the owner's Digimon
    /// (player-scope keyword). Grant live via HasKeyword; the Vortex attack consumer migrates separately.</summary>
    public static ICardEffect VortexCanAttackPlayersStaticEffect(Func<Permanent, bool>? attackerCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Vortex, scopeCardType: null, isInheritedEffect, condition, ScopePred(attackerCondition));

    /// <summary>(PRIM-W4) <c>ChangeLinkMaxStaticEffect</c> — continuous ±link-maximum on the owner's Digimon
    /// (player-scope LinkedMaxDelta modifier, queryable). Link enforcement consumer migrates separately
    /// (preemptive seal, same as ChangeSelfLinkMax).</summary>
    public static ICardEffect ChangeLinkMaxStaticEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new PlayerScopeModifierEffect(card, ModifierHelpers.LinkedMaxDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopePredicate: ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>TreatAsDigimonStaticEffect</c> — grants the TreatAsDigimon keyword. Grant live via
    /// HasKeyword; the card-type-aware consumers migrate separately (preemptive seal).</summary>
    public static ICardEffect TreatAsDigimonStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.TreatAsDigimon, isInheritedEffect, condition);

    /// <summary>(PRIM-W4) <c>Gain1MemoryTamerOwnerDigimonConditionalEffect</c> — "[Start of Your Turn] if you
    /// have a matching Digimon, gain 1 memory." The per-permanent predicate is captured in
    /// <paramref name="condition"/> at porting time.</summary>
    public static ICardEffect Gain1MemoryTamerOwnerDigimonConditionalEffect(string effectDescription, Func<Permanent, bool>? permanentCondition, Func<bool>? condition, CardSource card)
    {
        // (FR2) The memory gain is CONDITIONAL on the owner controlling a Digimon matching permanentCondition
        // (AS-IS). Fold that predicate into the trigger gate so it is not gained unconditionally.
        Func<bool>? gate = permanentCondition is null
            ? condition
            : () => (condition is null || condition()) && OwnerControlsMatchingDigimon(card, permanentCondition);
        return new TriggeredGainMemoryEffect(card, EffectTiming.OnStartTurn, amount: 1,
            string.IsNullOrWhiteSpace(effectDescription) ? "[Start of Your Turn] Gain 1 memory." : effectDescription, extraCondition: gate);
    }

    /// <summary>(FR2) Whether <paramref name="card"/>'s owner controls at least one permanent in
    /// <paramref name="searchZones"/> (default: battle area) satisfying <paramref name="predicate"/> (evaluated
    /// as a <see cref="Permanent"/> 1:1).</summary>
    internal static bool OwnerControlsMatchingDigimon(CardSource card, Func<Permanent, bool> predicate, params ChoiceZone[] searchZones)
    {
        if (card.Context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        ChoiceZone[] zonesToSearch = searchZones is { Length: > 0 } ? searchZones : new[] { ChoiceZone.BattleArea };
        foreach (ChoiceZone zone in zonesToSearch)
        {
            foreach (HeadlessEntityId id in zones.GetCards(card.Owner, zone))
            {
                if (predicate(new Permanent(card.Context, id, card.Owner)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>(PRIM-W4) <c>EoTLose3Memory</c> — "[End of Your Turn] lose 3 memory."</summary>
    public static ICardEffect EoTLose3Memory(CardSource card) =>
        new TriggeredGainMemoryEffect(card, EffectTiming.OnEndTurn, amount: -3, "[End of Your Turn] Lose 3 memory.");

    /// <summary>(PRIM-W4) <c>CantSuspendStaticEffect</c> — this Digimon cannot be suspended (self CannotSuspend
    /// restriction consulted by the Suspend sink path). <paramref name="permanentCondition"/> per-card.</summary>
    public static ICardEffect CantSuspendStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotSuspendKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, RestrictionHelpers.CannotSuspendKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>CannotReturnToHandStaticEffect</c> — this Digimon cannot be returned to hand
    /// (self restriction consulted by the ReturnToHand sink path).</summary>
    public static ICardEffect CannotReturnToHandStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotReturnToHandKey, isInheritedEffect, condition, cardEffectCondition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, RestrictionHelpers.CannotReturnToHandKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition), cardEffectCondition);

    /// <summary>(PRIM-W4) <c>CannotReturnToDeckStaticEffect</c> — this Digimon cannot be returned to the deck
    /// (self restriction consulted by the ReturnToDeck sink paths).</summary>
    public static ICardEffect CannotReturnToDeckStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotReturnToDeckKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, RestrictionHelpers.CannotReturnToDeckKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>CanNotBeDestroyedByBattleStaticEffect</c> — this Digimon cannot be deleted in
    /// battle (effect deletion still applies). Registers a battle-only immunity flag read by
    /// BattleDeletionGate. Per-card predicates accepted for fidelity.</summary>
    public static ICardEffect CanNotBeDestroyedByBattleStaticEffect(Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition, Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null, bool isLinkedEffect = false) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, BattleDeletionGate.PreventBattleDeletionKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, BattleDeletionGate.PreventBattleDeletionKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>CanNotBeTrashedBySkillStaticEffect</c> / <c>ImmuneStackTrashingClass</c> — this
    /// Digimon's digivolution cards cannot be trashed by effects. Registers a stack-trash immunity flag read
    /// by the source-trash sink path.</summary>
    public static ICardEffect CanNotBeTrashedBySkillStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, MatchStateMutationSink.ImmuneStackTrashingKey, isInheritedEffect, condition, cardEffectCondition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, MatchStateMutationSink.ImmuneStackTrashingKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition), cardEffectCondition);

    /// <summary>(PRIM-W4) <c>ImmuneStackTrashingClass</c> — alias of <see cref="CanNotBeTrashedBySkillStaticEffect"/>.</summary>
    public static ICardEffect ImmuneStackTrashingClass(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, MatchStateMutationSink.ImmuneStackTrashingKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W4) <c>ReplaceTopSecurityWithFaceUpOptionMainEffect</c> — Option [Main]: add the TOP
    /// security card to hand, then place this card face up as the top security card.</summary>
    public static IActivatedCardEffect ReplaceTopSecurityWithFaceUpOptionMainEffect(CardSource card) =>
        new ReplaceBottomSecurityWithFaceUpEffect(card, "[Main] Add your top security card to the hand. Then, place this card face up as the top security card.", top: true);

    /// <summary>(PRIM-W4) <c>PlayMindLinkTamerFromDigivolutionCards</c> — plays a Tamer under-card (MindLink)
    /// from a Digimon's digivolution stack onto the field (cost-free). Reuses the play-from-under primitive;
    /// the <paramref name="cardName"/> narrowing is applied at porting time.</summary>
    public static IActivatedCardEffect PlayMindLinkTamerFromDigivolutionCards(CardSource card, string cardName, string effectDescription) =>
        new ActivatedPlayFromUnderEffect(card, string.IsNullOrWhiteSpace(effectDescription) ? $"Play {cardName} from under a Digimon." : effectDescription);

    /// <summary>(PRIM-W4) <c>CanNotBeAttackedSelfStaticEffect</c> — this Digimon cannot be attacked (self
    /// CannotBeAttacked restriction consulted on the defender by AttackPermanentAction).</summary>
    public static ICardEffect CanNotBeAttackedSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotBeAttackedKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W4) <c>RevealLibraryClass</c> — reveals the top <paramref name="revealCount"/> cards of
    /// the owner's deck. In the full-information headless model a pure reveal has no hidden-state change, so
    /// this is an informational primitive; any follow-up act on the revealed cards is authored per-card.</summary>
    public static IActivatedCardEffect RevealLibraryClass(CardSource card, int revealCount) =>
        new InformationalRevealEffect(card, revealCount, $"Reveal the top {revealCount} card(s) of your deck.");

    /// <summary>(PRIM-W2) Original: <c>PlaceSelfDelayOptionSecurityEffect(card)</c> — "[Security] place this
    /// card in the battle area" (a Delay Option triggered from security). Reuses the play-this-card-to-battle
    /// mechanism (<see cref="PlayThisCardToBattleEffect"/>, cost-free move to the battle area); the Delay
    /// option's later trigger is a per-card effect on the placed card.</summary>
    public static ICardEffect PlaceSelfDelayOptionSecurityEffect(CardSource card) =>
        new PlayThisCardToBattleEffect(card, "[Security] Place this card in the battle area.");

    /// <summary>(PRIM-W2) Original: <c>PlaySelfDigimonAfterBattleSecurityEffect(card, deleteDigimon)</c> —
    /// "[Security] play this Digimon" (from security to the battle area). Reuses the play-this-card-to-battle
    /// mechanism (<see cref="PlayThisCardToBattleEffect"/>). The "at end of battle" timing and the temporary
    /// (<c>deleteDigimon</c>) lifetime are per-card refinements on the placed Digimon.</summary>
    public static ICardEffect PlaySelfDigimonAfterBattleSecurityEffect(CardSource card) =>
        new PlayThisCardToBattleEffect(card, "[Security] Play this Digimon (from security).");

    /// <summary>(PRIM-W2) Original: <c>LinkEffect(card, condition)</c> — the &lt;Link&gt; activation: attach
    /// this card to a chosen own Digimon, paying the link cost (read from the card's <c>linkCost</c> data).</summary>
    public static ICardEffect LinkEffect(CardSource card, Func<bool>? condition = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        int linkCost = card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? inst) && inst is not null
            && card.Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
            && def.Metadata.TryGetValue("linkCost", out object? raw) && raw is int cost ? cost : 0;
        return new LinkSelfEffect(card, linkCost, $"Link (Cost: {linkCost}).");
    }

    /// <summary>(PRIM-W2) Original: <c>BlockerStaticEffect(permanentCondition, isInheritedEffect, card,
    /// condition, isLinkedEffect)</c> — grants Blocker to a set of permanents. Modeled as a PLAYER-SCOPE
    /// Blocker grant on the owner's Digimon (the common "your Digimon gain &lt;Blocker&gt;" form);
    /// <paramref name="permanentCondition"/>/<paramref name="isLinkedEffect"/> accepted for source fidelity,
    /// per-permanent narrowing beyond the owner scope is a per-card concern.</summary>
    public static ICardEffect BlockerStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Blocker, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W2) Original: <c>SetMemoryTo3TamerEffect(card)</c> — "[Start of Your Turn] If you have
    /// 2 or less memory, set your memory to 3." (Tamer memory-setter). Triggered on OnStartTurn.</summary>
    public static ICardEffect SetMemoryTo3TamerEffect(CardSource card) =>
        new TriggeredSetMemoryEffect(card, EffectTiming.OnStartTurn, targetMemory: 3, threshold: 2,
            "[Start of Your Turn] If you have 2 or less memory, set your memory to 3.");

    /// <summary>(PRIM-W2) Original: <c>ArmorPurgeEffect(card)</c> — grants ArmorPurge to self (Batch2).</summary>
    public static ICardEffect ArmorPurgeEffect(CardSource card) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.ArmorPurge, isInheritedEffect: false, condition: null);

    /// <summary>(PRIM-W2) Original: <c>CanNotAttackSelfStaticEffect(defenderCondition, isInheritedEffect, card,
    /// condition, effectName)</c> — "this Digimon cannot attack" (self). Registers a CannotAttack restriction
    /// (reusable <see cref="ContinuousSelfRestrictionEffect"/>) that AttackPermanentAction consults via
    /// ContinuousRestrictionGate.EvaluateAttack. <paramref name="defenderCondition"/>/<paramref name="effectName"/>
    /// are accepted for source fidelity; per-defender narrowing is a per-card concern.</summary>
    public static ICardEffect CanNotAttackSelfStaticEffect(Func<Permanent, bool>? defenderCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        defenderCondition is null
            ? new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotAttackKey, isInheritedEffect, condition)
            : new CanNotAttackDefenderConditionEffect(card, cs => defenderCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner)), isInheritedEffect, condition);

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
        new PlayerScopeModifierEffect(card, ModifierHelpers.DpDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopePredicate: ScopePred(permanentCondition));

    /// <summary>A triggered "[When ...] gain/lose N memory" effect (the common ActivateClass memory form).
    /// <paramref name="timing"/> is the branch timing the card declared it under.</summary>
    public static ICardEffect AddMemoryTriggerEffect(
        EffectTiming timing, int amount, bool isInheritedEffect, CardSource card, Func<bool>? condition, string description,
        Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null, bool? isOptional = null) =>
        new TriggeredMemoryEffect(card, timing, amount, isInheritedEffect, condition, description, triggerGate, maxCountPerTurn, hash, isOptional);

    /// <summary>Original: <c>PlaySelfTamerSecurityEffect</c> — a Tamer's [Security] "play this Tamer". Plays
    /// the revealed Tamer onto the battle area (cost-free), auto-registering its effects (G10-003).</summary>
    public static ICardEffect PlaySelfTamerSecurityEffect(CardSource card) =>
        new PlayThisCardToBattleEffect(card, "[Security] Play this Tamer.");

    /// <summary>An activated "select up to <paramref name="maxCount"/> matching permanents and delete them"
    /// effect (Option [Main] delete skill, e.g. ST1_16 / ST1_15).</summary>
    public static ICardEffect SelectAndDestroyEffect(
        CardSource card,
        Func<HeadlessEntityId, bool> canTarget,
        int maxCount,
        bool canEndNotMax,
        string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Destroy, description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>new SuspendPermanentsClass(perms, ..).Tap()</c>
    /// coroutine: select up to <paramref name="maxCount"/> matching permanents and suspend them.</summary>
    public static ICardEffect SelectAndSuspendEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Tap, description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS unsuspend coroutine: select up to
    /// <paramref name="maxCount"/> matching permanents and unsuspend them.</summary>
    public static ICardEffect SelectAndUnsuspendEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.UnTap, description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS bounce coroutine: select up to
    /// <paramref name="maxCount"/> matching permanents and return them to hand.</summary>
    public static ICardEffect SelectAndBounceEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Bounce, description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.PlayPermanentCards(.., root)</c>
    /// coroutine: select up to <paramref name="maxCount"/> of the owner's cards in <paramref name="fromZone"/>
    /// (Trash / Hand) matching <paramref name="canTarget"/> and play each onto the battle area (cost-free).
    /// The AS-IS <c>SelectCardEffect.Root</c> maps to <paramref name="fromZone"/>.</summary>
    public static ICardEffect SelectAndPlayFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        new ActivatedSelectAndPlayEffect(card, fromZone, canTarget, maxCount, canEndNotMax, description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.AddThisCardToHand(..)</c> — return
    /// this card to the owner's hand.</summary>
    public static IActivatedCardEffect AddThisCardToHandEffect(CardSource card) =>
        new ReturnThisCardToHandEffect(card, "Return this card to the hand.");

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.DigivolveIntoHandOrTrashCard(..)</c>:
    /// select up to <paramref name="maxCount"/> battle-area Digimon matching <paramref name="canTarget"/> and
    /// de-digivolve each by <paramref name="count"/> (remove its top digivolution card[s]).</summary>
    public static ICardEffect SelectAndDeDigivolveEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int count, bool canEndNotMax, string description) =>
        new ActivatedSelectAndDeDigivolveEffect(card, canTarget, maxCount, count, canEndNotMax, description);

    /// <summary>(PRIM-W5) Mirror of the AS-IS <c>CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect</c>:
    /// reveal the top <paramref name="revealCount"/> cards of the owner's deck, select per
    /// <paramref name="conditions"/>, route the rest to <paramref name="remainingTo"/>.</summary>
    public static IActivatedCardEffect SimplifiedRevealDeckTopCardsAndSelect(
        CardSource card, int revealCount, IReadOnlyList<SimplifiedSelectCardConditionClass> conditions,
        RevealDestination remainingTo, string description) =>
        new SimplifiedRevealAndSelectEffect(card, revealCount, conditions, remainingTo, description);

    /// <summary>(PRIM-W5/S2) <c>CanNotAffectedStaticEffect</c> — AS-IS <c>CanNotAffectedClass</c>:
    /// <c>CanNotAffect(target, effect) = CardCondition(target) &amp;&amp; SkillCondition(effect)</c>. Registers a
    /// continuous immunity under <see cref="HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate"/> (consumed by the sink's effect path),
    /// carrying <paramref name="skillCondition"/> — the per-card predicate over the CAUSING effect's source that
    /// decides WHICH effects the card is immune to (e.g. <c>src =&gt; src.Owner != card.Owner &amp;&amp; src.IsDigimon</c>
    /// for "opponent's Digimon effects only"). <b>skillCondition must be provided to mirror the original</b>; null
    /// falls back to opponent-only.</summary>
    public static ICardEffect CanNotAffectedStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? skillCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousImmunityEffect(card, skillCondition, isInheritedEffect, condition);

    /// <summary>(PRIM-W5) <c>ChangeCardNamesClass</c> — grants this card an additional name
    /// (<paramref name="addedName"/>), folded into <c>CardSource.CardNames</c>.</summary>
    public static ICardEffect ChangeCardNamesStaticEffect(string addedName, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ChangeCardNamesEffect(card, addedName, isInheritedEffect, condition);

    // ===== (PRIM-W5) special plays — DigiXros / Blast / Blast-DNA =====================================
    // The card DECLARES its recipe (SpecialPlayRecipeRegistry, keyed by card number); SpecialPlayAction then
    // offers/executes the fusion or free digivolve. These factories register the recipe and return a no-op
    // marker for the card's effect list.

    /// <summary>(PRIM-W5) <c>DigiXrosEffectFromNames</c> — declares this card's DigiXros recipe: the named
    /// materials (hand/field) that fuse under it. <paramref name="costReduction"/> / per-card target predicate
    /// accepted for fidelity; material consumption + cost are engine-handled at play time.</summary>
    public static ICardEffect DigiXrosEffectFromNames(CardSource card, int costReduction, object? canTargetCondition = null, params string[] names)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DigiXros, NameMaterials(names), MemoryCost: 0));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(PRIM-W5) DigiXros with ARBITRARY per-material predicates — the faithful form of the AS-IS
    /// <c>AddDigiXrosConditionClass</c> whose <c>getDigiXrosCondition</c> returns
    /// <c>DigiXrosConditionElement(CanSelectCardCondition, label)</c> per material. Each
    /// <paramref name="materials"/> slot carries the original's <c>CanSelectCardCondition</c> predicate 1:1.</summary>
    public static ICardEffect DigiXrosEffect(CardSource card, int costReduction, params SpecialPlayMaterial[] materials)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DigiXros, materials, MemoryCost: 0));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>A material slot matched by card name (the name-equality subset of a DigiXros condition).</summary>
    public static SpecialPlayMaterial MaterialByName(string name) =>
        new(cs => cs.EqualsCardName(name), name);

    private static IReadOnlyList<SpecialPlayMaterial> NameMaterials(IEnumerable<string> names) =>
        names.Select(MaterialByName).ToArray();

    /// <summary>(PRIM-W5) <c>BlastDigivolveEffect</c> — declares this card as Blast-capable: it may digivolve
    /// onto a single matching battle-area Digimon for free (SpecialPlayKind.Blast, via FreeDigivolveHelpers).</summary>
    public static ICardEffect BlastDigivolveEffect(CardSource card, Func<bool>? condition)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.Blast, Array.Empty<SpecialPlayMaterial>(), MemoryCost: 0, Condition: condition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(PRIM-W5) <c>BlastDNADigivolveEffect</c> — declares this card's Blast-DNA recipe: the material
    /// names (from <paramref name="blastDNAConditions"/>) fuse as sources, played for free (DnaDigivolve).</summary>
    public static ICardEffect BlastDNADigivolveEffect(CardSource card, IReadOnlyList<BlastDNACondition> blastDNAConditions, Func<bool>? condition)
    {
        var materials = (blastDNAConditions ?? Array.Empty<BlastDNACondition>())
            .Select(c => new SpecialPlayMaterial(c.Matches, c.Label)).ToArray();
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DnaDigivolve, materials, MemoryCost: 0, Condition: condition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(PRIM-W5) <c>AddJogressConditionClass</c> equivalent — declares this card's Jogress (DNA
    /// digivolve) recipe: the two material names that fuse under it (SpecialPlayKind.DnaDigivolve). Translate
    /// the AS-IS <c>GetJogress</c> callback's material names into <paramref name="names"/>.</summary>
    public static ICardEffect JogressEffectFromNames(CardSource card, Func<bool>? condition, params string[] names)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DnaDigivolve, NameMaterials(names), MemoryCost: 0, Condition: condition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(PRIM-W5) Jogress with ARBITRARY per-material predicates (faithful form of
    /// <c>AddJogressConditionClass</c>'s <c>GetJogress</c>).</summary>
    public static ICardEffect JogressEffect(CardSource card, Func<bool>? condition, params SpecialPlayMaterial[] materials)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DnaDigivolve, materials, MemoryCost: 0, Condition: condition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

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

    /// <summary>An activated "select up to <paramref name="maxCount"/> opponent Digimon and trash
    /// <paramref name="trashCount"/> of each host's digivolution cards from the bottom/top" effect
    /// (e.g. ST2_03 / ST2_06 / ST2_09).</summary>
    public static ICardEffect SelectAndTrashDigivolutionEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int trashCount, bool fromBottom, string description) =>
        new ActivatedSelectTrashDigivolutionEffect(card, canTarget, maxCount, trashCount, fromBottom, description);

    /// <summary>A triggered "[When ...] unsuspend this Digimon" effect (e.g. ST2_11). Pass
    /// <paramref name="maxCountPerTurn"/> = 1 (+ <paramref name="hash"/> for the original SetHashString) to
    /// mirror a [Once Per Turn] limit — enforced by the live trigger loop via <c>OnceFlagController</c>.</summary>
    public static ICardEffect UnsuspendSelfTriggerEffect(EffectTiming timing, CardSource card, string description, int? maxCountPerTurn = null, string? hash = null) =>
        new TriggeredUnsuspendSelfEffect(card, timing, description, maxCountPerTurn, hash);

    /// <summary>An activated "gain/lose <paramref name="amount"/> memory" skill (Option [Main] / [Security],
    /// e.g. ST2_13).</summary>
    public static ICardEffect GainMemoryActivatedEffect(CardSource card, int amount, string description) =>
        new ActivatedMemoryEffect(card, amount, description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon and return each to its owner's
    /// hand" effect (Option [Main] bounce, e.g. ST2_16).</summary>
    public static ICardEffect SelectAndBounceEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, string description) =>
        new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax: maxCount > 1, SelectPermanentEffect.Mode.Bounce, description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon and make each unable to attack
    /// and/or block for <paramref name="duration"/>" effect (e.g. ST2_14).</summary>
    public static ICardEffect SelectAndRestrictEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, EffectDuration duration, bool cannotAttack, bool cannotBlock, string description) =>
        new ActivatedTargetRestrictionEffect(card, canTarget, maxCount, duration, cannotAttack, cannotBlock, description);

    /// <summary>A triggered "[When ...] this Digimon gets +<paramref name="changeValue"/> DP for
    /// <paramref name="duration"/>" effect (e.g. ST3_01).</summary>
    public static ICardEffect SelfDpBuffTriggerEffect(
        EffectTiming timing, int changeValue, EffectDuration duration, CardSource card, Func<bool>? condition, string description,
        Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null) =>
        new TriggeredSelfDpBuffEffect(card, timing, changeValue, duration, condition, description, triggerGate, maxCountPerTurn, hash);

    /// <summary>A triggered "[When ...] &lt;Recovery +<paramref name="amount"/> (Deck)&gt;" effect (e.g. ST3_09).</summary>
    public static ICardEffect RecoveryTriggerEffect(EffectTiming timing, int amount, CardSource card, Func<bool>? condition, string description) =>
        new RecoverTriggerEffect(card, timing, amount, condition, description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon and give each
    /// +<paramref name="changeValue"/> Security Attack for <paramref name="duration"/>" effect (e.g. ST3_15 [Main]).</summary>
    public static ICardEffect SelectAndBuffSAttackEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int changeValue, EffectDuration duration, string description) =>
        new ActivatedTargetBuffEffect(card, canTarget, maxCount, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, description);

    /// <summary>An activated "all your Digimon get +<paramref name="changeValue"/> DP for
    /// <paramref name="duration"/>" player-scope effect (e.g. ST3_13 [Security]).</summary>
    public static ICardEffect PlayerScopeBuffDpEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.DpDeltaKey, changeValue, duration, scopeCardType: "Digimon", description);

    /// <summary>An activated "all of your opponent's Digimon get +<paramref name="changeValue"/> Security
    /// Attack for <paramref name="duration"/>" player-scope effect, scoped to <paramref name="opponentId"/>
    /// (e.g. ST3_15 [Security] "all opponent Digimon gain Security Attack -1").</summary>
    public static ICardEffect OpponentScopeBuffSAttackEffect(
        CardSource card, int changeValue, EffectDuration duration, HeadlessPlayerId opponentId, string description) =>
        new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, scopeCardType: "Digimon", description, scopePlayerId: opponentId);

    /// <summary>Original: <c>ChangeSecurityDigimonCardDPStaticEffect</c> — continuous ±DP on the owner's
    /// Security-zone Digimon matching <paramref name="cardCondition"/> (evaluated 1:1). The condition decides the
    /// affected set INCLUDING the player — e.g. ST3_12 "your Security Digimon get +2000 DP" targets the owner,
    /// while BT9_084/LM_040 "your opponent's Security Digimon get -DP" target the enemy — so scope is any-player
    /// and the predicate (not a hardcoded owner scope) selects.</summary>
    public static ICardEffect ChangeSecurityDigimonCardDPStaticEffect(
        Func<CardSource, bool> cardCondition,
        int changeValue,
        bool isInheritedEffect,
        CardSource card,
        Func<bool>? condition,
        string? effectName = null) =>
        new PlayerScopeModifierEffect(card, ModifierHelpers.DpDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopeZone: "Security", scopePredicate: cardCondition, scopeAnyPlayer: true);
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

    /// <summary>It is the opponent's turn.</summary>
    public static bool IsOpponentTurn(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return TurnOwnershipHelpers.IsOpponentTurn(card.Context.TurnController.Current.TurnPlayerId, card.Owner);
    }

    /// <summary>The card is part of a battle-area permanent (as the top card or a buried source).</summary>
    public static bool IsExistOnBattleArea(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return !card.PermanentOfThisCard().IsEmpty;
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>IsExistOnHand</c> (<c>card.Owner.HandCards
    /// .Contains(card)</c>): this card is in its owner's hand.</summary>
    public static bool IsExistOnHand(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Hand).Contains(card.InstanceId);
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>IsSuspended</c>: <paramref name="id"/>'s permanent
    /// is currently suspended (tapped). Reads the live <c>isSuspended</c> instance-metadata flag the engine
    /// maintains on tap/unsuspend.</summary>
    public static bool IsSuspended(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        return !id.IsEmpty
            && card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) && instance is not null
            && instance.Metadata.TryGetValue("isSuspended", out object? raw) && raw is true;
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>MatchConditionPermanentCount(predicate,
    /// isContainBreedingArea)</c>: the number of battle-area (optionally + breeding) permanents, across BOTH
    /// players, that satisfy <paramref name="condition"/>. The original takes a <c>Func&lt;Permanent,bool&gt;</c>;
    /// the headless uses the established entity-id predicate idiom (see <see cref="IsOpponentBattleAreaDigimon"/>),
    /// so card-side predicates compose CardEffectCommons helpers (IsSuspended, …) on the id.</summary>
    public static int MatchConditionPermanentCount(CardSource card, Func<HeadlessEntityId, bool> condition, bool isContainBreedingArea = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(condition);
        int count = 0;
        foreach (HeadlessEntityId id in AllFieldPermanents(card, isContainBreedingArea))
        {
            if (condition(id))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>HasMatchConditionPermanent</c>: at least one
    /// matching permanent exists (count &gt;= 1).</summary>
    public static bool HasMatchConditionPermanent(CardSource card, Func<HeadlessEntityId, bool> condition, bool isContainBreedingArea = false) =>
        MatchConditionPermanentCount(card, condition, isContainBreedingArea) >= 1;

    /// <summary>Both players' battle-area cards (optionally + breeding-area), in turn order. Enumerates raw
    /// instance ids; the caller's predicate decides Digimon-ness / ownership / suspendability.</summary>
    private static IEnumerable<HeadlessEntityId> AllFieldPermanents(CardSource card, bool isContainBreedingArea)
    {
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
        {
            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                yield return id;
            }

            if (isContainBreedingArea)
            {
                foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BreedingArea))
                {
                    yield return id;
                }
            }
        }
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

    /// <summary>(EX8-1) Mirror of the original <c>IsPermanentExistsOnBattleAreaDigimon(permanent)</c>:
    /// <paramref name="id"/> is a battle-area Digimon owned by EITHER player (used by "suspend 1 Digimon"
    /// targets and by the suspended-count threshold).</summary>
    public static bool IsBattleAreaDigimon(CardSource card, HeadlessEntityId id) =>
        IsOwnerBattleAreaDigimon(card, id) || IsOpponentBattleAreaDigimon(card, id);

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
        cardEffects.Add(new ReuseMainOptionEffect(effectName));
    }

    /// <summary>Mirror of the original <c>Permanent.HasNoDigivolutionCards</c> (entity-id form): the
    /// battle-area permanent topped by <paramref name="id"/> has no digivolution (under) cards.</summary>
    public static bool HasNoDigivolutionCards(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty)
        {
            return false;
        }

        DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, id);
        return stack.UnderCards.Count == 0;
    }

    /// <summary>Metadata flag marking a digivolution source as protected from being trashed (mirror of the
    /// original <c>CardSource.CanNotTrashFromDigivolutionCards</c>). Stamped on the source instance.</summary>
    public const string TrashProtectedKey = "cannotTrashFromDigivolution";

    /// <summary>Query scope for the dynamic delete-DP threshold raise effects (mirror of
    /// <c>MaxDP_DeleteEffect</c>'s raise-able cap).</summary>
    public const string MaxDpDeleteScope = "DeleteThreshold";

    /// <summary>The per-player additive delta a delete-threshold-raise effect carries.</summary>
    public const string MaxDpDeleteDeltaKey = "maxDpDeleteDelta";

    /// <summary>Mirror of the original <c>card.Owner.MaxDP_DeleteEffect(baseThreshold, ...)</c>: the current
    /// delete-DP threshold for the card's owner = <paramref name="baseThreshold"/> plus any raise effects
    /// (continuous bindings scoped to <see cref="MaxDpDeleteScope"/> carrying <see cref="MaxDpDeleteDeltaKey"/>
    /// for that owner). A "delete a Digimon with N DP or less" gate compares against this, not a flat base.</summary>
    public static int MaxDpDeleteThreshold(CardSource card, int baseThreshold)
    {
        ArgumentNullException.ThrowIfNull(card);
        int total = baseThreshold;
        foreach (EffectRequest effect in card.Context.EffectRegistry.GetContinuousEffects(new EffectQueryContext(MaxDpDeleteScope)))
        {
            if (effect.Context.OwnerPlayerId == card.Owner
                && effect.Context.Values.TryGetValue(MaxDpDeleteDeltaKey, out object? raw)
                && raw is int delta)
            {
                total += delta;
            }
        }

        return total;
    }

    /// <summary>Mirror of the original target gate
    /// <c>permanent.DigivolutionCards.Count(c =&gt; !c.CanNotTrashFromDigivolutionCards(...))</c>: the number of
    /// the host permanent's digivolution (under) cards that are NOT trash-protected.</summary>
    public static int TrashableDigivolutionCount(CardSource card, HeadlessEntityId hostId)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (hostId.IsEmpty)
        {
            return 0;
        }

        DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, hostId);
        int count = 0;
        foreach (StackedCard under in stack.UnderCards)
        {
            if (!IsTrashProtectedSource(card, under.InstanceId))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The host has at least one trashable (non-protected) digivolution card.</summary>
    public static bool HasTrashableDigivolutionCards(CardSource card, HeadlessEntityId hostId) =>
        TrashableDigivolutionCount(card, hostId) >= 1;

    private static bool IsTrashProtectedSource(CardSource card, HeadlessEntityId sourceId)
    {
        return !sourceId.IsEmpty
            && card.Context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? instance)
            && instance is not null
            && instance.Metadata.TryGetValue(TrashProtectedKey, out object? raw) && raw is true;
    }

    /// <summary>Mirror of the original <c>permanent.TopCard.HasLevel</c>: the host's top card carries a
    /// printed level (Digimon / DigiEgg do; Tamers / Options do not).</summary>
    public static bool TopCardHasLevel(CardSource card, HeadlessEntityId id) => LevelOf(card, id) > 0;

    /// <summary>Mirror of the original <c>Permanent.Level</c> (entity-id form): the printed level of the
    /// battle-area card topped by <paramref name="id"/> (0 when unknown), read from instance/def metadata.</summary>
    public static int LevelOf(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty || !card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) || instance is null)
        {
            return 0;
        }

        return ReadLevel(instance.Metadata)
            ?? (card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) && def is not null
                ? ReadLevel(def.Metadata) ?? 0
                : 0);
    }

    private static int? ReadLevel(IReadOnlyDictionary<string, object?> metadata)
    {
        foreach (string key in new[] { "level", "Level" })
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

    /// <summary>Mirror of the original <c>HasMatchConditionOpponentsPermanent</c> (entity-id predicate form):
    /// the opponent has at least one battle-area Digimon matching <paramref name="condition"/>.</summary>
    public static bool HasMatchConditionOpponentsPermanent(CardSource card, Func<HeadlessEntityId, bool> condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(condition);
        foreach (HeadlessEntityId id in OpponentBattleAreaDigimon(card))
        {
            if (condition(id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Mirror of the original <c>card.Owner.SecurityCards.Count</c>: the number of cards in the
    /// owner's security stack (used by security-count conditions, e.g. ST3_05 "4 or more", ST3_09 "3 or less").</summary>
    public static int SecurityCount(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Security).Count;
    }

    /// <summary>Mirror of the original <c>IsDPZeroDelete(hashtable)</c>: the just-deleted permanent (the
    /// trigger subject) was deleted by dropping to 0 DP — distinguished by the <c>DPZero</c> marker that
    /// <see cref="DpZeroDeletionHelpers"/> stamps (vs a battle or direct-Delete-effect deletion).</summary>
    public static bool IsDPZeroDelete(CardSource card, CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Request.Context.TriggerEntityId is not { } deleted || deleted.IsEmpty)
        {
            return false;
        }

        return card.Context.CardInstanceRepository.TryGetInstance(deleted, out CardInstanceRecord? instance)
            && instance is not null
            && instance.Metadata.TryGetValue(DpZeroDeletionHelpers.DpZeroKey, out object? raw) && raw is true;
    }

    /// <summary>Mirror of the original <c>CanTriggerOnPermanentDeleted(hashtable, permanentCondition)</c>: a
    /// permanent was just deleted (the trigger subject) and it satisfies <paramref name="permanentCondition"/>.</summary>
    public static bool CanTriggerOnPermanentDeleted(CardSource card, CardEffectResolveContext context, Func<HeadlessEntityId, bool> permanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(permanentCondition);
        return context.Request.Context.TriggerEntityId is { } deleted && !deleted.IsEmpty && permanentCondition(deleted);
    }

    /// <summary>The deleted-subject ownership/type predicate: <paramref name="id"/> is (was) an opponent's
    /// Digimon — zone-agnostic (the card may already be in the trash), so usable in deletion triggers.</summary>
    public static bool IsOpponentOwnedDigimon(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty || !card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) || instance is null)
        {
            return false;
        }

        if (instance.OwnerId == card.Owner)
        {
            return false;
        }

        return card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            && def is not null
            && string.Equals(def.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Mirror of the original <c>card.PermanentOfThisCard().battle.enemyPermanent(...)</c>: the
    /// entity this card's permanent is currently battling (the other participant of the in-progress attack),
    /// or empty when this permanent is not in a battle. Read from <c>AttackController.Current</c>.</summary>
    public static HeadlessEntityId CurrentBattleOpponent(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        HeadlessEntityId self = card.PermanentOfThisCard().TopInstanceId;
        if (self.IsEmpty)
        {
            return default;
        }

        HeadlessAttackState attack = card.Context.AttackController.Current;
        HeadlessEntityId attacker = attack.AttackerId ?? default;
        HeadlessEntityId defender = attack.BlockerId ?? attack.TargetId ?? default;
        if (self == attacker)
        {
            return defender;
        }

        if (self == defender)
        {
            return attacker;
        }

        return default;
    }

    /// <summary>The opponent player id (the first player in turn order that is not the card owner). Empty
    /// when there is no distinct opponent (e.g. uninitialized turn order).</summary>
    public static HeadlessPlayerId OpponentOf(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
        {
            if (player != card.Owner)
            {
                return player;
            }
        }

        return default;
    }

    /// <summary>The opponent's battle-area Digimon top cards (entity ids).</summary>
    private static IEnumerable<HeadlessEntityId> OpponentBattleAreaDigimon(CardSource card)
    {
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
        {
            if (player == card.Owner)
            {
                continue;
            }

            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                if (IsOpponentBattleAreaDigimon(card, id))
                {
                    yield return id;
                }
            }
        }
    }
}

/// <summary>
/// (G6-001) Maps a card number to its ported effect class. A ported card is a non-abstract
/// <see cref="CEntity_Effect"/> subclass whose type name equals the card number (e.g. class
/// <c>ST1_01</c> -> card "ST1_01"), so the dispatch is discovered by reflection — no manual table, and it
/// auto-grows as cards are ported. Un-ported cards (skeleton files with no class) simply aren't found.
/// </summary>
public static class CardEffectDispatch
{
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> ByCardNumber = new(Build);

    private static IReadOnlyDictionary<string, Type> Build()
    {
        var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (Type type in typeof(CEntity_Effect).Assembly.GetTypes())
        {
            if (type.IsAbstract
                || !type.IsSubclassOf(typeof(CEntity_Effect))
                || type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            map[type.Name] = type;
        }

        return map;
    }

    public static int Count => ByCardNumber.Value.Count;

    public static bool TryCreate(string? cardNumber, out CEntity_Effect? effect)
    {
        effect = null;
        if (string.IsNullOrWhiteSpace(cardNumber) || !ByCardNumber.Value.TryGetValue(cardNumber.Trim(), out Type? type))
        {
            return false;
        }

        effect = (CEntity_Effect)Activator.CreateInstance(type)!;
        return true;
    }

    /// <summary>
    /// Resolves a card's effect class honoring the <c>effectClass</c> alias. cards.json carries an
    /// <c>effectClass</c> per card which is authoritative: for most cards it equals the card number, but
    /// alias cards (e.g. ST2_07 / ST3_07 reuse <c>ST1_06</c>, and every alternate-art reprint <c>*_P2</c>
    /// reuses its base) point at another class. When the metadata carries a non-empty effectClass we resolve
    /// by it exclusively (an un-ported alias is a no-op, like an un-ported card); otherwise we fall back to
    /// the card number — so test-constructed records without effectClass metadata behave exactly as before.
    /// </summary>
    public static bool TryCreateForCard(CardRecord def, out CEntity_Effect? effect)
    {
        effect = null;
        if (def is null)
        {
            return false;
        }

        if (def.Metadata.TryGetValue("effectClass", out object? raw)
            && raw is string alias
            && !string.IsNullOrWhiteSpace(alias))
        {
            return TryCreate(alias, out effect);
        }

        return TryCreate(def.CardNumber, out effect);
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
        // (EX8-3) OnEndTurn self-statics (e.g. <Vortex> via VortexSelfEffect) register at enter-play like the
        // other self-static keyword timings; GR-006's EndOfTurnEffectAttack then reads the live binding at
        // turn end. The original keys <Vortex> under EffectTiming.OnEndTurn (EX8_074 region "Vortex").
        EffectTiming.OnEndTurn,
        EffectTiming.OnStartTurn,
    });

    /// <summary>(G6-001) Auto-register the effects of the card instance entering play, resolved from the
    /// dispatch by its card number. No-op (returns false) for cards with no ported effect class — so
    /// un-ported cards are unaffected.</summary>
    public static bool RegisterCard(EngineContext context, HeadlessEntityId instanceId, HeadlessPlayerId controller)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (instanceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? instance)
            || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            || effect is null)
        {
            return false;
        }

        RegisterOnEnterPlay(context, effect, def.CardNumber, new CardSource(context, instanceId, controller, instance.OwnerId));
        return true;
    }

    /// <summary>(G6-001) Remove every binding sourced from <paramref name="instanceId"/> (the card left
    /// play). Returns the number of bindings removed.</summary>
    public static int UnregisterCard(EngineContext context, HeadlessEntityId instanceId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (instanceId.IsEmpty)
        {
            return 0;
        }

        return context.EffectRegistry.RemoveWhere(binding => binding.Request.Context.SourceEntityId == instanceId);
    }

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
