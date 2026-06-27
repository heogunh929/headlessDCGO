using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-6.4 (remaining): state-change timing windows that are not zone-move-derived.
//   - OnAddDigivolutionCards — opened by DigivolveAction when the previous card is placed under the new
//     top as a digivolution source (scoped to the receiving card).
//   - OnFaceUpSecurityIncreased — opened by the mutation sink when a card is added to security FACE UP.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId Card = new("p1:main:C1");
HeadlessEntityId EvolveCardId = new("p1:main:E1");
HeadlessEntityId TargetCardId = new("p1:main:T1");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Timing constants are defined", () => Pure(ConstantsDefined)),
    ("Face-up AddToSecurity opens OnFaceUpSecurityIncreased", FaceUpSecurityEmits),
    ("Face-down AddToSecurity does NOT open OnFaceUpSecurityIncreased", FaceDownSecurityDoesNotEmit),
    ("Digivolve opens OnAddDigivolutionCards for the receiving card", DigivolveEmitsAddSources),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Tests ---------------------------------------------------------------

void ConstantsDefined()
{
    AssertTrue(!string.IsNullOrWhiteSpace(TriggerTimings.OnAddDigivolutionCards), "OnAddDigivolutionCards defined");
    AssertTrue(!string.IsNullOrWhiteSpace(TriggerTimings.OnFaceUpSecurityIncreased), "OnFaceUpSecurityIncreased defined");
}

async Task FaceUpSecurityEmits()
{
    EngineContext context = await CardOnField();
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Security(Card, faceUp: true));
    await sink.FlushAsync();
    AssertTrue(QueueOpens(context, TriggerTimings.OnFaceUpSecurityIncreased), "face-up add opened the window");
}

async Task FaceDownSecurityDoesNotEmit()
{
    EngineContext context = await CardOnField();
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Security(Card, faceUp: false));
    await sink.FlushAsync();
    AssertFalse(QueueOpens(context, TriggerTimings.OnFaceUpSecurityIncreased), "face-down add did not open the window");
}

async Task DigivolveEmitsAddSources()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 41);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("E1"), "E1", "Evolving", new Dictionary<string, object?>(),
        CardType: "Digimon", EvolutionCost: 2));
    cards.Upsert(new CardRecord(new HeadlessEntityId("T1"), "T1", "Base", new Dictionary<string, object?>(),
        CardType: "Digimon", PlayCost: 3));

    context.CardInstanceRepository.Upsert(new CardInstanceRecord(EvolveCardId, new HeadlessEntityId("E1"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(TargetCardId, new HeadlessEntityId("T1"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, EvolveCardId, ChoiceZone.None, ChoiceZone.Hand));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, TargetCardId, ChoiceZone.None, ChoiceZone.BattleArea));
    context.GameEventQueue.DrainPending();

    LegalAction digivolve = HeadlessActionFactory.Digivolve(P1, EvolveCardId, TargetCardId, memoryCost: 2);
    ActionProcessResult result = await new DigivolveAction().ProcessAsync(digivolve, context);
    AssertTrue(result.IsSuccess, $"digivolve succeeded ({result.Message})");

    AssertTrue(QueueOpens(context, TriggerTimings.OnAddDigivolutionCards), "OnAddDigivolutionCards opened");
}

// --- Helpers -------------------------------------------------------------

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry, context.GameEventQueue);

EffectMutation Security(HeadlessEntityId target, bool faceUp) =>
    new(MatchStateMutationSink.AddToSecurityKind, new HeadlessEntityId("src"),
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = target.Value,
            [MatchStateMutationSink.FaceUpKey] = faceUp,
        });

bool QueueOpens(EngineContext context, string timing) =>
    context.GameEventQueue.DrainPending().Any(e => TriggerTimingMap.Derive(e).Contains(timing));

async Task<EngineContext> CardOnField()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 23);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("C1"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Card, ChoiceZone.None, ChoiceZone.BattleArea));
    context.GameEventQueue.DrainPending();
    return context;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
