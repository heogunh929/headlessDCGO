using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-d: DontBattleSecurityDigimon (AS-IS EX4_013 "Ignore Battle") was MISSING. A revealed security Digimon
// whose intrinsic marker says so must NOT battle the attacker — even a stronger security Digimon leaves the
// attacker alive.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("control: a stronger security Digimon deletes the attacker", () => Run(ignoreBattle: false, expectAttackerAlive: false)),
    ("Ignore Battle: the attacker is NOT battled by the (stronger) security Digimon", () => Run(ignoreBattle: true, expectAttackerAlive: true)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Run(bool ignoreBattle, bool expectAttackerAlive)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 925);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    // (R3-W3c-4b B1) the DontBattleSecurityDigimon interface scan gates each effect with CanUse (→ CanTrigger/
    // CanActivate → GManager.instance), so drive it under an ambient match scope (A1/W3c-1 precedent).
    using var _ambient = AmbientMatchContext.Enter(ctx);
    var cards = (CardDatabase)ctx.CardRepository;

    // Attacker (P1), DP 5000.
    cards.Upsert(new CardRecord(new HeadlessEntityId("ATK"), "ATK", "Atk",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000 }, CardType: "Digimon"));
    var attacker = new HeadlessEntityId("p1:ATK");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(attacker, new HeadlessEntityId("ATK"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, attacker, ChoiceZone.None, ChoiceZone.BattleArea));

    // Security Digimon (P2), DP 7000 — normally deletes the 5000 attacker. CardNumber picks the Ignore-Battle
    // fixture when requested.
    string number = ignoreBattle ? "TfxIgnoreBattle" : "SECDIGI";
    cards.Upsert(new CardRecord(new HeadlessEntityId("SEC"), number, "Sec",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 7000 }, CardType: "Digimon"));
    var sec = new HeadlessEntityId("p2:SEC");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(sec, new HeadlessEntityId("SEC"), P2));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, sec, ChoiceZone.None, ChoiceZone.Security));

    var resolver = new SecurityResolver();
    await resolver.RunSecurityCheckLoopAsync(ctx, (IZoneStateReader)ctx.ZoneMover, P1, attacker, P2, strike: 1);

    bool attackerAlive = ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(attacker);
    if (attackerAlive != expectAttackerAlive)
        throw new InvalidOperationException($"attacker-alive expected {expectAttackerAlive}, got {attackerAlive}");
}
