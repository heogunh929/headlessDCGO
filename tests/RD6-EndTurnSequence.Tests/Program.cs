using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// RD6 (A-2): the [End of Your Turn] effect window must resolve in the ENDING player's still-live frame, BEFORE
// the turn flip (AS-IS AutoProcessing.EndTurnProcess:699, "step 3" — before the attack loop and the threshold
// re-check at :714). The live bug: headless emitted OnEndTurn AFTER the flip, so a [End of Your Turn] body that
// gates on `TurnPlayerId == Owner` (BT1_021 EoTLose3Memory, TriggeredGainMemoryEffect) NO-OPPED entirely in the
// new turn player's frame — its memory change was silently dropped. This asserts the effect now fires pre-flip
// against the correct owner, so the opponent inherits the mirrored (+|memory - 3|) memory.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("[End of Your Turn] lose-3-memory fires in the ENDING player's frame (pre-flip), not no-op post-flip", EoTFiresPreFlip),
    ("[End of Your Turn] gain memory that lifts the opponent below the threshold CONTINUES the turn (no flip)", EoTGainContinuesTurn),
    ("two activated [End of Your Turn] effects OPEN an order choice (pre-flip drain suspends), then re-applied EndTurn flips", MultiActivatedEoTSuspendsThenReapplies),
    ("the [End of Your Turn] effect window resolves BEFORE the end-of-turn attack offer (AS-IS window-then-attack order)", EoTWindowResolvesBeforeAttack),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task EoTFiresPreFlip()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(), new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceToMainAsync(match, P1);

    EngineContext context = match.Context;
    var cards = (CardDatabase)context.CardRepository;

    // BT1_021 (Red) carries "[End of Your Turn] Lose 3 memory." — place it in P1's battle area and register its
    // effects (the enter-play hook binds the OnEndTurn TriggeredGainMemoryEffect into the scheduler registry).
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxEndTurnLose3Memory"), "TfxEndTurnLose3Memory", "EoTLoser",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    var eot = new HeadlessEntityId("p1:field:TfxEoTLose3");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(eot, new HeadlessEntityId("TfxEndTurnLose3Memory"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, eot, ChoiceZone.None, ChoiceZone.BattleArea));
    context.RegisterEnteredCardEffects(eot, P1);

    // A plain level-3 Digimon that costs 3 — playing it crosses memory 0 -> -3, ending P1's turn (MemoryPass).
    const int cost = 3;
    cards.Upsert(new CardRecord(new HeadlessEntityId("VAN-D"), "VAN-D", "Vanilla",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon", PlayCost: cost));
    var hand = new HeadlessEntityId("p1:hand:VAN");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(hand, new HeadlessEntityId("VAN-D"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, hand, ChoiceZone.None, ChoiceZone.Hand));

    context.MemoryController.Set(0);
    LegalAction play = match.GetLegalActions(P1)
        .Single(x => x.ActionType == HeadlessActionTypes.PlayCard && x.Id.Value.Contains(hand.Value, StringComparison.Ordinal));
    await env.StepAsync(play);

    AssertEqual(-cost, context.MemoryController.Current.Current, "the costed play took memory to -3");
    AssertEqual(HeadlessPhase.MemoryPass, match.GetObservation().Turn.Phase, "memory < 0 put P1 into MemoryPass (turn ending)");

    // Hand over the turn. If the [End of Your Turn] window fires PRE-flip (the fix), BT1_021 loses 3 more memory
    // in P1's frame (-3 -> -6), so P2 inherits the mirrored +6. If it no-ops post-flip (the bug), P2 gets only +3.
    LegalAction endTurn = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.EndTurn);
    await env.StepAsync(endTurn);

    AssertEqual(P2.Value, match.GetObservation().Turn.TurnPlayerId?.Value ?? 0, "the turn handed over to P2");
    AssertEqual(cost + 3, context.MemoryController.Current.Current,
        "P2 inherits +6: the pre-flip [End of Your Turn] lose-3 fired in P1's frame (bug would leave +3)");
}

async Task EoTGainContinuesTurn()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(), new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceToMainAsync(match, P1);

    EngineContext context = match.Context;
    var cards = (CardDatabase)context.CardRepository;

    // Place a plain Digimon in P1's battle area and bind a scheduler "[End of Your Turn] gain 3 memory" effect.
    cards.Upsert(new CardRecord(new HeadlessEntityId("GAINER"), "GAINER", "Gainer",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    var gainer = new HeadlessEntityId("p1:field:GAINER");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(gainer, new HeadlessEntityId("GAINER"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, gainer, ChoiceZone.None, ChoiceZone.BattleArea));
    var gainSrc = new CardSource(context, gainer, P1, P1);
    var gainEffect = new TriggeredGainMemoryEffect(gainSrc, EffectTiming.OnEndTurn, 3, "[End of Your Turn] Gain 3 memory.");
    context.EffectRegistry.Register(gainEffect.ToBinding("p1:field:GAINER:eotgain"));

    // A cost-1 play crosses memory 0 -> -1 (the default threshold 1), putting P1 into MemoryPass (turn ending).
    cards.Upsert(new CardRecord(new HeadlessEntityId("VAN1"), "VAN1", "Vanilla1",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon", PlayCost: 1));
    var hand = new HeadlessEntityId("p1:hand:VAN1");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(hand, new HeadlessEntityId("VAN1"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, hand, ChoiceZone.None, ChoiceZone.Hand));

    context.MemoryController.Set(0);
    LegalAction play = match.GetLegalActions(P1)
        .Single(x => x.ActionType == HeadlessActionTypes.PlayCard && x.Id.Value.Contains(hand.Value, StringComparison.Ordinal));
    await env.StepAsync(play);
    AssertEqual(-1, context.MemoryController.Current.Current, "the cost-1 play took memory to -1");
    AssertEqual(HeadlessPhase.MemoryPass, match.GetObservation().Turn.Phase, "memory at threshold -> MemoryPass (turn ending)");

    // EndTurn: the [End of Your Turn] +3 fires PRE-flip (-1 -> +2), lifting the opponent below the threshold, so the
    // re-check keeps the turn going — no hand-over, phase reverts to Main.
    LegalAction endTurn = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.EndTurn);
    await env.StepAsync(endTurn);

    AssertEqual(P1.Value, match.GetObservation().Turn.TurnPlayerId?.Value ?? 0, "the turn did NOT hand over — the EoT gain kept it going");
    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "the turn reverted to Main (continues)");
    AssertEqual(2, context.MemoryController.Current.Current, "the EoT +3 lifted memory from -1 to +2 (above the turn-end threshold)");
}

async Task MultiActivatedEoTSuspendsThenReapplies()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(), new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceToMainAsync(match, P1);

    EngineContext context = match.Context;
    var cards = (CardDatabase)context.CardRepository;

    // Two "[End of Your Turn] draw 1" activated fixtures in P1's battle area — at OnEndTurn the window collects TWO
    // mandatory activated markers, so the ending player must ORDER them (a choice that suspends the pre-flip drain).
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxEndTurnDraw"), "TfxEndTurnDraw", "EoTDraw",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    for (int i = 0; i < 2; i++)
    {
        var id = new HeadlessEntityId($"p1:field:EOTDRAW{i}");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("TfxEndTurnDraw"), P1,
            Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    }

    // A cost-3 play crosses memory 0 -> -3, ending P1's turn (MemoryPass).
    cards.Upsert(new CardRecord(new HeadlessEntityId("VAN3"), "VAN3", "Vanilla3",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon", PlayCost: 3));
    var hand = new HeadlessEntityId("p1:hand:VAN3");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(hand, new HeadlessEntityId("VAN3"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, hand, ChoiceZone.None, ChoiceZone.Hand));

    context.MemoryController.Set(0);
    LegalAction play = match.GetLegalActions(P1)
        .Single(x => x.ActionType == HeadlessActionTypes.PlayCard && x.Id.Value.Contains(hand.Value, StringComparison.Ordinal));
    await env.StepAsync(play);
    AssertEqual(HeadlessPhase.MemoryPass, match.GetObservation().Turn.Phase, "P1 crossed into MemoryPass");

    // EndTurn opens the [End of Your Turn] order choice instead of ending the turn (the pre-flip drain suspended).
    LegalAction endTurn = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.EndTurn);
    await match.ApplyActionAsync(endTurn);
    await match.StepAsync();
    AssertEqual(P1.Value, match.GetObservation().Turn.TurnPlayerId?.Value ?? 0, "the turn did NOT hand over — an EoT order choice is pending");
    AssertTrue(context.ChoiceController.Current.IsPending, "the two activated [End of Your Turn] effects opened a pending order choice");

    // Resolve every pending window choice (order pick + any follow-up), then re-apply EndTurn until it flips.
    for (int guard = 0; guard < 8 && context.ChoiceController.Current.IsPending; guard++)
    {
        LegalAction resolve = match.GetLegalActions(P1).First(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
        await match.ApplyActionAsync(resolve);
        await match.StepAsync();
    }
    for (int guard = 0; guard < 4 && (match.GetObservation().Turn.TurnPlayerId?.Value ?? 0) == P1.Value; guard++)
    {
        LegalAction endTurn2 = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.EndTurn);
        await env.StepAsync(endTurn2);
    }

    AssertEqual(P2.Value, match.GetObservation().Turn.TurnPlayerId?.Value ?? 0,
        "after the order choice resolved, re-applied EndTurn flips the turn to P2 (no double-fire, no crash)");
}

async Task EoTWindowResolvesBeforeAttack()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(), new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    await AdvanceToMainAsync(match, P1);

    EngineContext context = match.Context;
    var cards = (CardDatabase)context.CardRepository;

    // BT1_021: "[End of Your Turn] lose 3 memory" — a memory effect that resolves in the OnEndTurn window.
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxEndTurnLose3Memory"), "TfxEndTurnLose3Memory", "EoTLoser",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    var eot = new HeadlessEntityId("p1:field:TfxEoTLose3");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(eot, new HeadlessEntityId("TfxEndTurnLose3Memory"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, eot, ChoiceZone.None, ChoiceZone.BattleArea));
    context.RegisterEnteredCardEffects(eot, P1);

    // A <Vortex> Digimon (P1, unsuspended) whose end-of-turn attack offer opens AFTER the OnEndTurn window (task 7);
    // + an opponent Digimon (P2) so Vortex has a legal target.
    var vortex = await PlaceBareDigimon(context, P1, "VTX", dp: 5000, suspended: false);
    var vfx = KeywordBaseBatch2Factory.Create(KeywordBaseBatch2Kind.Vortex, vortex, targetEntityId: null, isInherited: false, isLinked: false);
    context.EffectRegistry.Register(KeywordBaseBatch2Factory.ToBinding(vfx, P1, new EffectContext(P1, vortex)));
    await PlaceBareDigimon(context, P2, "FOE", dp: 3000, suspended: false);

    // A cost-3 play crosses memory 0 -> -3 (MemoryPass).
    cards.Upsert(new CardRecord(new HeadlessEntityId("VANv"), "VANv", "VanillaV",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon", PlayCost: 3));
    var hand = new HeadlessEntityId("p1:hand:VANv");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(hand, new HeadlessEntityId("VANv"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, hand, ChoiceZone.None, ChoiceZone.Hand));
    context.MemoryController.Set(0);
    LegalAction play = match.GetLegalActions(P1)
        .Single(x => x.ActionType == HeadlessActionTypes.PlayCard && x.Id.Value.Contains(hand.Value, StringComparison.Ordinal));
    await env.StepAsync(play);
    AssertEqual(HeadlessPhase.MemoryPass, match.GetObservation().Turn.Phase, "P1 crossed into MemoryPass");

    // EndTurn: the OnEndTurn window drains BT1_021 (memory -3 -> -6) FIRST, THEN the Vortex attack offer opens.
    LegalAction endTurn = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.EndTurn);
    await match.ApplyActionAsync(endTurn);
    await match.StepAsync();

    AssertEqual(P1.Value, match.GetObservation().Turn.TurnPlayerId?.Value ?? 0, "the turn did NOT hand over — the Vortex attack offer is pending");
    AssertTrue(context.ChoiceController.Current.IsPending, "the end-of-turn Vortex attack offer opened (a pending choice)");
    AssertEqual(-6, context.MemoryController.Current.Current,
        "BT1_021's [End of Your Turn] -3 already fired in the OnEndTurn window BEFORE the attack offer (task 7: window-then-attack); pre-fix order would leave -3");
}

// --- Helpers -------------------------------------------------------------

static async Task<HeadlessEntityId> PlaceBareDigimon(EngineContext context, HeadlessPlayerId owner, string tag, int dp, bool suspended)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = suspended }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static MatchConfig BuildMatchConfig()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(playerId).First(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
