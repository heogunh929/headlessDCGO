using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// B-3 (P1-6): a card that RE-ENTERS a play context (played, replayed from trash, de-digivolved, re-stacked) gets
// FRESH once-per-turn uses — AS-IS CardSource.Init() → InitUseCountThisTurn(). The headless keys the per-turn cap
// by the card INSTANCE (stable across a re-play), so without a reset a use spent in an earlier stint this turn
// would linger and wrongly cap the effect on re-entry.
// (uniform-사멸 flip / R6-Da'-6 D3) Re-targeted from the retired invented OnceFlags string-key model onto the
// AS-IS surface itself: uses register on the per-instance CEntity_EffectController (RegisterUseEffectThisTurn /
// isOverMaxCountPerTurn), and the reset seats call CEntity_EffectControllerStore.ResetUseCountForCard —
// CardEffectRegistrar.RegisterCard (enter-play), DigivolutionStackHelpers (tuck), LinkHelpers (link attach).
// This proves: (a) re-entering play refreshes that card's use, (b) other cards are untouched, (c) tuck and link
// attach reset the moved card only.

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
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(cardY, new HeadlessEntityId("DEF:Y"), P1, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, cardY, ChoiceZone.None, ChoiceZone.BattleArea));

    // Card X and card Y each spend a once-per-turn effect this turn (registered on their OWN instance's
    // CEntity_EffectController — the AS-IS UseEffectsThisTurn list).
    (CardSource srcX, ICardEffect effX) = CappedEffect(context, cardX);
    (CardSource srcY, ICardEffect effY) = CappedEffect(context, cardY);
    srcX.cEntity_EffectController.RegisterUseEffectThisTurn(effX);
    srcY.cEntity_EffectController.RegisterUseEffectThisTurn(effY);
    AssertTrue(srcX.cEntity_EffectController.isOverMaxCountPerTurn(effX, 1), "card X's once-per-turn is spent");
    AssertTrue(srcY.cEntity_EffectController.isOverMaxCountPerTurn(effY, 1), "card Y's once-per-turn is spent");

    // Card X re-enters play (e.g., bounced to hand then replayed) — the enter-play hook resets ITS use only.
    context.RegisterEnteredCardEffects(cardX, P1);

    AssertTrue(!srcX.cEntity_EffectController.isOverMaxCountPerTurn(effX, 1), "card X's once-per-turn use is FRESH after re-entering play (B-3)");
    AssertTrue(srcY.cEntity_EffectController.isOverMaxCountPerTurn(effY, 1), "card Y's use is UNTOUCHED (per-card reset, not a turn-wide reset)");
}

async Task TuckUnderResetsCardUse()
{
    // AS-IS: every path that puts a field card under another permanent clears its per-turn uses —
    // PlacePermanentToDigivolutionCards runs InitUseCountThisTurn on the tucked card (CardController.cs:3093)
    // after RemoveField Init()-reset the leaving stack (CardObjectController.cs:546-553); Jogress resets all
    // merged sources (:1509-1512); DigiXros materials reset per card (SelectDigiXrosClass.cs:923). The headless
    // tuck primitive (AddSourcesBottomAsync + context) mirrors that; MindLink/DNA/DigiXros route through it.
    EngineContext context = EngineContext.CreateDefault(randomSeed: 8);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var host = new HeadlessEntityId("p1:battle:HOST");
    var tucked = new HeadlessEntityId("p1:battle:TUCK");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(host, new HeadlessEntityId("DEF:HOST"), P1, Metadata: new Dictionary<string, object?>()));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(tucked, new HeadlessEntityId("DEF:TUCK"), P1, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, tucked, ChoiceZone.None, ChoiceZone.BattleArea));

    (CardSource srcT, ICardEffect effT) = CappedEffect(context, tucked);
    (CardSource srcH, ICardEffect effH) = CappedEffect(context, host);
    srcT.cEntity_EffectController.RegisterUseEffectThisTurn(effT);
    srcH.cEntity_EffectController.RegisterUseEffectThisTurn(effH);

    await DigivolutionStackHelpers.AddSourcesBottomAsync(
        context.CardInstanceRepository, context.ZoneMover, host, new[] { tucked }, ChoiceZone.BattleArea,
        context: context);

    AssertTrue(!srcT.cEntity_EffectController.isOverMaxCountPerTurn(effT, 1), "the tucked card's once-per-turn use is FRESH (AS-IS tuck reset)");
    AssertTrue(srcH.cEntity_EffectController.isOverMaxCountPerTurn(effH, 1), "the HOST's use is untouched (only the tucked card resets)");
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

    (CardSource srcL, ICardEffect effL) = CappedEffect(context, link);
    srcL.cEntity_EffectController.RegisterUseEffectThisTurn(effL);
    AssertTrue(srcL.cEntity_EffectController.isOverMaxCountPerTurn(effL, 1), "precondition: the link card's use is spent");

    bool attached = await LinkHelpers.AddLinkCardAsync(
        context.CardInstanceRepository, context.ZoneMover, host, link, ChoiceZone.BattleArea,
        context.GameEventQueue, context: context);
    AssertTrue(attached, "the link card attached");
    AssertTrue(!srcL.cEntity_EffectController.isOverMaxCountPerTurn(effL, 1), "the linked card's once-per-turn use is FRESH (AS-IS AddLinkCard reset)");
}

// --- Helpers -------------------------------------------------------------

// A [Once Per Turn]-capped ActivateClass bound to the card instance — the AS-IS cap currency
// (EffectSourceCard + empty HashString partition; register/check on the instance's CEntity_EffectController).
(CardSource Card, ICardEffect Effect) CappedEffect(EngineContext context, HeadlessEntityId id)
{
    var card = new CardSource(context, id, P1);
    var activateClass = new ActivateClass();
    activateClass.SetUpICardEffect("test capped effect", _ => true, card);
    activateClass.SetUpActivateClass(null, _ => Task.CompletedTask, 1, false, "test capped effect");
    return (card, activateClass);
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
