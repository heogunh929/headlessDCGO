using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #13 (mapping remediation): ActivateMainOfOptionSide must mirror AS-IS OptionMainEffect — re-run ONLY
// the [Main]-tagged OptionSkill effect, NOT every OptionSkill effect. The fixture TfxMainDisc has TWO OptionSkill
// effects: [Main] (gain 1 memory) and a non-[Main] (draw 1 card). AS-IS resolves only the [Main] one.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ActivateMainOfOptionSide resolves ONLY the [Main] effect (gain 1 memory, no draw)", MainOnly),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task MainOnly()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 913);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (harness triage) DoneStartGame gate: the [Main] OptionSkill's CanTrigger (re-checked on the direct
    // ActivateMainOfOptionSide path, ActivatedEffectResolver.cs:513) returns false before the game start
    // (phase None). AS-IS this re-run only happens mid-play (BT25_104 shared WD/WA), where DoneStartGame is
    // already true — past phase None. Set a live phase so the discriminator (IsMainOptionEffect, already live)
    // is what's under test, not the pre-game guard.
    ctx.TurnController.SetPhase(HeadlessDCGO.Engine.Headless.Runtime.HeadlessPhase.Main);
    ctx.MemoryController.Set(0);

    var cards = (CardDatabase)ctx.CardRepository;
    // CardNumber "TfxMainDisc" makes CardEffectDispatch bind the TfxMainDisc fixture (two OptionSkill effects).
    cards.Upsert(new CardRecord(new HeadlessEntityId("OPT"), "TfxMainDisc", "Opt", new Dictionary<string, object?>(), CardType: "Option"));
    var id = new HeadlessEntityId("p1:battle:OPT");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("OPT"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));

    // Give P1 a non-empty library so a stray draw would be observable.
    for (int i = 0; i < 3; i++)
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId($"L{i}"), $"L{i}", $"L{i}", new Dictionary<string, object?>(), CardType: "Digimon"));
        var lid = new HeadlessEntityId($"p1:lib:L{i}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(lid, new HeadlessEntityId($"L{i}"), P1));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, lid, ChoiceZone.None, ChoiceZone.Library));
    }
    int handBefore = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Hand).Count;

    await CardEffectCommons.ActivateMainOfOptionSide(new CardSource(ctx, id, P1), new CardSource(ctx, id, P1));

    int handAfter = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Hand).Count;
    AssertEqual(1, ctx.MemoryController.Current.Current, "the [Main] effect ran (gained 1 memory)");
    AssertEqual(handBefore, handAfter, "the non-[Main] draw effect did NOT run (hand unchanged)");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
