// F1-Tier1: OnAddHand / OnAddSecurity activated-bridge expansion.
//
// AS-IS OnAddHand: CardObjectController.AddHandCards(list, isDraw, cardEffect) fires ONE
// StackSkillInfos(hashtable {Players = distinct owners, CardEffect, CardSources = the whole added LIST}, OnAddHand)
// per hand-add (:559,616). Gate CanTriggerOnHandAdded / CanTriggerWhenAddHand is PLAYER-SCOPE and (for the real
// cards) requires CardEffect != null — so a turn/mulligan draw (cardEffect=null) never fires. N cards added in one
// call = ONE fire. AS-IS OnAddSecurity: IAddSecurity.AddSecurity fires ONE StackSkillInfos(hashtable {Player,
// CardSources=[that one card]}, OnAddSecurity) per SINGLE card (CardController.cs:5478) — PER-CARD, NOT a batch.
//
// This F1-Tier1 change REGISTERS both timings in ActivatedBridgeTimings.EventBroadcast; derives them from a ->Hand /
// ->Security CardMoved (TriggerTimingMap); stamps a shared ADD-HAND batch id + CAUSE effect id on the effect-driven
// draw / return-to-hand (so OnAddHand collapses N adds to one fire and enforces CardEffect != null); and leaves
// OnAddSecurity UNCOLLAPSED (per-card, matching AS-IS's per-IAddSecurity fire).
//
// END-TO-END bridge tests (zone move + GameFlowProcessor.RunToStableAsync).
//
// Witnesses (REAL cards, dispatched by class-name = card-number):
//   * BT15_083 (BT15/Blue): [Your Turn] "when one of YOUR Digimon's effects adds cards to your hand, by suspending
//     this Tamer, gain 1 memory" — SuspendSelfAndGainMemoryBody, SELF-scope, exercises the OnAddHand cause gate
//     (owner + Digimon-effect).
//   * BT8_090 (BT8/Yellow): [Your Turn] "when a card is added to your security, you may suspend this Tamer to gain
//     1 memory" — SuspendSelfAndGainMemoryBody, SELF-scope OnAddSecurity (no cause).
// Uncapped fixture (TfxOnAddCounter) proves the batch model the single-fire real cards would mask: OnAddHand
// collapse (+1 for N in one batch, +2 for two batches, +0 for a non-effect/opponent add) and OnAddSecurity
// PER-CARD (+N for N adds — no collapse).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // controls the witness / reactor (turn player)
HeadlessPlayerId P2 = new(2);   // the opponent

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT15_083 fires: own-Digimon-effect hand add gains +1 (suspend+memory) via OnAddHand", Bt15FiresOnOwnDigimonEffectHandAdd),
    ("BT15_083 cause-owner negative: an OPPONENT-owned effect adding to your hand does NOT fire", Bt15OpponentCauseRejected),
    ("BT15_083 cause-type negative: a TAMER-effect (non-Digimon) hand add does NOT fire", Bt15NonDigimonCauseRejected),
    ("BT15_083 CardEffect!=null negative: a NON-effect hand add (no cause id) does NOT fire", Bt15NonEffectRejected),
    ("BT8_090 fires: own-security add gains +1 (suspend+memory) via OnAddSecurity", Bt8FiresOnOwnSecurityAdd),
    ("BT8_090 player-scope negative: adding to the OPPONENT'S security does NOT fire", Bt8OpponentSecurityRejected),
    ("OnAddHand batch collapse: an effect DRAWING 2 cards in ONE batch fires the uncapped reactor ONCE (+1)", HandDrawBatchCollapseUncapped),
    ("OnAddHand batch collapse: RETURNING 2 cards to hand in ONE batch fires the uncapped reactor ONCE (+1)", HandReturnBatchCollapseUncapped),
    ("OnAddHand independent batches: two SEPARATE hand-add batches fire the uncapped reactor TWICE (+2)", HandTwoIndependentBatches),
    ("OnAddHand cause negative: a non-effect (no cause) hand add fires the uncapped reactor +0", HandNonEffectNoFire),
    ("OnAddHand player-scope negative: adding to the OPPONENT'S hand fires the P1 uncapped reactor +0", HandOpponentNoFire),
    ("OnAddSecurity PER-CARD: adding 2 cards to security fires the uncapped reactor TWICE (+2 — NO collapse)", SecurityPerCardNoCollapse),
    ("OnAddSecurity recovery PER-CARD: recovering 2 library cards to security fires the uncapped reactor TWICE (+2)", SecurityRecoveryPerCard),
    ("OnAddHand via SINK (real ResolveAddHandBatchId->ApplyDraw): drawing 2 fires the uncapped reactor ONCE (+1)", HandDrawViaSinkCollapse),
    ("OnAddHand {Players} cross-owner: one batch adding to BOTH players' hands fires the P1 reactor ONCE (+1, any-match)", HandCrossOwnerAnyMatch),
    ("OnAddHand opponent-scope POSITIVE: an effect adding to the OPPONENT'S hand fires the opp-scope reactor (+1)", HandOpponentScopeFires),
    ("OnAddSecurity opponent-scope POSITIVE: 2 cards added to the OPPONENT'S security fire the opp-scope reactor TWICE (+2)", SecurityOpponentScopeFires),
    ("P2-1 unified counter: OnAddSecurity add id shares the deletion/discard/add-hand counter space (interleaved-monotonic)", UnifiedCounterInvariant),
    ("P2-1 co-drain: OnAddSecurity adds co-drain with a deletion — all fire, NO spurious cross-batch order choice (+1)", SecurityAddCoDrainDeletion),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task Bt15FiresOnOwnDigimonEffectHandAdd()
{
    EngineContext ctx = Setup(seed: 80);
    var self = await Place(ctx, P1, "BT15_083", "BT15_083", "Tamer");
    var digimonSource = await Place(ctx, P1, "SRC", "SRCd", "Digimon"); // a P1-owned Digimon effect source
    ctx.MemoryController.Set(0);
    var card = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);

    // isOptional "by suspending …" -> answer the optional yes (the effect's stable activation id).
    Enqueue(ctx, ChoiceResult.Select(self));

    // Effect-driven hand add: cause = a P1-owned DIGIMON source -> cause.Owner == owner && cause.IsDigimon.
    await ctx.ZoneMover.AddToHandAsync(P1, card, ctx.NextAddHandBatchId(), digimonSource);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current, "BT15_083 gained +1 memory (own-Digimon-effect hand add, suspend cost paid)");
    AssertTrue(IsSuspended(ctx, self), "BT15_083's Tamer was suspended (the cost)");
}

async Task Bt15OpponentCauseRejected()
{
    EngineContext ctx = Setup(seed: 81);
    var self = await Place(ctx, P1, "BT15_083", "BT15_083", "Tamer");
    var foeSource = await Place(ctx, P2, "SRC2", "SRC2d", "Digimon"); // a P2-owned Digimon effect source
    ctx.MemoryController.Set(0);
    var card = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(self));

    // The OPPONENT'S effect adds to P1's hand — cause.Owner (P2) != owner (P1) -> gate fails.
    await ctx.ZoneMover.AddToHandAsync(P1, card, ctx.NextAddHandBatchId(), foeSource);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current, "the cause-owner gate rejected the opponent's effect — no memory gain");
}

async Task Bt15NonDigimonCauseRejected()
{
    EngineContext ctx = Setup(seed: 82);
    var self = await Place(ctx, P1, "BT15_083", "BT15_083", "Tamer");
    var tamerSource = await Place(ctx, P1, "SRCT", "SRCt", "Tamer"); // a P1-owned NON-Digimon (Tamer) source
    ctx.MemoryController.Set(0);
    var card = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(self));

    // The cause is owner-owned but NOT a Digimon effect -> cause.IsDigimon false -> gate fails.
    await ctx.ZoneMover.AddToHandAsync(P1, card, ctx.NextAddHandBatchId(), tamerSource);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current, "the IsDigimonEffect gate rejected the Tamer-effect hand add — no memory gain");
}

async Task Bt15NonEffectRejected()
{
    EngineContext ctx = Setup(seed: 83);
    var self = await Place(ctx, P1, "BT15_083", "BT15_083", "Tamer");
    ctx.MemoryController.Set(0);
    var card = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(self));

    // A NON-effect hand add: no cause id -> the AS-IS CardEffect==null rejection.
    await ctx.ZoneMover.AddToHandAsync(P1, card, ctx.NextAddHandBatchId(), causeEffectId: null);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current, "the CardEffect!=null gate rejected the non-effect hand add — no memory gain");
}

async Task Bt8FiresOnOwnSecurityAdd()
{
    EngineContext ctx = Setup(seed: 84);
    var self = await Place(ctx, P1, "BT8_090", "BT8_090", "Tamer");
    ctx.MemoryController.Set(0);
    var card = await ZoneCard(ctx, P1, "S1", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(self));

    // A card is added to P1's security (no cause needed for OnAddSecurity).
    await ctx.ZoneMover.AddToSecurityAsync(P1, card, faceUp: false);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current, "BT8_090 gained +1 memory (own-security add, suspend cost paid)");
    AssertTrue(IsSuspended(ctx, self), "BT8_090's Tamer was suspended (the cost)");
}

async Task Bt8OpponentSecurityRejected()
{
    EngineContext ctx = Setup(seed: 85);
    var self = await Place(ctx, P1, "BT8_090", "BT8_090", "Tamer");
    ctx.MemoryController.Set(0);
    var card = await ZoneCard(ctx, P2, "S2", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(self));

    // A card is added to the OPPONENT'S security — player (P2) != owner (P1) -> no fire.
    await ctx.ZoneMover.AddToSecurityAsync(P2, card, faceUp: false);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current, "the player-scope gate rejected the opponent-security add — no memory gain");
}

async Task HandDrawBatchCollapseUncapped()
{
    EngineContext ctx = Setup(seed: 86);
    var self = await Place(ctx, P1, "TfxOnAddCounter", "OACTR", "Digimon");
    var cause = await Place(ctx, P1, "SRC", "SRCd", "Digimon");
    await ZoneCard(ctx, P1, "L1", ChoiceZone.Library);
    await ZoneCard(ctx, P1, "L2", ChoiceZone.Library);
    ctx.MemoryController.Set(0);

    // ONE effect DRAWS 2 cards -> ONE shared add-hand batch id -> reactor fires ONCE.
    await ctx.ZoneMover.DrawAsync(P1, 2, ctx.NextAddHandBatchId(), cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current, "2 drawn cards in one batch fired the uncapped reactor once (+1, not +2)");
}

async Task HandReturnBatchCollapseUncapped()
{
    EngineContext ctx = Setup(seed: 87);
    var self = await Place(ctx, P1, "TfxOnAddCounter", "OACTR", "Digimon");
    var cause = await Place(ctx, P1, "SRC", "SRCd", "Digimon");
    var c1 = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);
    var c2 = await ZoneCard(ctx, P1, "H2", ChoiceZone.Library);
    ctx.MemoryController.Set(0);

    // ONE effect returns 2 cards to hand -> same cached add-hand id -> reactor fires ONCE.
    long batch = ctx.NextAddHandBatchId();
    await ctx.ZoneMover.AddToHandAsync(P1, c1, batch, cause);
    await ctx.ZoneMover.AddToHandAsync(P1, c2, batch, cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current, "2 cards returned in one batch fired the uncapped reactor once (+1, not +2)");
}

async Task HandTwoIndependentBatches()
{
    EngineContext ctx = Setup(seed: 88);
    var self = await Place(ctx, P1, "TfxOnAddCounter", "OACTR", "Digimon");
    var cause = await Place(ctx, P1, "SRC", "SRCd", "Digimon");
    var c1 = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);
    var c2 = await ZoneCard(ctx, P1, "H2", ChoiceZone.Library);
    ctx.MemoryController.Set(0);

    await ctx.ZoneMover.AddToHandAsync(P1, c1, ctx.NextAddHandBatchId(), cause); // batch A
    await ctx.ZoneMover.AddToHandAsync(P1, c2, ctx.NextAddHandBatchId(), cause); // batch B
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(2, ctx.MemoryController.Current.Current, "two independent hand-add batches fired the uncapped reactor twice (+2)");
}

async Task HandNonEffectNoFire()
{
    EngineContext ctx = Setup(seed: 89);
    var self = await Place(ctx, P1, "TfxOnAddCounter", "OACTR", "Digimon");
    var c1 = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);
    ctx.MemoryController.Set(0);

    // No cause id (a turn/mulligan draw shape) -> CanTriggerOnHandAdded CardEffect!=null gate rejects it.
    await ctx.ZoneMover.AddToHandAsync(P1, c1, ctx.NextAddHandBatchId(), causeEffectId: null);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current, "a non-effect hand add did NOT fire OnAddHand (CardEffect!=null gate)");
}

async Task HandOpponentNoFire()
{
    EngineContext ctx = Setup(seed: 90);
    var self = await Place(ctx, P1, "TfxOnAddCounter", "OACTR", "Digimon");
    var cause = await Place(ctx, P1, "SRC", "SRCd", "Digimon");
    var c1 = await ZoneCard(ctx, P2, "H1", ChoiceZone.Library);
    ctx.MemoryController.Set(0);

    // Adding to the OPPONENT'S hand — subject owner (P2) != reactor owner (P1) -> no fire.
    await ctx.ZoneMover.AddToHandAsync(P2, c1, ctx.NextAddHandBatchId(), cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current, "the player-scope gate rejected the opponent-hand add — no fire");
}

async Task SecurityPerCardNoCollapse()
{
    EngineContext ctx = Setup(seed: 91);
    var self = await Place(ctx, P1, "TfxOnAddCounter", "OACTR", "Digimon");
    var s1 = await ZoneCard(ctx, P1, "S1", ChoiceZone.Library);
    var s2 = await ZoneCard(ctx, P1, "S2", ChoiceZone.Library);
    ctx.MemoryController.Set(0);

    // Two SEPARATE ->Security CardMoved in one drain — OnAddSecurity is PER-CARD (AS-IS per IAddSecurity), NO
    // batch collapse — so the uncapped reactor fires TWICE (+2). (A wrong collapse would give +1.)
    await ctx.ZoneMover.AddToSecurityAsync(P1, s1, faceUp: false);
    await ctx.ZoneMover.AddToSecurityAsync(P1, s2, faceUp: false);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(2, ctx.MemoryController.Current.Current, "2 security adds fired the uncapped reactor twice (+2 — OnAddSecurity is per-card, no collapse)");
}

async Task SecurityRecoveryPerCard()
{
    EngineContext ctx = Setup(seed: 92);
    var self = await Place(ctx, P1, "TfxOnAddCounter", "OACTR", "Digimon");
    await ZoneCard(ctx, P1, "L1", ChoiceZone.Library);
    await ZoneCard(ctx, P1, "L2", ChoiceZone.Library);
    ctx.MemoryController.Set(0);

    // Recovery of 2 library cards -> 2 ->Security CardMoved (AS-IS IAddSecurityFromLibrary loops AddSecurityCard,
    // each firing IAddSecurity) -> the uncapped reactor fires TWICE (+2).
    await ctx.ZoneMover.AddSecurityFromLibraryAsync(P1, 2, faceUp: false);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(2, ctx.MemoryController.Current.Current, "recovering 2 cards fired the uncapped reactor twice (+2 — per-card)");
}

// (coverage gap) The existing HandDrawBatchCollapseUncapped injects the batch id directly via
// ZoneMover.DrawAsync(...batchId...), BYPASSING the sink. This drives the REAL effect path: an effect stages a
// DrawCards mutation, the sink's ResolveAddHandBatchId allocates the shared id and ApplyDraw stamps it — so "N draw
// -> OnAddHand once" is proven through MatchStateMutationSink, not a hand-threaded id.
async Task HandDrawViaSinkCollapse()
{
    EngineContext ctx = Setup(seed: 93);
    await Place(ctx, P1, "TfxOnAddCounter", "OACTR", "Digimon");
    var cause = await Place(ctx, P1, "SRC", "SRCd", "Digimon"); // the P1-owned effect cause (CardEffect != null)
    await ZoneCard(ctx, P1, "L1", ChoiceZone.Library);
    await ZoneCard(ctx, P1, "L2", ChoiceZone.Library);
    ctx.MemoryController.Set(0);

    // ONE effect draws 2 cards THROUGH the sink -> ResolveAddHandBatchId (one cached id) -> ApplyDraw stamps it.
    await SinkFlush(ctx, sink => sink.Apply(new EffectMutation(
        MatchStateMutationSink.DrawCardsKind, cause,
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.PlayerIdKey] = P1,
            [MatchStateMutationSink.CountKey] = 2,
        })));
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "2 cards drawn via the SINK (real ResolveAddHandBatchId->ApplyDraw stamp) fired the uncapped reactor once (+1)");
}

// (coverage gap) {Players} cross-owner any-match: AS-IS fires ONE StackSkillInfos(OnAddHand) whose Players set holds
// BOTH owners when a single add spans both hands; a self-scope reactor (owner==P1) fires because P1 is in the set.
// Headless emits one ->Hand CardMoved per card sharing ONE batch id; the collapse-AFTER-gate lets the P1-owner card
// claim the reactor (any-match) while the co-batched P2 card neither blocks nor double-fires it.
async Task HandCrossOwnerAnyMatch()
{
    EngineContext ctx = Setup(seed: 94);
    await Place(ctx, P1, "TfxOnAddCounter", "OACTR", "Digimon");   // self-scope reactor (owner P1)
    var cause = await Place(ctx, P1, "SRC", "SRCd", "Digimon");
    var c1 = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);    // -> P1 hand
    var c2 = await ZoneCard(ctx, P2, "H2", ChoiceZone.Library);    // -> P2 hand
    ctx.MemoryController.Set(0);

    // ONE batch id spans BOTH owners' hand adds.
    long batch = ctx.NextAddHandBatchId();
    await ctx.ZoneMover.AddToHandAsync(P1, c1, batch, cause);
    await ctx.ZoneMover.AddToHandAsync(P2, c2, batch, cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "a cross-owner batch fired the P1 self-scope reactor exactly once (+1 — any-match on the P1 card, no double, no block)");
}

// (coverage gap) opponent-scope POSITIVE for OnAddHand: the existing suite only had opponent-scope NEGATIVES against
// a self-scope reactor. TfxOnAddCounterOpp fires when the OPPONENT'S hand grows (player != owner).
async Task HandOpponentScopeFires()
{
    EngineContext ctx = Setup(seed: 95);
    await Place(ctx, P1, "TfxOnAddCounterOpp", "OACTRO", "Digimon");  // opp-scope reactor (owner P1)
    var cause = await Place(ctx, P2, "SRC2", "SRC2d", "Digimon");     // a P2-owned effect cause
    var c1 = await ZoneCard(ctx, P2, "H1", ChoiceZone.Library);
    ctx.MemoryController.Set(0);

    // An effect adds to the OPPONENT'S (P2's) hand -> subject owner (P2) != reactor owner (P1) -> fires.
    await ctx.ZoneMover.AddToHandAsync(P2, c1, ctx.NextAddHandBatchId(), cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "the opponent-scope reactor fired when the opponent's hand grew (+1)");
}

// (coverage gap) opponent-scope POSITIVE for OnAddSecurity, PER-CARD: 2 cards added to the opponent's security fire
// the opp-scope reactor twice (no collapse).
async Task SecurityOpponentScopeFires()
{
    EngineContext ctx = Setup(seed: 96);
    await Place(ctx, P1, "TfxOnAddCounterOpp", "OACTRO", "Digimon");  // opp-scope reactor (owner P1)
    var s1 = await ZoneCard(ctx, P2, "S1", ChoiceZone.Library);
    var s2 = await ZoneCard(ctx, P2, "S2", ChoiceZone.Library);
    ctx.MemoryController.Set(0);

    // Two cards added to the OPPONENT'S (P2's) security -> per-card fire for the P1 opp-scope reactor (+2).
    await ctx.ZoneMover.AddToSecurityAsync(P2, s1, faceUp: false, addSecurityBatchId: ctx.NextSecurityAddBatchId());
    await ctx.ZoneMover.AddToSecurityAsync(P2, s2, faceUp: false, addSecurityBatchId: ctx.NextSecurityAddBatchId());
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(2, ctx.MemoryController.Current.Current,
        "2 opponent-security adds fired the opp-scope reactor twice (+2 — per-card)");
}

// (P2-1 remediation) the add-security batch id shares the ONE deletion/discard/add-hand counter space — it is
// interleaved-monotonic with the others (no separate DcgoMatch event-Sequence space). This pins the exact fix.
Task UnifiedCounterInvariant()
{
    EngineContext ctx = Setup(seed: 97);
    long a = ctx.NextDeletionBatchId();
    long b = ctx.NextSecurityAddBatchId();
    long c = ctx.NextDiscardBatchId();
    long d = ctx.NextSecurityAddBatchId();
    long e = ctx.NextAddHandBatchId();
    AssertTrue(a < b && b < c && c < d && d < e,
        $"add-security ids share the unified counter (strictly ascending across timings): {a},{b},{c},{d},{e}");
    return Task.CompletedTask;
}

// (P2-1 remediation) OnAddSecurity co-drains with a deletion batch in ONE window. Both timings' ids now come from
// the SAME counter space, so the window sequences the per-card add-security triggers AND the leave trigger with NO
// spurious cross-batch order choice (the former event-Sequence stamp lived in a different space, mis-ordering the
// raw-BatchId comparison in WindowResolver.FilterToMinimumBatch). +2 security adds and -1 leave = +1, 0 prompts.
async Task SecurityAddCoDrainDeletion()
{
    EngineContext ctx = Setup(seed: 98);
    var cards = (CardDatabase)ctx.CardRepository;
    // The deletion-side reactor (uncapped OnLeaveFieldAnyone, -1 per leave), registered like the D-1 suite.
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOnLeaveFieldCounter"), "TfxOnLeaveFieldCounter", "LeaveCtr",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var leaveReactor = new HeadlessEntityId("1:battle:LEAVECTR");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(leaveReactor, new HeadlessEntityId("TfxOnLeaveFieldCounter"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, leaveReactor, ChoiceZone.None, ChoiceZone.BattleArea));
    ctx.RegisterEnteredCardEffects(leaveReactor, P1);

    await Place(ctx, P1, "TfxOnAddCounter", "OACTR", "Digimon");   // security-side reactor (+1 per add)
    var s1 = await ZoneCard(ctx, P1, "S1", ChoiceZone.Library);
    var s2 = await ZoneCard(ctx, P1, "S2", ChoiceZone.Library);
    var foe = await Foe(ctx, "F1");
    ctx.MemoryController.Set(0);

    // ONE sink flush: add 2 cards to P1 security (2 distinct add-security ids) AND delete a P2 Digimon (1 deletion
    // id) — all in the unified counter space. They co-drain into one window.
    await SinkFlush(ctx, sink =>
    {
        sink.Apply(new EffectMutation(MatchStateMutationSink.AddToSecurityKind, new HeadlessEntityId("effect:adder"),
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = s1.Value }));
        sink.Apply(new EffectMutation(MatchStateMutationSink.AddToSecurityKind, new HeadlessEntityId("effect:adder"),
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = s2.Value }));
        sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("effect:deleter"),
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = foe.Value }));
    });
    int prompts = await DriveToCompletion(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "both security adds (+2) and the single leave (-1) fired -> net +1 (all triggers resolved)");
    AssertEqual(0, prompts,
        "the cross-timing co-drain sequenced with NO spurious order choice (0 WindowChoice prompts) — unified counter space");
}

// --- Helpers -------------------------------------------------------------

EngineContext Setup(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string tag, string cardType)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> ZoneCard(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{owner.Value}{tag}");
    cards.Upsert(new CardRecord(def, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

async Task<HeadlessEntityId> Foe(EngineContext ctx, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:foe:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"2:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P2, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task SinkFlush(EngineContext ctx, Action<MatchStateMutationSink> build)
{
    var sink = new MatchStateMutationSink(
        ctx.CardInstanceRepository, log: null, ctx.ZoneMover, ctx.MemoryController,
        ctx.GameEventQueue, context: ctx);
    build(sink);
    // (harness) the sink's DrawClass.Draw emits its OnDraw window via GManager.instance.autoProcessing, which
    // resolves off the ambient match scope — flush under an Enter scope exactly as the RunToStable loop does.
    using (AmbientMatchContext.Enter(ctx))
    {
        await sink.FlushAsync();
    }
}

// Drive RunToStable to a fixpoint, answering any WindowChoice by selecting its first candidate; returns the NUMBER
// of WindowChoice prompts answered (the observable that a cross-timing co-drain must NOT present — must be ZERO).
async Task<int> DriveToCompletion(EngineContext ctx)
{
    int prompts = 0;
    var processor = new MetadataActionProcessor();
    for (int guard = 0; guard < 100; guard++)
    {
        HeadlessChoiceState choice = ctx.ChoiceController.Current;
        if (choice.IsPending && choice.Type == ChoiceType.WindowChoice)
        {
            prompts++;
            await processor.ProcessAsync(
                HeadlessActionFactory.ResolveChoice(choice.PlayerId!.Value, ChoiceResult.Select(choice.CandidateIds[0])), ctx);
            continue;
        }

        await new GameFlowProcessor().RunToStableAsync(ctx);
        if (!ctx.ChoiceController.Current.IsPending)
        {
            return prompts;
        }
    }

    throw new Exception("window did not drive to completion within the guard bound");
}

void Enqueue(EngineContext ctx, ChoiceResult choice) => ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(choice);

bool IsSuspended(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
    && rec.Metadata.TryGetValue("isSuspended", out object? raw) && raw is true;

void AssertEqual(int expected, int actual, string what)
{
    if (expected != actual) throw new Exception($"{what}: expected {expected}, got {actual}");
}

void AssertTrue(bool cond, string what)
{
    if (!cond) throw new Exception($"expected true: {what}");
}
