// PRIM: triggered-activated resolution bridge — a card's ACTIVATED effect ([When Attacking] draw 1) at a general
// trigger timing (OnAllyAttack) is now resolved by GameFlowProcessor auto-processing (previously dropped — only
// IHeadlessCardEffect mutation triggers resolved). Fixture: TfxWhenAttackDraw.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Attacker = new("1:battle:ATK");

var tests = new (string Name, Func<Task> Body)[]
{
    ("[When Attacking] draw 1 (activated) fires via the auto-processing bridge on OnAllyAttack", DrawsOnAttack),
    ("a NON-subject card's OnAllyAttack does not fire another card's activated trigger", ScopedToSubject),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task DrawsOnAttack()
{
    EngineContext ctx = await Setup();
    int before = HandCount(ctx, P1);
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnAllyAttack, actor: P1, subject: Attacker);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before + 1, HandCount(ctx, P1), "the attacker's [When Attacking] draw 1 resolved via the bridge");
}

async Task ScopedToSubject()
{
    EngineContext ctx = await Setup();
    int before = HandCount(ctx, P1);
    // subject = a different card with no activated trigger -> the attacker's draw must NOT fire.
    var other = new HeadlessEntityId("1:battle:OTHER");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(other, new HeadlessEntityId("DEF:O"), P1, Metadata: new Dictionary<string, object?>()));
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnAllyAttack, actor: P1, subject: other);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(before, HandCount(ctx, P1), "another card's attack did not fire the fixture's draw");
}

// --- Harness ---
async Task<EngineContext> Setup()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 9);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxWhenAttackDraw"), "TfxWhenAttackDraw", "Atk", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(Attacker, new HeadlessEntityId("TfxWhenAttackDraw"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Attacker, ChoiceZone.None, ChoiceZone.BattleArea));
    for (int i = 1; i <= 5; i++)
    {
        var lib = new HeadlessEntityId($"1:lib:{i}");
        cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:L{i}"), $"L{i}", $"L{i}", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(lib, new HeadlessEntityId($"DEF:L{i}"), P1, Metadata: new Dictionary<string, object?>()));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, lib, ChoiceZone.None, ChoiceZone.Library));
    }
    return ctx;
}
int HandCount(EngineContext ctx, HeadlessPlayerId p) => ((IZoneStateReader)ctx.ZoneMover).GetCards(p, ChoiceZone.Hand).Count;
static void AssertEqual<T>(T e, T a, string l) { if (!EqualityComparer<T>.Default.Equals(e,a)) throw new InvalidOperationException($"{l}: expected '{e}', actual '{a}'."); }
