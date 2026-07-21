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
/// <c>ContinuousScopeEvaluation</c> as the modifier gate). Reused across the CanNot* self-static primitives.
///
/// (이연④-e, design item RD-④E-SELFRESTR) KEEP + MARK — real-card production census = 0 (the CanNot* self-static
/// factories were flipped to <see cref="JointRestrictionEffect"/> / kind-classes; NO factory constructs this). Its
/// surviving producers are load-bearing test scaffolding for a LIVE key path with NO invention-free retarget:
/// tests/FAILd-06 and the TfxOptionIgnoreColor fixture (tests/RD2-OptionColor) use it as the generic self-continuous
/// writer of <c>DigivolveAction.IgnoreColorRequirementKey</c> — the ignore-COLOUR flag read live by DigivolveAction
/// and OptionColorRequirement (the AS-IS UseRequirements / IgnoreColorConditionClass has no lowered kind-class yet —
/// stage-B continuous scan pending). Retire alongside that stage-B decision. (Cf. SelfKeywordByNameEffect /
/// RD-SELFKW-BYNAME.)</summary>
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
/// predicates (the original's <c>Func&lt;Permanent,bool&gt;</c>) beyond CardType/meta scoping are per-card.
///
/// (이연④-e, design item RD-④E-PSRESTR) KEEP + MARK — real-card production census = 0 (NO factory constructs this;
/// the player-scope CanNot* factories flipped to <see cref="JointRestrictionEffect"/> / kind-classes). Its surviving
/// producer is a load-bearing witness with NO invention-free retarget: tests/P0R-PrintedPlayerScopeImmunity drives
/// the PRINTED player-scope cannot-attack + the AS-IS immunity exemption term (<c>!subject.CanNotBeAffected(this)</c>,
/// Permanent.cs:2267/2290) that THIS effect embeds into its joint predicate (see ToBinding below). Plain
/// <see cref="JointRestrictionEffect"/> does not carry that immunity term, so retargeting P0R to it would lose the
/// exemption behaviour — no faithful flip exists until a printed-player-scope-restriction kind-class is ported.
/// Retire alongside that corpus decision. (Cf. SelfKeywordByNameEffect / RD-SELFKW-BYNAME.)</summary>
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


// (이연④-e) The old-model `DisplayDetailEffect` (inert tooltip-text mirror of AS-IS `AddDetailClass`) was DELETED
// here. It had ZERO producers: `CardEffectFactory.AddDetailClass` (CardEffectFactory.cs:1250) was flipped to return
// the ported AS-IS kind-class `CardEffects.AddDetailClass` (which real cards build), and no card, factory, or test
// constructs `DisplayDetailEffect`. The binding it produced carried only a `display.detail` string with no engine
// consumer beyond the AS-IS UI. With no producers left, this type is census-0 and removed (structural-invention
// campaign 이연④-e, 캠페인 4호).


// (이연④-e) The old-model `AddedDigivolutionRequirementEffect` and `AddedDigivolutionRequirementPredicateEffect`
// (registry-key added-digivolution-requirement carriers, writing DigivolveAction.AddedEvolution{Condition,Predicate,
// Cost,Level,…}Key onto a ContinuousRestrictionGate binding) were DELETED here. Both had ZERO producers (census-0):
// no factory or real card constructs them, and the only writers of the AddedEvolution* keys were these two classes.
// The LIVE added-digivolution path is the new-model `IAddDigivolutionRequirementEffect` interface scan (AS-IS
// `CardSource.EvoCosts`/`CostList`, via `CardSource.AddedDigivolutionCosts`), which `DigivolveAction` reaches
// through `NewModelAddedDigivolutionCosts` (the RD-P6B-15 union) — the AS-IS `AddDigivolutionRequirementClass`
// kind-class registers no binding, so this interface scan is the sole observable path. The now-dead legacy branch
// in `DigivolveAction.OwnAddedRequirementRequests` (the `is AddedDigivolutionRequirement*Effect` type-checks) was
// removed alongside. With no producers left, these types are census-0 and removed (structural-invention campaign
// 이연④-e, 캠페인 4호).


/// <summary>(PRIM-W2) A self-static keyword grant BY NAME — for keywords outside the Batch1/Batch2 enums
/// (Raid / Barrier / Collision / Fortitude / Evade) whose behaviour gates read a metadata flag. Registers a
/// keyword binding (keywords = [name], target self) so <see cref="ContinuousKeywordGate.HasKeyword"/> reports
/// it live; the same bar as the Batch2 self-statics. Condition / inherited carried on the binding values.</summary>
// RESIDUE (design item RD-SELFKW-BYNAME): old-model EffectBinding producer. Batch1/Batch2 twins deleted
// (이연④-d) as zero-producer dead code — their real-card factories were already flipped to new-model
// kind-classes in a prior wave. This by-name variant is KEPT: it has 0 real-card producers (the invented
// MindLinkSelfEffect factory is test-only; real MindLink card EX11_070 runs MindLinkClass, not this) but
// remains load-bearing test scaffolding — tests/G3.5-C910 (Collision/Execute consumer witnesses) and
// tests/G9-037 (MindLink round-trip) register keyword bindings through it, read by the LIVE registry-half of
// ContinuousKeywordGate.HasKeyword. Retire alongside a MindLink/keyword-consumer corpus decision (no faithful
// new-model re-point exists for the MindLink continuous keyword without inventing a kind-class).
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
/// (context overload) resolves it for any of the scoped player's cards.
///
/// (이연④-e, design item RD-④E-PSKEYWORD) KEEP + MARK — 2-hop real-card production census = 0 (the five factory
/// methods Piercing/Blitz/Retaliation/Decoy/Barrier StaticEffect have ZERO real-card callers; ST6_12 is a STOP that
/// WOULD need this primitive but is unported — no "select N and grant keyword" factory exists). Its surviving
/// producers are load-bearing witnesses for the LIVE gate <see cref="ContinuousKeywordGate.HasKeyword"/> player-scope
/// path (tests/G9-028 BlockerStatic, tests/G9-050 PlayerScopePredicate, tests/PRIM-P0.AddSkillLiveSet — the AS-IS
/// AddSkillClass live-set semantics). NO invention-free kind-class flip exists for a player-scope keyword grant
/// (this IS the AddSkillClass port), exactly the SelfKeywordByNameEffect / RD-SELFKW-BYNAME situation. Retire
/// alongside a MindLink/AddSkillClass keyword-corpus decision.</summary>
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
/// docs/porting/play_option_and_delayed_player_effect_design.md.
///
/// (이연④-e, design item RD-④E-TRIGGERGRANT) KEEP + MARK — 2-hop real-card production census = 0 (its sole factory
/// <c>CardEffectFactory.GrantTriggeredEffectToScopedSet</c> has ZERO real-card callers). Its surviving producer is a
/// load-bearing witness for the AS-IS AddSkillClass triggered-set-splice path (tests/PRIM-P0.TriggerGrantSetSplice —
/// BT8_031 style "your Digimon gain '[When Attacking] …'"), the live <c>AutoProcessingTriggerCollector</c> grant
/// seam. NO invention-free flip exists (this IS the AddSkillClass triggered-grant port; it is also the blocked
/// primitive RD-P6C3-C1 — a NEW-model nested effect throws NotSupportedException). Retire alongside the AddSkillClass
/// corpus decision. (Cf. SelfKeywordByNameEffect / RD-SELFKW-BYNAME.)</summary>
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
///
/// (이연④-e, design item RD-④E-PSMODIFIER) KEEP + MARK — real-card production census = 0 (the ChangeDP factory that
/// produced this was flipped to a kind-class; NO factory constructs it). Its surviving producers are load-bearing
/// witnesses for the LIVE <see cref="ContinuousModifierGate"/> player-scope DP path: tests/FAILa-02
/// (ImmuneFromDPMinus causing-effect predicate — needs a player-scope DP-minus SOURCE), tests/G9-050
/// (PlayerScopePredicate), tests/G3.5-N2 (ContinuousBattleDp). Retargeting each to the flipped ChangeDP kind-class
/// factory requires per-test AS-IS re-verification (a causing-source-bearing player-scope DP delta) — deferred to a
/// witness-retarget pass; kept meanwhile so the gate coverage is not lost. (Cf. SelfKeywordByNameEffect /
/// RD-SELFKW-BYNAME.)
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


// (이연④-f) ContinuousCanNotPlayOptionEffect (old-model CanNotPlayOption registry carrier) DELETED. Its ToBinding
// wrote the CanNotPlayOptionScan JointPredicateKey binding consumed by the scan's registry-half (regions ①②
// GetContinuousEffects + region ③ BuildContinuousRequests). Every producer now emits the AS-IS kind-class
// `CanNotPlayClass` (ICanNotPlayCardEffect), read by the scan's interface-scan half: EX1_072's player-bucket grant
// via AddEffectToPlayer (region ①), BT8_057 as a field static (region ②), the TfxOptionForbidsSelf fixture as the
// option's own [None] effect (region ③). The scan's registry-half reads are now DEAD (no producer) and are retained
// only pending the atomic registry-scan deletion.


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


// (이연④-c) The old-model `ChangeCardNamesEffect` (ICardEffect-only, invisible to the live `CardSource.CardNames`
// IChangeCardNamesEffect scan) was DELETED here. Its `ToBinding` wrote `CardSource.AddedCardNameKey`, but R1-e
// dropped that registry read from `CardNames` (AS-IS has no such branch — an added name is an IChangeCardNamesEffect
// enumerated by the scan), leaving the write DEAD and the factory output production-INERT. `ChangeCardNamesStaticEffect`
// now emits the AS-IS kind-class `CardEffects.ChangeCardNamesClass` (IChangeCardNamesEffect), which the live scan sees;
// all real cards (BT14_097 …) already build that kind-class directly. With no producers left, this type is census-0
// and removed (structural-invention campaign 이연④-c).


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

    // (이연④-e, substrate reclassification) The old-model registry-WRITE (`EffectBinding ToBinding(string)`) was
    // DELETED. It wrote the `JointRestrictionEffect.PredicateKey(CannotBeSelectedBySkillKey)` binding, but that key
    // has ZERO readers: `CannotBeSelectedBySkillKey` is never passed to `RestrictionScan.IsRestricted` — the
    // select-by-skill restriction was rehoused (R1-d) onto the live AS-IS interface scan
    // `Permanent.CanSelectBySkill` / `SelectPermanentEffect`, which enumerates the `ICanNotSelectBySkillEffect`
    // this class implements (over a permanent's `EffectList(None)`; see `SetUpICardEffect` in the ctor). With no
    // registry reader, the registry-half was 사문 (dead-letter), so removing it registers NOTHING — the class is a
    // pure kind-class-style substrate effect, live only through the interface (④-a ToBinding-removal precedent).
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


// (이연④-c) The old-model `CanNotBeRemovedEffect` (ICardEffect-only, invisible to the live AS-IS
// `Permanent.CanBeRemoved()` ICanNotBeRemovedEffect scan) was DELETED here. Its `ToBinding` wrote the
// `CannotBeRemovedKey` joint-restriction registry binding read by `MatchStateMutationSink.IsRemovalBlockedByScan`,
// but it had ZERO production producers (census-0): the real card EX6_044 and the `CanNotBeRemovedStaticEffect`
// factory both build the new-model kind-class `CardEffects.CanNotBeRemovedClass` (an ICanNotBeRemovedEffect, no
// ToBinding). That kind-class was invisible to the registry-only chokepoint, so EX6_044's removal-protection was
// production-INERT; `IsRemovalBlockedByScan` now UNIONs the live `Permanent.CanBeRemoved()` interface scan (which
// enumerates the kind-class), resurrecting it. With no producers left, this type is census-0 and removed
// (structural-invention campaign 이연④-c).


/// <summary>(d-remediation, true-scan) AS-IS <c>ICanNotMoveEffect</c> / <c>Permanent.CanMove</c>: SCANNED over every
/// field permanent's effects; while any usable one's predicate <c>CanNotMove(candidate, causing)</c> holds, the
/// candidate cannot move (the move gate passes a null causing effect, AS-IS <c>CanNotMove(TopCard, null)</c>).
///
/// (이연④-f, registry-half retired) LIVE-only now. Formerly DUAL: the <c>ToBinding</c> registry-half fed the
/// breeding→battle move gate (BT1_089) via <c>RestrictionScan.IsRestricted(CannotMoveKey)</c>. That consumer was
/// re-pointed to the AS-IS interface-scan half (<c>Permanent.CanMove</c> — the same <c>permanent.CanMove</c> the
/// AS-IS BT1_089:56 uses), so nothing reads the CannotMoveKey binding any more. The <c>ToBinding</c> is dropped:
/// with no <c>ToBinding</c> method the <see cref="LegacyBindingBridge"/> reflective lowering returns false and the
/// registrar leaves this effect out of the registry entirely — the interface half (this effect sits on the card's
/// live EffectList and implements <see cref="ICanNotMoveEffect"/>) is the sole path, feeding <c>Permanent.CanMove</c>
/// exactly as AS-IS <c>CanNotMoveClass</c> does. Its joint predicate <c>f(candidate, causingSource)</c> is retained
/// (the joint carrier the fixture/factory build via a single Func; not splittable into the kind-class's separate
/// cardCondition/cardEffectCondition in general — true-scan-for-joint-predicates).</summary>
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

    // (이연④-f) ToBinding retired — see the class summary. No registry-half: the reflective LegacyBindingBridge
    // lowering finds no ToBinding and the registrar skips registry registration; the live EffectList /
    // ICanNotMoveEffect path (Permanent.CanMove) is the sole consumer.
}


// (이연④-e) The old-model `CannotIgnoreDigivolutionConditionEffect` (ICardEffect-only, invisible to the live AS-IS
// `ICannotIgnoreDigivolutionConditionEffect` / `Player.CanIgnoreDigivolutionRequirement` interface scan — it
// implemented only ICardEffect) was DELETED here. Its `ToBinding` wrote the `CannotIgnoreDigivolutionConditionKey`
// joint-restriction registry binding, but that key has ZERO readers: `IsDigivolveIgnoreBlocked` (DigivolveAction.cs:
// 725) was rehoused in R3-W3c-4b D from the retired registry RestrictionScan to the AS-IS-literal live getter
// `Player.CanIgnoreDigivolutionRequirement` (Player.cs:552-573, the `ICannotIgnoreDigivolutionConditionEffect` veto
// scan). It also had ZERO producers (census-0): the real card BT8_059 and the `CannotIgnoreDigivolutionCondition...`
// factory (CardEffectFactory.cs:621) both build the new-model kind-class `CardEffects.CannotIgnoreDigivolutionConditionClass`
// (an `ICannotIgnoreDigivolutionConditionEffect`, no `ToBinding`), which the live scan sees. With no producers left,
// this type is census-0 and removed (structural-invention campaign 이연④-e, 캠페인 4호).


// (이연④-c) The old-model `DontBattleSecurityDigimonEffect` (ICardEffect-only, with a `SkipsBattle` method the
// security resolver never called — it scans the AS-IS-literal `IDontBattleSecurityDigimonEffect.DontBattleSecurityDigimon`
// interface) was DELETED here. It was ALREADY census-0: the `DontBattleSecurityDigimonStaticEffect` factory (flipped
// in R3-W3c-4b B1) and the real card EX5_053 both build the new-model kind-class
// `CardEffects.DontBattleSecurityDigimonClass` (an IDontBattleSecurityDigimonEffect), which the resolver's live scan
// (SecurityResolver.cs:556/571) sees. With no producers left, this type is census-0 and removed (structural-invention
// campaign 이연④-c).

