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
using ChangeCostClass = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ChangeCostClass;


// (이연③-b RETIRED) `ArtsDigivolveSelfEffect` DELETED — the mirror-invented Arts-Digivolve self-resolution was
// an orphaned DUPLICATE with no production call-site (the factory was re-pointed off it at the P6 kind-class
// flip). The AS-IS Arts-Digivolve surface is now live and resolved: `CardEffectFactory.ArtsDigivolveEffect`
// (KeyWordEffects/ArtsDigivolve.cs, RD-P6C2-10 RESOLVED) returns an OptionResolutionClass whose ResolutionCoroutine
// runs SelectPermanentEffect + `new PlayCardClass(payCost:false, root:Execution, target, activateETB).PlayCard()`
// — used live by the real Arts cards BT9_109 / BT25_104 / BT25_092 / BT25_089. The cost-free digivolve RULE
// (attach on top, target folds as a source, no cost, WhenDigivolving fires) is covered GREEN by
// G3.5-D6.FreeDigivolve (4/4). Class + resolver switch case removed; G9-071's invented-duplicate Arts witness
// retired onto that coverage.


/// <summary>(PRIM-P0-flow B.O.3) The headless mirror of the AS-IS <c>DigivolveIntoHandOrTrashCard</c> (309
/// cards): select 1 of the owner's Digimon (<paramref name="targetPredicate"/>), select a source card in
/// <c>sourceZone</c> (Hand / Trash, matching <c>sourcePredicate</c>) that can legally digivolve onto it, pay the
/// cost per <see cref="DigivolveCost"/>, and place the source onto the target as a digivolution (stack fold +
/// WhenDigivolving). A generalization of <see cref="ArtsDigivolveSelfEffect"/> adding source-card selection and
/// cost. v1 ENFORCES digivolution requirements (the TryGetEvolutionCost gate); requirement-bypass is a follow-up.
/// See docs/porting/select_and_digivolve_design.md.</summary>
public sealed class SelectAndDigivolveEffect : IActivatedCardEffect
{
    private readonly ChoiceZone _sourceZone;
    private readonly Func<HeadlessEntityId, bool> _sourcePredicate;
    private readonly Func<HeadlessEntityId, bool> _targetPredicate;
    private readonly DigivolveCost _cost;
    private readonly int _costAmount;

    public SelectAndDigivolveEffect(CardSource card, ChoiceZone sourceZone, Func<HeadlessEntityId, bool> sourcePredicate,
        Func<HeadlessEntityId, bool> targetPredicate, DigivolveCost cost, int costAmount, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(sourcePredicate);
        ArgumentNullException.ThrowIfNull(targetPredicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _sourceZone = sourceZone;
        _sourcePredicate = sourcePredicate;
        _targetPredicate = targetPredicate;
        _cost = cost;
        _costAmount = costAmount;
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

        // 1. Select the target Digimon (own battle area, predicate, not digivolve-restricted).
        var targetCandidates = zones.GetCards(Card.Owner, ChoiceZone.BattleArea)
            .Where(_targetPredicate)
            .Where(id => !Headless.Runtime.ContinuousRestrictionGate.EvaluateDigivolve(context, id).IsRestricted)
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: Card.Owner))
            .ToList();
        if (targetCandidates.Count == 0)
        {
            return;
        }

        ChoiceResult targetResult = await context.ChoiceProvider.ChooseAsync(
            new ChoiceRequest(ChoiceType.Card, Card.Owner, Description, minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.BattleArea, targetCandidates),
            cancellationToken).ConfigureAwait(false);
        if (targetResult.SelectedIds.Count == 0)
        {
            return;
        }

        HeadlessEntityId targetId = targetResult.SelectedIds[0];

        // 2. Select the source card in the zone that can legally digivolve onto the target (TryGetEvolutionCost
        // is the AS-IS CanPlayCardTargetFrame legality + requirement gate).
        var sourceCandidates = zones.GetCards(Card.Owner, _sourceZone)
            .Where(_sourcePredicate)
            .Where(id => Headless.Runtime.DigivolveAction.TryGetEvolutionCost(context, id, targetId, out _, out _))
            .Select(id => new ChoiceCandidate(id, id.Value, _sourceZone, IsSelectable: true, ownerId: Card.Owner))
            .ToList();
        if (sourceCandidates.Count == 0)
        {
            return;
        }

        ChoiceResult sourceResult = await context.ChoiceProvider.ChooseAsync(
            new ChoiceRequest(ChoiceType.Card, Card.Owner, Description, minCount: 1, maxCount: 1, canSkip: false, _sourceZone, sourceCandidates),
            cancellationToken).ConfigureAwait(false);
        if (sourceResult.SelectedIds.Count == 0)
        {
            return;
        }

        HeadlessEntityId sourceId = sourceResult.SelectedIds[0];

        // 3. Resolve the cost.
        int normalCost = Headless.Runtime.DigivolveAction.TryGetEvolutionCost(context, sourceId, targetId, out int resolved, out _) ? resolved : 0;
        int payCost = _cost switch
        {
            DigivolveCost.Free => 0,
            DigivolveCost.Fixed => Math.Max(0, _costAmount),
            DigivolveCost.Reduced => Math.Max(0, normalCost - _costAmount),
            _ => normalCost,
        };

        // 4. Pay (skip if the owner cannot afford — AS-IS CanPlayCardTargetFrame would not have offered it).
        if (payCost > 0)
        {
            if (!context.MemoryController.CanPay(payCost))
            {
                return;
            }

            context.MemoryController.Pay(payCost);
        }

        // 5. Place the source onto the target as a digivolution (same order as ArtsDigivolveSelfEffect).
        // (RD-R3-02) top-swap continuity markers — the permanent persists; AttachTargetAsSource ReKeys.
        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(Card.Owner, targetId, ChoiceZone.BattleArea, ChoiceZone.None,
                Metadata: PermanentBookkeepingStore.ContinuityMoveMetadata), cancellationToken).ConfigureAwait(false);
        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(Card.Owner, sourceId, _sourceZone, ChoiceZone.BattleArea,
                Metadata: PermanentBookkeepingStore.ContinuityMoveMetadata), cancellationToken).ConfigureAwait(false);
        Headless.Runtime.DigivolveAction.AttachTargetAsSource(context.CardInstanceRepository, sourceId, targetId);
        TriggerEventEmitter.Emit(context.GameEventQueue, Headless.Effects.TriggerTimings.WhenDigivolving, actor: Card.Owner, subject: sourceId);
        CardEffectRegistrar.RegisterCard(context, sourceId, Card.Owner);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Select-and-digivolve is resolved via the activation flow, not registered: {Description}");
}


// (이연③-b RETIRED) `LinkSelfEffect` DELETED — the mirror-invented <Link> self-play activation was an orphaned
// DUPLICATE with no production call-site (the factory was re-pointed off it at the P6 kind-class flip). The AS-IS
// <Link> surface is now live and resolved: `CardEffectFactory.LinkEffect` (KeyWordEffects/Link.cs, RD-P6C2-7
// RESOLVED) returns an ActivateClass whose coroutine attaches via `new ILinkCard(...).LinkCard()`. That canonical
// path is covered GREEN by G9-031.LinkSecurity (LinkAttaches: real K:Link card EX10_029, declared LinkCondition
// cost 2, attach + memory 5→3). Class + resolver switch case removed; G9-070's invented-duplicate LinkFlow witness
// retired onto that coverage (its DeclarationReadable case — the synthetic-card LinkCondition observability
// stage-B gap — stays as-is).


// (uniform-사멸 flip) `LinkFromHandOrSourcesToSelfBody` DELETED — consumer-0 invented IEffectBody (the
// interface died with the uniform ActivatedEffect corpus; the live <Link> surface is
// CardEffectFactory.LinkEffect + LinkHelpers).



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


// (이연③-b RETIRED) `ReuseWhenDigivolvingEffect` DELETED — the mirror-invented "[All Turns] re-activate this
// card's [When Digivolving] effects" carrier was test-only (only TfxWhenDigivolveDelete's OptionSkill branch
// constructed it). EX8_074 (the real card) already superseded it: region #6 delivers the SAME re-activation
// through the AS-IS OnEnterFieldAnyone + play-window broadcast (StackSkillInfos) path, NOT this bespoke marker
// (EX8_074.cs #6 comment: "no-ops for this card once ReuseWhenDigivolvingEffect is gone"). The live rule is
// covered by G9-012.LiveAllTurnsReactivation (real EX8_074 through the pump, 3/3 green). Class + resolver
// switch case + the Tfx OptionSkill branch + G9-009 test #5 removed together.


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


/// <summary>(PRIM-P0-flow) An activated "choose one of the following modes" menu (AS-IS UserSelectionManager
/// SetBool/IntSelection). Each available mode is a labeled branch (an existing <see cref="ICardEffect"/>); the
/// selected branch is dispatched recursively by the ActivatedEffectResolver. Modes whose availability predicate
/// returns false are OMITTED from the menu, mirroring the AS-IS conditional <c>selectionElements.Add</c>. The
/// menu is mandatory (pick exactly one of the offered modes). See docs/porting/mode_choice_primitive_design.md.</summary>
public sealed class ModeChoiceEffect : IActivatedCardEffect
{
    /// <summary>One mode: a menu label, an optional availability predicate (null = always available), and the
    /// branch effect run when this mode is chosen.</summary>
    public readonly record struct Mode(string Label, Func<bool>? IsAvailable, ICardEffect Branch);

    private const string ModeToken = "mode";
    private readonly IReadOnlyList<Mode> _modes;

    public ModeChoiceEffect(CardSource card, string description, IReadOnlyList<Mode> modes)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(modes);
        Card = card;
        Description = description;
        _modes = modes;
    }

    public CardSource Card { get; }

    public string Description { get; }

    /// <summary>Modes whose availability predicate passes (null predicate = always available).</summary>
    public IReadOnlyList<Mode> AvailableModes() => _modes.Where(m => m.IsAvailable?.Invoke() ?? true).ToList();

    /// <summary>The mandatory labeled-menu ChoiceRequest — one candidate per available mode (synthetic id
    /// <c>"{inst}#mode#{index}"</c> + the mode's label).</summary>
    public ChoiceRequest BuildRequest(IReadOnlyList<Mode> available)
    {
        var candidates = new List<ChoiceCandidate>(available.Count);
        for (int index = 0; index < available.Count; index++)
        {
            candidates.Add(new ChoiceCandidate(
                new HeadlessEntityId($"{Card.InstanceId.Value}#{ModeToken}#{index}"),
                available[index].Label, ChoiceZone.BattleArea, IsSelectable: true, ownerId: Card.Owner));
        }

        return new ChoiceRequest(
            ChoiceType.ModeChoice, Card.Owner, Description,
            minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.BattleArea, candidates);
    }

    /// <summary>The branch effect for the chosen candidate id (parses the index off <c>"{inst}#mode#{index}"</c>).</summary>
    public ICardEffect BranchFor(IReadOnlyList<Mode> available, HeadlessEntityId selectedId)
    {
        string[] parts = selectedId.Value.Split('#');
        return int.TryParse(parts.Length > 2 ? parts[2] : null, out int index) && index >= 0 && index < available.Count
            ? available[index].Branch
            : available[0].Branch;
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Mode-choice effect is resolved via the activation flow, not registered: {Description}");
}


// (uniform-사멸 flip) `ActivatedSelectEffect` + `SelectDestroyThenTrashSecurityBody` + `SuspendCostReductionEffect`
// DELETED — the uniform ActivatedEffect corpus died consumer-0: the B5 Tfx fixtures were retired, the AD1_025
// re-port dropped the composite body's only construction, and the SuspendCostReduction rule surface is the
// AS-IS inline shape (TfxBeforePayCost/EX8_074 region #1: SelectPermanentEffect(Mode.Custom) +
// SuspendPermanentsClass.Tap + UntilCalculateFixedCostEffect ChangeCostClass; availability = region #2's
// hidden isCheckAvailability ChangeCostClass folded by GetPayingCostWithBaseCost). G1R-001 rows retired.





// (R6-Db) ActivatedSelectBounceAndDiscardSourcesEffect (ST4_16-shaped bounce+source-discard) DELETED —
// consumer-0 in production (no card producer; its factory helper was already removed). Its ONLY remaining
// consumer, the GREEN C3-Witness case (9) "bounce ignores trash-protection", was re-targeted onto the REAL
// substrate this class merely composed: DigivolutionStackHelpers.TrashSourcesAsync(honorProtection:false)
// THEN SelectPermanentEffect(Mode.Bounce), in the same AS-IS HandBounceClaass.Bounce order (discard BEFORE
// bounce). G1R-001 row retired.


/// <summary>(PRIM-P0 B.O.4) A non-interactive one-shot before-pay cost reduction: when this card is being
/// played/digivolved and <see cref="_condition"/> holds, register a one-shot <c>playCostDelta = -amount</c>
/// self modifier tagged <see cref="EffectDuration.UntilCalculateFixedCost"/> (cleared once the cost is locked).
/// The headless mirror of the AS-IS <c>BeforePayCost</c> ActivateClass that does
/// <c>card.Owner.UntilCalculateFixedCostEffect.Add(_ =&gt; changeCostClass)</c> (e.g. BT18_057). Non-interactive
/// counterpart of <see cref="SuspendCostReductionEffect"/>. Reduces THIS play's own cost. See
/// docs/porting/cost_modification_design.md.</summary>
public sealed class BeforePayCostReductionEffect : IActivatedCardEffect
{
    private readonly Func<int> _amount;
    private readonly Func<bool>? _condition;

    public BeforePayCostReductionEffect(CardSource card, Func<int> amount, Func<bool>? condition, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _amount = amount;
        _condition = condition;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    /// <summary>Register the one-shot reduction if the condition holds and the amount is positive.</summary>
    public void Apply()
    {
        if (_condition is not null && !_condition())
        {
            return;
        }

        int amount = _amount();
        if (amount <= 0)
        {
            return;
        }

        // (R6-Da'-3) AS-IS BeforePayCost ActivateClass: card.Owner.UntilCalculateFixedCostEffect.Add(_ =>
        // changeCostClass). Register a ONE-SHOT self cost-reduction ChangeCostClass into the OWNER's
        // UntilCalculateFixedCost bucket (cleared once the play's cost is locked — EffectDurationExpiry.
        // ExpireFixedCostCalc / PlayCardAction). Gated ONLY on `cardSource == this card` (no target-permanent
        // restriction) so it reduces THIS card's cost whether paid as a play OR a digivolution — a given action
        // pays exactly one, so it fires exactly once. Read back by CardSource.GetPayingCostWithBaseCost's
        // GetChangedPayingCost fold. Replaces the INVENTED EffectRegistry NumericModifier binding (which reduced
        // both metrics via separate PlayCost/DigivolutionCost delta keys but bypassed the AS-IS CanUse gate).
        int reduce = amount;
        var changeCostClass = new ChangeCostClass();
        changeCostClass.SetUpICardEffect($"Cost -{reduce}", _ => true, Card);
        changeCostClass.SetUpChangeCostClass(
            changeCostFunc: (cs, cost, root, targetPermanents) => cost - reduce,
            cardSourceCondition: cs => cs == Card,
            rootCondition: root => true,
            isUpDown: () => true,
            isCheckAvailability: () => false,
            isChangePayingCost: () => true);
        new Player(Card.Context, Card.Owner).UntilCalculateFixedCostEffect.Add(_ => changeCostClass);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Before-pay cost reduction is resolved via the activation flow, not registered: {Description}");
}




// (R6-Da'-3) invented granted-continuous BUFF body classes DELETED — `ActivatedTargetBuffEffect` (select-and-buff
// DP/SA) and `ActivatedPlayerScopeBuffEffect` (player/opponent-scope buff). Both registered their duration-tagged
// continuous onto the INVENTED `EffectRegistry.Register(EffectBinding)` bridge and had NO resolver case; their only
// consumers were the 6 zero-call-site buff factory seats (CardEffectFactory `AsUniformActivated` — also deleted).
// D2=A: the live buff behavior is the AS-IS duration-bucket (`CardEffectCommons.ChangeDigimonDP` /
// `ChangeDigimonSAttackPlayerEffect` → AddEffectToPermanent/Player), already re-ported inline into the cards
// (ST1_13/14/08, ST3_11/13/14/15/16, BT2_035/092/097/099). EffectDurationExpiry sweeps the bucket at reset.


// (A6 / ST2.Blue disposal) invented body `ActivatedSelectTrashDigivolutionEffect` DELETED — it reached
// consumer-0: its factory helper (SelectAndTrashDigivolutionEffect) was already gone, its former cards
// (ST2_03/06/09) are re-ported to the inline AS-IS ActivateClass + SelectPermanentEffect Mode.Custom +
// TrashDigivolutionCardsFromTopOrBottom, and its only remaining consumers — the stale ST2.Blue white-box casts —
// were re-targeted onto that live ActivateClass surface (the CardEffect.ST2.Blue suite is now 12/12 truthful
// green). No resolver switch case existed. G1R-001 row RETIRED with this deletion.


/// <summary>(PRIM special-play) AS-IS <c>IDigiBurst</c>: a <c>[Digi-Burst N]</c> effect — trash N of THIS card's
/// OWN digivolution sources as a cost, then resolve <see cref="InnerEffect"/>. Gated on the permanent holding at
/// least <see cref="Count"/> digivolution cards (AS-IS <c>CanDigiBurst</c>). Resolved via the activation flow.</summary>
public sealed class DigiBurstActivatedEffect : IActivatedCardEffect
{
    public DigiBurstActivatedEffect(
        CardSource card, int count, ICardEffect innerEffect, string description,
        EffectTiming grantTiming = EffectTiming.None)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(innerEffect);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        Count = count < 1 ? 1 : count;
        InnerEffect = innerEffect;
        Description = description;
        GrantTiming = grantTiming;
    }

    public CardSource Card { get; }

    public int Count { get; }

    public ICardEffect InnerEffect { get; }

    public string Description { get; }

    /// <summary>(R6-Da'-4 / RD-P6B-6) The live-read timing at which the Digi-Burst body is a CONTINUOUS
    /// keyword-static grant ("This gains &lt;keyword&gt;") — e.g. <see cref="EffectTiming.OnDetermineDoSecurityCheck"/>
    /// for Pierce (the timing <c>NewModelContinuousScan.HasPierce</c> scans). A non-<see cref="EffectTiming.None"/>
    /// value means the resolver registers <see cref="InnerEffect"/> into the permanent's AS-IS duration bucket
    /// (<see cref="CardEffectCommons.AddEffectToPermanent"/>) at this timing rather than resolving it as an
    /// activated body — mirroring the AS-IS card idiom where the Digi-Burst coroutine calls a keyword Gain*
    /// (GainPierce/GainBlocker) with the timing baked in. <see cref="EffectTiming.None"/> = an activated body
    /// (draw/delete/trash), resolved via its coroutine.</summary>
    public EffectTiming GrantTiming { get; }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Digi-Burst effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>(PRIM special-play) AS-IS <c>DNADigivolveWithHandOrTrashCardIntoHandOrTrash</c>
/// (DNADigivolveEffects.cs:256) — an EFFECT-driven DNA Digivolution: DNA-digivolve INTO a card taken from the
/// hand or trash (<see cref="IntoCondition"/>, zone by <see cref="IntoFromHand"/>) by fusing a battle-area
/// permanent (<see cref="PermanentCondition"/>) together with a hand/trash material (<see cref="MaterialCondition"/>,
/// zone by <see cref="MaterialFromHand"/>) under it. Resolved via the activation flow (auto-matched, like the
/// other special plays). EX6_072 / EX11_059.</summary>
public sealed class DnaFromHandOrTrashActivatedEffect : IActivatedCardEffect
{
    public DnaFromHandOrTrashActivatedEffect(
        CardSource card, Func<CardSource, bool> intoCondition, Func<CardSource, bool> permanentCondition,
        Func<CardSource, bool> materialCondition, bool intoFromHand, bool materialFromHand, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(intoCondition);
        ArgumentNullException.ThrowIfNull(permanentCondition);
        ArgumentNullException.ThrowIfNull(materialCondition);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        IntoCondition = intoCondition;
        PermanentCondition = permanentCondition;
        MaterialCondition = materialCondition;
        IntoFromHand = intoFromHand;
        MaterialFromHand = materialFromHand;
        Description = description;
    }

    public CardSource Card { get; }

    public Func<CardSource, bool> IntoCondition { get; }

    public Func<CardSource, bool> PermanentCondition { get; }

    public Func<CardSource, bool> MaterialCondition { get; }

    public bool IntoFromHand { get; }

    public bool MaterialFromHand { get; }

    public string Description { get; }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"DNA-from-hand/trash effect is resolved via the activation flow, not registered: {Description}");
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

            // (2026-07-11 re-review, BT1_088) AS-IS maxCount == -1 is the RevealDeckTopCardsAndProcessForAll
            // shape: EVERY matching revealed card routes to SelectedTo AUTOMATICALLY — no player selection at
            // all (the maxCount is never read). Offering a skippable prompt here invented a decision point
            // ("send the matched Digimon to the deck bottom instead") that AS-IS does not have, polluting the
            // RL action space. Only a POSITIVE maxCount is a real select.
            if (condition.MaxCount < 0)
            {
                foreach (ChoiceCandidate candidate in candidates)
                {
                    if (picked.Add(candidate.Id))
                    {
                        StageMove(sink, candidate.Id, condition.SelectedTo);
                    }
                }

                continue;
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
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId };
        // (F1 reveal-remainder) an unselected revealed card sent to the trash is IsBeingRevealed==true at the
        // trash moment (AS-IS RevealLibrary.cs resets IsBeingRevealed only AFTER TrashRevealedCards runs), so its
        // OnDiscardLibrary broadcast is filtered out by the !IsBeingRevealed gate (WhenDiscardLibrary.cs:23-26).
        // Mirror that by stamping the reveal marker onto the discard so CanTriggerWhenDiscardLibrary rejects it.
        if (destination == RevealDestination.Trash)
        {
            values[MatchStateMutationSink.RevealTrashFlagKey] = true;
        }

        sink.Apply(new EffectMutation(kind, Card.InstanceId, values));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Reveal-and-select effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>
/// (P4) Mirror of the FULL AS-IS <c>CardEffectCommons.RevealDeckTopCardsAndSelect</c> with a
/// <c>SelectCardConditionClass[]</c> (multi-condition, BT10-096/BT10-097/ST17-11 shape,
/// RevealLibrary.cs:229-465): reveal the top N cards, then run each pass SEQUENTIALLY over the shared
/// revealed pool — per-pass maxCount = Min(pass.MaxCount, matching in the CURRENT pool), chosen cards
/// leave the pool, a pass with no matching card is skipped, per-pass destination
/// (<see cref="RevealDestination.Custom"/> = no move, recorded on <see cref="CustomSelections"/> for the
/// card script's follow-up). <c>canNoAction</c> (with ≥2 passes) opens the AS-IS whole-effect opt-out
/// first; <c>mutualConditions</c> mirrors the exact relaxation rule (RevealLibrary.cs:302-308). Remaining
/// cards go to <see cref="RemainingTo"/>; ≥2 deck-bound remainders open the AS-IS ordering pick (bottom =
/// pick order; top = first pick topmost). Driven by the activation <c>ChoiceProvider</c>; all moves staged
/// on the shared sink.
/// </summary>
public sealed class RevealMultiSelectEffect : IActivatedCardEffect
{
    private readonly IReadOnlyList<HeadlessDCGO.Engine.Headless.Runtime.RevealSelectPass> _passes;
    private readonly bool _canNoAction;
    private readonly bool _isOpponentDeck;
    private readonly bool _mutualConditions;
    private readonly List<HeadlessEntityId> _customSelections = new();

    public RevealMultiSelectEffect(
        CardSource card,
        int revealCount,
        IReadOnlyList<HeadlessDCGO.Engine.Headless.Runtime.RevealSelectPass> passes,
        RevealDestination remainingTo,
        string description,
        bool canNoAction = false,
        bool isOpponentDeck = false,
        bool mutualConditions = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(passes);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        RevealCount = revealCount;
        _passes = passes;
        RemainingTo = remainingTo;
        Description = description;
        _canNoAction = canNoAction;
        _isOpponentDeck = isOpponentDeck;
        _mutualConditions = mutualConditions;
    }

    public CardSource Card { get; }

    public int RevealCount { get; }

    public RevealDestination RemainingTo { get; }

    public string Description { get; }

    /// <summary>The cards picked by <see cref="RevealDestination.Custom"/> passes — the card script's
    /// follow-up (e.g. "play it, ignoring its play cost") consumes them after resolution.</summary>
    public IReadOnlyList<HeadlessEntityId> CustomSelections => _customSelections;

    public async Task ResolveAsync(MatchStateMutationSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _customSelections.Clear();
        EngineContext context = Card.Context;
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        HeadlessPlayerId chooser = Card.Owner;
        HeadlessPlayerId revealPlayer = _isOpponentDeck ? Opponent(context, chooser) : chooser;
        if (revealPlayer.IsEmpty)
        {
            return;
        }

        List<HeadlessEntityId> pool = zones.GetCards(revealPlayer, ChoiceZone.Library)
            .Take(Math.Max(0, RevealCount)).ToList();
        if (pool.Count == 0)
        {
            return; // AS-IS: nothing to reveal -> no-op.
        }

        bool doAction = true;
        if (_canNoAction && _passes.Count >= 2)
        {
            // AS-IS whole-effect opt-out (RevealLibrary.cs:264-283): with >=2 conditions and canNoAction,
            // the player first decides whether to select at all; declining sends everything to the
            // remaining-cards place.
            var optOut = new ChoiceRequest(
                ChoiceType.Card, chooser, $"{Description} — select?",
                minCount: 0, maxCount: 1, canSkip: true, ChoiceZone.Library,
                new[] { new ChoiceCandidate(new HeadlessEntityId($"reveal-optout:{Card.InstanceId.Value}"), "Select", ChoiceZone.Library, IsSelectable: true, ownerId: chooser) });
            ChoiceResult optResult = await context.ChoiceProvider.ChooseAsync(optOut, cancellationToken).ConfigureAwait(false);
            doAction = !optResult.IsSkipped && optResult.SelectedIds.Count > 0;
        }

        var chosen = new List<HeadlessEntityId>();
        if (doAction)
        {
            for (int passIndex = 0; passIndex < _passes.Count; passIndex++)
            {
                var pass = _passes[passIndex];
                int matching = pool.Count(pass.Condition);
                if (matching == 0)
                {
                    continue;   // AS-IS: the loop continues to the next condition.
                }

                bool canNoSelect = pass.CanNoSelect;
                // AS-IS mutualConditions (RevealLibrary.cs:302-308): a later pass becomes optional when
                // exactly one card was chosen so far, it also satisfies THIS pass, and pass[0] has no
                // candidates left.
                if (!canNoSelect && _mutualConditions && passIndex > 0 &&
                    chosen.Count == 1 && pass.Condition(chosen[0]) &&
                    !pool.Any(_passes[0].Condition))
                {
                    canNoSelect = true;
                }

                int maxCount = Math.Min(pass.MaxCount, matching);
                int minCount = canNoSelect ? 0 : (pass.CanEndNotMax ? Math.Min(1, maxCount) : maxCount);
                ChoiceCandidate[] candidates = pool
                    .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Library, IsSelectable: pass.Condition(id), ownerId: revealPlayer))
                    .ToArray();
                var request = new ChoiceRequest(
                    ChoiceType.Card, chooser, pass.Message,
                    minCount, maxCount, canSkip: canNoSelect, ChoiceZone.Library, candidates);
                ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
                if (result.IsSkipped)
                {
                    continue;
                }

                foreach (HeadlessEntityId id in result.SelectedIds)
                {
                    if (!pool.Remove(id))
                    {
                        continue;
                    }

                    chosen.Add(id);
                    if (pass.Destination == RevealDestination.Custom)
                    {
                        _customSelections.Add(id);   // no move — the card script's follow-up handles it.
                    }
                    else
                    {
                        StageMove(sink, id, pass.Destination);
                    }
                }
            }
        }

        await HandleRemainingAsync(sink, context, chooser, pool, cancellationToken).ConfigureAwait(false);
    }

    private async Task HandleRemainingAsync(
        MatchStateMutationSink sink, EngineContext context, HeadlessPlayerId chooser,
        List<HeadlessEntityId> remaining, CancellationToken cancellationToken)
    {
        if (remaining.Count == 0)
        {
            return;
        }

        RevealDestination destination = RemainingTo;
        if (destination == RevealDestination.DeckTopOrBottom)
        {
            // AS-IS ReturnRevealedCardsToLibraryTopOrBottom: the controller picks Top vs Bottom first.
            var placeRequest = new ChoiceRequest(
                ChoiceType.Card, chooser, "Place the remaining cards on the top or the bottom of the deck.",
                minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.Library,
                new[]
                {
                    new ChoiceCandidate(new HeadlessEntityId($"reveal-place-top:{Card.InstanceId.Value}"), "Top", ChoiceZone.Library, IsSelectable: true, ownerId: chooser),
                    new ChoiceCandidate(new HeadlessEntityId($"reveal-place-bottom:{Card.InstanceId.Value}"), "Bottom", ChoiceZone.Library, IsSelectable: true, ownerId: chooser),
                });
            ChoiceResult placeResult = await context.ChoiceProvider.ChooseAsync(placeRequest, cancellationToken).ConfigureAwait(false);
            destination = placeResult.SelectedIds.Count > 0 &&
                placeResult.SelectedIds[0].Value.StartsWith("reveal-place-top", StringComparison.Ordinal)
                ? RevealDestination.DeckTop
                : RevealDestination.DeckBottom;
        }

        IReadOnlyList<HeadlessEntityId> ordered = remaining;
        if (remaining.Count >= 2 && destination is RevealDestination.DeckTop or RevealDestination.DeckBottom)
        {
            // AS-IS ReturnRevealedCardsToLibraryBottom/Top: the controller specifies the order — a full
            // sequential pick; pick order = placement order ("lower numbers on top").
            var orderRequest = new ChoiceRequest(
                ChoiceType.Card, chooser, "Specify the order of the remaining cards.",
                minCount: remaining.Count, maxCount: remaining.Count, canSkip: false, ChoiceZone.Library,
                remaining.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Library, IsSelectable: true, ownerId: chooser)).ToArray());
            ChoiceResult orderResult = await context.ChoiceProvider.ChooseAsync(orderRequest, cancellationToken).ConfigureAwait(false);
            if (orderResult.SelectedIds.Count == remaining.Count)
            {
                ordered = orderResult.SelectedIds;
            }
        }

        if (destination == RevealDestination.DeckTop)
        {
            // Top: the FIRST pick ends up topmost — stage in reverse pick order (each top-insert stacks).
            ordered = ordered.Reverse().ToArray();
        }

        foreach (HeadlessEntityId id in ordered)
        {
            StageMove(sink, id, destination);
        }
    }

    private static HeadlessPlayerId Opponent(EngineContext context, HeadlessPlayerId player)
    {
        foreach (HeadlessPlayerId candidate in context.TurnController.Current.PlayerOrder)
        {
            if (!candidate.IsEmpty && candidate != player)
            {
                return candidate;
            }
        }

        return default;
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
        var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId };
        // (F1 reveal-remainder) an unselected revealed card sent to the trash is IsBeingRevealed==true at the
        // trash moment (AS-IS RevealLibrary.cs resets IsBeingRevealed only AFTER TrashRevealedCards runs), so its
        // OnDiscardLibrary broadcast is filtered out by the !IsBeingRevealed gate (WhenDiscardLibrary.cs:23-26).
        // Mirror that by stamping the reveal marker onto the discard so CanTriggerWhenDiscardLibrary rejects it.
        if (destination == RevealDestination.Trash)
        {
            values[MatchStateMutationSink.RevealTrashFlagKey] = true;
        }

        sink.Apply(new EffectMutation(kind, Card.InstanceId, values));
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
// (이연③-b RE-TARGETED) `DestroyPermanentsEffect` DELETED — the test-only IActivatedCardEffect wrapper for
// "directly delete a pre-computed list" is retired. The AS-IS delete path is the DeleteKind sink mutation
// through the centralised immunity / deletion-prevention gate — live via `CardEffectCommons.DestroyPermanent`
// (the AS-IS DestroyPermanentsClass(target).Destroy() sink helper) and SelectPermanentEffect Mode.Destroy. Its
// sole consumer, TfxDestroy, was re-pointed to that AS-IS sink path inline (NewSink + DestroyPermanent +
// FlushAsync) through an ActivateClass coroutine — G9-017 assertions (delete-all + cannotBeDeleted immunity)
// preserved. Class + resolver switch case removed.


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


// (C-Act re-home) TrainingActivatedEffect retired — the invented <Training> firing-half (this ActivateClass
// substitute + the Train mutation + DigivolutionStackHelpers.TrainAsync) is replaced by the AS-IS window path:
// CardEffectFactory.TrainingEffect (KeyWordEffects/Training.cs) resolves through the activated flow directly.


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


// (이연③-b RE-TARGETED) `HatchDigiEggEffect` DELETED — the test-only IActivatedCardEffect wrapper for the
// CanHatch-gated digi-egg hatch is retired. The AS-IS hatch path (the empty-breeding + available-egg guard
// then `ZoneMover.HatchDigitamaAsync`) is live in BT1_089 (its [Main] hatch branch) and TurnStateMachine's
// breeding-phase hatch. Its sole consumer, TfxHatch, was re-pointed to that AS-IS path inline via an
// ActivateClass coroutine (BT1_089 idiom) — G9-018 assertions preserved. Class + resolver switch case removed.


// (이연③-b RE-TARGETED) `PlayCardEffect` DELETED — the test-only IActivatedCardEffect wrapper for a simple
// cost-free play is retired (sibling `PlayThisCardToBattleEffect` was likewise soaked, 이연③-A). The AS-IS
// play path is the PlayCardKind sink mutation, whose handler routes through PlayCardClass.PlayCard()
// (MatchStateMutationSink.ApplyPlayCard, :421-425) — the mechanism live cards use. Its sole consumer,
// TfxPlayCard, was re-pointed to that AS-IS sink path inline (NewSink + PlayCardKind + FlushAsync) through an
// ActivateClass coroutine — G9-019 assertions preserved. Class + resolver switch case removed.


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


// (R6-Da'-5) invented body `ActivatedSelectAndDeDigivolveEffect` DELETED — census-0 producer (only the
// [Obsolete] factory helper `CardEffectFactory.SelectAndDeDigivolveEffect` constructed one, and its only
// consumer was the G9-046 DeDigivolve white-box test). Its resolver case is deleted with it. The de-digivolve
// RULE surface is the shared `MatchStateMutationSink.DeDigivolveKind` mutation, covered live by
// `CardEffectCommons.DeDigivolvePermanent` (C5-witness, EX8_051 ESS) and the SelectDeDigivolve /
// MassDeDigivolveThenConditionalDestroy primitives (BT3_107 / BT3_112).


// (R6-Da'-1) invented body `ActivatedSelectAndPlayEffect` DELETED — 0 consumers after the factory helper
// `SelectAndPlayFromZoneEffect` deletion and the G9-046 SelectAndPlay-case removal. The real AS-IS surface is
// `CardEffectCommons.PlayPermanentCards(..., root)` / SelectCardEffect (Root.Trash/Hand) — live in the
// re-ported corpus (e.g. BT9_081).


/// <summary>(PRIM-P0 B.O.5) The headless mirror of AS-IS <c>CardEffectCommons.PlayOptionCards</c>: select up to
/// <paramref name="maxCount"/> of the owner's Option cards in <c>sourceZone</c> (matching
/// <paramref name="optionPredicate"/>) and PLAY each as a nested effect — trash it, open OnUseOption, and resolve
/// its [Main] (OptionSkill) effects through the SAME activation sink/choice cycle. v1 plays cost-free (the 34-card
/// bulk). See docs/porting/play_option_and_delayed_player_effect_design.md.</summary>
public sealed class PlayOptionCardEffect : IActivatedCardEffect
{
    public PlayOptionCardEffect(CardSource card, ChoiceZone sourceZone, Func<HeadlessEntityId, bool> optionPredicate,
        int maxCount, bool canEndNotMax, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(optionPredicate);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        SourceZone = sourceZone;
        OptionPredicate = optionPredicate;
        MaxCount = maxCount;
        CanEndNotMax = canEndNotMax;
        Description = description;
    }

    public CardSource Card { get; }

    public ChoiceZone SourceZone { get; }

    public Func<HeadlessEntityId, bool> OptionPredicate { get; }

    public int MaxCount { get; }

    public bool CanEndNotMax { get; }

    public string Description { get; }

    /// <summary>The zone-card select for the Option(s) to play (from <see cref="SourceZone"/>).</summary>
    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        // (E3-P1-1 / AS-IS CardEffectCommons.PlayOptionCards, CardEffectCommons.cs:65-68) the effect-driven option
        // play path filters candidates by `!cardSource.CanNotPlayThisOption` BEFORE the select — the same legality
        // gate the hand-play path uses (TurnStateMachine 2076/2533). CanNotPlayThisOption spans regions ①②③
        // (CanNotPlayOptionScan) AND `!MatchColorRequirement` (OptionColorRequirement), so BOTH halves apply here.
        var candidates = ((IZoneStateReader)Card.Context.ZoneMover).GetCards(Card.Owner, SourceZone)
            .Where(OptionPredicate)
            .Where(id => !CanNotPlayOptionScan.CanNotPlay(Card.Context, Card.Owner, id)
                && OptionColorRequirement.Matches(Card.Context, Card.Owner, id))
            .Select(id => EffectChoiceHelpers.Candidate(id, id.Value, SourceZone, isSelectable: true, Card.Owner))
            .ToList();
        int max = Math.Min(MaxCount, candidates.Count);
        return EffectChoiceHelpers.CreatePermanentRequest(Card.Owner, Description, minCount: CanEndNotMax ? 0 : max, maxCount: max, canSkip: CanEndNotMax, candidates);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Play-option effect is resolved via the activation flow, not registered: {Description}");
}


// (R6-Da'-1) invented body `ActivatedSelectFromZoneEffect` DELETED — 0 consumers after the factory helper
// deletions (SelectAndAddToHandFromZone/SelectAndTrashFromZone/SelectAndPutSecurityFromZone) and the
// TfxSelectFollowUp inline-migration. The real AS-IS surface is SelectCardEffect (full 16-param SetUp,
// Mode.AddHand/Discard over Root.Trash/Library/…) — live in the re-ported corpus (e.g. BT2_090, BT10_084).


// (R6-Da'-3) invented `ActivatedTargetRestrictionEffect` (select-and-restrict → registered one duration-tagged
// EffectRegistry restriction binding per pick, scope ContinuousRestrictionGate.Scope) DELETED — census-0. The
// live "select N Digimon, they can't attack/block until <duration>" behavior is the AS-IS inline ActivateClass +
// SelectPermanentEffect(Mode.Custom) whose per-target coroutine runs CardEffectCommons.GainCanNotAttack /
// GainCanNotBlock (AddEffectToPermanent duration bucket, read by ContinuousRestrictionGate via
// NewModelContinuousScan) — already re-ported inline into ST2_14 / ST4_12 / BT1_113. Its CardEffectFactory
// helper (SelectAndRestrictEffect) had no live caller and its resolver case was dead. EffectDurationExpiry sweeps
// the permanent bucket at reset.


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


// (이연③-A DEAD) The mirror-invented `PlayThisCardToBattleEffect` (Tamer [Security] "Play this Tamer",
// ST1_12 / ST2_12 / ST3_12) is DELETED — census-0 producer at HEAD: every printed-card production site was
// re-pointed to the AS-IS PlayCardClass factory shape (CardEffectFactory.cs:929/1295 "Replaces the old
// mirror-invented PlayThisCardToBattleEffect version" → ActivateClass flow). Class + resolver switch case
// removed; only comment/test-comment mentions of the retired name remained.


// (R6-Db D4 EXHAUSTED) The mirror-invented `PlaySelfAtEndOfBattleSecurityEffect` (+ its one-shot
// `PlaySelfAtEndOfBattleTriggerEffect` carrier in TriggeredEffects.cs) is DELETED. It was a parallel
// EffectRegistry OnEndBattle-trigger substitute for the AS-IS `Player.UntilEndBattleEffects` bucket; the real
// AS-IS idiom is now landed directly in CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect
// (UntilEndBattleEffects.Add → OnEndBattle → cost-free play). RD-P6C3-B2 RESOLVED. G1R-001 ledger → 0 rows.


/// <summary>(PRIM-W2 #9) AS-IS <c>Gain2MemoryOptionDelayEffect</c> — the [Main] &lt;Delay&gt; activation: TRASH
/// this card's own battle-area permanent (the Delay option), and ONLY IF it was actually trashed, gain
/// <see cref="Amount"/> memory. Mirrors AS-IS (DeletePeremanentAndProcessAccordingToResult(self) → successProcess
/// AddMemory). Replaces the former stub that mapped it to an UNCONDITIONAL start-of-turn memory gain (wrong
/// trigger AND no self-trash cost).</summary>
public sealed class TrashSelfThenGainMemoryDelayEffect : IActivatedCardEffect
{
    public TrashSelfThenGainMemoryDelayEffect(CardSource card, int amount)
    {
        ArgumentNullException.ThrowIfNull(card);
        Card = card;
        Amount = amount;
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public async Task ResolveAsync(CancellationToken cancellationToken)
    {
        // AS-IS card.PermanentOfThisCard(): only a battle-area permanent (the placed Delay option) can be
        // trashed to activate — no permanent means nothing trashed, hence no gain.
        if (!((IZoneStateReader)Card.Context.ZoneMover).GetCards(Card.Owner, ChoiceZone.BattleArea).Contains(Card.InstanceId))
        {
            return;
        }

        var permanent = new Permanent(Card.Context, Card.InstanceId, Card.Owner);
        await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
            new[] { permanent },
            Card,
            successProcess: async _ =>
            {
                // The self-trash succeeded — gain the memory (turn-relative sign applied by the sink).
                var sink = new MatchStateMutationSink(
                    Card.Context.CardInstanceRepository, Card.Context.LogSink, Card.Context.ZoneMover,
                    Card.Context.MemoryController, Card.Context.EffectRegistry, Card.Context.GameEventQueue, context: Card.Context);
                sink.Apply(new EffectMutation(
                    MatchStateMutationSink.AddMemoryKind, Card.InstanceId,
                    new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount }));
                await sink.FlushAsync().ConfigureAwait(false);
            },
            failureProcess: null,
            cancellationToken).ConfigureAwait(false);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException("Delay-option trash-self-then-gain effect is resolved via the activation flow, not registered.");
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
    private readonly string _cardType;
    private readonly string? _cardName;
    private readonly Func<CardSource, bool>? _canTarget;
    private readonly bool _isOptional;
    private readonly bool _selfStackOnly;

    /// <summary>(K5) <paramref name="cardType"/> selects which under-cards are candidates ("Digimon" for the
    /// ST2_15-style play-from-under; "Tamer" for the MindLink play-back). <paramref name="cardName"/>
    /// optionally narrows to a specific card name (AS-IS PlayMindLinkTamerFromDigivolutionCards).
    /// (G9 / BT3_030) <paramref name="canTarget"/> — when supplied — REPLACES the cardType/cardName filter with an
    /// arbitrary predicate over the under-card (e.g. "own Digimon, Lv≤4, playable"); this flattens the AS-IS
    /// two-level "pick a permanent, then pick one of ITS matching under-cards" select into the outcome-equivalent
    /// single select over the same reachable under-card set. <paramref name="isOptional"/> = the AS-IS
    /// canNoSelect:true ("you may"). <paramref name="selfStackOnly"/> = the AS-IS
    /// <c>card.PermanentOfThisCard().DigivolutionCards</c> scope (only THIS card's own stack, e.g.
    /// PlayMindLinkTamer / BT1_044 "under this card"), vs the default all-owner-Digimon 2-level scope
    /// ("1 of your Digimon", ST2_15).</summary>
    public ActivatedPlayFromUnderEffect(
        CardSource card, string description, string cardType = "Digimon", string? cardName = null,
        Func<CardSource, bool>? canTarget = null, bool isOptional = false, bool selfStackOnly = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardType);
        Card = card;
        Description = description;
        _cardType = cardType;
        _cardName = cardName;
        _canTarget = canTarget;
        _isOptional = isOptional;
        _selfStackOnly = selfStackOnly;
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
        int min = _isOptional ? 0 : max;
        return EffectChoiceHelpers.CreatePermanentRequest(Card.Owner, Description, minCount: min, maxCount: max, canSkip: _isOptional, candidates);
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
            // (PlayMindLinkTamer / BT1_044) selfStackOnly = AS-IS card.PermanentOfThisCard(): restrict to the
            // permanent whose stack THIS card belongs to (its own top or an under-card of it), not every owner Digimon.
            if (_selfStackOnly && top != Card.InstanceId && !stack.UnderCards.Any(u => u.InstanceId == Card.InstanceId))
            {
                continue;
            }

            foreach (StackedCard under in stack.UnderCards)
            {
                if (IsCandidateCard(under.InstanceId))
                {
                    yield return (under.InstanceId, top);
                }
            }
        }
    }

    private bool IsCandidateCard(HeadlessEntityId id)
    {
        // (G9) an explicit predicate REPLACES the cardType/cardName filter (e.g. BT3_030's "own Digimon, Lv≤4,
        // playable" under-card gate).
        if (_canTarget is not null)
        {
            return _canTarget(new CardSource(Card.Context, id, Card.Owner));
        }

        return Card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) && instance is not null
            && Card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) && def is not null
            && string.Equals(def.CardType, _cardType, StringComparison.OrdinalIgnoreCase)
            && (_cardName is null || string.Equals(def.Name, _cardName, StringComparison.OrdinalIgnoreCase))
            // (AS-IS CanSelectCardCondition) the under-card must be playable as a new permanent (cost-free).
            && CardEffectCommons.CanPlayAsNewPermanent(new CardSource(Card.Context, id, Card.Owner), payCost: false, null);
    }

    // (uniform-사멸 flip) explicit IEffectBody half REMOVED — the interface died with the uniform
    // ActivatedEffect corpus; the public BuildRequest/Apply surface is unchanged.

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Play-from-under effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>(BT1_078 / BT3_063 / BT3_070 / BT3_073) Reveal the top <c>_revealCount()</c> library cards, optionally
/// select 1 matching card (<c>_canSelect</c>), place the remaining revealed cards at the deck bottom, then apply
/// the reveal follow-up (<see cref="RevealPlayMode"/>): DigivolveOntoSelf (BT1_078 — costless digivolve onto this
/// card's own permanent) or PlayAsNewPermanent (BT3_063/070/073 — play the selected card as a new permanent,
/// cost-free). 1:1 mirror of the AS-IS SimplifiedRevealDeckTopCardsAndSelect(revealCount, one Custom-mode select
/// maxCount:1 canNoSelect:true, remaining:DeckBottom) followed by the per-card play/digivolve. The digivolve
/// reuses <see cref="FreeDigivolveHelpers.DigivolveFreeAsync"/> (the newly-digivolved top registers its effects);
/// the play reuses <see cref="CardEffectCommons.PlayPermanentCards"/> (root: Library, activateETB:true). The
/// reveal count is a Func to support BT3_073's dynamic count (= the opponent's battle-area Digimon count,
/// re-evaluated at resolve).</summary>
public sealed class RevealSelectThenPlaySelectedEffect : IActivatedCardEffect
{
    private readonly Func<int> _revealCount;
    private readonly Func<HeadlessEntityId, bool> _canSelect;
    private readonly RevealPlayMode _mode;

    public RevealSelectThenPlaySelectedEffect(
        CardSource card, Func<int> revealCount, Func<HeadlessEntityId, bool> canSelect,
        RevealPlayMode mode, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(revealCount);
        ArgumentNullException.ThrowIfNull(canSelect);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _revealCount = revealCount;
        _canSelect = canSelect;
        _mode = mode;
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

        HeadlessPlayerId owner = Card.Owner;
        List<HeadlessEntityId> revealed = zones.GetCards(owner, ChoiceZone.Library).Take(Math.Max(0, _revealCount())).ToList();
        if (revealed.Count == 0)
        {
            return; // AS-IS: nothing to reveal -> no-op (CanActivate already required >=1 library card).
        }

        HeadlessEntityId selected = default;
        List<ChoiceCandidate> candidates = revealed.Where(_canSelect)
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Library, IsSelectable: true, ownerId: owner))
            .ToList();
        if (candidates.Count > 0)
        {
            // AS-IS canNoSelect:true -> the pick is optional (skippable); maxCount 1.
            var request = new ChoiceRequest(
                ChoiceType.Card, owner, Description, minCount: 0, maxCount: 1, canSkip: true, ChoiceZone.Library, candidates);
            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.IsSkipped && result.SelectedIds.Count > 0)
            {
                selected = result.SelectedIds[0];
            }
        }

        // AS-IS remaining-cards place: DeckBottom (every revealed card except the one selected to play/digivolve).
        foreach (HeadlessEntityId id in revealed)
        {
            if (id != selected)
            {
                await context.ZoneMover.MoveToDeckBottomAsync(owner, id, cancellationToken).ConfigureAwait(false);
            }
        }

        if (selected.IsEmpty)
        {
            return;
        }

        switch (_mode)
        {
            case RevealPlayMode.DigivolveOntoSelf:
            {
                // AS-IS: costless single-target digivolve of the selected library card onto this card's own
                // permanent (payCost:false, root:Library).
                bool digivolved = await FreeDigivolveHelpers.DigivolveFreeAsync(
                    context.CardInstanceRepository, context.ZoneMover, selected, Card.InstanceId,
                    ChoiceZone.Library, context.GameEventQueue, cancellationToken).ConfigureAwait(false);
                if (digivolved)
                {
                    CardEffectRegistrar.RegisterCard(context, selected, owner);
                    // (RD-1 / AS-IS CardController.cs:1526-1529) this effect-driven free digivolve IS isEvolution
                    // (PlayCardClass with targetPermanent, root:Library) -> DigivolveCount_ThisTurn++ AND draw 1.
                    // AS-IS reveal is PEEK-ONLY (RevealLibrary.cs:749-790 only sets IsBeingRevealed; the revealed
                    // cards stay physically on the library top). By this point the non-selected revealed cards have
                    // already been sent to the deck BOTTOM (loop above) and the selected card has left the library
                    // via the digivolve, so the isEvolution draw hits the card BELOW the revealed batch -- exactly
                    // as AS-IS. (The earlier deferral premised a card-REMOVAL reveal model that AS-IS never uses;
                    // design item E1-01.)
                    await DigivolveCommons.OnDigivolveCompletedAsync(context, owner, cancellationToken).ConfigureAwait(false);
                }

                break;
            }

            case RevealPlayMode.PlayAsNewPermanent:
            {
                // AS-IS: PlayPermanentCards(selected, payCost:false, isTapped:false, root:Library, activateETB:true).
                await CardEffectCommons.PlayPermanentCards(
                    new[] { new CardSource(context, selected, owner) }, Card, payCost: false, isTapped: false,
                    root: ChoiceZone.Library, activateETB: true).ConfigureAwait(false);
                break;
            }
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Reveal-select-then-play effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>(BT1_084 [When Attacking]) Select exactly 1 matching card (<c>_canSelect</c>) from THIS card's own
/// permanent's digivolution-source stack, return it to the owner's hand, then unsuspend this card. 1:1 mirror
/// of AS-IS SelectCardEffect(root: Custom over selectedPermanent.DigivolutionCards, mode: AddHand, maxCount:
/// Min(1, matching), canNoSelect:()=>false) THEN a per-card self follow-up. The specific-source return reuses
/// <see cref="DigivolutionStackHelpers.PlaySpecificSourceAsync"/> (destination Hand); the follow-up
/// (<paramref name="onSelected"/>) runs afterwards — BT1_084 stages a self-unsuspend on the sink
/// (<see cref="CardEffectCommons.UnsuspendSelf"/>), BT3_112 applies a self GainCanNotBeBlocked (Unblockable)
/// grant to the registry. Both honour the sink/registry immunity + restriction gates.</summary>
public sealed class SelectDigivolutionSourceToHandThenSelfFollowUpEffect : IActivatedCardEffect
{
    private readonly Func<CardSource, bool> _canSelect;
    private readonly bool _isOptional;
    private readonly Action<MatchStateMutationSink> _onSelected;

    public SelectDigivolutionSourceToHandThenSelfFollowUpEffect(
        CardSource card, Func<CardSource, bool> canSelect, bool isOptional,
        Action<MatchStateMutationSink> onSelected, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canSelect);
        ArgumentNullException.ThrowIfNull(onSelected);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _canSelect = canSelect;
        _isOptional = isOptional;
        _onSelected = onSelected;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public async Task ResolveAsync(MatchStateMutationSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        EngineContext context = Card.Context;

        var permanent = new Permanent(context, Card.InstanceId, Card.Owner);
        List<HeadlessEntityId> matching = permanent.DigivolutionCards
            .Where(c => _canSelect(c))
            .Select(c => c.InstanceId)
            .ToList();
        if (matching.Count == 0)
        {
            return; // AS-IS: CanActivate already ensured >=1; defensive.
        }

        var candidates = matching
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.DigivolutionCards, IsSelectable: true, ownerId: Card.Owner))
            .ToList();
        // AS-IS canNoSelect:() => false is the pick's rule ONCE the "you can" is activated; in the auto-firing
        // subject-scoped bridge, the activation optionality (isOptional) is modeled as a skippable request —
        // skipping = declining to activate; selecting = the mandatory pick of exactly 1.
        var request = new ChoiceRequest(
            ChoiceType.Card, Card.Owner, Description, minCount: _isOptional ? 0 : 1, maxCount: 1, canSkip: _isOptional, ChoiceZone.DigivolutionCards, candidates);
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return;
        }

        HeadlessEntityId sourceId = result.SelectedIds[0];
        await DigivolutionStackHelpers.PlaySpecificSourceAsync(
            context.CardInstanceRepository, context.ZoneMover, Card.InstanceId, sourceId, ChoiceZone.Hand, cancellationToken).ConfigureAwait(false);

        _onSelected(sink);
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Select-digivolution-source-to-hand-then-self-follow-up effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>(G8 / BT3_019 [When Digivolving]) OPTIONAL select 1 hand card matching <c>_canTarget</c>, place it
/// on TOP of THIS card's own digivolution stack (AS-IS <c>Permanent.AddDigivolutionCardsTop</c>), then gain
/// <c>_memoryGain</c> memory — only when a card is actually placed. 1:1 mirror of AS-IS SelectHandEffect(Custom,
/// maxCount 1, canNoSelect:true) -> AddDigivolutionCardsTop(selected) -> AddMemory(N). The attach reuses
/// <see cref="DigivolutionStackHelpers.AddSourcesTopAsync"/>; the memory is staged on the sink.</summary>
public sealed class SelectHandAttachToOwnStackThenMemoryEffect : IActivatedCardEffect
{
    private readonly Func<CardSource, bool> _canTarget;
    private readonly int _memoryGain;

    public SelectHandAttachToOwnStackThenMemoryEffect(CardSource card, Func<CardSource, bool> canTarget, int memoryGain, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _canTarget = canTarget;
        _memoryGain = memoryGain;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public async Task ResolveAsync(MatchStateMutationSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        EngineContext context = Card.Context;
        var zones = (IZoneStateReader)context.ZoneMover;

        List<HeadlessEntityId> candidates = zones.GetCards(Card.Owner, ChoiceZone.Hand)
            .Where(id => _canTarget(new CardSource(context, id, Card.Owner)))
            .ToList();
        if (candidates.Count == 0)
        {
            return; // AS-IS CanActivate guards >=1; defensive.
        }

        var choiceCandidates = candidates
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true, ownerId: Card.Owner))
            .ToList();
        // AS-IS canNoSelect:true (the "you may") -> optional pick of exactly 1.
        var request = new ChoiceRequest(
            ChoiceType.Card, Card.Owner, Description, minCount: 0, maxCount: 1, canSkip: true, ChoiceZone.Hand, choiceCandidates);
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return;
        }

        await DigivolutionStackHelpers.AddSourcesTopAsync(
            context.CardInstanceRepository, context.ZoneMover, Card.InstanceId,
            new[] { result.SelectedIds[0] }, ChoiceZone.Hand, cancellationToken,
            context: context,
            // (F1-Tier2 OnAddDigivolutionCards) effect place-under (top) — this card's own effect is the cause.
            gameEventQueue: context.GameEventQueue, causeSourceId: Card.InstanceId).ConfigureAwait(false);

        if (_memoryGain != 0)
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.AddMemoryKind, Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = _memoryGain }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Select-hand-attach-to-own-stack effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>(G10 / BT3_107 [Main]) Select 1 <c>_canTarget</c>-matching permanent, trigger De-Digivolve
/// <c>_count</c> on it, then — reading the RESULTING (post-de-digivolve) top — destroy it when
/// <c>_destroyIf</c> holds. 1:1 mirror of AS-IS SelectPermanentEffect(Custom, 1) -> IDegeneration(count) ->
/// (if new TopCard predicate) DestroyPermanentsClass. The de-digivolve runs directly (a flush boundary) so the
/// new top's cost/DP is observable before the destroy decision. The permanent's top id changes on de-digivolve
/// (old top trashed, immediate under-source promoted); the post-state permanent is identified by that promoted
/// id (<c>under[Count - removed]</c>). The destroy is staged on the sink (immunity/prevention gates apply).</summary>
public sealed class SelectDeDigivolveThenConditionalDestroyEffect : IActivatedCardEffect
{
    private readonly Func<HeadlessEntityId, bool> _canTarget;
    private readonly int _count;
    private readonly Func<Permanent, bool> _destroyIf;

    public SelectDeDigivolveThenConditionalDestroyEffect(
        CardSource card, Func<HeadlessEntityId, bool> canTarget, int count, Func<Permanent, bool> destroyIf, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentNullException.ThrowIfNull(destroyIf);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _canTarget = canTarget;
        _count = count;
        _destroyIf = destroyIf;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public async Task ResolveAsync(MatchStateMutationSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        EngineContext context = Card.Context;
        List<HeadlessEntityId> candidates = CardEffectCommons.MatchConditionPermanentIds(Card, _canTarget).ToList();
        if (candidates.Count == 0)
        {
            return; // AS-IS CanActivate guards >=1; defensive.
        }

        var choiceCandidates = candidates
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: DeDigivolveDestroyHelpers.OwnerOf(context, id)))
            .ToList();
        var request = new ChoiceRequest(
            ChoiceType.Card, Card.Owner, Description, minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.BattleArea, choiceCandidates);
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return;
        }

        HeadlessEntityId selectedId = result.SelectedIds[0];
        HeadlessPlayerId targetOwner = DeDigivolveDestroyHelpers.OwnerOf(context, selectedId);

        // Predict the post-de-digivolve top (immediate under-source) BEFORE the move, then de-digivolve directly
        // (a flush boundary). under-cards are bottom→top, so removing N promotes under[Count - N].
        IReadOnlyList<CardSource> under = new Permanent(context, selectedId, targetOwner).DigivolutionCards;
        // (b-remediation) AS-IS ImmuneFromDeDigivolve() — a target with a continuous de-digivolve immunity is skipped.
        int removed = DeDigivolveHelpers.IsDeDigivolveImmune(context, selectedId)
            ? 0
            : await DeDigivolveHelpers.DeDigivolveAsync(
                context.CardInstanceRepository, context.ZoneMover, selectedId, _count, context.GameEventQueue, cancellationToken).ConfigureAwait(false);
        HeadlessEntityId newTopId = removed > 0 && removed <= under.Count ? under[under.Count - removed].InstanceId : selectedId;

        var postPermanent = new Permanent(context, newTopId, targetOwner);
        if (_destroyIf(postPermanent))
        {
            CardEffectCommons.DestroyPermanent(sink, Card, newTopId);
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Select-de-digivolve-then-conditional-destroy effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>(G10 / BT3_112 [When Digivolving]) Trigger De-Digivolve <c>_count</c> on EVERY
/// <c>_deDigivolveTarget</c>-matching permanent, then — re-scanning the resulting tops — destroy EVERY
/// <c>_destroyTarget</c>-matching permanent. 1:1 mirror of AS-IS's TWO INDEPENDENT scans: a de-digivolve loop
/// over <c>Enemy.GetBattleAreaDigimons()</c> gated by <c>!TopCard.CanNotBeAffected</c> (DP-INDEPENDENT), THEN
/// <c>Enemy.GetBattleAreaDigimons().Where(CanSelectPermanentCondition)</c> where CanSelectPermanentCondition =
/// opp battle Digimon &amp;&amp; <c>DP ≤ MaxDP_DeleteEffect(5000)</c> &amp;&amp; <c>CanBeDestroyedBySkill</c> &amp;&amp;
/// <c>!TopCard.CanNotBeAffected</c>. The de-digivolve and destroy predicates are DISTINCT and each is scanned
/// independently — a high-DP Digimon that fails the destroy predicate is STILL de-digivolved. The destroy re-scan
/// reads post-de-digivolve DP/top state (the de-digivolve runs directly = flush boundary). Each destroy stages on
/// the sink (immunity/deletion-prevention gates apply). The two predicates are supplied by the card wiring so the
/// AS-IS split is enforced structurally (not folded into one).</summary>
public sealed class MassDeDigivolveThenConditionalDestroyEffect : IActivatedCardEffect
{
    private readonly Func<HeadlessEntityId, bool> _deDigivolveTarget;
    private readonly int _count;
    private readonly Func<HeadlessEntityId, bool> _destroyTarget;

    public MassDeDigivolveThenConditionalDestroyEffect(
        CardSource card, Func<HeadlessEntityId, bool> deDigivolveTarget, int count,
        Func<HeadlessEntityId, bool> destroyTarget, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(deDigivolveTarget);
        ArgumentNullException.ThrowIfNull(destroyTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _deDigivolveTarget = deDigivolveTarget;
        _count = count;
        _destroyTarget = destroyTarget;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public async Task ResolveAsync(MatchStateMutationSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        EngineContext context = Card.Context;

        // AS-IS scan #1 — De-Digivolve each de-digivolve-target permanent (!TopCard.CanNotBeAffected, DP-independent);
        // their top ids change as this proceeds.
        foreach (HeadlessEntityId id in CardEffectCommons.MatchConditionPermanentIds(Card, _deDigivolveTarget))
        {
            // (b-remediation) AS-IS ImmuneFromDeDigivolve() — skip a target with a continuous de-digivolve immunity.
            if (DeDigivolveHelpers.IsDeDigivolveImmune(context, id))
            {
                continue;
            }

            await DeDigivolveHelpers.DeDigivolveAsync(
                context.CardInstanceRepository, context.ZoneMover, id, _count, context.GameEventQueue, cancellationToken).ConfigureAwait(false);
        }

        // AS-IS scan #2 — re-scan the SEPARATE destroy predicate over the post-de-digivolve tops and destroy each.
        foreach (HeadlessEntityId id in CardEffectCommons.MatchConditionPermanentIds(Card, _destroyTarget))
        {
            CardEffectCommons.DestroyPermanent(sink, Card, id);
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Mass-de-digivolve-then-conditional-destroy effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>(G12 / BT3_100 Part A [Main]) Choose a single count (<c>_canNoSelect ? 0 : 1</c>..<c>_maxCount</c>), then
/// trash that many digivolution cards (from the TOP or BOTTOM per <c>_fromBottom</c>) from EVERY
/// <c>_target</c>-matching permanent that has ≥1 TRASHABLE (non-protected) under-card — each capped at its own
/// digivolution-card count. 1:1 mirror of AS-IS SelectCountEffect(MaxCount, CanNoSelect) -> foreach eligible
/// target: TrashDigivolutionCardsFromTopOrBottom(Min(count, DigivolutionCards.Count)). Fidelity notes:
/// (1) AS-IS eligibility (BT3_100:55) requires <c>Count(!CanNotTrashFromDigivolutionCards) >= 1</c> — mirrored by
/// <see cref="CardEffectCommons.HasTrashableDigivolutionCards"/> (a permanent with only trash-protected sources
/// is NOT a target, so if it were the sole candidate no count prompt appears). (2) AS-IS
/// <c>CanNoSelect:false</c> EXCLUDES 0 from the count candidates (SelectCountEffect.cs:79-90). The per-source
/// trash-protection skip itself lives in the trash path (RemoveSourcesAsync, mirroring ITrashDigivolutionCards).
/// The target set is snapshotted BEFORE the count choice (AS-IS gathers selectedPermanents first).</summary>
public sealed class ChooseCountThenTrashDigivolutionEffect : IActivatedCardEffect
{
    private readonly Func<HeadlessEntityId, bool> _target;
    private readonly int _maxCount;
    private readonly bool _fromBottom;
    private readonly bool _canNoSelect;

    public ChooseCountThenTrashDigivolutionEffect(
        CardSource card, Func<HeadlessEntityId, bool> target, int maxCount, bool fromBottom, bool canNoSelect, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _target = target;
        _maxCount = maxCount;
        _fromBottom = fromBottom;
        _canNoSelect = canNoSelect;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public async Task ResolveAsync(MatchStateMutationSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        EngineContext context = Card.Context;

        // Snapshot the targets (AS-IS collects selectedPermanents before asking the count). Eligibility mirrors
        // AS-IS `DigivolutionCards.Count(!CanNotTrashFromDigivolutionCards) >= 1` (trash-protected-only stacks excluded).
        List<HeadlessEntityId> targets = CardEffectCommons.MatchConditionPermanentIds(Card, _target)
            .Where(id => CardEffectCommons.HasTrashableDigivolutionCards(Card, id))
            .ToList();
        if (targets.Count == 0)
        {
            return;
        }

        // AS-IS: CanNoSelect:false excludes 0 from the count candidates -> minimum 1.
        var request = new ChoiceRequest(
            ChoiceType.Count, Card.Owner, Description, minCount: _canNoSelect ? 0 : 1, maxCount: _maxCount, canSkip: false,
            ChoiceZone.None, Array.Empty<ChoiceCandidate>());
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        int count = result.SelectedCount ?? 0;
        if (count <= 0)
        {
            return;
        }

        foreach (HeadlessEntityId id in targets)
        {
            int available = new Permanent(context, id, DeDigivolveDestroyHelpers.OwnerOf(context, id)).DigivolutionCards.Count;
            int trash = Math.Min(count, available);
            if (trash <= 0)
            {
                continue;
            }

            sink.Apply(new EffectMutation(
                MatchStateMutationSink.TrashDigivolutionCardsKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = id.Value,
                    [MatchStateMutationSink.CountKey] = trash,
                    [MatchStateMutationSink.FromBottomKey] = _fromBottom,
                }));
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Choose-count-then-trash-digivolution effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>(G13 / BT3_102 [Main]) Ask the OPPONENT a binary yes/no decision (<c>_yesLabel</c> / <c>_noLabel</c>)
/// and branch: <c>_ifYes</c> when they choose yes, <c>_ifNo</c> when they choose no. When <c>_autoNoWhen</c>
/// holds (e.g. the opponent has no security to trash) the choice is skipped and <c>_ifNo</c> runs directly. 1:1
/// mirror of AS-IS UserSelectionManager.SetBoolSelection(selectPlayer: card.Owner.Enemy) -> branch (or
/// SetBool(false) when there is nothing to decide). The binary menu reuses the ModeChoice mechanism but is owned
/// by the OPPONENT (the ModeChoice primitive is owner-scoped); the branch actions stage on the sink.</summary>
public sealed class OpponentBinaryChoiceEffect : IActivatedCardEffect
{
    private const string ModeToken = "mode";
    private readonly string _yesLabel;
    private readonly string _noLabel;
    private readonly Action<MatchStateMutationSink> _ifYes;
    private readonly Action<MatchStateMutationSink> _ifNo;
    private readonly Func<bool>? _autoNoWhen;

    public OpponentBinaryChoiceEffect(
        CardSource card, string description, string yesLabel, string noLabel,
        Action<MatchStateMutationSink> ifYes, Action<MatchStateMutationSink> ifNo, Func<bool>? autoNoWhen = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(yesLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(noLabel);
        ArgumentNullException.ThrowIfNull(ifYes);
        ArgumentNullException.ThrowIfNull(ifNo);
        Card = card;
        Description = description;
        _yesLabel = yesLabel;
        _noLabel = noLabel;
        _ifYes = ifYes;
        _ifNo = ifNo;
        _autoNoWhen = autoNoWhen;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public async Task ResolveAsync(MatchStateMutationSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        // AS-IS SetBool(false) when there is nothing to decide -> the "no" branch runs with no prompt.
        if (_autoNoWhen is not null && _autoNoWhen())
        {
            _ifNo(sink);
            return;
        }

        HeadlessPlayerId opponent = CardEffectCommons.OpponentOf(Card);
        var candidates = new List<ChoiceCandidate>
        {
            new(new HeadlessEntityId($"{Card.InstanceId.Value}#{ModeToken}#0"), _yesLabel, ChoiceZone.BattleArea, IsSelectable: true, ownerId: opponent),
            new(new HeadlessEntityId($"{Card.InstanceId.Value}#{ModeToken}#1"), _noLabel, ChoiceZone.BattleArea, IsSelectable: true, ownerId: opponent),
        };
        var request = new ChoiceRequest(
            ChoiceType.ModeChoice, opponent, Description, minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.BattleArea, candidates);
        ChoiceResult result = await Card.Context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);

        int index = 1; // default to the "no" branch (a skipped/empty answer = declined).
        if (!result.IsSkipped && result.SelectedIds.Count > 0)
        {
            string[] parts = result.SelectedIds[0].Value.Split('#');
            if (parts.Length > 2 && int.TryParse(parts[2], out int parsed))
            {
                index = parsed;
            }
        }

        if (index == 0)
        {
            _ifYes(sink);
        }
        else
        {
            _ifNo(sink);
        }
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Opponent-binary-choice effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>(BT1_056 [On Play]) Select up to <c>maxCount</c> of the owner's cards spanning MULTIPLE candidate
/// zones (Hand ∪ Trash) matching <c>_canTarget</c>, then play each as a new permanent COST-FREE from its OWN
/// origin zone. 1:1 mirror of AS-IS "you may play 1 [X] from your hand or recycle bin" — a single logical
/// select over a combined candidate pool where each candidate carries its own source zone into the play
/// mutation (the AS-IS "from hand / from trash" zone prompt is UI sugar; the outcome set is identical).
/// Generalises <see cref="ActivatedSelectAndPlayEffect"/> (a single fixed fromZone) to a multi-zone pool.</summary>
public sealed class ActivatedSelectAndPlayFromZonesEffect : IActivatedCardEffect
{
    private readonly IReadOnlyList<ChoiceZone> _fromZones;
    private readonly Func<HeadlessEntityId, bool> _canTarget;
    private readonly int _maxCount;
    private readonly bool _canEndNotMax;

    public ActivatedSelectAndPlayFromZonesEffect(
        CardSource card, IReadOnlyList<ChoiceZone> fromZones, Func<HeadlessEntityId, bool> canTarget,
        int maxCount, bool canEndNotMax, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(fromZones);
        ArgumentNullException.ThrowIfNull(canTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _fromZones = fromZones;
        _canTarget = canTarget;
        _maxCount = maxCount;
        _canEndNotMax = canEndNotMax;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    private ChoiceZone? ZoneOf(IZoneStateReader zones, HeadlessEntityId id)
    {
        foreach (ChoiceZone zone in _fromZones)
        {
            if (zones.GetCards(Card.Owner, zone).Contains(id))
            {
                return zone;
            }
        }

        return null;
    }

    public ChoiceRequest BuildRequest(IEnumerable<HeadlessPlayerId> players)
    {
        var zones = (IZoneStateReader)Card.Context.ZoneMover;
        var candidates = new List<ChoiceCandidate>();
        foreach (ChoiceZone zone in _fromZones)
        {
            foreach (HeadlessEntityId id in zones.GetCards(Card.Owner, zone).Where(_canTarget))
            {
                candidates.Add(EffectChoiceHelpers.Candidate(id, id.Value, zone, isSelectable: true, Card.Owner));
            }
        }

        int max = Math.Min(_maxCount, candidates.Count);
        return EffectChoiceHelpers.CreatePermanentRequest(Card.Owner, Description, minCount: _canEndNotMax ? 0 : max, maxCount: max, canSkip: _canEndNotMax, candidates);
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(selected);
        var zones = (IZoneStateReader)Card.Context.ZoneMover;
        foreach (HeadlessEntityId id in selected)
        {
            if (id.IsEmpty)
            {
                continue;
            }

            ChoiceZone fromZone = ZoneOf(zones, id) ?? _fromZones[0];
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.PlayCardKind,
                Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = id.Value,
                    [MatchStateMutationSink.FromZoneKey] = fromZone.ToString(),
                }));
        }
    }

    // (uniform-사멸 flip) explicit IEffectBody half REMOVED — the interface died with the uniform
    // ActivatedEffect corpus; the public BuildRequest/Apply surface is unchanged.

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Multi-zone select-and-play effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>(BT1_087 [On Play]) Look at your security stack, select exactly 1 card in it and add it to your
/// hand; if that card has <c>_recoveryColor</c>, &lt;Recovery +1 (Deck)&gt;; then shuffle your security stack.
/// 1:1 mirror of AS-IS SelectCardEffect(root:Security, mode:AddHand, maxCount:Min(1,count), canNoSelect:()=>
/// false) with an AfterSelect color-gated IRecovery(owner,1) keyed off the SPECIFIC selected card, followed by
/// an unconditional RandomUtility.ShuffledDeckCards(SecurityCards). Add-to-hand / recovery / shuffle are staged
/// on the sink so they flush in that AS-IS order (the recovered card is shuffled in).</summary>
public sealed class SecuritySelectToHandColorRecoveryShuffleEffect : IActivatedCardEffect
{
    private readonly string _recoveryColor;

    public SecuritySelectToHandColorRecoveryShuffleEffect(CardSource card, string recoveryColor, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryColor);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _recoveryColor = recoveryColor;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public async Task ResolveAsync(MatchStateMutationSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sink);
        EngineContext context = Card.Context;
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return;
        }

        HeadlessPlayerId owner = Card.Owner;
        List<HeadlessEntityId> security = zones.GetCards(owner, ChoiceZone.Security).ToList();
        if (security.Count == 0)
        {
            return; // AS-IS: CanActivate already required >=1 security card; defensive.
        }

        var candidates = security
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Security, IsSelectable: true, ownerId: owner))
            .ToList();
        // AS-IS canNoSelect:() => false -> mandatory pick of exactly 1.
        var request = new ChoiceRequest(
            ChoiceType.Card, owner, Description, minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.Security, candidates);
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return;
        }

        HeadlessEntityId selected = result.SelectedIds[0];
        // Add the selected security card to hand (moves it out of the security stack).
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnToHandKind, Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = selected.Value }));

        // If the added card matches the recovery color, <Recovery +1 (Deck)> (top library card -> security top).
        if (new CardSource(context, selected, owner).HasCardColor(_recoveryColor))
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.RecoverKind, Card.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.PlayerIdKey] = owner.Value,
                    [MatchStateMutationSink.CountKey] = 1,
                }));
        }

        // Then shuffle the security stack (deferred; flushes after the add-to-hand + recovery moves).
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ShuffleSecurityKind, Card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.PlayerIdKey] = owner.Value }));
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Security-select color-recovery-shuffle effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>(G4) Atomic "draw N, THEN discard M from your hand" — the AS-IS DrawClass→then→discard coroutine.
/// A thin activated wrapper over <see cref="CardEffectCommons.DrawAndDiscardCards"/> (which already draws AND
/// flushes BEFORE building the discard candidate pool from the resulting hand, so the drawn cards are
/// discardable — the atomicity a split draw+select cannot give). Both player roles = the card's owner.
/// <paramref name="canNoSelect"/> = the discard is optional ("you may"); <paramref name="canEndNotMax"/> = the
/// discard is "up to M" (min 1) rather than exactly M. Resolved via the activation flow (needs the live
/// ChoiceProvider). e.g. BT3_006 [On Deletion] draw 1 then discard 1 (mandatory); BT3_088 [When Digivolving]
/// draw 2 then discard up to 2 (canEndNotMax:true).</summary>
public sealed class ActivatedDrawThenDiscardEffect : IActivatedCardEffect
{
    private readonly int _drawAmount;
    private readonly int _trashAmount;
    private readonly Func<CardSource, bool>? _canTrash;
    private readonly bool _canNoSelect;
    private readonly bool _canEndNotMax;

    public ActivatedDrawThenDiscardEffect(
        CardSource card, int drawAmount, int trashAmount, Func<CardSource, bool>? canTrash,
        bool canNoSelect, bool canEndNotMax, string description)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Card = card;
        _drawAmount = drawAmount;
        _trashAmount = trashAmount;
        _canTrash = canTrash;
        _canNoSelect = canNoSelect;
        _canEndNotMax = canEndNotMax;
        Description = description;
    }

    public CardSource Card { get; }

    public string Description { get; }

    public Task ResolveAsync(CancellationToken cancellationToken) =>
        CardEffectCommons.DrawAndDiscardCards(
            (Card.Owner, Card.Owner), _drawAmount, _trashAmount, Card,
            _canTrash, _canNoSelect, _canEndNotMax, cancellationToken);

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Draw-then-discard effect is resolved via the activation flow, not registered: {Description}");
}


/// <summary>
/// The runtime seam: builds a card's effect bindings (across the given timings) and registers them into
/// the EffectRegistry. Call when a card enters play. Returns the registered bindings for inspection.
/// </summary>
/// <summary>(G5 / BT3_031 / BT3_111) A continuous "while <c>_condition</c>, digivolving THIS card (matching
/// <c>_cardCondition</c>) from a root matching <c>_rootCondition</c> onto a target permanent matching
/// <c>_permanentCondition</c> costs <c>_changeValue</c> (±, or SET when <c>_setFixedCost</c>)". 1:1 mirror of
/// AS-IS <c>ChangeDigivolutionCostStaticEffect(changeValue, permanentCondition, cardCondition, rootCondition,
/// condition, setFixedCost)</c> registered at <c>EffectTiming.None</c>. Unlike the scalar self-modifier overload
/// (which the continuous registrar lowers for BATTLE-AREA cards only), the AS-IS target of this shape is a card
/// IN HAND (<c>condition = card in owner's hand</c>). Headless does not register continuous statics for hand
/// cards, so this effect is read DISPATCH-FIRST off the moving card at digivolution-cost resolution
/// (<see cref="CollectOwnGatedModifiers"/>, called by <c>ContinuousModifierGate.ResolveDigivolutionCost</c>) — the
/// same zone-independent dispatch idiom as <c>CardSource.LinkConditionOf</c>/<c>AppFusionConditionOf</c>. It is an
/// <see cref="IActivatedCardEffect"/> only in the "not auto-registered; resolved elsewhere" sense (the registrar
/// skips it; the activated flow never routes an <c>EffectTiming.None</c> effect).</summary>
public sealed class DigivolutionCostGateEffect : IActivatedCardEffect
{
    private readonly int _changeValue;
    private readonly Func<Permanent, bool> _permanentCondition;
    private readonly Func<CardSource, bool> _cardCondition;
    private readonly Func<ChoiceZone, bool>? _rootCondition;
    private readonly Func<bool>? _condition;
    private readonly bool _setFixedCost;

    public DigivolutionCostGateEffect(
        CardSource card, int changeValue, Func<Permanent, bool> permanentCondition,
        Func<CardSource, bool> cardCondition, Func<ChoiceZone, bool>? rootCondition,
        Func<bool>? condition, bool setFixedCost)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(permanentCondition);
        ArgumentNullException.ThrowIfNull(cardCondition);
        Card = card;
        _changeValue = changeValue;
        _permanentCondition = permanentCondition;
        _cardCondition = cardCondition;
        _rootCondition = rootCondition;
        _condition = condition;
        _setFixedCost = setFixedCost;
    }

    public CardSource Card { get; }

    // Whether this gate applies to the given digivolution (moving card, its root zone, the target FROM permanent).
    private bool Applies(CardSource movingCard, ChoiceZone root, Permanent targetPermanent)
    {
        if (_condition is not null && !_condition())
        {
            return false;
        }

        return _cardCondition(movingCard)
            && (_rootCondition is null || _rootCondition(root))
            && _permanentCondition(targetPermanent);
    }

    private NumericModifier ToModifier(HeadlessEntityId movingCardId)
    {
        string id = $"{Card.InstanceId.Value}:digivolveCostGate:{movingCardId.Value}";
        return _setFixedCost
            ? NumericModifier.Set(id, NumericModifierMetric.DigivolutionCost, _changeValue)
            : NumericModifier.Add(id, NumericModifierMetric.DigivolutionCost, _changeValue);
    }

    /// <summary>Dispatch-first collection of the MOVING card's own gated digivolution-cost deltas for a digivolve
    /// (moving card -> <paramref name="targetPermanentId"/>). Returns the matching modifiers to fold into the cost;
    /// empty when the moving card declares none / none apply. Zone-independent (works for a card in hand).</summary>
    public static IReadOnlyList<NumericModifier> CollectOwnGatedModifiers(
        EngineContext context, HeadlessEntityId movingCardId, HeadlessEntityId targetPermanentId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (movingCardId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(movingCardId, out CardInstanceRecord? inst) || inst is null
            || !context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? entity) || entity is null)
        {
            return Array.Empty<NumericModifier>();
        }

        HeadlessPlayerId owner = inst.OwnerId;
        var movingCard = new CardSource(context, movingCardId, owner);
        ChoiceZone root = CurrentZone(context, owner, movingCardId);
        var targetPermanent = new Permanent(context, targetPermanentId, owner);

        List<NumericModifier>? modifiers = null;
        foreach (ICardEffect effect in entity.CardEffects(EffectTiming.None, movingCard))
        {
            if (effect is DigivolutionCostGateEffect gate && gate.Applies(movingCard, root, targetPermanent))
            {
                (modifiers ??= new List<NumericModifier>()).Add(gate.ToModifier(movingCardId));
            }
        }

        return (IReadOnlyList<NumericModifier>?)modifiers ?? Array.Empty<NumericModifier>();
    }

    private static ChoiceZone CurrentZone(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId cardId)
    {
        var zones = (IZoneStateReader)context.ZoneMover;
        foreach (KeyValuePair<ChoiceZone, IReadOnlyList<HeadlessEntityId>> pair in zones.Snapshot(owner))
        {
            if (pair.Value.Contains(cardId))
            {
                return pair.Key;
            }
        }

        return ChoiceZone.None;
    }

    public EffectBinding ToBinding(string effectId) =>
        throw new NotSupportedException($"Digivolution-cost gate is read dispatch-first at cost resolution, not registered: {Card.InstanceId.Value}");
}

