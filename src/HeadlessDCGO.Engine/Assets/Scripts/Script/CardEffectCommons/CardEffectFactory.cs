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
/// Headless mirror of the original <c>CardEffectFactory</c>. Method names match the original so ported
/// card bodies read 1:1. Each returns an <see cref="ICardEffect"/> the registrar lowers to a binding.
/// </summary>
public static partial class CardEffectFactory
{
    /// <summary>(B-5 uniform migration) Wrap a composable <see cref="IEffectBody"/> as a uniform
    /// <see cref="ActivatedEffect"/> so the activated-effect resolver applies the SHARED once-per-turn cap
    /// (<paramref name="maxCountPerTurn"/>) + optional yes/no gate (<paramref name="isOptional"/>) that the
    /// per-shape resolver cases could not express. For a plain activated skill (Option / [Main] / Security)
    /// <paramref name="timing"/> is <see cref="EffectTiming.None"/> and <paramref name="canUse"/> is null —
    /// the timing block the card registers the effect under carries the AS-IS timing; a broadcast trigger
    /// passes its own timing/gate. 1:1 mirror of the AS-IS <c>ActivateClass</c>
    /// (SetUpActivateClass(canActivate, coroutine, maxCountPerTurn, isOptional, description)).</summary>
    internal static ActivatedEffect AsUniformActivated(
        CardSource card,
        IEffectBody body,
        string description,
        bool isOptional = false,
        int? maxCountPerTurn = null,
        EffectTiming timing = EffectTiming.None,
        Func<CardEffectResolveContext, bool>? canUse = null,
        Func<bool>? canActivate = null) =>
        new ActivatedEffect(card, timing, canUse, canActivate, body, maxCountPerTurn, isOptional, description);

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

    /// <summary>Original: <c>ChangeSelfDPStaticEffect&lt;Func&lt;int&gt;&gt;</c> — continuous ±DP on self with a
    /// dynamic (read-time) value (e.g. BT1_073 "+1000 per suspended opponent Digimon"). Mirror of the
    /// existing SAttack dynamic overload; the underlying <see cref="ContinuousSelfModifierEffect"/> already
    /// supports the dynamic-value plumbing, only the DP-key factory overload was missing.</summary>
    public static ICardEffect ChangeSelfDPStaticEffect(Func<int> changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)
    {
        ArgumentNullException.ThrowIfNull(changeValue);
        return new ContinuousSelfModifierEffect(card, ModifierHelpers.DpDeltaKey, changeValue: 0, isInheritedEffect, condition, dynamicValue: changeValue);
    }

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

    /// <summary>(G5 / BT3_031 / BT3_111) Full AS-IS <c>ChangeDigivolutionCostStaticEffect(changeValue,
    /// permanentCondition, cardCondition, rootCondition, condition, setFixedCost)</c>: a continuous cost gate on
    /// digivolving <paramref name="cardCondition"/>-matching cards (from a <paramref name="rootCondition"/>-matching
    /// root) onto a <paramref name="permanentCondition"/>-matching target FROM permanent. Unlike the scalar
    /// self-modifier overload above, this gates on the digivolving-FROM permanent's identity AND is read
    /// DISPATCH-FIRST off the moving card (so it applies while that card is in HAND — the AS-IS
    /// <c>condition = card in owner's hand</c> — which the continuous registrar never scans). See
    /// <see cref="DigivolutionCostGateEffect"/>. <paramref name="setFixedCost"/> = SET the cost rather than ±.</summary>
    public static ICardEffect ChangeDigivolutionCostStaticEffect(
        int changeValue, Func<Permanent, bool> permanentCondition, Func<CardSource, bool> cardCondition,
        Func<ChoiceZone, bool>? rootCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition,
        bool setFixedCost) =>
        new DigivolutionCostGateEffect(card, changeValue, permanentCondition, cardCondition, rootCondition, condition, setFixedCost);

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
        // (A2) level/minLevel/maxLevel = the AS-IS hard level gate on the digivolving-FROM permanent
        // (GetEvoCost) — previously accepted and dropped, which silently widened ~111 cards' alt-digivolve.
        new AddedDigivolutionRequirementPredicateEffect(card, permanentCondition, digivolutionCost, ignoreDigivolutionRequirement, isInheritedEffect: false, condition, targetCardCondition: cardCondition, costEquation: costEquation, level: level, minLevel: minLevel, maxLevel: maxLevel);

    /// <summary>(PRIM-W5) <c>DrawCardsEffect</c> — the declarative form of the AS-IS
    /// <c>new DrawClass(owner, count, ...).Draw()</c> coroutine: the owner draws <paramref name="count"/>
    /// cards. Use this in place of the original draw coroutine.</summary>
    public static IActivatedCardEffect DrawCardsEffect(CardSource card, int count) =>
        new DrawEffect(card, count, $"Draw {count} card(s).");

    /// <summary>(G4) <c>DrawThenDiscardEffect</c> — atomic "draw <paramref name="drawAmount"/>, then discard
    /// <paramref name="trashAmount"/> from your hand" (AS-IS draw-then-discard coroutine). Wraps
    /// <see cref="CardEffectCommons.DrawAndDiscardCards"/> (draws+flushes before the discard candidate pool is
    /// built, so drawn cards are discardable). <paramref name="discardOptional"/> = "you may discard";
    /// <paramref name="discardUpTo"/> = "discard up to N" (min 1) vs exactly N. Resolved via the activation flow.</summary>
    public static IActivatedCardEffect DrawThenDiscardEffect(
        CardSource card, int drawAmount, int trashAmount, string description,
        Func<CardSource, bool>? canTrash = null, bool discardOptional = false, bool discardUpTo = false) =>
        new ActivatedDrawThenDiscardEffect(card, drawAmount, trashAmount, canTrash, discardOptional, discardUpTo, description);

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
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Retaliation, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition));

    /// <summary>(PRIM-W2) Original: <c>RaidSelfEffect</c> — grants Raid (attack-switch) to self.
    /// <paramref name="rootCardEffect"/>/<paramref name="isLinkedEffect"/> accepted for source fidelity.</summary>
    public static ICardEffect RaidSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, ICardEffect? rootCardEffect = null, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Raid, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition));

    /// <summary>(PRIM-W2) Original: <c>BarrierSelfEffect</c> — grants Barrier (deletion-replacement) to self.</summary>
    public static ICardEffect BarrierSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Barrier, isInheritedEffect, condition);

    /// <summary>(PRIM-W2) Original: <c>CollisionSelfStaticEffect</c> — grants Collision (forced-block) to self.</summary>
    public static ICardEffect CollisionSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Collision, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition));

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

    /// <summary>(PRIM-W3) <c>DecodeSelfEffect</c> — grants Decode to self (Batch2).
    /// (C-1 witness) <paramref name="sourceCondition"/> = the AS-IS per-card predicate a leaving card's
    /// digivolution source must pass to be a free-play candidate (BT19_024: Blue Lv.4) — stored on the grant
    /// and consumed by the PRE candidate filter (previously accepted only the bare keyword, letting any Digimon
    /// source be decoded). <paramref name="decodeStrings"/> is the AS-IS display text (colour/level label),
    /// carried for source fidelity; the real filter is the predicate.</summary>
    public static ICardEffect DecodeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition,
        string[]? decodeStrings = null, Func<CardSource, bool>? sourceCondition = null) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Decode, isInheritedEffect, condition,
            sourceCondition is null
                ? null
                : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.Decode.DecodeSourceConditionKey] = sourceCondition,
                });

    /// <summary>(PRIM-W3) <c>ProgressSelfStaticEffect</c> — grants Progress to self (Batch2).</summary>
    public static ICardEffect ProgressSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Progress, isInheritedEffect, condition);

    /// <summary>(PRIM-W3/A4) <c>PartitionSelfEffect</c> — grants Partition to self (Batch2).
    /// <paramref name="cardSourceConditions"/> = the AS-IS two-entry <c>PartitionCondition</c> list defining
    /// colour group 1 ([0]) and group 2 ([1]) — stored on the grant and consumed by the per-group candidate
    /// filter (previously accepted and dropped, which let any two sources be played).</summary>
    public static ICardEffect PartitionSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, IReadOnlyList<PartitionCondition>? cardSourceConditions = null) =>
        new SelfKeywordBatch2Effect(card, KeywordBaseBatch2Kind.Partition, isInheritedEffect, condition,
            cardSourceConditions is null
                ? null
                : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [PartitionCondition.PartitionConditionsKey] = cardSourceConditions,
                });

    /// <summary>(PRIM-W3) <c>IcecladSelfStaticEffect</c> — grants Iceclad to self.</summary>
    public static ICardEffect IcecladSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Iceclad, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>DecoySelfEffect</c> — grants Decoy (deletion-replacement) to self.
    /// (D1) <paramref name="permanentCondition"/> narrows the PROTECTED target (AS-IS Decoy.cs
    /// <c>CanSelectPermanentCondition</c>: evaluated live against the other permanent being spared, e.g.
    /// "Decoy ([Bagra Army])") — stored on the grant and read by <c>DeletionReplacementGate</c>.</summary>
    public static ICardEffect DecoySelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, Func<Permanent, bool>? permanentCondition = null, string? effectName = null, string? effectDescription = null) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Decoy, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W3/C1) <c>FragmentSelfEffect</c> — grants Fragment (deletion-replacement) to self.
    /// <paramref name="trashValue"/> is the AS-IS Fragment &lt;X&gt; count (sources trashed to survive) —
    /// stored on the grant and read by <c>DeletionReplacementGate.FragmentCostOf</c> (previously dropped,
    /// collapsing every Fragment to X=1).</summary>
    public static ICardEffect FragmentSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, int trashValue = 0, string? effectName = null, string? effectDescription = null) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Fragment, isInheritedEffect, condition,
            extraValues: trashValue > 0
                ? new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.FragmentTrashValueKey] = trashValue }
                : null);

    /// <summary>(PRIM-W3) <c>ExecuteSelfEffect</c> — grants Execute to self.</summary>
    public static ICardEffect ExecuteSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Execute, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>ScapegoatSelfEffect</c> — grants Scapegoat (deletion-replacement) to self.</summary>
    public static ICardEffect ScapegoatSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null, string? effectDescription = null, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Scapegoat, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition));

    /// <summary>(C9) AS-IS linked-effect gate (Permanent.cs:1532 / ICardEffect.cs:403): an effect flagged
    /// <c>isLinkedEffect</c> is ACTIVE only while its source card is a LINK card of a battle-area permanent
    /// (<c>cardSource.IsLinked</c>), evaluated LIVE on every read — breaking the link stops the effect (the
    /// original has no removal event, only the two live guards). Wrapping the stored condition gives that
    /// gate to every consumer with no per-gate changes.</summary>
    internal static Func<bool>? LinkedGate(CardSource card, bool isLinkedEffect, Func<bool>? condition) =>
        !isLinkedEffect
            ? condition
            : () => card.IsLinked && (condition?.Invoke() ?? true);

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
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Reboot, scopeCardType: null, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition), ScopePred(permanentCondition));

    /// <summary>(PRIM-W3) <c>CanNotAttackStaticEffect(...)</c> — the scoped player's Digimon cannot attack
    /// (player-scope CannotAttack restriction consulted by AttackPermanentAction). Per-permanent predicate is
    /// per-card.</summary>
    public static ICardEffect CanNotAttackStaticEffect(HeadlessPlayerId scopePlayerId, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        new ContinuousPlayerScopeRestrictionEffect(card, scopePlayerId, RestrictionHelpers.CannotAttackKey, scopeCardType: null, isInheritedEffect, condition);

    /// <summary>(joint-migration) Canonical CannotAttack — <paramref name="predicate"/> is the AS-IS joint
    /// <c>CanNotAttack(attacker, defender)</c> (defender may be null when the gate has no specific defender).</summary>
    public static ICardEffect CanNotAttackJointStaticEffect(Func<CardSource, CardSource?, bool> predicate, CardSource card, Func<bool>? condition = null) =>
        new JointRestrictionEffect(card, RestrictionHelpers.CannotAttackKey, predicate, condition);

    /// <summary>(PRIM-W3) <c>Gain1MemoryTamerOpponentDigimonEffect(card)</c> — "[Start of Your Main Phase] if
    /// your opponent has a Digimon, gain 1 memory." AS-IS description is <c>[Start of Your Main Phase]</c>, so it
    /// registers at <see cref="EffectTiming.OnStartMainPhase"/> (emitted by MetadataActionProcessor at main-phase
    /// entry) — NOT OnStartTurn, which fires earlier during unsuspend/draw.</summary>
    public static ICardEffect Gain1MemoryTamerOpponentDigimonEffect(CardSource card) =>
        new TriggeredGainMemoryEffect(card, EffectTiming.OnStartMainPhase, amount: 1,
            "[Start of Your Main Phase] If your opponent has a Digimon, gain 1 memory.",
            extraCondition: () => CardEffectCommons.MatchConditionPermanentCount(card, id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)) > 0);

    /// <summary>(PRIM-W2 #9) AS-IS <c>Gain2MemoryOptionDelayEffect(card)</c> — a [Main] &lt;Delay&gt; option: TRASH
    /// this card's own battle-area permanent to activate, and ONLY IF trashed, gain 2 memory. 1:1 via
    /// <see cref="TrashSelfThenGainMemoryDelayEffect"/> (was wrongly an unconditional start-of-turn gain).</summary>
    public static ICardEffect Gain2MemoryOptionDelayEffect(CardSource card) =>
        new TrashSelfThenGainMemoryDelayEffect(card, amount: 2);

    /// <summary>(PRIM-W3) <c>CanNotBeBlockedStaticSelfEffect</c> — this Digimon cannot be blocked (unblockable);
    /// consulted by BlockTiming when enumerating blocker candidates.</summary>
    public static ICardEffect CanNotBeBlockedStaticSelfEffect(Func<Permanent, bool>? defenderCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        new ContinuousSelfRestrictionEffect(
            card, RestrictionHelpers.CannotBeBlockedKey, isInheritedEffect, condition,
            counterpartPredicate: defenderCondition is null
                ? null
                : cs => defenderCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner)));

    /// <summary>(PRIM-W3) <c>CantUnsuspendStaticEffect</c> — this Digimon does not unsuspend; consulted by the
    /// Unsuspend step.</summary>
    public static ICardEffect CantUnsuspendStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotUnsuspendKey, isInheritedEffect, condition);

    /// <summary>(PRIM-W3) <c>CanNotBeDestroyedBySkillStaticEffect</c> — this Digimon cannot be deleted by
    /// effects/skills (battle deletion still applies); consulted by the effect-sourced delete path. Mirrors
    /// AS-IS <c>CanNotBeDestroyedBySkillStaticEffect(permanentCondition, cardEffectCondition, …)</c>: immunity
    /// applies only when BOTH the per-permanent predicate AND the CAUSING-effect predicate pass — the AS-IS
    /// <c>Func&lt;ICardEffect,bool&gt;</c> maps to <paramref name="cardEffectCondition"/> over the causing effect's
    /// SOURCE card (e.g. "can't be deleted by your OPPONENT's effects" = IsOpponentEffect). <paramref
    /// name="permanentCondition"/> null → self restriction; non-null → player-scope over matching permanents.</summary>
    public static ICardEffect CanNotBeDestroyedBySkillStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotBeDeletedBySkillKey, isInheritedEffect, condition, cardEffectCondition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, RestrictionHelpers.CannotBeDeletedBySkillKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition), cardEffectCondition);

    /// <summary>(PRIM-W3) <c>ChangeSAttackStaticEffect</c> — continuous ±security attack on the owner's Digimon
    /// (player-scope SA modifier consulted by ContinuousModifierGate.ResolveSecurityAttack). Mirrors the SA
    /// analogue of <see cref="ChangeDPStaticEffect"/>; <paramref name="permanentCondition"/> per-card.
    /// (G6) <paramref name="scopeAnyPlayer"/>:true drops the owner-only scope so the predicate decides — needed
    /// for an OPPONENT-scoped SA modifier ("your opponent's Digimon get -N SA"), a 1:1 mirror of
    /// <see cref="ChangeSecurityDigimonCardDPStaticEffect"/>'s any-player scope. Default false = owner-scope.</summary>
    public static ICardEffect ChangeSAttackStaticEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool scopeAnyPlayer = false) =>
        new PlayerScopeModifierEffect(card, ModifierHelpers.SAttackDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopePredicate: ScopePred(permanentCondition), scopeAnyPlayer: scopeAnyPlayer);

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

    /// <summary>(PRIM-W4/FR2) <c>CanNotBeDestroyedStaticEffect</c> — registers a continuous Delete/Prevent
    /// replacement (battle + effect deletion), honoured by BattleDeletionGate and the effect-delete path.
    /// null <paramref name="permanentCondition"/> = the self form ("THIS Digimon cannot be deleted");
    /// non-null = the SET form ("your &lt;X&gt; Digimon cannot be deleted") — a player-scope prevent with the
    /// predicate evaluated 1:1 per permanent. (An earlier revision of this doc said SET was unbuilt — stale.)</summary>
    public static ICardEffect CanNotBeDestroyedStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, ReplacementHelpers.PreventDeletionKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, ReplacementHelpers.PreventDeletionKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>ImmuneFromDPMinusStaticEffect</c> — this Digimon is immune to DP-reducing effects
    /// (D-A3). Registers a continuous DpReduction/Immune replacement honoured by ContinuousDpGate. Mirrors AS-IS
    /// <c>ImmuneFromDPMinus(permanent, cardEffect)</c>: immunity applies only when BOTH the per-permanent
    /// predicate AND the CAUSING-effect predicate pass. <paramref name="cardEffectCondition"/> is the AS-IS
    /// <c>Func&lt;ICardEffect,bool&gt;</c> mapped to the DP-reducing effect's SOURCE card (e.g. "immune to your
    /// OPPONENT's DP-minus" = IsOpponentEffect); null → immune to ALL DP reductions. The predicate is evaluated
    /// per DP-reducing modifier by ContinuousDpGate against that modifier's <c>SourceEntityId</c>.</summary>
    public static ICardEffect ImmuneFromDPMinusStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, ReplacementHelpers.ImmuneFromDpMinusKey, isInheritedEffect, condition, cardEffectCondition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, ReplacementHelpers.ImmuneFromDpMinusKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition), cardEffectCondition);

    /// <summary>(PRIM-P0 B.O.6 / #5) <c>CannotReduceCostClass</c> — the play/digivolution cost of this card (or,
    /// with <paramref name="permanentCondition"/>, the owner's matching cards) cannot be reduced. Registers a
    /// continuous CostReduction/Immune restriction honoured by ContinuousModifierGate.Resolve{Play,Digivolution}Cost.
    /// <paramref name="costKind"/> mirrors the AS-IS <c>targetPermanentsCondition</c>: <see cref="CostReductionScope.Digivolve"/>
    /// (AS-IS count&gt;=1, e.g. BT5_021 "opponent can't reduce DIGIVOLUTION costs") protects ONLY the digivolution
    /// cost — NOT the play cost; <see cref="CostReductionScope.Play"/> the reverse; <see cref="CostReductionScope.Both"/>
    /// (default, trivial predicate) protects either.</summary>
    public static ICardEffect CanNotReduceCostStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, CostReductionScope costKind = CostReductionScope.Both, bool scopeAnyPlayer = false)
    {
        string key = costKind switch
        {
            CostReductionScope.Play => ReplacementHelpers.ImmuneFromPlayCostReductionKey,
            CostReductionScope.Digivolve => ReplacementHelpers.ImmuneFromDigivolutionCostReductionKey,
            _ => ReplacementHelpers.ImmuneFromCostReductionKey,
        };
        // (#5) permanentCondition folds the AS-IS cardCondition (which card) AND playerCondition (the payer, via
        // the permanent's OwnerId). scopeAnyPlayer:true lets it reach the OPPONENT's cards — e.g. BT5_021
        // "your OPPONENT can't reduce DIGIVOLUTION costs" = permanentCondition p.OwnerId != card.Owner && IsDigimon,
        // costKind Digivolve, scopeAnyPlayer true.
        return permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, key, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, key, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition), scopeAnyPlayer: scopeAnyPlayer);
    }

    /// <summary>(PRIM-P0 B.O.6) <c>CannotAddSecurityClass</c> — <paramref name="scopePlayer"/> cannot add cards to
    /// their security (AS-IS Player.CanAddSecurity). <paramref name="causingEffectPredicate"/> mirrors the AS-IS
    /// <c>CardEffectCondition</c> — the restriction fires only when the CAUSING effect matches (e.g.
    /// IsOpponentEffect); null = block every add. Consulted at the AddToSecurity / Recover mutation chokes.</summary>
    public static ICardEffect CanNotAddSecurityStaticEffect(HeadlessPlayerId scopePlayer, bool isInheritedEffect, CardSource card, Func<bool>? condition, Func<CardSource, bool>? causingEffectPredicate = null) =>
        new ContinuousPlayerScopeRestrictionEffect(card, scopePlayer, RestrictionHelpers.CannotAddSecurityKey, scopeCardType: null, isInheritedEffect, condition, causingEffectPredicate: causingEffectPredicate);

    /// <summary>(PRIM-P0 B.O.6) <c>CannotAddMemoryClass</c> — <paramref name="scopePlayer"/> cannot gain memory
    /// (AS-IS Player.CanAddMemory). <paramref name="causingEffectPredicate"/> mirrors the AS-IS
    /// <c>CardEffectCondition</c> (null = block every gain). Consulted at the AddMemory mutation choke.</summary>
    public static ICardEffect CanNotAddMemoryStaticEffect(HeadlessPlayerId scopePlayer, bool isInheritedEffect, CardSource card, Func<bool>? condition, Func<CardSource, bool>? causingEffectPredicate = null) =>
        new ContinuousPlayerScopeRestrictionEffect(card, scopePlayer, RestrictionHelpers.CannotAddMemoryKey, scopeCardType: null, isInheritedEffect, condition, causingEffectPredicate: causingEffectPredicate);

    /// <summary>(PRIM-W4) <c>AllianceStaticEffect</c> — grants Alliance to the owner's Digimon (player-scope
    /// keyword). <paramref name="permanentCondition"/> per-card.</summary>
    public static ICardEffect AllianceStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Alliance, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    // (PRIM-P0 AddSkillClass) player-scope keyword grants — "your Digimon (matching permanentCondition) gain
    // <keyword>". These are the headless port target for AS-IS AddSkillClass whose getEffects grants a
    // <keyword>SelfEffect to a live-matched set: the player-scope binding re-evaluates the set per query, so a
    // Digimon that enters AFTER the grant still gains the keyword (proven in PRIM-P0.AddSkillLiveSet.Tests).
    /// <summary>(PRIM-P0 AddSkill) grants Piercing to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect PiercingStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Piercing, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Blitz to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect BlitzStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Blitz, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Retaliation to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect RetaliationStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Retaliation, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Scapegoat (deletion-replacement) to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect ScapegoatStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Scapegoat, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Decoy (deletion-replacement) to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect DecoyStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Decoy, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) grants Barrier to the owner's matching Digimon (player-scope).</summary>
    public static ICardEffect BarrierStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Barrier, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-P0 AddSkill) AS-IS AddSkillClass whose getEffects splices a TRIGGERED activated effect onto
    /// a live-matched set: <paramref name="nestedTriggeredEffect"/> (built to read the triggering card via
    /// TriggerEntityId and apply the per-card predicate + a nested activated resolution) fires for any event whose
    /// actor is <paramref name="scopePlayer"/> (the live set). The nested effect's ToBinding sets the granted
    /// timing.</summary>
    public static ICardEffect GrantTriggeredEffectToScopedSet(CardSource card, HeadlessPlayerId scopePlayer, ICardEffect nestedTriggeredEffect, bool scopeAnyPlayer = false) =>
        new PlayerScopeTriggerGrantEffect(card, scopePlayer, nestedTriggeredEffect, scopeAnyPlayer);

    /// <summary>(PRIM-W4) <c>JammingStaticEffect</c> — grants Jamming to the owner's Digimon (player-scope
    /// keyword). <paramref name="permanentCondition"/>/<paramref name="isLinkedEffect"/> per-card.</summary>
    public static ICardEffect JammingStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Jamming, scopeCardType: null, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition), ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>AscensionSelfEffect</c> — grants the Ascension keyword (post-deletion → security).
    /// Grant live via HasKeyword; DeletionReplacementGate's hasAscension consumer migrates separately.</summary>
    public static ICardEffect AscensionSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new SelfKeywordByNameEffect(card, ContinuousKeywordGate.Ascension, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition));

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

    /// <summary>(b-remediation) <c>ImmuneFromDeDigivolveStaticEffect</c> — AS-IS <c>ImmuneFromDeDigivolveClass</c>
    /// (<c>IImmuneFromDeDigivolveEffect</c>, consumed by <c>Permanent.ImmuneFromDeDigivolve()</c>): a continuous
    /// "cannot be de-digivolved" restriction on self (or, with <paramref name="permanentCondition"/>, the owner's
    /// matching Digimon). Previously the consumer (DeDigivolveKind) only read a metadata flag that NOTHING wrote,
    /// so the immunity could not be granted. Registers the restriction under the shared
    /// <see cref="HeadlessDCGO.Engine.Headless.Runtime.DeDigivolveHelpers.CannotBeDeDigivolvedKey"/> so the sink's
    /// de-digivolve handler skips an immune target.</summary>
    public static ICardEffect ImmuneFromDeDigivolveStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, HeadlessDCGO.Engine.Headless.Runtime.DeDigivolveHelpers.CannotBeDeDigivolvedKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, HeadlessDCGO.Engine.Headless.Runtime.DeDigivolveHelpers.CannotBeDeDigivolvedKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(d-remediation) <c>CanNotSelectBySkillStaticEffect</c> — AS-IS <c>ICanNotSelectBySkillEffect</c> /
    /// <c>Permanent.CanSelectBySkill</c>: the protected permanent(s) cannot be CHOSEN as a target by a skill
    /// effect (untargetability). <paramref name="permanentCondition"/> = AS-IS <c>CanNotSelectBySkill(candidate, …)</c>
    /// = which candidates are protected (self when null). <paramref name="causingEffectPredicate"/> = the skill-side
    /// gate ("cannot be chosen by your opponent's effects" ⇒ source-owner != owner). Registered candidate-scoped
    /// (scopeAnyPlayer so it can protect ANY player's matching cards); consumed by
    /// <see cref="HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect"/>.BuildRequest.</summary>
    /// <summary>(true-scan) AS-IS <c>SetUpCanNotSelectBySkillClass(CanNotSelectBySkill)</c>: <paramref name="predicate"/>
    /// is the joint AS-IS <c>CanNotSelectBySkill(candidate, skillSource)</c> — evaluated at select time by scanning
    /// EVERY field effect (SelectPermanentEffect), not baked into a scope.</summary>
    public static ICardEffect CanNotSelectBySkillStaticEffect(Func<CardSource, CardSource, bool> predicate, CardSource card, Func<bool>? condition = null) =>
        new CanNotSelectBySkillEffect(card, predicate, condition);

    /// <summary>(d-remediation) <c>CanNotBeRemovedStaticEffect</c> — AS-IS <c>ICanNotBeRemovedEffect</c> /
    /// <c>Permanent.CanNotBeRemoved</c>: the protected permanent(s) "can't leave the battle area except by a
    /// deletion effect" (EX6_044/BT16_051). Registers a continuous restriction consumed at the sink's THREE return
    /// chokepoints (bounce + deck-bounce top/bottom); deletion is NOT blocked. <paramref name="permanentCondition"/>
    /// = AS-IS PermanentCondition (which permanents are protected; self when null), <paramref name="causingEffectPredicate"/>
    /// = the effect-source gate ("except by YOUR OPPONENT's effects").</summary>
    /// <summary>(true-scan) AS-IS <c>SetUpCanNotBeRemovedClass(PermanentCondition)</c>: <paramref name="predicate"/>
    /// is the AS-IS <c>CanNotBeRemoved(candidate)</c>, evaluated by scanning EVERY field effect (in the sink's
    /// return chokepoints). Blocks bounce + deck-bounce, NOT deletion.</summary>
    public static ICardEffect CanNotBeRemovedStaticEffect(Func<CardSource, bool> predicate, CardSource card, Func<bool>? condition = null) =>
        new CanNotBeRemovedEffect(card, predicate, condition);

    /// <summary>(d-remediation) <c>CanNotMoveStaticEffect</c> — AS-IS <c>ICanNotMoveEffect</c> /
    /// <c>Permanent.CanMove</c>: the protected permanent(s) cannot MOVE (breeding→battle promotion). Registers a
    /// continuous restriction consulted by the legal-action dispatcher's move gate.</summary>
    /// <summary>(true-scan) AS-IS <c>SetUpCanNotMoveClass(CanNotMove)</c>: <paramref name="predicate"/> is the AS-IS
    /// <c>CanNotMove(candidate, causing)</c>, evaluated by scanning EVERY field effect in the move gate (causing is
    /// null there, AS-IS <c>CanNotMove(TopCard, null)</c>).</summary>
    public static ICardEffect CanNotMoveStaticEffect(Func<CardSource, CardSource?, bool> predicate, CardSource card, Func<bool>? condition = null) =>
        new CanNotMoveEffect(card, predicate, condition);

    /// <summary>(d-remediation) <c>DontHaveDPStaticEffect</c> — AS-IS <c>IDontHaveDPEffect</c> /
    /// <c>Permanent.HasDP==false</c>: the protected permanent(s) are treated as having NO DP — AS-IS
    /// <c>Permanent.DP</c> then returns -1, overriding the base DP and every DP modifier. Registers a continuous
    /// restriction honoured at the top of <c>ContinuousDpGate.ResolveDp</c>.</summary>
    public static ICardEffect DontHaveDPStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool scopeAnyPlayer = false) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.DontHaveDpKey, isInheritedEffect, condition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, RestrictionHelpers.DontHaveDpKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition), scopeAnyPlayer: scopeAnyPlayer);

    /// <summary>(d-remediation) <c>DontBattleSecurityDigimonStaticEffect</c> — AS-IS <c>DontBattleSecurityDigimonClass</c>:
    /// intrinsic "Ignore Battle" marker (EX4_013). Returned by the card at <see cref="EffectTiming.None"/>; the
    /// security resolver consults it when the card is revealed as a security Digimon and skips the attacker battle.
    /// <paramref name="cardSourceCondition"/> = AS-IS CardSourceCondition (usually <c>cs == card</c>).</summary>
    public static ICardEffect DontBattleSecurityDigimonStaticEffect(Func<CardSource, bool> cardSourceCondition, CardSource card, Func<bool>? condition) =>
        new DontBattleSecurityDigimonEffect(card, cardSourceCondition, condition);

    /// <summary>(d-remediation) <c>CannotIgnoreDigivolutionConditionStaticEffect</c> — AS-IS
    /// <c>ICannotIgnoreDigivolutionConditionEffect</c> / <c>Player.CanIgnoreDigivolutionRequirement</c>: while this
    /// is active NO player may ignore digivolution requirements (BT8_059 "Players can't ignore digivolution
    /// requirements") — it negates the ignore-level/ignore-colour grants. Registered board-wide (scopeAnyPlayer,
    /// so the digivolve target is in scope); honoured by DigivolveAction's evolution-condition gate. Default
    /// <paramref name="scopeAnyPlayer"/>:true mirrors the AS-IS global scan.</summary>
    /// <summary>(true-scan) AS-IS <c>SetUpCannotIgnoreDigivolutionConditionClass</c>: <paramref name="predicate"/>
    /// is the AS-IS joint <c>cannotIgnoreDigivolutionCondition(digivolvingCard, target)</c>, evaluated by scanning
    /// EVERY field effect in DigivolveAction. BT8_059 uses <c>(_, _) =&gt; true</c> (global lock).</summary>
    public static ICardEffect CannotIgnoreDigivolutionConditionStaticEffect(Func<CardSource, CardSource, bool> predicate, CardSource card, Func<bool>? condition = null) =>
        new CannotIgnoreDigivolutionConditionEffect(card, predicate, condition);

    /// <summary>(d-remediation) <c>ChangeEndTurnMinMemoryStaticEffect</c> — AS-IS <c>IChangeEndTurnMinMemoryEffect</c>
    /// / <c>AutoProcessing.TurnEndMinMemory</c>: SETS the memory the opponent must reach for the turn to auto-end
    /// (default 1) to <paramref name="minMemory"/> (BT14_081/BT17_069 set it to 3, so the turn player keeps acting
    /// until memory is -3). Registered self-scoped; the turn-pass gate scans the turn player's cards for the value.</summary>
    public static ICardEffect ChangeEndTurnMinMemoryStaticEffect(int minMemory, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousSelfModifierEffect(card, ModifierHelpers.EndTurnMinMemoryKey, minMemory, isInheritedEffect, condition);

    /// <summary>(d-remediation) <c>ChangeBaseCardNameStaticEffect</c> — AS-IS <c>ChangeBaseCardNameClass</c>: SETS
    /// the target's original card name to <paramref name="newName"/> (BT14_097 "original name is [Sukamon]"),
    /// REPLACING the printed name for all name matching (<c>CardSource.CardNames</c> / EqualsCardName). Grant on
    /// the target permanent with a duration for a temporary change.</summary>
    public static ICardEffect ChangeBaseCardNameStaticEffect(string newName, CardSource card, Func<bool>? condition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        var effect = new CardEffects.ChangeBaseCardNameClass();
        effect.SetUpICardEffect("Change base card name", condition, card);
        effect.SetUpChangeBaseCardNameClass((_, _) => new List<string> { newName });
        return effect;
    }

    /// <summary>(PRIM-W4) <c>CollisionStaticEffect</c> — grants Collision to the owner's Digimon (player-scope
    /// keyword). Grant live via HasKeyword; BlockTiming's hasCollision consumer migrates separately.</summary>
    public static ICardEffect CollisionStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Collision, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4/K1) <c>VortexCanAttackPlayersStaticEffect</c> — the AS-IS
    /// <c>IVortexCanAttackPlayersEffect</c>: a marker letting a permanent that is resolving its Vortex
    /// attack target the PLAYER (it does NOT grant Vortex itself — the previous Vortex-keyword lowering was
    /// a flatten and wrongly opened the end-of-turn window for non-Vortex allies).
    /// <paramref name="attackerCondition"/> = AS-IS AttackerCondition, evaluated against the attacker.</summary>
    public static ICardEffect VortexCanAttackPlayersStaticEffect(Func<Permanent, bool>? attackerCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.VortexCanAttackPlayers, scopeCardType: null, isInheritedEffect, condition, ScopePred(attackerCondition));

    /// <summary>(PRIM-W4) <c>ChangeLinkMaxStaticEffect</c> — continuous ±link-maximum on the owner's Digimon
    /// (player-scope LinkedMaxDelta modifier, queryable). Link enforcement consumer migrates separately
    /// (preemptive seal, same as ChangeSelfLinkMax).</summary>
    public static ICardEffect ChangeLinkMaxStaticEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new PlayerScopeModifierEffect(card, ModifierHelpers.LinkedMaxDeltaKey, changeValue, scopeCardType: "Digimon", condition, scopePredicate: ScopePred(permanentCondition));

    /// <summary>(PRIM-W4/K4) <c>TreatAsDigimonStaticEffect</c> — "also treated as a Digimon". AS-IS
    /// <c>TreatAsDigimonClass.IsDigimon(permanent)</c> evaluates <paramref name="permanentCondition"/>
    /// against the permanent BEING JUDGED (any of the owner's, e.g. a Tamer), so the grant is player-scope
    /// with the predicate — the previous self-only lowering dropped the predicate. Consumed by the central
    /// <see cref="ContinuousKeywordGate.IsDigimon(EngineContext, HeadlessEntityId)"/> chokepoint.</summary>
    public static ICardEffect TreatAsDigimonStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.TreatAsDigimon, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition));

    /// <summary>(PRIM-W4) <c>Gain1MemoryTamerOwnerDigimonConditionalEffect</c> — "[Start of Your Main Phase] if
    /// you have a matching Digimon, gain 1 memory." The per-permanent predicate is captured in
    /// <paramref name="condition"/> at porting time. Registers at <see cref="EffectTiming.OnStartMainPhase"/>
    /// (AS-IS "[Start of Your Main Phase]" — both known cards BT23_081/BT23_083 return it under that timing),
    /// NOT OnStartTurn.</summary>
    public static ICardEffect Gain1MemoryTamerOwnerDigimonConditionalEffect(string effectDescription, Func<Permanent, bool>? permanentCondition, Func<bool>? condition, CardSource card)
    {
        // (FR2) The memory gain is CONDITIONAL on the owner controlling a Digimon matching permanentCondition
        // (AS-IS). Fold that predicate into the trigger gate so it is not gained unconditionally.
        Func<bool>? gate = permanentCondition is null
            ? condition
            : () => (condition is null || condition()) && OwnerControlsMatchingDigimon(card, permanentCondition);
        return new TriggeredGainMemoryEffect(card, EffectTiming.OnStartMainPhase, amount: 1,
            string.IsNullOrWhiteSpace(effectDescription) ? "[Start of Your Main Phase] Gain 1 memory." : effectDescription, extraCondition: gate);
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
    /// (self restriction consulted by the ReturnToDeck sink paths). Mirrors AS-IS
    /// <c>CannotReturnToDeckStaticEffect(permanentCondition, cardEffectCondition, …)</c>: the restriction fires
    /// only when the returning effect's source matches <paramref name="cardEffectCondition"/> (AS-IS
    /// <c>Func&lt;ICardEffect,bool&gt;</c> over the causing effect's source card; e.g. "can't be returned to the
    /// deck by your OPPONENT's effects" = IsOpponentEffect). Null → unconditional. Read by the sink's
    /// IsRestrictedFromCause, exactly like <see cref="CannotReturnToHandStaticEffect"/>.</summary>
    public static ICardEffect CannotReturnToDeckStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, RestrictionHelpers.CannotReturnToDeckKey, isInheritedEffect, condition, cardEffectCondition)
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, RestrictionHelpers.CannotReturnToDeckKey, scopeCardType: null, isInheritedEffect, condition, ScopePred(permanentCondition), cardEffectCondition);

    /// <summary>(PRIM-W4) <c>CanNotBeDestroyedByBattleStaticEffect</c> — this Digimon cannot be deleted in
    /// battle (effect deletion still applies). Registers a battle-only immunity flag read by
    /// BattleDeletionGate. Per-card predicates accepted for fidelity.</summary>
    public static ICardEffect CanNotBeDestroyedByBattleStaticEffect(Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition, Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null, bool isLinkedEffect = false) =>
        permanentCondition is null
            ? new ContinuousSelfRestrictionEffect(card, BattleDeletionGate.PreventBattleDeletionKey, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition))
            : new ContinuousPlayerScopeRestrictionEffect(card, card.Owner, BattleDeletionGate.PreventBattleDeletionKey, scopeCardType: null, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition), ScopePred(permanentCondition));

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

    /// <summary>(PRIM-W4/K5) <c>PlayMindLinkTamerFromDigivolutionCards</c> — plays the Mind-Linked TAMER
    /// under-card matching <paramref name="cardName"/> from THIS card's own digivolution stack onto the field
    /// (cost-free). AS-IS 1:1: candidates are TAMER under-cards of <c>card.PermanentOfThisCard()</c> (own stack,
    /// via <c>selfStackOnly</c> — NOT every owner Digimon), narrowed to the card name AND playable
    /// (CanPlayAsNewPermanent, in the candidate filter); the select is OPTIONAL (AS-IS canNoSelect:true).</summary>
    public static IActivatedCardEffect PlayMindLinkTamerFromDigivolutionCards(CardSource card, string cardName, string effectDescription)
    {
        string description = string.IsNullOrWhiteSpace(effectDescription) ? $"Play {cardName} from under this Digimon." : effectDescription;
        return AsUniformActivated(card, new ActivatedPlayFromUnderEffect(
            card,
            description,
            cardType: "Tamer",
            cardName: string.IsNullOrWhiteSpace(cardName) ? null : cardName,
            isOptional: true,
            selfStackOnly: true), description);
    }

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

    /// <summary>(PRIM-W2 #10) AS-IS <c>PlaySelfDigimonAfterBattleSecurityEffect(card, deleteDigimon)</c> —
    /// "[Security] AT THE END OF THE BATTLE, play this Digimon from security cost-free." Now 1:1: the [Security]
    /// effect defers the play to <see cref="EffectTiming.OnEndBattle"/> (via
    /// <see cref="PlaySelfAtEndOfBattleSecurityEffect"/>) rather than playing immediately. When
    /// <paramref name="deleteDigimon"/> is not <see cref="EffectDuration.UntilEndBattle"/>, the played Digimon is
    /// deleted at the matching turn end (UntilOwnerTurnEnd / UntilOpponentTurnEnd / UntilEachTurnEnd).</summary>
    public static ICardEffect PlaySelfDigimonAfterBattleSecurityEffect(CardSource card, EffectDuration deleteDigimon = EffectDuration.UntilEndBattle) =>
        new PlaySelfAtEndOfBattleSecurityEffect(card, DeleteTimingString(deleteDigimon));

    /// <summary>Map the AS-IS <c>deleteDigimon</c> EffectDuration to the turn-end self-delete marker string the
    /// end-turn cleanup sweep consumes (null = no delete, i.e. UntilEndBattle).</summary>
    private static string? DeleteTimingString(EffectDuration deleteDigimon) => deleteDigimon switch
    {
        EffectDuration.UntilOwnerTurnEnd => "own",
        EffectDuration.UntilOpponentTurnEnd => "opponent",
        EffectDuration.UntilEachTurnEnd => "each",
        _ => null,
    };

    /// <summary>(PRIM-W2) Original: <c>LinkEffect(card, condition)</c> — the &lt;Link&gt; activation: attach
    /// this card to a chosen own Digimon, paying the link cost (read from the card's <c>linkCost</c> data).</summary>
    /// <summary>(W6-X) 1:1 mirror of AS-IS <c>AddDetailClass(canUseCondition, permanentCondition, detail,
    /// triggerEffect, card)</c> (CardEffectFactory.cs:1523) — DISPLAY-ONLY (PermanentDetail.cs tooltip text;
    /// the <c>triggerEffect</c> bool has ZERO consumers in the AS-IS codebase). Registers an inert binding
    /// carrying the detail string for observability; no game behavior.</summary>
    public static ICardEffect AddDetailClass(
        Func<bool>? canUseCondition, Func<Permanent, bool>? permanentCondition, string detail, bool triggerEffect, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _ = permanentCondition;
        _ = triggerEffect;
        return new DisplayDetailEffect(card, detail ?? string.Empty, canUseCondition);
    }

    /// <summary>(W6-A2) 1:1 mirror of AS-IS <c>ArtsDigivolveEffect(card)</c>
    /// (CardEffectFactory/KeyWordEffects/ArtsDigivolve.cs): while this Option resolves (executing area),
    /// digivolve it COST-FREE onto an owner Digimon that satisfies the normal digivolution requirement
    /// (<c>CanPlayCardTargetFrame</c> — the port's evolution-cost gate, cost unpaid).</summary>
    public static IActivatedCardEffect ArtsDigivolveEffect(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return new ArtsDigivolveSelfEffect(card);
    }

    /// <summary>(W6-F) 1:1 mirror of AS-IS <c>AddAppfuseMethodByCondition(cardConditions, card, cost,
    /// effectName)</c> (CardEffectFactory/AddAppfusionMethod.cs:16): declares the App-Fusion condition —
    /// the host's TOP card matches condition i and one of its LINK cards matches a DIFFERENT condition j
    /// (i != j), each material used once. Cost defaults 0.</summary>
    public static ICardEffect AddAppfuseMethodByCondition(
        IReadOnlyList<Func<CardSource, bool>> cardConditions, CardSource card, int cost = 0, string effectName = "App Fusion")
    {
        ArgumentNullException.ThrowIfNull(cardConditions);
        ArgumentNullException.ThrowIfNull(card);

        bool DigimonCondition(Permanent permanent)
        {
            if (!CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
            {
                return false;
            }

            for (int i = 0; i < cardConditions.Count; i++)
            {
                if (!cardConditions[i](permanent.TopCard))
                {
                    continue;
                }

                for (int j = 0; j < cardConditions.Count; j++)
                {
                    if (i != j && LinkedViews(permanent).Any(linked => cardConditions[j](linked)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool LinkedCondition(Permanent permanent, CardSource linkedCard)
        {
            for (int i = 0; i < cardConditions.Count; i++)
            {
                if (!cardConditions[i](permanent.TopCard))
                {
                    continue;
                }

                for (int j = 0; j < cardConditions.Count; j++)
                {
                    if (i != j && cardConditions[j](linkedCard))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        var effect = new CardEffects.AddAppFusionConditionClass();
        effect.SetUpICardEffect(effectName, null, card);
        effect.SetUpAddAppFusionConditionClass(cardSource =>
            cardSource.InstanceId == card.InstanceId
                ? new AppFusionCondition(LinkedCondition, DigimonCondition, cost)
                : null);
        effect.SetNotShowUI(true);
        return effect;

        static IEnumerable<CardSource> LinkedViews(Permanent permanent)
        {
            EngineContext context = permanent.TopCard.Context;
            if (!context.CardInstanceRepository.TryGetInstance(permanent.InstanceId, out CardInstanceRecord? host) || host is null)
            {
                yield break;
            }

            foreach (HeadlessEntityId linkId in Headless.Runtime.LinkHelpers.ReadLinkedCardIds(host.Metadata))
            {
                yield return new CardSource(context, linkId, permanent.OwnerId, permanent.OwnerId);
            }
        }
    }

    /// <summary>(W6-F) 1:1 mirror of AS-IS <c>AddAppfuseMethodByName(cardNames, card, cost, effectName)</c>
    /// (AddAppfusionMethod.cs): the by-NAME sugar over <see cref="AddAppfuseMethodByCondition"/>.</summary>
    public static ICardEffect AddAppfuseMethodByName(
        IReadOnlyList<string> cardNames, CardSource card, int cost = 0, string effectName = "App Fusion") =>
        AddAppfuseMethodByCondition(
            cardNames.Select<string, Func<CardSource, bool>>(name => cs => cs.EqualsCardName(name)).ToList(),
            card, cost, effectName);

    /// <summary>(W6-L) 1:1 mirror of AS-IS <c>AddSelfLinkConditionStaticEffect(permanentCondition, linkCost,
    /// card, condition, cardCondition, effectName)</c> (CardEffectFactory/AddLinkRequirement.cs:11): the
    /// timing-None declaration "this card may LINK onto an owner Digimon matching
    /// <paramref name="permanentCondition"/>, paying <paramref name="linkCost"/>". The separate
    /// <c>LinkEffect(card)</c> (OnDeclaration) is the play action that consumes it.</summary>
    public static ICardEffect AddSelfLinkConditionStaticEffect(
        Func<Permanent, bool> permanentCondition, int linkCost, CardSource card,
        Func<bool>? condition = null, Func<CardSource, bool>? cardCondition = null, string? effectName = null)
    {
        ArgumentNullException.ThrowIfNull(permanentCondition);
        ArgumentNullException.ThrowIfNull(card);
        Func<CardSource, bool> cardGate = cardCondition ?? (cardSource => cardSource.InstanceId == card.InstanceId);
        var effect = new CardEffects.AddLinkConditionClass();
        effect.SetUpICardEffect(effectName ?? "Link", condition, card);
        effect.SetUpAddLinkConditionClass(cardSource =>
            cardSource.InstanceId == card.InstanceId && cardGate(cardSource)
                ? new LinkCondition(permanentCondition, linkCost)
                : null);
        return effect;
    }

    public static ICardEffect LinkEffect(CardSource card, Func<bool>? condition = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        // (W6-L) the declared LinkCondition (AddSelfLinkConditionStaticEffect) is authoritative — AS-IS
        // LinkEffect reads card.linkCondition.cost; the definition-metadata linkCost is the data fallback.
        int linkCost = card.LinkConditionOf()?.cost
            ?? (card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? inst) && inst is not null
                && card.Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
                && def.Metadata.TryGetValue("linkCost", out object? raw) && raw is int cost ? cost : 0);
        return new LinkSelfEffect(card, linkCost, $"Link (Cost: {linkCost}).");
    }

    /// <summary>(PRIM-W2) Original: <c>BlockerStaticEffect(permanentCondition, isInheritedEffect, card,
    /// condition, isLinkedEffect)</c> — grants Blocker to a set of permanents. Modeled as a PLAYER-SCOPE
    /// Blocker grant on the owner's Digimon (the common "your Digimon gain &lt;Blocker&gt;" form);
    /// <paramref name="permanentCondition"/>/<paramref name="isLinkedEffect"/> accepted for source fidelity,
    /// per-permanent narrowing beyond the owner scope is a per-card concern.</summary>
    public static ICardEffect BlockerStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false) =>
        new ContinuousPlayerScopeKeywordEffect(card, card.Owner, ContinuousKeywordGate.Blocker, scopeCardType: null, isInheritedEffect, LinkedGate(card, isLinkedEffect, condition), ScopePred(permanentCondition));

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
        Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null, bool? isOptional = null,
        string? effectIdSuffix = null) =>
        new TriggeredMemoryEffect(card, timing, amount, isInheritedEffect, condition, description, triggerGate, maxCountPerTurn, hash, isOptional, effectIdSuffix);

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
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Destroy, description), description);

    /// <summary>(PRIM-P0-flow) An activated "choose one of the following modes" menu (AS-IS UserSelectionManager
    /// SetBool/IntSelection). Each mode is a labeled branch effect; a mode with an availability predicate that
    /// returns false is omitted. The selected branch resolves through the same activation flow / sink.</summary>
    public static ICardEffect SelectModeEffect(CardSource card, string description, params ModeChoiceEffect.Mode[] modes) =>
        new ModeChoiceEffect(card, description, modes);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>new SuspendPermanentsClass(perms, ..).Tap()</c>
    /// coroutine: select up to <paramref name="maxCount"/> matching permanents and suspend them.</summary>
    public static ICardEffect SelectAndSuspendEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Tap, description), description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS unsuspend coroutine: select up to
    /// <paramref name="maxCount"/> matching permanents and unsuspend them.</summary>
    public static ICardEffect SelectAndUnsuspendEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.UnTap, description), description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS bounce coroutine: select up to
    /// <paramref name="maxCount"/> matching permanents and return them to hand.</summary>
    public static ICardEffect SelectAndBounceEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax, SelectPermanentEffect.Mode.Bounce, description), description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> matching permanents and return
    /// them to the owner's deck (top or bottom). AS-IS SelectPermanentEffect.Mode PutLibraryTop/Bottom.</summary>
    public static ICardEffect SelectAndReturnToDeckEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool toTop, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax,
            toTop ? SelectPermanentEffect.Mode.PutLibraryTop : SelectPermanentEffect.Mode.PutLibraryBottom, description), description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> matching permanents and place
    /// them into the owner's security (top or bottom). AS-IS SelectPermanentEffect.Mode PutSecurityTop/Bottom.</summary>
    public static ICardEffect SelectAndPutSecurityEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool toTop, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax,
            toTop ? SelectPermanentEffect.Mode.PutSecurityTop : SelectPermanentEffect.Mode.PutSecurityBottom, description), description);

    /// <summary>(PRIM-W5) Declarative form of the AS-IS <c>CardEffectCommons.PlayPermanentCards(.., root)</c>
    /// coroutine: select up to <paramref name="maxCount"/> of the owner's cards in <paramref name="fromZone"/>
    /// (Trash / Hand) matching <paramref name="canTarget"/> and play each onto the battle area (cost-free).
    /// The AS-IS <c>SelectCardEffect.Root</c> maps to <paramref name="fromZone"/>.</summary>
    public static ICardEffect SelectAndPlayFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectAndPlayEffect(card, fromZone, canTarget, maxCount, canEndNotMax, description), description);

    /// <summary>(PRIM-P0 B.O.5) AS-IS <c>CardEffectCommons.PlayOptionCards</c>: select up to
    /// <paramref name="maxCount"/> of the owner's Option cards in <paramref name="sourceZone"/> and play each as a
    /// nested effect (trash → OnUseOption → resolve its [Main]). Cost-free (v1).</summary>
    public static ICardEffect PlayOptionCardEffect(
        CardSource card, ChoiceZone sourceZone, Func<HeadlessEntityId, bool> optionPredicate, int maxCount, bool canEndNotMax, string description) =>
        new PlayOptionCardEffect(card, sourceZone, optionPredicate, maxCount, canEndNotMax, description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> of the owner's cards in
    /// <paramref name="fromZone"/> (Trash / Library / Security …) matching <paramref name="canTarget"/> and add
    /// each to the owner's hand. AS-IS SelectCardEffect.Mode AddHand.</summary>
    public static ICardEffect SelectAndAddToHandFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description,
        Action<CardSource, MatchStateMutationSink>? onSelectedAny = null) =>
        AsUniformActivated(card, new ActivatedSelectFromZoneEffect(card, fromZone, canTarget, maxCount, canEndNotMax, MatchStateMutationSink.ReturnToHandKind, description, onSelectedAny), description);

    /// <summary>(PRIM-P0-flow B.O.3) Select up to <paramref name="maxCount"/> of the owner's cards in
    /// <paramref name="fromZone"/> matching <paramref name="canTarget"/> and trash each. AS-IS
    /// SelectCardEffect.Mode Discard.</summary>
    public static ICardEffect SelectAndTrashFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectFromZoneEffect(card, fromZone, canTarget, maxCount, canEndNotMax, MatchStateMutationSink.TrashCardKind, description), description);

    /// <summary>(G16) Select up to <paramref name="maxCount"/> of the owner's CARDS in <paramref name="fromZone"/>
    /// (e.g. Trash) matching <paramref name="canTarget"/> and place each on TOP of the owner's security stack,
    /// FACE-DOWN — the AS-IS "place 1 &lt;X&gt; card from trash face-down on top of security" (IAddSecurity).
    /// Distinct from <c>SelectAndPutSecurityEffect</c> (which targets battle-area PERMANENTS, Mode PutSecurity);
    /// this routes a zone card via <see cref="MatchStateMutationSink.AddToSecurityKind"/> (its defaults =
    /// face-down + top, matching the AS-IS).</summary>
    public static ICardEffect SelectAndPutSecurityFromZoneEffect(
        CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description) =>
        AsUniformActivated(card, new ActivatedSelectFromZoneEffect(card, fromZone, canTarget, maxCount, canEndNotMax, MatchStateMutationSink.AddToSecurityKind, description), description);

    /// <summary>(PRIM-P0-flow B.O.3) AS-IS <c>DigivolveIntoHandOrTrashCard</c>: select 1 of the owner's Digimon
    /// (<paramref name="targetPredicate"/>) and a source card in <paramref name="sourceZone"/> (Hand / Trash,
    /// <paramref name="sourcePredicate"/>) that can digivolve onto it, pay the cost, and digivolve. v1 enforces
    /// requirements.</summary>
    public static ICardEffect SelectAndDigivolveEffect(
        CardSource card, ChoiceZone sourceZone, Func<HeadlessEntityId, bool> sourcePredicate,
        Func<HeadlessEntityId, bool> targetPredicate, DigivolveCost cost, int costAmount, string description) =>
        new SelectAndDigivolveEffect(card, sourceZone, sourcePredicate, targetPredicate, cost, costAmount, description);

    /// <summary>(PRIM-P0 B.O.4) A one-shot before-pay reduction of THIS card's own play/digivolve cost by
    /// <paramref name="amount"/> when <paramref name="condition"/> holds (AS-IS BeforePayCost ActivateClass →
    /// UntilCalculateFixedCostEffect.Add). Non-interactive.</summary>
    public static ICardEffect BeforePayCostReductionEffect(CardSource card, int amount, Func<bool>? condition, string description) =>
        new BeforePayCostReductionEffect(card, () => amount, condition, description);

    /// <summary>(PRIM-P0 B.O.4) As above with a dynamic reduction amount (e.g. -1 per matching card).</summary>
    public static ICardEffect BeforePayCostReductionEffect(CardSource card, Func<int> amount, Func<bool>? condition, string description) =>
        new BeforePayCostReductionEffect(card, amount, condition, description);

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

    /// <summary>(P4) Mirror of the FULL AS-IS <c>CardEffectCommons.RevealDeckTopCardsAndSelect</c> with a
    /// <c>SelectCardConditionClass[]</c> (multi-condition sequential passes over the shared revealed pool,
    /// BT10-096/BT10-097/ST17-11 shape). Each original condition maps to one
    /// <see cref="HeadlessDCGO.Engine.Headless.Runtime.RevealSelectPass"/> (predicate 1:1, maxCount,
    /// Mode → destination — original <c>Mode.Custom</c> = <see cref="RevealDestination.Custom"/>, read the
    /// picks back from <see cref="RevealMultiSelectEffect.CustomSelections"/>).</summary>
    public static IActivatedCardEffect RevealDeckTopCardsAndSelect(
        CardSource card, int revealCount, IReadOnlyList<HeadlessDCGO.Engine.Headless.Runtime.RevealSelectPass> selectCardConditions,
        RevealDestination remainingCardsPlace, string description,
        bool canNoAction = false, bool isOpponentDeck = false, bool mutualConditions = false) =>
        new RevealMultiSelectEffect(card, revealCount, selectCardConditions, remainingCardsPlace, description, canNoAction, isOpponentDeck, mutualConditions);

    /// <summary>(PRIM-W5/S2) <c>CanNotAffectedStaticEffect</c> — AS-IS <c>CanNotAffectedClass</c>:
    /// <c>CanNotAffect(target, effect) = CardCondition(target) &amp;&amp; SkillCondition(effect)</c>. Registers a
    /// continuous immunity under <see cref="HeadlessDCGO.Engine.Headless.Runtime.ContinuousImmunityGate"/> (consumed by the sink's effect path),
    /// carrying <paramref name="skillCondition"/> — the per-card predicate over the CAUSING effect's source that
    /// decides WHICH effects the card is immune to (e.g. <c>src =&gt; src.Owner != card.Owner &amp;&amp; src.IsDigimon</c>
    /// for "opponent's Digimon effects only"). <b>skillCondition must be provided to mirror the original</b>; null
    /// falls back to opponent-only.</summary>
    public static ICardEffect CanNotAffectedStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? skillCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        // (C2) permanentCondition = AS-IS CardCondition (WHICH permanents are protected) — evaluated live
        // against the protected target (previously accepted and dropped; null keeps the self-only grant).
        new ContinuousImmunityEffect(card, skillCondition, isInheritedEffect, condition, targetPredicate: ScopePred(permanentCondition));

    /// <summary>(C-3) <c>CanNotTrashFromDigivolutionCardsStaticEffect</c> — AS-IS
    /// <c>CanNotTrashFromDigivolutionCardsClass</c>: <c>CanNotTrashFromDigivolutionCards(source, effect) =
    /// CardCondition(source) &amp;&amp; CardEffectCondition(effect) &amp;&amp; !source.IsFlipped</c>. Registers a
    /// continuous protection under <see cref="HeadlessDCGO.Engine.Headless.Runtime.TrashProtectionScan"/> — the
    /// EFFECT-trash filter consults it, the deletion path bypasses it (AS-IS DiscardEvoRoots).
    /// <paramref name="cardCondition"/> = WHICH source (e.g. name contains "X Antibody");
    /// <paramref name="cardEffectCondition"/> = WHICH effect (evaluated over the causing effect's source; BT9_109
    /// = <c>effect != null</c> ⇒ always). <paramref name="condition"/> = the effect's own CanUse gate
    /// (BT9_109 = host <c>IsExistOnBattleArea</c>) — protection lapses when the granting host leaves the field.</summary>
    public static ICardEffect CanNotTrashFromDigivolutionCardsStaticEffect(
        Func<CardSource, bool> cardCondition, Func<CardSource, bool> cardEffectCondition,
        bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousTrashProtectionEffect(card, cardCondition, cardEffectCondition, isInheritedEffect, condition);

    /// <summary>(E-3) <c>CanNotPlayOptionStaticEffect</c> — AS-IS <c>CanNotPlayClass</c>: a continuous
    /// "an Option matching <paramref name="cardCondition"/> cannot be played" effect scanned by the option-play
    /// legality gate (<see cref="HeadlessDCGO.Engine.Headless.Runtime.CanNotPlayOptionScan"/>).
    /// <paramref name="cardCondition"/> = AS-IS <c>CanNotPlay</c> (WHICH option — owner / IsOption);
    /// <paramref name="condition"/> = the effect's own CanUse gate. Registered as a FIELD static (subject to the
    /// AS-IS stack-position membership); use <see cref="AddCanNotPlayOptionToPlayer"/> for the AS-IS
    /// player-bucket (region ①) duration-bound form.</summary>
    public static ICardEffect CanNotPlayOptionStaticEffect(
        Func<CardSource, bool> cardCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition) =>
        new ContinuousCanNotPlayOptionEffect(card, cardCondition, isInheritedEffect, condition);

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

    /// <summary>(PRIM special-play) DigiXros whose material slots may ALSO be satisfied by cards from the TRASH
    /// (up to <paramref name="maxTrashCount"/>) and/or a Tamer's digivolution sources (up to
    /// <paramref name="maxUnderTamerCount"/>) — AS-IS <c>AddMaxTrashCountDigiXrosClass</c> /
    /// <c>maxTamerDigivolutionCardsCount</c>, whose <c>getMaxTrashCount</c> Func is threaded 1:1 (evaluated per
    /// play). Pass null (or a Func returning 0) for a source not allowed.</summary>
    public static ICardEffect DigiXrosWithExtraMaterialsEffect(
        CardSource card, int costReduction,
        Func<CardSource, int>? maxTrashCount, Func<CardSource, int>? maxUnderTamerCount,
        params SpecialPlayMaterial[] materials)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(
            SpecialPlayKind.DigiXros, materials, MemoryCost: 0, Condition: null,
            MaxTrashCount: maxTrashCount, MaxUnderTamerCount: maxUnderTamerCount));
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

    /// <summary>(PRIM special-play) AS-IS Burst Digivolution (<c>BurstDigivolutionCondition</c>): this hand card
    /// digivolves onto a target battle-area Digimon (<paramref name="digimonCondition"/>) while a matching Tamer
    /// (<paramref name="tamerCondition"/>) is returned to the hand, paying <paramref name="cost"/>. The target is
    /// the recipe material; the Tamer is matched + bounced by the action.</summary>
    public static ICardEffect BurstDigivolveEffect(
        CardSource card, Func<CardSource, bool> digimonCondition, Func<CardSource, bool> tamerCondition,
        int cost = 0, Func<bool>? condition = null)
    {
        ArgumentNullException.ThrowIfNull(digimonCondition);
        ArgumentNullException.ThrowIfNull(tamerCondition);
        var target = new SpecialPlayMaterial(digimonCondition, "Burst target Digimon");
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(
            SpecialPlayKind.Burst, new[] { target }, MemoryCost: cost, Condition: condition, TamerCondition: tamerCondition));
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

    /// <summary>(Jogress by levels) AS-IS <c>AddJogressLevelsClass</c> — makes THIS card count as extra level(s)
    /// when it is a Jogress / DNA-Digivolution material (e.g. "Also treated as level 6 for DNA Digivolution").
    /// <paramref name="getLevels"/> maps the digivolving (Jogress) card to the extra levels this material grants;
    /// read by <see cref="CardSource.JogressLevelsAgainst"/>. Level-based material predicates then test it.</summary>
    public static ICardEffect AddJogressLevelsEffect(
        CardSource card, Func<CardSource, IReadOnlyList<int>> getLevels, Func<bool>? condition = null,
        string name = "Also treated as additional levels for DNA Digivolution")
    {
        var effect = new CardEffects.AddJogressLevelsClass();
        effect.SetUpICardEffect(name, condition, card);
        effect.SetUpAddJogressLevelsClass(getLevels);
        return effect;
    }

    /// <summary>(PRIM special-play) AS-IS <c>DNADigivolveWithHandOrTrashCardIntoHandOrTrash</c> — an effect-driven
    /// DNA Digivolution into a hand/trash card (<paramref name="intoCondition"/>) fusing a battle-area permanent
    /// (<paramref name="permanentCondition"/>) with a hand/trash material (<paramref name="materialCondition"/>).
    /// Resolved via the activation flow.</summary>
    public static ICardEffect DnaDigivolveFromHandOrTrashEffect(
        CardSource card, Func<CardSource, bool> intoCondition, Func<CardSource, bool> permanentCondition,
        Func<CardSource, bool> materialCondition, bool intoFromHand, bool materialFromHand,
        string description = "DNA Digivolve using a hand/trash card") =>
        new DnaFromHandOrTrashActivatedEffect(
            card, intoCondition, permanentCondition, materialCondition, intoFromHand, materialFromHand, description);

    /// <summary>(PRIM-W5) Jogress with ARBITRARY per-material predicates (faithful form of
    /// <c>AddJogressConditionClass</c>'s <c>GetJogress</c>).</summary>
    public static ICardEffect JogressEffect(CardSource card, Func<bool>? condition, params SpecialPlayMaterial[] materials)
    {
        SpecialPlayRecipeRegistry.Register(card.CardNumber, new SpecialPlayRecipe(SpecialPlayKind.DnaDigivolve, materials, MemoryCost: 0, Condition: condition));
        return new SpecialPlayRecipeMarkerEffect(card);
    }

    /// <summary>(AD1-J) 1:1 mirror of the AS-IS <c>GetJogressConditionClass(permanentCondition1, description1,
    /// permanentCondition2, description2, card, cost, canUseCondition)</c> (CardEffectFactory.cs:752): the
    /// PREDICATE form of DNA digivolution — each material slot is an arbitrary Permanent predicate, evaluated
    /// against the owner's battle-area Digimon (the AS-IS <c>AddJogressConditionClass</c> wraps each with
    /// <c>IsPermanentExistsOnOwnerBattleAreaDigimon</c>). Lowers to <see cref="JogressEffect"/>.
    /// <paramref name="cost"/> is accepted for source-signature fidelity and IGNORED — the original drops it
    /// before <c>GetJogressConditions</c> (whose cost defaults to 0), so the predicate form is always cost 0.</summary>
    public static ICardEffect GetJogressConditionClass(
        Func<Permanent, bool> permanentCondition1, string description1,
        Func<Permanent, bool> permanentCondition2, string description2,
        CardSource card, int cost = 0, Func<bool>? canUseCondition = null)
    {
        ArgumentNullException.ThrowIfNull(permanentCondition1);
        ArgumentNullException.ThrowIfNull(permanentCondition2);
        ArgumentNullException.ThrowIfNull(card);
        _ = cost; // AS-IS quirk: the parameter is not forwarded (always cost 0).
        return JogressEffect(
            card, canUseCondition,
            AsMaterial(permanentCondition1, description1),
            AsMaterial(permanentCondition2, description2));

        SpecialPlayMaterial AsMaterial(Func<Permanent, bool> permanentCondition, string description) =>
            new(cs => cs.IsDigimon && cs.Owner == card.Owner
                    && permanentCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner)),
                description);
    }

    /// <summary>An activated "select up to <paramref name="maxCount"/> matching Digimon and give each
    /// +<paramref name="changeValue"/> DP for <paramref name="duration"/>" effect (e.g. ST1_13 [Main]).</summary>
    public static ICardEffect SelectAndBuffDpEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int changeValue, EffectDuration duration, string description) =>
        AsUniformActivated(card, new ActivatedTargetBuffEffect(card, canTarget, maxCount, ModifierHelpers.DpDeltaKey, changeValue, duration, description), description);

    /// <summary>An activated "all your Digimon gain +<paramref name="changeValue"/> Security Attack for
    /// <paramref name="duration"/>" player-scope effect (e.g. ST1_13 [Security]).</summary>
    public static ICardEffect PlayerScopeBuffSAttackEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        AsUniformActivated(card, new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, scopeCardType: "Digimon", description), description);

    /// <summary>An activated "all your Security Digimon get +<paramref name="changeValue"/> DP for
    /// <paramref name="duration"/>" player-scope effect, scoped to the owner's Security-zone Digimon
    /// (e.g. ST1_14).</summary>
    public static ICardEffect PlayerScopeBuffSecurityDpEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        AsUniformActivated(card, new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.DpDeltaKey, changeValue, duration, scopeCardType: "Digimon", description, scopeZone: "Security"), description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> opponent Digimon and trash
    /// <paramref name="trashCount"/> of each host's digivolution cards from the bottom/top" effect
    /// (e.g. ST2_03 / ST2_06 / ST2_09).</summary>
    public static ICardEffect SelectAndTrashDigivolutionEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int trashCount, bool fromBottom, string description) =>
        AsUniformActivated(card, new ActivatedSelectTrashDigivolutionEffect(card, canTarget, maxCount, trashCount, fromBottom, description), description);

    /// <summary>(PRIM special-play) AS-IS <c>IDigiBurst</c> — <c>[Digi-Burst N] &lt;effect&gt;</c>: trash N of this
    /// card's own digivolution sources as a cost, then resolve <paramref name="innerEffect"/>. Offered only when
    /// the permanent holds &gt;= N sources. Wrap the card's Digi-Burst body as the inner effect.</summary>
    public static ICardEffect DigiBurstEffect(CardSource card, int count, ICardEffect innerEffect, string description) =>
        new DigiBurstActivatedEffect(card, count, innerEffect, description);

    /// <summary>A triggered "[When ...] unsuspend this Digimon" effect (e.g. ST2_11). Pass
    /// <paramref name="maxCountPerTurn"/> = 1 (+ <paramref name="hash"/> for the original SetHashString) to
    /// mirror a [Once Per Turn] limit — enforced by the live trigger loop via <c>OnceFlagController</c>.</summary>
    public static ICardEffect UnsuspendSelfTriggerEffect(EffectTiming timing, CardSource card, string description, int? maxCountPerTurn = null, string? hash = null,
        Func<CardEffectResolveContext, bool>? triggerGate = null) =>
        new TriggeredUnsuspendSelfEffect(card, timing, description, maxCountPerTurn, hash, triggerGate);

    /// <summary>An activated "gain/lose <paramref name="amount"/> memory" skill (Option [Main] / [Security],
    /// e.g. ST2_13).</summary>
    public static ICardEffect GainMemoryActivatedEffect(CardSource card, int amount, string description) =>
        new ActivatedMemoryEffect(card, amount, description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon and return each to its owner's
    /// hand" effect (Option [Main] bounce, e.g. ST2_16).</summary>
    public static ICardEffect SelectAndBounceEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, string description) =>
        AsUniformActivated(card, new ActivatedSelectEffect(card, canTarget, maxCount, canNoSelect: false, canEndNotMax: maxCount > 1, SelectPermanentEffect.Mode.Bounce, description), description);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon, return each to its owner's
    /// hand, AND trash all of that Digimon's digivolution cards" effect (Option [Main] bounce, e.g. ST4_16).
    /// AS-IS <c>HandBounceClaass.Bounce()</c> unconditionally runs <c>permanent.DiscardEvoRoots()</c>
    /// immediately before the top card leaves the field for EVERY hand-bounce (Permanent.cs:106) — see
    /// <see cref="ActivatedSelectBounceAndDiscardSourcesEffect"/>.</summary>
    public static ICardEffect SelectAndBounceWithSourceDiscardEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, string description) =>
        AsUniformActivated(card, new ActivatedSelectBounceAndDiscardSourcesEffect(card, canTarget, maxCount, description), description);

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
    public static ICardEffect RecoveryTriggerEffect(EffectTiming timing, int amount, CardSource card, Func<bool>? condition, string description,
        Func<CardEffectResolveContext, bool>? triggerGate = null) =>
        new RecoverTriggerEffect(card, timing, amount, condition, description, triggerGate);

    /// <summary>An activated "select up to <paramref name="maxCount"/> Digimon and give each
    /// +<paramref name="changeValue"/> Security Attack for <paramref name="duration"/>" effect (e.g. ST3_15 [Main]).</summary>
    public static ICardEffect SelectAndBuffSAttackEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int changeValue, EffectDuration duration, string description) =>
        AsUniformActivated(card, new ActivatedTargetBuffEffect(card, canTarget, maxCount, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, description), description);

    /// <summary>An activated "all your Digimon get +<paramref name="changeValue"/> DP for
    /// <paramref name="duration"/>" player-scope effect (e.g. ST3_13 [Security]).</summary>
    public static ICardEffect PlayerScopeBuffDpEffect(
        CardSource card, int changeValue, EffectDuration duration, string description) =>
        AsUniformActivated(card, new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.DpDeltaKey, changeValue, duration, scopeCardType: "Digimon", description), description);

    /// <summary>An activated "all of your opponent's Digimon get +<paramref name="changeValue"/> Security
    /// Attack for <paramref name="duration"/>" player-scope effect, scoped to <paramref name="opponentId"/>
    /// (e.g. ST3_15 [Security] "all opponent Digimon gain Security Attack -1").</summary>
    public static ICardEffect OpponentScopeBuffSAttackEffect(
        CardSource card, int changeValue, EffectDuration duration, HeadlessPlayerId opponentId, string description) =>
        AsUniformActivated(card, new ActivatedPlayerScopeBuffEffect(card, ModifierHelpers.SecurityAttackDeltaKey, changeValue, duration, scopeCardType: "Digimon", description, scopePlayerId: opponentId), description);

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

