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
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


/// <summary>
/// A continuous numeric self-modifier (DP / security attack / cost). Lowers to a continuous-role binding
/// targeting the source card, carrying the delta under the matching <see cref="ModifierHelpers"/> key plus
/// optional inherited / condition markers, so <c>ContinuousDpGate</c> /
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
    public ContinuousSelfRestrictionEffect(CardSource card, string restrictionKey, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? causingEffectPredicate = null, Func<CardSource, bool>? counterpartPredicate = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(restrictionKey);
        Card = card;
        RestrictionKey = restrictionKey;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        CausingEffectPredicate = causingEffectPredicate;
        CounterpartPredicate = counterpartPredicate;
    }

    public CardSource Card { get; }

    public string RestrictionKey { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    /// <summary>(FR2/M-2) AS-IS cardEffectCondition — the restriction only blocks effects whose causing effect's
    /// SOURCE card matches this. Null = blocks any effect.</summary>
    public Func<CardSource, bool>? CausingEffectPredicate { get; }

    /// <summary>(W6-G) AS-IS defenderCondition/attackerCondition — the restriction only applies when the COUNTERPART
    /// (blocker for BeBlocked, attacker for BeAttacked) matches this predicate; embedded into the canonical joint
    /// predicate (<c>JointRestrictionEffect.PredicateKey</c>) and evaluated by <c>RestrictionScan</c>. Null = any counterpart.</summary>
    public Func<CardSource, bool>? CounterpartPredicate { get; }

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

        // (joint-migration) canonical joint predicate synthesised from this SELF restriction:
        // subject = this card; the 2nd arg (counterpart participant OR causing effect source) must satisfy any
        // provided predicate (cp==null + a predicate ⇒ not restricted, mirroring IsRestrictedFromCause).
        HeadlessEntityId selfId = Card.InstanceId;
        Func<CardSource, bool>? causing = CausingEffectPredicate;
        Func<CardSource, bool>? counterpart = CounterpartPredicate;
        values[JointRestrictionEffect.PredicateKey(RestrictionKey)] = (Func<CardSource, CardSource?, bool>)((subject, cp) =>
            subject.InstanceId == selfId
            && (causing is null || (cp is not null && causing(cp)))
            && (counterpart is null || (cp is not null && counterpart(cp))));

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

    private readonly bool _scopeAnyPlayer;

    public ContinuousPlayerScopeRestrictionEffect(CardSource card, HeadlessPlayerId scopePlayerId, string restrictionKey, string? scopeCardType, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? scopePredicate = null, Func<CardSource, bool>? causingEffectPredicate = null, bool scopeAnyPlayer = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(restrictionKey);
        Card = card;
        // (R3-W3c-1) carry the printing card as the base EffectSourceCard so that, when this restriction effect is
        // passed AS-IS as the causing `cardEffect` to a subject's CanNotBeAffected scan (the :283 immunity exemption,
        // AS-IS Permanent.cs:2267 `cardEffect1`), the immunity's SkillCondition can read the causing effect's source.
        SetEffectSourceCard(card);
        _scopePlayerId = scopePlayerId;
        RestrictionKey = restrictionKey;
        ScopeCardType = scopeCardType;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        ScopePredicate = scopePredicate;
        CausingEffectPredicate = causingEffectPredicate;
        _scopeAnyPlayer = scopeAnyPlayer;
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
        if (_scopeAnyPlayer)
        {
            // (#5) apply to ANY player's matching cards — the ScopePredicate decides (e.g. "the OPPONENT's
            // Digimon", via p.OwnerId), mirroring the AS-IS playerCondition over the payer.
            values[PlayerScopeContinuousHelpers.ScopeAnyPlayerKey] = true;
        }

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

        // (joint-migration) additively emit the canonical joint predicate synthesised from this PLAYER-SCOPE
        // restriction: subject membership = (anyPlayer OR owner==scopePlayer) ∧ cardType ∧ scopePredicate; the 2nd
        // arg (causing effect source) must satisfy any causing predicate (mirrors PlayerScopeContinuousHelpers +
        // IsRestrictedFromCause).
        HeadlessPlayerId scopePlayer = _scopePlayerId;
        bool anyPlayer = _scopeAnyPlayer;
        string? scopeType = ScopeCardType;
        Func<CardSource, bool>? scopePred = ScopePredicate;
        Func<CardSource, bool>? causingP = CausingEffectPredicate;
        // (P0-restr) AS-IS checks `!TopCard.CanNotBeAffected(cardEffect)` on the SUBJECT for a PRINTED player-scope
        // cannot-attack / cannot-block (Permanent.cs:2267/2290 attack, :2194 block player-scan) — a subject immune to
        // the printing card's effects is exempt. Only these kinds are immunity-checked in AS-IS (CanMove/CanSuspend/
        // CanUnsuspend do NOT check it), so the term is scoped to the confirmed set to avoid inventing immunity.
        // (R3-W3c-1) The immunity term is rehomed from the registry gate (ContinuousImmunityGate.BlocksOpponentEffect,
        // which read the joint predicate registered by the OLD-model CanNotAffectedStaticEffect) to the AS-IS-literal
        // live scan `!subject.CanNotBeAffected(this)` — AS-IS passes the restriction effect itself as `cardEffect1`
        // (Permanent.cs:2267/2290), and this effect (`ContinuousPlayerScopeRestrictionEffect`) IS that ICardEffect.
        // This is part of the RD-W3A-01 consumer-side rehousing that unblocks the CanNotAffectedStaticEffect flip.
        ICardEffect self = this;
        bool immunityChecked = RestrictionKey == RestrictionHelpers.CannotAttackKey || RestrictionKey == RestrictionHelpers.CannotBlockKey;
        values[JointRestrictionEffect.PredicateKey(RestrictionKey)] = (Func<CardSource, CardSource?, bool>)((subject, cp) =>
            (anyPlayer || subject.Owner == scopePlayer)
            && (string.IsNullOrWhiteSpace(scopeType) || (subject.IsCardType(scopeType)))
            && (scopePred is null || scopePred(subject))
            && (causingP is null || (cp is not null && causingP(cp)))
            && (!immunityChecked || !subject.CanNotBeAffected(self)));

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


/// <summary>(W6-X) inert mirror of AS-IS <c>AddDetailClass</c> — tooltip text only (no consumer beyond
/// the AS-IS UI); the binding carries the string for observability.</summary>
public sealed class DisplayDetailEffect : ICardEffect
{
    private readonly CardSource _card;
    private readonly string _detail;
    private readonly Func<bool>? _condition;

    public DisplayDetailEffect(CardSource card, string detail, Func<bool>? condition)
    {
        _card = card ?? throw new ArgumentNullException(nameof(card));
        _detail = detail;
        _condition = condition;
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { ["display.detail"] = _detail };
        if (_condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = _condition;
        }

        var context = new EffectContext(
            _card.Controller, _card.Owner, _card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { _card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), _card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, queryScopes: null, effect: null, duration: null);
    }
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
    public AddedDigivolutionRequirementPredicateEffect(CardSource card, Func<Permanent, bool> predicate, int digivolutionCost, bool ignoreDigivolutionRequirement, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? targetCardCondition = null, Func<int>? costEquation = null, int level = -1, int minLevel = -1, int maxLevel = -1)
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
        Level = level;
        MinLevel = minLevel;
        MaxLevel = maxLevel;
    }

    /// <summary>(A2) AS-IS level / minLevel / maxLevel — a HARD level gate on the digivolving-FROM permanent,
    /// separate from (and evaluated before) <see cref="Predicate"/>. Exact <see cref="Level"/> wins; the
    /// min/max range applies only when Level &lt; 0. -1 = unset.</summary>
    public int Level { get; }
    public int MinLevel { get; }
    public int MaxLevel { get; }

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

        if (Level >= 0)
        {
            values[DigivolveAction.AddedEvolutionLevelKey] = Level;
        }

        if (MinLevel >= 0)
        {
            values[DigivolveAction.AddedEvolutionMinLevelKey] = MinLevel;
        }

        if (MaxLevel >= 0)
        {
            values[DigivolveAction.AddedEvolutionMaxLevelKey] = MaxLevel;
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
    public SelfKeywordBatch2Effect(CardSource card, KeywordBaseBatch2Kind kind, bool isInheritedEffect, Func<bool>? condition, IReadOnlyDictionary<string, object?>? extraValues = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
        Kind = kind;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        ExtraValues = extraValues;
    }

    public CardSource Card { get; }

    public KeywordBaseBatch2Kind Kind { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    /// <summary>(A4) additional binding values carried on the grant (e.g. Partition's stored
    /// <c>PartitionCondition</c> list) — consumed live by the keyword's behaviour gate.</summary>
    public IReadOnlyDictionary<string, object?>? ExtraValues { get; }

    public EffectBinding ToBinding(string effectId)
    {
        var context = new EffectContext(
            Card.Controller,
            Card.Owner,
            Card.InstanceId,
            triggerEntityId: null,
            targetEntityIds: new[] { Card.InstanceId },
            values: ExtraValues);
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
    public SelfKeywordByNameEffect(CardSource card, string keywordName, bool isInheritedEffect, Func<bool>? condition, Func<CardSource, bool>? permanentCondition = null, IReadOnlyDictionary<string, object?>? extraValues = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(keywordName);
        Card = card;
        KeywordName = keywordName;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        PermanentCondition = permanentCondition;
        ExtraValues = extraValues;
    }

    /// <summary>(C1) additional binding values carried on the grant (e.g. Fragment's trashValue).</summary>
    public IReadOnlyDictionary<string, object?>? ExtraValues { get; }

    public CardSource Card { get; }

    public string KeywordName { get; }

    public bool IsInheritedEffect { get; }

    public Func<bool>? Condition { get; }

    /// <summary>(D1) The keyword's per-card <c>permanentCondition</c>, evaluated against the OTHER
    /// permanent the keyword acts on (AS-IS Decoy: the protected Digimon), not the holder. Stored on the
    /// binding under <see cref="ContinuousKeywordGate.PermanentConditionKey"/> and read live by the
    /// consuming gate (<see cref="ContinuousKeywordGate.KeywordGrantAcceptsSubject"/>).</summary>
    public Func<CardSource, bool>? PermanentCondition { get; }

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

        if (PermanentCondition is not null)
        {
            values[ContinuousKeywordGate.PermanentConditionKey] = PermanentCondition;
        }

        if (ExtraValues is not null)
        {
            foreach (KeyValuePair<string, object?> pair in ExtraValues)
            {
                values[pair.Key] = pair.Value;
            }
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


/// <summary>(PRIM-P0 AddSkill) The headless mirror of AS-IS AddSkillClass whose getEffects splices a TRIGGERED
/// activated effect onto a live-matched set. Wraps a nested triggered effect's binding with the TriggerGrant +
/// player-scope markers so it is registered under the nested effect's timing but fires for ANY event whose actor
/// is the scoped player; the collector injects the triggering card as the subject so the nested effect (built to
/// read TriggerEntityId and apply its per-card predicate) resolves against that card. See
/// docs/porting/play_option_and_delayed_player_effect_design.md.</summary>
public sealed class PlayerScopeTriggerGrantEffect : ICardEffect
{
    public PlayerScopeTriggerGrantEffect(CardSource card, HeadlessPlayerId scopePlayer, ICardEffect nestedEffect, bool scopeAnyPlayer = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(nestedEffect);
        Card = card;
        ScopePlayer = scopePlayer;
        NestedEffect = nestedEffect;
        ScopeAnyPlayer = scopeAnyPlayer;
    }

    public CardSource Card { get; }

    public HeadlessPlayerId ScopePlayer { get; }

    public ICardEffect NestedEffect { get; }

    public bool ScopeAnyPlayer { get; }

    public EffectBinding ToBinding(string effectId)
    {
        // (P6 cluster3) old-model lowering via LegacyBindingBridge (ToBinding left the ICardEffect contract);
        // a NEW-model nested effect on this grant path has no grant store yet — STOP, design item RD-P6C3-C1.
        if (!LegacyBindingBridge.TryToBinding(NestedEffect, effectId, out EffectBinding? inner) || inner is null)
        {
            throw new NotSupportedException(
                $"PlayerScopeTriggerGrantEffect: nested '{NestedEffect.GetType().Name}' is a NEW-model effect — no legacy ToBinding lowering (design item RD-P6C3-C1).");
        }

        Headless.Effects.EffectContext ctx = inner.Request.Context;
        var values = new Dictionary<string, object?>(ctx.Values, StringComparer.Ordinal)
        {
            [AutoProcessingTriggerCollector.TriggerGrantKey] = true,
            [Headless.Effects.PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
        };
        if (ScopeAnyPlayer)
        {
            values[Headless.Effects.PlayerScopeContinuousHelpers.ScopeAnyPlayerKey] = true;
        }
        else
        {
            values[Headless.Effects.PlayerScopeContinuousHelpers.ScopePlayerIdKey] = ScopePlayer.Value;
        }

        var newCtx = new Headless.Effects.EffectContext(ctx.SourcePlayerId, ctx.OwnerPlayerId, ctx.SourceEntityId, ctx.TriggerEntityId, ctx.TargetEntityIds, values);
        return new EffectBinding(
            new EffectRequest(inner.Request.EffectId, inner.Request.ControllerId, inner.Request.Timing, newCtx),
            inner.Keywords, inner.QueryRoles, inner.QueryScopes, inner.Effect, inner.Duration);
    }
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


// (이연④-b RD-IMM-01 RESOLVED) The old-model `ContinuousImmunityEffect` (ICardEffect-only, invisible to the live
// `CardSource.CanNotBeAffected` ICanNotAffectedEffect scan) was DELETED here. Its ④-a state was an inert live-list
// marker: `PermanentEffectFactory.DigimonEffectImmunity/OptionEffectImmunity` produced it and BT25_019 / EX11_074
// added it to their `UntilOpponentTurnEndEffects` bucket, but the scan never saw it — so the real cards' immunity
// was production-inert. Those two factory methods now emit the AS-IS kind-class `CanNotAffectedClass`
// (ICanNotAffectedEffect), which the live scan sees; FAILa-03 calls the factory directly and EXEMPLAR-T2B drives
// the live immunity. With no producers left, this type is census-0 and removed (structural-invention campaign 1/22).


/// <summary>(R3-W3c-4) A minimal cause carrier — an <see cref="ICardEffect"/> whose only meaningful data is its
/// <c>EffectSourceCard</c>. Used to route id/source-only substrate consumers (trash-protection filters,
/// stack-trash immunity, …) through the AS-IS live joint-scan getters, every one of which takes the causing
/// <c>ICardEffect</c> and reduces it to its non-null-ness / <c>EffectSourceCard</c> (the same reduction
/// <c>ActivatedHashtableBridge.CauseStub</c> uses for driving-event payloads). The old-model
/// <c>ContinuousTrashProtectionEffect</c> (which lowered this concept into a dead registry binding) is retired:
/// the sole producer today is BT9_109's inline <c>CanNotTrashFromDigivolutionCardsClass</c>, served by the live
/// <see cref="CardSource.CanNotTrashFromDigivolutionCards"/> scan.
///
/// (design item RD-BCE-01) <see cref="For(EngineContext, HeadlessEntityId)"/> collapses to a source-less cause
/// (a fresh BareCauseEffect with no EffectSourceCard) when the id is empty/unresolvable, and both factories always
/// return a NON-null ICardEffect. Some AS-IS restriction/immunity predicates distinguish a null causing effect
/// (a RULE-sourced action, e.g. battle/end-of-turn, which many `CanNotAffect`/`CanNotBeTrashed` conditions treat
/// as "not an opponent effect" ⇒ NOT immune) from a real-but-unknown source. A source-LESS BareCauseEffect is not
/// byte-identical to AS-IS `null`: the getter's own `_cardEffect == null` early-out (CanNotBeAffected :743) is NOT
/// taken, and an IsOpponentEffect check reads `EffectSourceCard?.Owner` = null-owner rather than short-circuiting.
/// In practice the sink/Commons consumers here always carry a real card source (SourceEntityId / sourceCard), so
/// this divergence is latent; revisit if a genuinely rule-sourced (null-cause) mutation is ever routed through a
/// BareCauseEffect gate.</summary>
public sealed class BareCauseEffect : ICardEffect
{
    /// <summary>A bare cause whose <c>EffectSourceCard</c> is <paramref name="sourceCard"/> (the AS-IS collapse of
    /// the causing effect to its source card). Null <paramref name="sourceCard"/> yields a source-less cause.</summary>
    public static BareCauseEffect For(CardSource? sourceCard)
    {
        var stub = new BareCauseEffect();
        if (sourceCard is not null)
        {
            stub.SetEffectSourceCard(sourceCard);
        }

        return stub;
    }

    /// <summary>A bare cause whose <c>EffectSourceCard</c> resolves <paramref name="sourceId"/> to a
    /// <see cref="CardSource"/> (owner read from the repository). Empty id — OR an id that resolves to no live
    /// instance (hence no owner) — yields a source-less cause: a <see cref="CardSource"/> requires a non-empty
    /// controller, and an unresolvable cause matches no narrowed restriction predicate (AS-IS "unknown causing
    /// source does not block a conditional restriction").</summary>
    public static BareCauseEffect For(EngineContext context, HeadlessEntityId sourceId)
    {
        if (sourceId.IsEmpty
            || !(context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? instance) && instance is not null)
            || instance.OwnerId.IsEmpty)
        {
            return new BareCauseEffect();
        }

        return For(new CardSource(context, sourceId, instance.OwnerId, instance.OwnerId));
    }
}


/// <summary>(E-3) A continuous "an Option matching <see cref="CardCondition"/> cannot be played" effect
/// registered under <see cref="HeadlessDCGO.Engine.Headless.Runtime.CanNotPlayOptionScan"/> (AS-IS
/// <c>CanNotPlayClass</c> — <c>ICanNotPlayCardEffect.CanNotPlay(cardSource) = cardCondition(cardSource)</c>).
/// The option-play legality gate scans every such active effect (<see cref="CanNotPlayThisOption"/> regions
/// ①②③). <paramref name="playerScope"/> distinguishes a PLAYER-bucket grant (AS-IS <c>AddEffectToPlayer</c>
/// region ①, granter not a field permanent — bypasses the field membership check) from a FIELD static (region
/// ②, e.g. BT8_057, subject to the AS-IS EffectList_ForCard stack-position membership).</summary>
public sealed class ContinuousCanNotPlayOptionEffect : ICardEffect
{
    public ContinuousCanNotPlayOptionEffect(
        CardSource card,
        Func<CardSource, bool> cardCondition,
        bool isInheritedEffect,
        Func<bool>? condition,
        bool playerScope = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardCondition);
        Card = card;
        CardCondition = cardCondition;
        IsInheritedEffect = isInheritedEffect;
        Condition = condition;
        PlayerScope = playerScope;
    }

    public CardSource Card { get; }

    /// <summary>AS-IS <c>CanNotPlayClass._cardCondition</c> — WHICH option (owner / IsOption / …) this forbids,
    /// evaluated against the option being played.</summary>
    public Func<CardSource, bool> CardCondition { get; }

    public bool IsInheritedEffect { get; }

    /// <summary>AS-IS <c>cardEffect.CanUse(null)</c> gate — the effect's live usability condition.</summary>
    public Func<bool>? Condition { get; }

    /// <summary>True for an AS-IS region-① player-bucket grant (bypasses the field stack-position membership).</summary>
    public bool PlayerScope { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);

        Func<CardSource, bool> cardCondition = CardCondition;
        // AS-IS CanNotPlay(option) = cardCondition(option). Single-arg joint (no causing-effect arg — the AS-IS
        // interface takes only the CardSource being played).
        values[HeadlessDCGO.Engine.Headless.Runtime.CanNotPlayOptionScan.JointPredicateKey] =
            (Func<CardSource, bool>)(option => cardCondition(option));

        if (PlayerScope)
        {
            values[HeadlessDCGO.Engine.Headless.Runtime.CanNotPlayOptionScan.PlayerScopeKey] = true;
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
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null,
            // Registered field-wide (no target): the option-play scan enumerates the scope, not a per-card target.
            targetEntityIds: Array.Empty<HeadlessEntityId>(),
            values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous,
            new[] { HeadlessDCGO.Engine.Headless.Runtime.CanNotPlayOptionScan.Scope }, effect: null, duration: null);
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
        };

        // (joint-migration) canonical joint: subject = this card; cannot attack a defender matching the predicate.
        HeadlessEntityId selfId = Card.InstanceId;
        Func<CardSource, bool> defPred = DefenderPredicate;
        values[JointRestrictionEffect.PredicateKey(RestrictionHelpers.CannotAttackKey)] = (Func<CardSource, CardSource?, bool>)((subject, cp) =>
            subject.InstanceId == selfId && cp is not null && defPred(cp));

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


/// <summary>(d-remediation, true-scan) AS-IS <c>ICanNotSelectBySkillEffect</c> / <c>Permanent.CanSelectBySkill</c>:
/// a continuous effect that, when SCANNED over every field permanent's effects, forbids a candidate from being
/// CHOSEN by a skill. Carries the AS-IS JOINT predicate <c>CanNotSelectBySkill(candidate, skillSource)</c> as a
/// single runtime Func (NOT split into scope + causing) so a non-separable predicate is preserved, exactly like
/// the original's <c>foreach permanent … effect.CanNotSelectBySkill(this, skill)</c> loop.</summary>
public sealed class CanNotSelectBySkillEffect : ICardEffect, ICanNotSelectBySkillEffect
{
    private readonly Func<CardSource, CardSource, bool> _predicate;
    private readonly Func<bool>? _condition;

    public CanNotSelectBySkillEffect(CardSource card, Func<CardSource, CardSource, bool> predicate, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(predicate);
        Card = card;
        _predicate = predicate;
        _condition = condition;
        // (R3-W3c-4c D-1) wire the base ICardEffect so the LIVE getter Permanent.CanSelectBySkill can consult
        // this effect over a permanent's EffectList(None) (its scan gates on cardEffect.CanUse(null), which
        // returns false unless a CanUseCondition is set). Mirrors the AS-IS SetUpICardEffect construction.
        SetUpICardEffect("Can't be selected by skill", h => _condition is null || _condition(), card);
        SetNotShowUI(true);
    }

    public CardSource Card { get; }

    /// <summary>(R3-W3c-4c D-1 joint↔separate reconciliation) AS-IS
    /// <c>ICanNotSelectBySkillEffect.CanNotSelectBySkill(permanent, cardEffect)</c>: the AS-IS kind-class ANDs a
    /// SEPARATE <c>PermanentCondition(permanent)</c> and <c>CardEffectCondition(cardEffect)</c>. The headless
    /// carrier instead holds the (possibly non-separable) JOINT predicate <c>f(candidate, skillSource)</c> as a
    /// single Func (memory: scope+causing must NOT be split). This adapter maps the AS-IS interface args onto the
    /// joint — the candidate = <c>permanent.TopCard</c>, the skill source = <c>cardEffect.EffectSourceCard</c> —
    /// and reproduces the AS-IS non-null guards verbatim (both the permanent's top and the causing effect's source
    /// must exist), so no card that AS-IS would treat as untargetable is missed and none is over-blocked.</summary>
    public bool CanNotSelectBySkill(Permanent permanent, ICardEffect cardEffect)
    {
        if (permanent?.TopCard is not null && cardEffect?.EffectSourceCard is not null)
        {
            return _predicate(permanent.TopCard, cardEffect.EffectSourceCard);
        }

        return false;
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        // (joint-migration) canonical joint key: f(candidate, selectingSkillSource); the sink falls back to the
        // candidate itself when there is no distinct selecting source (AS-IS Permanent.CanSelectBySkill self-cause).
        Func<CardSource, CardSource, bool> pred = _predicate;
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [JointRestrictionEffect.PredicateKey(RestrictionHelpers.CannotBeSelectedBySkillKey)] =
                (Func<CardSource, CardSource?, bool>)((subject, cp) => pred(subject, cp ?? subject)),
        };
        if (_condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = _condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}


/// <summary>(joint-migration) Canonical restriction effect: carries the AS-IS JOINT predicate
/// <c>f(subject, counterpart)</c> for a restriction <c>kind</c> as a single runtime Func, evaluated by scanning
/// EVERY field permanent's effects (<see cref="HeadlessDCGO.Engine.Headless.Runtime.RestrictionScan"/>) — 1:1 with
/// the AS-IS <c>foreach permanent → effect.CanX(subject, counterpart)</c> loop. Replaces the split
/// (subject-scope ∧ counterpart/causing predicate) form, so a non-separable predicate is preserved and a port can
/// copy the AS-IS predicate verbatim. The 2nd arg is polymorphic per kind (defender / attacker / blocker / causing
/// source); null when the check has no counterpart.</summary>
public sealed class JointRestrictionEffect : ICardEffect
{
    /// <summary>Binding-values key carrying the joint predicate for restriction <paramref name="kind"/>.</summary>
    public static string PredicateKey(string kind) => "joint.restrict:" + kind;

    private readonly string _kind;
    private readonly Func<CardSource, CardSource?, bool> _predicate;
    private readonly Func<bool>? _condition;

    public JointRestrictionEffect(CardSource card, string kind, Func<CardSource, CardSource?, bool> predicate, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(predicate);
        Card = card;
        _kind = kind;
        _predicate = predicate;
        _condition = condition;
    }

    public CardSource Card { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [PredicateKey(_kind)] = _predicate };
        if (_condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = _condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}


/// <summary>(d-remediation, true-scan) AS-IS <c>ICanNotBeRemovedEffect</c> / <c>Permanent.CanNotBeRemoved</c>:
/// SCANNED over every field permanent's effects; while any usable one's predicate <c>CanNotBeRemoved(candidate)</c>
/// holds, the candidate cannot leave the battle area except by deletion (EX6_044). AS-IS single-arg predicate.</summary>
public sealed class CanNotBeRemovedEffect : ICardEffect
{
    private readonly Func<CardSource, bool> _predicate;
    private readonly Func<bool>? _condition;

    public CanNotBeRemovedEffect(CardSource card, Func<CardSource, bool> predicate, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(predicate);
        Card = card;
        _predicate = predicate;
        _condition = condition;
    }

    public CardSource Card { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        // (joint-migration) canonical joint key — single-arg AS-IS predicate (no counterpart).
        Func<CardSource, bool> pred = _predicate;
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [JointRestrictionEffect.PredicateKey(RestrictionHelpers.CannotBeRemovedKey)] =
                (Func<CardSource, CardSource?, bool>)((subject, _) => pred(subject)),
        };
        if (_condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = _condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}


/// <summary>(d-remediation, true-scan) AS-IS <c>ICanNotMoveEffect</c> / <c>Permanent.CanMove</c>: SCANNED over every
/// field permanent's effects; while any usable one's predicate <c>CanNotMove(candidate, causing)</c> holds, the
/// candidate cannot move (the move gate passes a null causing effect, AS-IS <c>CanNotMove(TopCard, null)</c>).</summary>
public sealed class CanNotMoveEffect : ICardEffect, ICanNotMoveEffect
{
    private readonly Func<CardSource, CardSource?, bool> _predicate;
    private readonly Func<bool>? _condition;

    public CanNotMoveEffect(CardSource card, Func<CardSource, CardSource?, bool> predicate, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(predicate);
        Card = card;
        _predicate = predicate;
        _condition = condition;
        // (R3-W3c-4c D-1) wire the base ICardEffect so the LIVE getter Permanent.CanMove can consult this effect
        // over a permanent's EffectList(None) (its scan gates on cardEffect.CanUse(null)). AS-IS SetUpICardEffect.
        SetUpICardEffect("Can't move", h => _condition is null || _condition(), card);
        SetNotShowUI(true);
    }

    public CardSource Card { get; }

    /// <summary>(R3-W3c-4c D-1 joint↔separate reconciliation) AS-IS
    /// <c>ICanNotMoveEffect.CanNotMove(cardSource, cardEffect)</c>: the AS-IS <c>CanNotMoveClass</c> ANDs a
    /// SEPARATE <c>_cardCondition(cardSource)</c> and <c>_cardEffectCondition(cardEffect)</c>. The headless carrier
    /// holds the JOINT predicate <c>f(candidate, causingSource)</c> as a single Func; this adapter maps the AS-IS
    /// interface args onto it (the causing source = <c>cardEffect.EffectSourceCard</c>, or null — the move gate
    /// passes <c>CanNotMove(TopCard, null)</c>, AS-IS Permanent.CanMove).</summary>
    public bool CanNotMove(CardSource cardSource, ICardEffect cardEffect)
    {
        if (cardSource is not null)
        {
            return _predicate(cardSource, cardEffect?.EffectSourceCard);
        }

        return false;
    }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        // (joint-migration) canonical joint key — the move gate passes a null causing effect (AS-IS CanNotMove(top, null)).
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [JointRestrictionEffect.PredicateKey(RestrictionHelpers.CannotMoveKey)] = _predicate,
        };
        if (_condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = _condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}


/// <summary>(d-remediation, true-scan) AS-IS <c>ICannotIgnoreDigivolutionConditionEffect</c> /
/// <c>Player.CanIgnoreDigivolutionRequirement</c>: SCANNED over every field permanent's effects; while any usable
/// one's JOINT predicate <c>cannotIgnoreDigivolutionCondition(digivolvingCard, target)</c> holds, ignore-grants are
/// negated (BT8_059). Carries the AS-IS predicate as a single runtime Func.</summary>
public sealed class CannotIgnoreDigivolutionConditionEffect : ICardEffect
{
    private readonly Func<CardSource, CardSource, bool> _predicate;
    private readonly Func<bool>? _condition;

    public CannotIgnoreDigivolutionConditionEffect(CardSource card, Func<CardSource, CardSource, bool> predicate, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(predicate);
        Card = card;
        _predicate = predicate;
        _condition = condition;
    }

    public CardSource Card { get; }

    public EffectBinding ToBinding(string effectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectId);
        // (joint-migration) canonical joint key: f(digivolvingCard, target under-card). Requires a target (the
        // digivolve consults it with the under-card), so a null counterpart never blocks.
        Func<CardSource, CardSource, bool> pred = _predicate;
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [JointRestrictionEffect.PredicateKey(RestrictionHelpers.CannotIgnoreDigivolutionConditionKey)] =
                (Func<CardSource, CardSource?, bool>)((subject, cp) => cp is not null && pred(subject, cp)),
        };
        if (_condition is not null)
        {
            values[ContinuousSelfModifierEffect.ConditionKey] = _condition;
        }

        var context = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: new[] { Card.InstanceId }, values: values);
        return new EffectBinding(
            new EffectRequest(new HeadlessEntityId(effectId), Card.Controller, "Continuous", context),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }, effect: null, duration: null);
    }
}


/// <summary>(d-remediation) AS-IS <c>DontBattleSecurityDigimonClass</c> (<c>IDontBattleSecurityDigimonEffect</c>):
/// an INTRINSIC marker a card returns at <see cref="EffectTiming.None"/> (e.g. EX4_013 "Ignore Battle") — when the
/// card is revealed as a security Digimon, the attacker does NOT battle it. Not a registered continuous effect
/// (the security card is not on the battle area); the security resolver dispatches the revealed card's own effects
/// and consults this marker. Mirrors AS-IS <c>CanUse</c> (<paramref name="condition"/>) + <c>CardSourceCondition</c>
/// (evaluated against the revealed card).</summary>
public sealed class DontBattleSecurityDigimonEffect : ICardEffect
{
    private readonly Func<CardSource, bool> _cardSourceCondition;
    private readonly Func<bool>? _condition;

    public DontBattleSecurityDigimonEffect(CardSource card, Func<CardSource, bool> cardSourceCondition, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardSourceCondition);
        Card = card;
        _cardSourceCondition = cardSourceCondition;
        _condition = condition;
    }

    public CardSource Card { get; }

    /// <summary>True when the revealed security card should NOT battle the attacker.</summary>
    public bool SkipsBattle(CardSource revealedCard) =>
        (_condition is null || _condition()) && _cardSourceCondition(revealedCard);

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException("DontBattleSecurityDigimon is an intrinsic security-check marker, not a registered effect.");
}

