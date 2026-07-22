using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// RE-HOME of G9-073.W6ProcessCommons (retired 2026-07-23 stale-pin teardown). The old suite's three stale pins
// were retired with it (AddEffectToPermanent / DigivolveIntoHandOrTrashCard / DNADigivolve..., which observed
// un-bridged new-model kind-classes via a synthetic un-dispatched fixture — stage-B RED, not a live defect).
// The four BEHAVIORAL process-coroutine assertions below are re-driven UNCHANGED through the live process
// surface and pass: the timed target DP/SAttack modifier folds+expires, the player-scope predicate DP modifier,
// the sink-driven AddThisCardToHand + PlayPermanentCards (option filtered, cost paid, tap honoured), and the
// SelectTrashDigivolutionCards host+source pick. Adjacent coverage: W3c3-DpDeltaGrant (the ChangeDigimonDP/
// ChangeDigimonSAttack/ChangeDigimonDPPlayerEffect bucket grant+expiry witness), PILOT-S3.Witness
// (SelectTrashDigivolutionCards), CardEffect.ST2.Blue / CardEffect.ST3.Yellow (PlayPermanentCards /
// AddThisCardToHand on real ported cards).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ChangeDigimonDP/SAttack: timed target modifier folds and expires", TimedStatMods),
    ("ChangeDigimonDPPlayerEffect: player-scope predicate modifier, duration-tagged", PlayerScopeDp),
    ("AddThisCardToHand + PlayPermanentCards: sink-driven moves, option filtered, cost paid", HandAndPlay),
    ("SelectTrashDigivolutionCards: host pick then source picks, budget respected", SelectTrashSources),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task TimedStatMods()
{
    EngineContext ctx = Ctx();
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // DoneStartGame gate (CanTrigger)
    using var scope = AmbientMatchContext.Enter(ctx);
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var target = await Put(ctx, P1, "TGT", ChoiceZone.BattleArea, dp: 5000);

    AssertTrue(CardEffectCommons.ChangeDigimonDP(Perm(ctx, target), 2000, EffectDuration.UntilOpponentTurnEnd, V(ctx, src)), "DP grant");
    AssertEqual(7000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, target).DP, "+2000 folded");
    AssertTrue(CardEffectCommons.ChangeDigimonSAttack(Perm(ctx, target), 1, EffectDuration.UntilOpponentTurnEnd, V(ctx, src)), "SA grant");
    AssertEqual(2, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, target).Strike, "+1 SA folded");

    // The grant is not a registry binding; it expires at the AS-IS bucket-reset site (HeadlessEndTurnCleanupFlow).
    // UntilOpponentTurnEnd (granted on P1's turn) elapses at the end of the OPPONENT's (P2's) turn.
    ExpireOpponentTurnEnd(ctx);
    AssertEqual(5000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, target).DP, "DP expired at the boundary");
    AssertEqual(1, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, target).Strike, "SA expired");
}

async Task PlayerScopeDp()
{
    EngineContext ctx = Ctx();
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // DoneStartGame gate (CanTrigger)
    using var scope = AmbientMatchContext.Enter(ctx);
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var big = await Put(ctx, P1, "BIG", ChoiceZone.BattleArea, level: 6);
    var small = await Put(ctx, P1, "SMALL", ChoiceZone.BattleArea, level: 3);

    AssertTrue(CardEffectCommons.ChangeDigimonDPPlayerEffect(p => p.Level >= 6, 3000, EffectDuration.UntilOpponentTurnEnd, V(ctx, src)), "grant");
    AssertEqual(8000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, big).DP, "matching digimon buffed");
    AssertEqual(5000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, small).DP, "non-matching untouched (predicate 1:1)");

    ExpireOpponentTurnEnd(ctx);
    AssertEqual(5000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, big).DP, "expired");
}

async Task HandAndPlay()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var trashCard = await Put(ctx, P1, "TC", ChoiceZone.Trash);
    await CardEffectCommons.AddThisCardToHand(V(ctx, trashCard), V(ctx, src));
    AssertTrue(InZone(ctx, P1, ChoiceZone.Hand, trashCard), "AddThisCardToHand moved it");

    var digimon = await Put(ctx, P1, "PLAYME", ChoiceZone.Trash, playCost: 3);
    var option = await Put(ctx, P1, "OPT", ChoiceZone.Trash, cardType: "Option");
    await CardEffectCommons.PlayPermanentCards(
        new[] { V(ctx, digimon), V(ctx, option) }, V(ctx, src),
        payCost: true, isTapped: true, root: ChoiceZone.Trash, activateETB: true);

    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, digimon), "digimon played from trash");
    AssertTrue(!InZone(ctx, P1, ChoiceZone.BattleArea, option), "option filtered out (CanPlayAsNewPermanent)");
    AssertEqual(2, ctx.MemoryController.Current.Current, "play cost 3 paid");
    ctx.CardInstanceRepository.TryGetInstance(digimon, out CardInstanceRecord? played);
    AssertTrue(played!.Metadata.TryGetValue("isSuspended", out object? tap) && tap is true, "isTapped honoured");
}

async Task SelectTrashSources()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var host = await Put(ctx, P2, "HOST", ChoiceZone.BattleArea);
    var u1 = await Put(ctx, P2, "U1", ChoiceZone.Trash);
    var u2 = await Put(ctx, P2, "U2", ChoiceZone.Trash);
    await DigivolutionStackHelpers.AddSourcesBottomAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host, new[] { u1, u2 }, ChoiceZone.Trash);

    var provider = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(host));
    provider.Enqueue(ChoiceResult.Select(u1));
    Permanent? reportedHost = null;
    await CardEffectCommons.SelectTrashDigivolutionCards(
        permanentCondition: null, cardCondition: null, maxCount: 1, canNoTrash: false,
        isFromOnly1Permanent: true, V(ctx, src),
        afterSelectionCoroutine: (h, picks) => { reportedHost = h; return Task.CompletedTask; });

    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, u1), "the picked source was trashed");
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec);
    var remaining = (rec!.Metadata[DigivolutionStackReader.SourceIdsKey] as IEnumerable<string>)?.ToArray() ?? Array.Empty<string>();
    AssertTrue(remaining.Contains(u2.Value) && !remaining.Contains(u1.Value), "only the pick left the stack");
    AssertTrue(reportedHost?.InstanceId == host, "afterSelection callback saw the host");
}

// --- Harness ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 973);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

// End the OPPONENT's (P2's) turn so UntilOpponentTurnEnd buckets granted on P1's turn reset — the AS-IS
// bucket-reset path (HeadlessEndTurnCleanupFlow) that replaces the retired registry sweep.
void ExpireOpponentTurnEnd(EngineContext ctx) =>
    new HeadlessEndTurnCleanupFlow().Cleanup(ctx, new HeadlessTurnState(
        TurnNumber: 2, TurnPlayerId: P2, NonTurnPlayerId: P1,
        Phase: HeadlessPhase.End, StepCursor: TurnStepCursor.PhaseStart, IsFirstTurn: false, PlayerOrder: new[] { P1, P2 }));

async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone,
    string cardType = "Digimon", int dp = 5000, int level = 4, int? playCost = null, string? name = null,
    string? cardNumber = null, int? evoCost = null, string? evoCondition = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, cardNumber ?? tag, name ?? tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level },
        CardType: cardType, PlayCost: playCost, EvolutionCost: evoCost, EvolutionCondition: evoCondition));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));
Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id));
HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;
bool InZone(EngineContext ctx, HeadlessPlayerId p, ChoiceZone z, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(p, z).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
    }
}
