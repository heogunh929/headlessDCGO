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
    ("(tuck) a field permanent placed UNDER another permanent resets its once-per-turn use (AS-IS RemoveField+PlacePermanent Init)", TuckUnderResetsCardUse),
    ("(link) a card attached as a LINK card resets its once-per-turn use (AS-IS AddLinkCard InitUseCountThisTurn)", LinkAttachResetsCardUse),
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

async Task TuckUnderResetsCardUse()
{
    // AS-IS: every path that puts a field card under another permanent clears its per-turn uses —
    // PlacePermanentToDigivolutionCards runs InitUseCountThisTurn on the tucked card (CardController.cs:3093)
    // after RemoveField Init()-reset the leaving stack (CardObjectController.cs:546-553); Jogress resets all
    // merged sources (:1509-1512); DigiXros materials reset per card (SelectDigiXrosClass.cs:923). The headless
    // tuck primitive (AddSourcesBottomAsync + onceFlags) mirrors that; MindLink/DNA/DigiXros route through it.
    EngineContext context = EngineContext.CreateDefault(randomSeed: 8);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var host = new HeadlessEntityId("p1:battle:HOST");
    var tucked = new HeadlessEntityId("p1:battle:TUCK");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(host, new HeadlessEntityId("DEF:HOST"), P1, Metadata: new Dictionary<string, object?>()));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(tucked, new HeadlessEntityId("DEF:TUCK"), P1, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, tucked, ChoiceZone.None, ChoiceZone.BattleArea));

    EffectRequest reqTucked = OnceRequest("TUCK:ae:eff", tucked);
    EffectRequest reqHost = OnceRequest("HOST:ae:eff", host);
    context.OnceFlags.Consume(reqTucked, 1);
    context.OnceFlags.Consume(reqHost, 1);

    await DigivolutionStackHelpers.AddSourcesBottomAsync(
        context.CardInstanceRepository, context.ZoneMover, host, new[] { tucked }, ChoiceZone.BattleArea,
        onceFlags: context.OnceFlags);

    AssertTrue(context.OnceFlags.CanActivate(reqTucked, 1), "the tucked card's once-per-turn use is FRESH (AS-IS tuck reset)");
    AssertTrue(!context.OnceFlags.CanActivate(reqHost, 1), "the HOST's use is untouched (only the tucked card resets)");
}

async Task LinkAttachResetsCardUse()
{
    // AS-IS AddLinkCard: cardSource.cEntity_EffectController.InitUseCountThisTurn() right after the attach
    // (CardController.cs:3393).
    EngineContext context = EngineContext.CreateDefault(randomSeed: 9);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var host = new HeadlessEntityId("p1:battle:LHOST");
    var link = new HeadlessEntityId("p1:battle:LINK");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(host, new HeadlessEntityId("DEF:LHOST"), P1, Metadata: new Dictionary<string, object?>()));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(link, new HeadlessEntityId("DEF:LINK"), P1, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, link, ChoiceZone.None, ChoiceZone.BattleArea));

    EffectRequest reqLink = OnceRequest("LINK:ae:eff", link);
    context.OnceFlags.Consume(reqLink, 1);

    bool attached = await LinkHelpers.AddLinkCardAsync(
        context.CardInstanceRepository, context.ZoneMover, host, link, ChoiceZone.BattleArea,
        context.GameEventQueue, context: context);
    AssertTrue(attached, "the link card attached");
    AssertTrue(context.OnceFlags.CanActivate(reqLink, 1), "the linked card's once-per-turn use is FRESH (AS-IS AddLinkCard reset)");
}

// --- Helpers -------------------------------------------------------------

EffectRequest OnceRequest(string effectId, HeadlessEntityId source) =>
    new(new HeadlessEntityId(effectId), P1, "OnTest",
        new EffectContext(P1, P1, source, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
