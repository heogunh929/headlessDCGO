using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// B-3 (P1-6): a card that RE-ENTERS a play context (played, replayed from trash, de-digivolved, re-stacked) gets
// FRESH once-per-turn uses — AS-IS CardSource.Init() → InitUseCountThisTurn(). The headless keys the per-turn cap by
// the card INSTANCE (stable across a re-play), so without a reset a use spent in an earlier stint this turn would
// linger and wrongly cap the effect on re-entry. CardEffectRegistrar.RegisterCard (the enter-play hook) now resets
// ONLY the entering card's counts. This proves: (a) re-entering play refreshes that card's use, (b) other cards are
// untouched.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("re-entering play resets the entering card's once-per-turn use, and only that card's", ReEnterPlayResetsCardUse),
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

async Task ReEnterPlayResetsCardUse()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)context.CardRepository;

    // A real card def (dispatches), placed in P1's battle area, so RegisterCard runs its enter-play path.
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOncePerTurnInteractiveTrash"), "TfxOncePerTurnInteractiveTrash", "X",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var cardX = new HeadlessEntityId("p1:battle:X");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(cardX, new HeadlessEntityId("TfxOncePerTurnInteractiveTrash"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, cardX, ChoiceZone.None, ChoiceZone.BattleArea));
    var cardY = new HeadlessEntityId("p1:battle:Y");

    // Card X and card Y each spend a once-per-turn effect this turn (keyed by their own instance as the source).
    EffectRequest reqX = OnceRequest("X:ae:eff", cardX);
    EffectRequest reqY = OnceRequest("Y:ae:eff", cardY);
    context.OnceFlags.Consume(reqX, 1);
    context.OnceFlags.Consume(reqY, 1);
    AssertTrue(!context.OnceFlags.CanActivate(reqX, 1), "card X's once-per-turn is spent");
    AssertTrue(!context.OnceFlags.CanActivate(reqY, 1), "card Y's once-per-turn is spent");

    // Card X re-enters play (e.g., bounced to hand then replayed) — the enter-play hook resets ITS use only.
    context.RegisterEnteredCardEffects(cardX, P1);

    AssertTrue(context.OnceFlags.CanActivate(reqX, 1), "card X's once-per-turn use is FRESH after re-entering play (B-3)");
    AssertTrue(!context.OnceFlags.CanActivate(reqY, 1), "card Y's use is UNTOUCHED (per-card reset, not a turn-wide reset)");
}

// --- Helpers -------------------------------------------------------------

EffectRequest OnceRequest(string effectId, HeadlessEntityId source) =>
    new(new HeadlessEntityId(effectId), P1, "OnTest",
        new EffectContext(P1, P1, source, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
