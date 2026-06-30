using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-007 (EX8_074 Stage 3, brick 3 — availability + #1<->#2 coupling). With memory floor -10:
// a 6-cost card is UNaffordable at memory -5 (CanPay(6): -5-6=-11 < -10) but the BeforePayCost suspend
// reduction (-4) makes the effective availability cost 2, which IS affordable (CanPay(2): -7 >= -10).
//   - brick 3 makes the card a LEGAL play when >= 2 of your Digimon are suspendable.
//   - the coupling: when the full cost is unaffordable the suspend is MANDATORY (CanSkip=false), so you
//     cannot take the reduced-cost play and then decline the suspend.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Card with >= 2 suspendable Digimon is a legal play at reduced affordability (full unaffordable)", OfferedWhenReducedAffordable),
    ("Card with < 2 suspendable Digimon is NOT offered (no reduction, full unaffordable)", NotOfferedWithoutReduction),
    ("No false offer: with no suspendable Digimon the unaffordable card stays unplayable", NotOfferedNoSuspendable),
    ("Coupling: when full cost is unaffordable the suspend is MANDATORY (CanSkip=false)", MandatoryWhenUnaffordable),
    ("Coupling: when full cost is affordable the suspend is OPTIONAL (CanSkip=true)", OptionalWhenAffordable),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task OfferedWhenReducedAffordable()
{
    EngineContext context = Context();
    context.MemoryController.Set(-5); // full 6 unaffordable; reduced 2 affordable.
    var hand = await PlaceFixtureCard(context, P1, playCost: 6);
    await PlaceDigimon(context, P1, "U1", suspended: false);
    await PlaceDigimon(context, P1, "U2", suspended: false);

    AssertTrue(IsPlayOffered(context, hand), "the card IS a legal play (offered at the reduced availability cost)");
}

async Task NotOfferedWithoutReduction()
{
    EngineContext context = Context();
    context.MemoryController.Set(-5);
    var hand = await PlaceFixtureCard(context, P1, playCost: 6);
    await PlaceDigimon(context, P1, "U1", suspended: false); // only 1 suspendable -> gate closed -> no reduction.

    AssertTrue(!IsPlayOffered(context, hand), "the card is NOT offered (full cost unaffordable, gate closed)");
}

async Task NotOfferedNoSuspendable()
{
    EngineContext context = Context();
    context.MemoryController.Set(-5);
    var hand = await PlaceFixtureCard(context, P1, playCost: 6);
    await PlaceDigimon(context, P1, "S1", suspended: true); // present but already suspended -> not suspendable.

    AssertTrue(!IsPlayOffered(context, hand), "no suspendable Digimon -> unaffordable card stays unplayable");
}

async Task MandatoryWhenUnaffordable()
{
    EngineContext context = Context();
    context.MemoryController.Set(-5); // cannot afford the full 6.
    var hand = await PlaceFixtureCard(context, P1, playCost: 6);
    await PlaceDigimon(context, P1, "U1", suspended: false);
    await PlaceDigimon(context, P1, "U2", suspended: false);

    ChoiceRequest req = SuspendRequest(context, hand);
    AssertTrue(!req.CanSkip, "the suspend cannot be skipped when the reduction is the only way to pay");
    AssertEqual(2, req.MinCount, "exactly 2 must be selected (mandatory)");
}

async Task OptionalWhenAffordable()
{
    EngineContext context = Context();
    context.MemoryController.Set(8); // can afford the full 6.
    var hand = await PlaceFixtureCard(context, P1, playCost: 6);
    await PlaceDigimon(context, P1, "U1", suspended: false);
    await PlaceDigimon(context, P1, "U2", suspended: false);

    ChoiceRequest req = SuspendRequest(context, hand);
    AssertTrue(req.CanSkip, "the suspend is optional when the full cost is affordable");
    AssertEqual(0, req.MinCount, "may select zero (optional)");
}

// --- Helpers -------------------------------------------------------------

bool IsPlayOffered(EngineContext context, HeadlessEntityId cardId) =>
    new PlayCardAction().GetLegalActions(context, P1)
        .Any(a => a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out object? id)
            && id is HeadlessEntityId e && e == cardId);

ChoiceRequest SuspendRequest(EngineContext context, HeadlessEntityId cardId)
{
    var card = new CardSource(context, cardId, P1);
    bool Suspendable(HeadlessEntityId id) =>
        CardEffectCommons.IsOwnerBattleAreaDigimon(card, id) && !CardEffectCommons.IsSuspended(card, id);
    var effect = new SuspendCostReductionEffect(card, Suspendable, suspendCount: 2, costReduction: 4,
        description: "Suspend 2 Digimon to get Play Cost -4");
    return effect.BuildRequest(new[] { P1, P2 });
}

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceFixtureCard(EngineContext context, HeadlessPlayerId owner, int playCost)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("TfxBeforePayCost");
    cards.Upsert(new CardRecord(defId, "TfxBeforePayCost", "BPC",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["playCost"] = playCost }, CardType: "Digimon", PlayCost: playCost));
    var id = new HeadlessEntityId($"{owner.Value}:hand:BPC");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, bool suspended)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
