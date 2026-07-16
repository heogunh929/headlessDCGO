using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a (mapping remediation): PlacePermanentInSecurityAndProcessAccordingToResult must honour the AS-IS
// isFaceUp parameter (previously hardcoded face-DOWN) AND the CanAddSecurity gate (previously bypassed by the
// direct ZoneMover call). Now routed through the sink's AddToSecurity mutation. A face-up add is observable via
// the OnFaceUpSecurityIncreased timing window (headless does not persist a per-card face-up flag).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("isFaceUp:true opens the OnFaceUpSecurityIncreased window", () => Place(isFaceUp: true, restrict: false, expectFaceUpEvent: true, expectPlaced: true)),
    ("isFaceUp:false does NOT open the face-up window", () => Place(isFaceUp: false, restrict: false, expectFaceUpEvent: false, expectPlaced: true)),
    ("CanNotAddSecurity restriction BLOCKS the placement (stays on the battle area)", () => Place(isFaceUp: true, restrict: true, expectFaceUpEvent: false, expectPlaced: false)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Place(bool isFaceUp, bool restrict, bool expectFaceUpEvent, bool expectPlaced)
{
    EngineContext ctx = Ctx();
    // (R3-W3c-4c B3) the placement routes through the sink's AddToSecurity restriction gate = the live
    // Player.CanAddSecurity scan, which gates each effect with CanUse (→ CanTrigger, DoneStartGame → GManager),
    // so drive the placement under an ambient match scope.
    using var _ambient = AmbientMatchContext.Enter(ctx);
    var card = await Battle(ctx, "DIG");
    ctx.GameEventQueue.DrainPending();   // clear the battle-area move event
    if (restrict)
    {
        // (R3-W3c-4c B3) P1 can't add to security — grant the AS-IS kind-class CannotAddSecurityClass on a
        // separate P1 field permanent (the live scan walks Players_ForTurnPlayer's field permanents); dispatched
        // via the TfxCannotAddSecurity fixture by CardNumber.
        TfxCannotAddSecurity.ScopePlayer = P1;
        TfxCannotAddSecurity.CausingPredicate = null;
        var gdef = new HeadlessEntityId("GRANT");
        ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(gdef, "TfxCannotAddSecurity", "GRANT",
            new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var gid = new HeadlessEntityId("p1:battle:GRANT");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(gid, gdef, P1));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, gid, ChoiceZone.None, ChoiceZone.BattleArea));
        ctx.GameEventQueue.DrainPending();   // clear the grantor's battle-area move event
    }

    await CardEffectCommons.PlacePermanentInSecurityAndProcessAccordingToResult(
        new Permanent(ctx, card, P1), toTop: true, new CardSource(ctx, card, P1),
        successProcess: null, failureProcess: null, isFaceUp: isFaceUp);

    var zones = (IZoneStateReader)ctx.ZoneMover;
    bool inSecurity = zones.GetCards(P1, ChoiceZone.Security).Contains(card);
    bool onBattle = zones.GetCards(P1, ChoiceZone.BattleArea).Contains(card);
    AssertTrue(inSecurity == expectPlaced, $"placed-in-security == {expectPlaced}");
    if (!expectPlaced)
    {
        AssertTrue(onBattle, "blocked placement leaves the card on the battle area");
    }

    bool faceUpEvent = ctx.GameEventQueue.DrainPending().Any(e =>
        e.Metadata.TryGetValue(AutoProcessingTriggerCollector.TriggerTimingKey, out object? t)
        && t as string == TriggerTimings.OnFaceUpSecurityIncreased);
    AssertTrue(faceUpEvent == expectFaceUpEvent, $"OnFaceUpSecurityIncreased emitted == {expectFaceUpEvent}");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 916);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);   // (R3-W3c-4c B3) past Setup so CanUse→CanTrigger passes.
    return ctx;
}

async Task<HeadlessEntityId> Battle(EngineContext ctx, string tag)
{
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(tag), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(tag), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
