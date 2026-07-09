using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (RD-1) AS-IS CardController.cs:1526-1529 — every digivolution draws one card (DigivolveCount_ThisTurn++ ;
// new DrawClass(owner,1,null).Draw()). This locks that a normal DigivolveAction draws 1 from the deck and
// bumps the per-turn digivolve counter. Previously the headless port only bumped the counter (no draw).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

await NormalDigivolveDraws();

async Task NormalDigivolveDraws()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 1526);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.MemoryController.Set(5);
    var zones = (IZoneStateReader)context.ZoneMover;
    var cards = (CardDatabase)context.CardRepository;

    // Target Red Lv4 on the battle area; evolving Red@4 card in hand.
    var baseDef = new HeadlessEntityId("REDBASE");
    cards.Upsert(new CardRecord(baseDef, "REDBASE", "RedBase",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" }, ["level"] = 4, ["dp"] = 3000 }, CardType: "Digimon"));
    var target = new HeadlessEntityId("p1:battle:REDBASE");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(target, baseDef, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, target, ChoiceZone.None, ChoiceZone.BattleArea));

    var evoDef = new HeadlessEntityId("EVO");
    cards.Upsert(new CardRecord(evoDef, "EVO", "Evo",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 5, ["fixedDigivolutionCost"] = 2 },
        CardType: "Digimon", EvolutionCondition: "Red@4"));
    var evo = new HeadlessEntityId("p1:hand:EVO");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(evo, evoDef, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = 2 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, evo, ChoiceZone.None, ChoiceZone.Hand));

    // Two cards in the library (deck) so the digivolve draw has something to draw.
    var topOfDeck = new HeadlessEntityId("p1:deck:D1");
    var d2 = new HeadlessEntityId("p1:deck:D2");
    foreach (var id in new[] { topOfDeck, d2 })
    {
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, baseDef, P1));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Library));
    }

    int handBefore = zones.GetCards(P1, ChoiceZone.Hand).Count;        // = 1 (evo)
    int deckBefore = zones.GetCards(P1, ChoiceZone.Library).Count;     // = 2
    int countBefore = context.PlayerTurnCounters.Get(P1, PlayerTurnCounterController.DigivolveCountKey);

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    Check(result.IsSuccess, $"digivolve succeeds ({result.Message})");
    Check(zones.GetCards(P1, ChoiceZone.BattleArea).Contains(evo), "evolving card is the new top on the battle area");

    var handAfter = zones.GetCards(P1, ChoiceZone.Hand);
    int deckAfter = zones.GetCards(P1, ChoiceZone.Library).Count;

    // evo left the hand (now the top), then 1 card was drawn from the deck into hand → net hand count unchanged
    // at 1, but the occupant is the drawn deck card, not the evo card.
    Check(deckAfter == deckBefore - 1, $"digivolve drew 1 card from the deck (deck {deckBefore}->{deckAfter})");
    Check(handAfter.Count == handBefore, $"hand holds the drawn card after evo left it (count {handBefore}->{handAfter.Count})");
    Check(!handAfter.Contains(evo) && handAfter.Contains(topOfDeck), "the hand's card is the drawn deck card, not the evolving card");
    Check(context.PlayerTurnCounters.Get(P1, PlayerTurnCounterController.DigivolveCountKey) == countBefore + 1,
        "the per-turn digivolve counter incremented");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-1 digivolve-draw checks passed.");
