// F1-Tier1: OnDiscardHand / OnDiscardSecurity / OnDiscardLibrary activated-bridge expansion.
//
// AS-IS fires each of these via a global StackSkillInfos(hashtable {DiscardedCards = the whole LIST, CardEffect},
// timing) — a broadcast whose reactors live on OTHER field cards and self-gate (OnTrashHand.cs /
// WhenDiscardSecurity.cs / WhenDiscardLibrary.cs). N cards discarded by ONE effect = ONE StackSkillInfos = ONE
// reactor fire. Headless derives each timing from the discarded card's CardMoved (Hand/Security/Library->Trash,
// TriggerTimingMap) and this F1-Tier1 change REGISTERS the three timings in ActivatedBridgeTimings.EventBroadcast,
// stamps a shared discard/security-loss batch id + the CAUSE effect id on the effect-driven trash, and collapses
// the per-card CardMoved events to one reactor fire per batch (the AS-IS any-match). Hand/Security additionally
// require CardEffect != null (a NON-effect trash — attack security-CHECK reveal — is rejected); Library does not.
//
// END-TO-END bridge tests (zone move + GameFlowProcessor.RunToStableAsync).
//
// Witnesses (REAL cards, dispatched by class-name = card-number):
//   * ST16_14 (ST16/Purple): [All Turns] "when one of YOUR effects trashes a card in your hand, by suspending this
//     Tamer, gain 1 memory" — SuspendSelfAndGainMemoryBody, SELF-scope, exercises the cause-effect gate.
//   * BT19_071 (BT19/Purple): [All Turns][Once Per Turn] "when effects trash cards from your deck, delete 1 of your
//     opponent's level<=5 Digimon" — activated SELECT-DESTROY, ANYONE/cross-card, Library (no cause gate).
// Uncapped fixture (TfxDiscardCounter) proves the batch collapse the single-fire real cards would mask.
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
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
    ("ST16_14 fires: own-effect hand discard gains +1 (suspend+memory) via OnDiscardHand", St16FiresOnOwnEffectHandDiscard),
    ("ST16_14 cause-gate negative: an OPPONENT'S effect discarding your hand card does NOT fire (cause owner != owner)", St16OpponentEffectRejected),
    ("ST16_14 CardEffect!=null negative: a NON-effect hand trash (no cause id) does NOT fire", St16NonEffectRejected),
    ("BT19_071 fires: trashing YOUR deck deletes an opponent level<=5 Digimon via OnDiscardLibrary", Bt19FiresOnOwnDeckTrash),
    ("BT19_071 owner-gate negative: trashing the OPPONENT'S deck does NOT fire", Bt19OpponentDeckRejected),
    ("BT19_071 [Once Per Turn] cap: a SECOND own-deck trash the same turn does NOT delete again", Bt19OncePerTurnCap),
    ("hand batch collapse: an effect discarding 2 hand cards in ONE batch fires the UNCAPPED reactor ONCE (+1)", HandBatchCollapseUncapped),
    ("hand independent batches: two SEPARATE hand-discard batches fire the uncapped reactor TWICE (+2)", HandTwoIndependentBatches),
    ("library batch collapse: an effect trashing 2 deck cards in ONE batch fires the UNCAPPED reactor ONCE (+1)", LibraryBatchCollapseUncapped),
    ("security effect-trash fires; a NON-effect security trash (no cause) does NOT fire OnDiscardSecurity", SecurityEffectVsNonEffect),
    ("security batch collapse: trashing 2 security in ONE effect batch fires the uncapped reactor ONCE (+1)", SecurityBatchCollapseUncapped),
    ("reveal over-fire: reveal-then-trash remainder does NOT fire OnDiscardLibrary (IsBeingRevealed mirror); a direct deck trash still fires", RevealRemainderTrashDoesNotOverfire),
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

async Task St16FiresOnOwnEffectHandDiscard()
{
    EngineContext ctx = Setup(seed: 60);
    ctx.MemoryController.Set(0);
    var self = await Place(ctx, P1, "ST16_14", "ST16_14", "Tamer");
    var handCard = await HandCard(ctx, P1, "H1");

    // isOptional "by suspending …" -> answer the optional yes (the effect's stable EffectId; no capHash).
    Enqueue(ctx, ChoiceResult.Select(new HeadlessEntityId($"{self.Value}:ae")));

    // Effect-driven hand discard: cause = a P1-owned effect source (self), so cause.Owner == card.Owner.
    await ctx.ZoneMover.TrashCardAsync(P1, handCard, ctx.NextDiscardBatchId(), self);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current, "ST16_14 gained +1 memory (suspend cost paid, own-effect hand discard)");
    AssertTrue(IsSuspended(ctx, self), "ST16_14's Tamer was suspended (the cost)");
}

async Task St16OpponentEffectRejected()
{
    EngineContext ctx = Setup(seed: 61);
    ctx.MemoryController.Set(0);
    var self = await Place(ctx, P1, "ST16_14", "ST16_14", "Tamer");
    var foeSource = await Place(ctx, P2, "SRC2", "SRC2", "Digimon"); // a P2-owned effect source
    var handCard = await HandCard(ctx, P1, "H1");

    Enqueue(ctx, ChoiceResult.Select(new HeadlessEntityId($"{self.Value}:ae")));

    // The OPPONENT'S effect trashes P1's hand card — cause.Owner (P2) != card.Owner (P1) -> SkillCondition fails.
    await ctx.ZoneMover.TrashCardAsync(P1, handCard, ctx.NextDiscardBatchId(), foeSource);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current, "the cause-effect gate rejected the opponent's effect — no memory gain");
}

async Task St16NonEffectRejected()
{
    EngineContext ctx = Setup(seed: 62);
    ctx.MemoryController.Set(0);
    var self = await Place(ctx, P1, "ST16_14", "ST16_14", "Tamer");
    var handCard = await HandCard(ctx, P1, "H1");

    Enqueue(ctx, ChoiceResult.Select(new HeadlessEntityId($"{self.Value}:ae")));

    // A NON-effect hand trash: no cause id -> the AS-IS CardEffect==null rejection.
    await ctx.ZoneMover.TrashCardAsync(P1, handCard, ctx.NextDiscardBatchId(), causeEffectId: null);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(0, ctx.MemoryController.Current.Current, "the CardEffect!=null gate rejected the non-effect hand trash — no memory gain");
}

async Task Bt19FiresOnOwnDeckTrash()
{
    EngineContext ctx = Setup(seed: 63);
    var self = await Place(ctx, P1, "BT19_071", "BT19_071", "Digimon");
    var foe = await Place(ctx, P2, "FOE", "FOEdef", "Digimon"); // level 4 opponent Digimon
    var deckCard = await LibraryCard(ctx, P1, "L1");

    Enqueue(ctx, ChoiceResult.Select(foe)); // the mandatory select-destroy target

    await ctx.ZoneMover.TrashCardAsync(P1, deckCard, ctx.NextDiscardBatchId(), self);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InTrash(ctx, P2, foe), "BT19_071 deleted the opponent's level<=5 Digimon on own-deck trash");
}

async Task Bt19OpponentDeckRejected()
{
    EngineContext ctx = Setup(seed: 64);
    var self = await Place(ctx, P1, "BT19_071", "BT19_071", "Digimon");
    var foe = await Place(ctx, P2, "FOE", "FOEdef", "Digimon");
    var foeSource = await Place(ctx, P2, "SRC2", "SRC2", "Digimon");
    var foeDeckCard = await LibraryCard(ctx, P2, "L2"); // the OPPONENT'S deck

    Enqueue(ctx, ChoiceResult.Select(foe)); // would be needed on a spurious fire — left unconsumed if correct

    await ctx.ZoneMover.TrashCardAsync(P2, foeDeckCard, ctx.NextDiscardBatchId(), foeSource);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(!InTrash(ctx, P2, foe), "the owner-gate (discarded card owner == card owner) rejected the opponent's deck trash — no delete");
}

async Task Bt19OncePerTurnCap()
{
    EngineContext ctx = Setup(seed: 65);
    var self = await Place(ctx, P1, "BT19_071", "BT19_071", "Digimon");
    var foeA = await Place(ctx, P2, "FOEA", "FOEA", "Digimon");
    var foeB = await Place(ctx, P2, "FOEB", "FOEB", "Digimon");
    var deck1 = await LibraryCard(ctx, P1, "L1");
    var deck2 = await LibraryCard(ctx, P1, "L2");

    Enqueue(ctx, ChoiceResult.Select(foeA));
    await ctx.ZoneMover.TrashCardAsync(P1, deck1, ctx.NextDiscardBatchId(), self);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertTrue(InTrash(ctx, P2, foeA), "first own-deck trash deleted foeA");

    // Second own-deck trash the SAME turn — the [Once Per Turn] cap blocks the re-fire.
    Enqueue(ctx, ChoiceResult.Select(foeB));
    await ctx.ZoneMover.TrashCardAsync(P1, deck2, ctx.NextDiscardBatchId(), self);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertTrue(!InTrash(ctx, P2, foeB), "the [Once Per Turn] cap blocked the second delete the same turn");
}

async Task HandBatchCollapseUncapped()
{
    EngineContext ctx = Setup(seed: 66);
    ctx.MemoryController.Set(0);
    var self = await Place(ctx, P1, "TfxDiscardCounter", "DCTR", "Digimon");
    var h1 = await HandCard(ctx, P1, "H1");
    var h2 = await HandCard(ctx, P1, "H2");

    // ONE effect discards 2 hand cards -> ONE shared discard batch id -> reactor fires ONCE.
    long batch = ctx.NextDiscardBatchId();
    await ctx.ZoneMover.TrashCardAsync(P1, h1, batch, self);
    await ctx.ZoneMover.TrashCardAsync(P1, h2, batch, self);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current, "2 hand cards in one batch fired the uncapped reactor once (+1, not +2)");
}

async Task HandTwoIndependentBatches()
{
    EngineContext ctx = Setup(seed: 67);
    ctx.MemoryController.Set(0);
    var self = await Place(ctx, P1, "TfxDiscardCounter", "DCTR", "Digimon");
    var h1 = await HandCard(ctx, P1, "H1");
    var h2 = await HandCard(ctx, P1, "H2");

    await ctx.ZoneMover.TrashCardAsync(P1, h1, ctx.NextDiscardBatchId(), self); // batch A
    await ctx.ZoneMover.TrashCardAsync(P1, h2, ctx.NextDiscardBatchId(), self); // batch B
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(2, ctx.MemoryController.Current.Current, "two independent hand-discard batches fired the uncapped reactor twice (+2)");
}

async Task LibraryBatchCollapseUncapped()
{
    EngineContext ctx = Setup(seed: 68);
    ctx.MemoryController.Set(0);
    var self = await Place(ctx, P1, "TfxDiscardCounter", "DCTR", "Digimon");
    var l1 = await LibraryCard(ctx, P1, "L1");
    var l2 = await LibraryCard(ctx, P1, "L2");

    long batch = ctx.NextDiscardBatchId();
    await ctx.ZoneMover.TrashCardAsync(P1, l1, batch, self);
    await ctx.ZoneMover.TrashCardAsync(P1, l2, batch, self);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current, "2 deck cards in one batch fired the uncapped reactor once (+1, not +2)");
}

async Task SecurityEffectVsNonEffect()
{
    // Effect-driven security trash fires OnDiscardSecurity (+1); a NON-effect security trash (no cause id — the
    // attack security-CHECK reveal shape) does NOT fire it (CardEffect!=null gate).
    EngineContext ctx = Setup(seed: 69);
    ctx.MemoryController.Set(0);
    var self = await Place(ctx, P1, "TfxDiscardCounter", "DCTR", "Digimon");
    await Security(ctx, P1, 2);

    // Non-effect first: no cause -> rejected.
    await ctx.ZoneMover.TrashSecurityAsync(P1, count: 1, fromTop: true, securityLossBatchId: null, causeEffectId: null);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(0, ctx.MemoryController.Current.Current, "a non-effect security trash did NOT fire OnDiscardSecurity (CardEffect!=null gate)");

    // Effect-driven next: cause present -> fires.
    await ctx.ZoneMover.TrashSecurityAsync(P1, count: 1, fromTop: true, ctx.NextSecurityLossBatchId(), causeEffectId: self);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(1, ctx.MemoryController.Current.Current, "an effect-driven security trash fired OnDiscardSecurity (+1)");
}

async Task SecurityBatchCollapseUncapped()
{
    EngineContext ctx = Setup(seed: 70);
    ctx.MemoryController.Set(0);
    var self = await Place(ctx, P1, "TfxDiscardCounter", "DCTR", "Digimon");
    await Security(ctx, P1, 3);

    // One IDestroySecurity trashing 2 cards -> one shared security-loss id -> OnDiscardSecurity fires ONCE.
    await ctx.ZoneMover.TrashSecurityAsync(P1, count: 2, fromTop: true, ctx.NextSecurityLossBatchId(), causeEffectId: self);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertEqual(1, ctx.MemoryController.Current.Current, "2 security cards in one effect batch fired the uncapped reactor once (+1, not +2)");
}

async Task RevealRemainderTrashDoesNotOverfire()
{
    // F-1 adversarial-review P1: the reveal path (SimplifiedRevealAndSelectEffect) routes its unselected remainder
    // to the trash via a TrashCard mutation, which F1 derives as a Library->Trash CardMoved => OnDiscardLibrary.
    // AS-IS suppresses that broadcast for a Library reactor because CanTriggerWhenDiscardLibrary any-matches only
    // !IsBeingRevealed cards and the reveal-trashed remainder is IsBeingRevealed==true (RevealLibrary.cs resets the
    // flag only AFTER TrashRevealedCards). The reveal-remainder trash is stamped with the RevealTrashFlagKey mirror,
    // so the uncapped Library reactor must NOT fire. A DIRECT effect-driven deck trash (no reveal marker) still fires.
    EngineContext ctx = Setup(seed: 71);
    ctx.MemoryController.Set(0);
    var reactor = await Place(ctx, P1, "TfxDiscardCounter", "DCTR", "Digimon"); // uncapped OnDiscardLibrary reactor
    var revealSource = await Place(ctx, P1, "REVSRC", "REVSRC", "Digimon");      // the (inert) reveal effect source
    var l1 = await LibraryCard(ctx, P1, "L1");
    var l2 = await LibraryCard(ctx, P1, "L2");

    // Reveal the top 2 deck cards with NO matching select condition -> both remainder cards route to the trash.
    // (이연③-f RE-TARGET) driven through the AS-IS commons CardEffectCommons.RevealDeckTopCardsAndProcessForAll
    // (a never-matching Custom condition), replacing the retired SimplifiedRevealAndSelectEffect class. The
    // commons' StageRevealMovesAsync stamps the SAME RevealTrashFlagKey on the reveal->trash move and flushes
    // its own internal sink.
    var revealCard = new CardSource(ctx, revealSource, P1, P1);
    var activate = new ActivateClass();
    activate.SetUpICardEffect("reveal top 2, trash the rest", _ => true, revealCard);
    await CardEffectCommons.RevealDeckTopCardsAndProcessForAll(
        revealCount: 2,
        simplifiedSelectCardCondition: new SimplifiedSelectCardConditionClass(
            canTargetCondition: _ => false, message: "", mode: SelectCardEffect.Mode.Custom,
            maxCount: -1, selectCardCoroutine: null),
        remainingCardsPlace: RemainingCardsPlace.Trash,
        activateClass: activate);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InTrash(ctx, P1, l1) && InTrash(ctx, P1, l2), "the revealed remainder was actually trashed (Library->Trash)");
    AssertEqual(0, ctx.MemoryController.Current.Current,
        "reveal-remainder trash did NOT fire OnDiscardLibrary (IsBeingRevealed mirror suppressed the over-fire)");

    // Control: a DIRECT effect-driven deck trash (no reveal marker) DOES fire the same reactor (+1) — proving the
    // suppression is scoped to the reveal path, not a blanket disable of OnDiscardLibrary.
    var l3 = await LibraryCard(ctx, P1, "L3");
    await ctx.ZoneMover.TrashCardAsync(P1, l3, ctx.NextDiscardBatchId(), revealSource);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(1, ctx.MemoryController.Current.Current,
        "a direct (non-reveal) effect-driven deck trash still fires OnDiscardLibrary (+1)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Setup(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
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

// A generic (no effect class) card placed in the owner's HAND.
async Task<HeadlessEntityId> HandCard(EngineContext ctx, HeadlessPlayerId owner, string tag) =>
    await ZoneCard(ctx, owner, tag, ChoiceZone.Hand);

// A generic (no effect class) card placed in the owner's LIBRARY (deck).
async Task<HeadlessEntityId> LibraryCard(EngineContext ctx, HeadlessPlayerId owner, string tag) =>
    await ZoneCard(ctx, owner, tag, ChoiceZone.Library);

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

async Task Security(EngineContext ctx, HeadlessPlayerId owner, int count)
{
    var cards = (CardDatabase)ctx.CardRepository;
    for (int i = 1; i <= count; i++)
    {
        var def = new HeadlessEntityId($"DEF:SEC{owner.Value}{i}");
        cards.Upsert(new CardRecord(def, $"SEC{i}", $"SEC{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var id = new HeadlessEntityId($"{owner.Value}:sec:{i}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>()));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Security));
    }
}

void Enqueue(EngineContext ctx, ChoiceResult choice) => ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(choice);

bool InTrash(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Trash).Contains(id);

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
