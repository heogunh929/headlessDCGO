using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// B-2 (P1-5): the main-phase "declare a battle-area permanent's [Main] activated skill" ACTION
// (MainSkillActivateAction) — AS-IS TurnStateMachine.SetActSkill + the declarative main-loop branch
// (TurnStateMachine.cs:1174-1195). Before this, headless had NO way to actively use a permanent [Main] skill
// (OnDeclaration was only proxied at attack). These tests prove: (a) a usable [Main] once-per-turn skill is
// OFFERED as an ActivateMain legal move, (b) declaring it resolves the effect AND consumes the per-turn use,
// (c) once spent it is NOT re-offered this turn (AS-IS CanDeclareSkillList → CanUse cap) and re-processing is
// illegal, (d) a fresh turn (ResetForTurn) restores it, (e) the enumeration is scoped to the acting player's
// own battle area.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("a usable [Main] once-per-turn skill is offered, resolves + consumes, is not re-offered, and re-processing is illegal", DeclareConsumesAndCapsOut),
    ("a fresh turn (ResetForTurn) restores the spent [Main] skill", ResetRestoresDeclaration),
    ("[Main] skill enumeration is scoped to the acting player's own battle area", ScopedToOwnBattleArea),
    ("a non-uniform [Main] Digi-Burst that cannot pay its source cost is NOT offered (AS-IS CanDigiBurst gate)", UnpayableDigiBurstNotOffered),
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

async Task DeclareConsumesAndCapsOut()
{
    EngineContext context = await SetupWithDeclarer(P1, seedLibrary: 3);
    var permanent = new HeadlessEntityId("1:battle:MD");
    var action = new MainSkillActivateAction();

    LegalAction? offered = FindActivateMain(action, context, P1, permanent);
    AssertTrue(offered is not null, "the [Main] once-per-turn skill is offered as an ActivateMain legal move");
    AssertTrue(offered!.ActionType == HeadlessActionTypes.ActivateMain, "the offered action is ActivateMain");

    int before = HandCount(context, P1);
    ActionProcessResult result = await action.ProcessAsync(offered, context);
    AssertTrue(result.IsSuccess, "declaring the [Main] skill succeeds");
    AssertEqual(before + 1, HandCount(context, P1), "the [Main] skill's draw resolved (hand +1)");

    // (B-2) the per-turn use is now spent — the skill is no longer offered this turn (AS-IS CanDeclareSkillList
    // excludes a capped-out effect via CanUse's cap check).
    LegalAction? afterCap = FindActivateMain(action, context, P1, permanent);
    AssertTrue(afterCap is null, "the spent [Main] once-per-turn skill is NOT re-offered this turn");

    // Re-processing the now-capped action is rejected at the legality boundary (Validate re-checks CanDeclareAt).
    ActionProcessResult reProcess = await action.ProcessAsync(offered, context);
    AssertTrue(!reProcess.IsSuccess, "re-processing the capped [Main] skill is illegal (no second draw)");
    AssertEqual(before + 1, HandCount(context, P1), "no second draw occurred");
}

async Task ResetRestoresDeclaration()
{
    EngineContext context = await SetupWithDeclarer(P1, seedLibrary: 3);
    var permanent = new HeadlessEntityId("1:battle:MD");
    var action = new MainSkillActivateAction();

    await action.ProcessAsync(FindActivateMain(action, context, P1, permanent)!, context);
    AssertTrue(FindActivateMain(action, context, P1, permanent) is null, "spent this turn");

    // A new turn resets every card's per-turn use (AS-IS InitUseCountThisTurn) — the skill is declarable again.
    context.OnceFlags.ResetForTurn(turnSequence: 1, turnPlayerId: P1);
    AssertTrue(FindActivateMain(action, context, P1, permanent) is not null, "the [Main] skill is offered again next turn (B-2 cap reset)");
}

async Task ScopedToOwnBattleArea()
{
    // P1 has the declarer; P2 has an identical permanent. Generating P1's legal actions must offer ONLY P1's.
    EngineContext context = await SetupWithDeclarer(P1, seedLibrary: 3);
    var foe = new HeadlessEntityId("2:battle:MD");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(foe, new HeadlessEntityId("TfxMainDeclareDraw"), P2, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, foe, ChoiceZone.None, ChoiceZone.BattleArea));

    var action = new MainSkillActivateAction();
    IReadOnlyList<LegalAction> p1Actions = action.GetLegalActions(context, P1);
    AssertTrue(p1Actions.Any(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out object? v) && Equals(v, new HeadlessEntityId("1:battle:MD"))),
        "P1's own [Main] skill is offered to P1");
    AssertTrue(!p1Actions.Any(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out object? v) && Equals(v, foe)),
        "P2's [Main] skill is NOT offered when generating P1's actions (own-battle-area scope)");
}

async Task UnpayableDigiBurstNotOffered()
{
    // A [Main] <Digi-Burst 2> permanent with NO digivolution sources cannot pay its cost. AS-IS CanDeclareSkillList
    // excludes it (CanUse → CanUseCondition → CanDigiBurst needs >= 2 trashable sources); CanDeclareAt must too,
    // rather than surface a phantom ActivateMain that resolves to a no-op.
    EngineContext context = EngineContext.CreateDefault(randomSeed: 11);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxMainDigiBurstDraw"), "TfxMainDigiBurstDraw", "DB",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var permanent = new HeadlessEntityId("1:battle:DB");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(permanent, new HeadlessEntityId("TfxMainDigiBurstDraw"), P1, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, permanent, ChoiceZone.None, ChoiceZone.BattleArea));

    var action = new MainSkillActivateAction();
    AssertTrue(FindActivateMain(action, context, P1, permanent) is null,
        "an unpayable [Main] Digi-Burst (0 sources < 2) is NOT offered as an ActivateMain legal move");
}

// --- Helpers -------------------------------------------------------------

async Task<EngineContext> SetupWithDeclarer(HeadlessPlayerId owner, int seedLibrary)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 9);
    context.TurnController.Initialize(new[] { P1, P2 }, owner);
    var cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxMainDeclareDraw"), "TfxMainDeclareDraw", "MD",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));

    var permanent = new HeadlessEntityId($"{owner.Value}:battle:MD");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(permanent, new HeadlessEntityId("TfxMainDeclareDraw"), owner, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, permanent, ChoiceZone.None, ChoiceZone.BattleArea));

    for (int i = 0; i < seedLibrary; i++)
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:L{i}"), $"L{i}", $"L{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var lib = new HeadlessEntityId($"{owner.Value}:lib:{i}");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(lib, new HeadlessEntityId($"DEF:L{i}"), owner, Metadata: new Dictionary<string, object?>()));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, lib, ChoiceZone.None, ChoiceZone.Library));
    }

    return context;
}

LegalAction? FindActivateMain(MainSkillActivateAction action, EngineContext context, HeadlessPlayerId playerId, HeadlessEntityId permanent) =>
    action.GetLegalActions(context, playerId)
        .FirstOrDefault(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out object? v) && Equals(v, permanent));

int HandCount(EngineContext context, HeadlessPlayerId playerId) =>
    context.ZoneMover is IZoneStateReader reader ? reader.GetCards(playerId, ChoiceZone.Hand).Count : 0;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual(int expected, int actual, string label) { if (expected != actual) throw new InvalidOperationException($"{label}: expected {expected}, got {actual}."); }
