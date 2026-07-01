using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G9-013 (LA-4): the LA-1 [When Digivolving] and LA-3 [All Turns] live windows resume safely under an
// interactive DeferredChoiceProvider — the SAME action-processor resume seam that already drives Option /
// Security activations (MetadataActionProcessor.ResolveChoiceAsync -> DeferredActivations.Pending re-invoke).
// G9-011 / G9-012 only exercised the SYNCHRONOUS (ScriptedChoiceProvider) path; here each window suspends at
// the choice (commit-once: the digivolve / play happened, but the effect applied nothing yet) and a follow-up
// ResolveChoice action — routed through MetadataActionProcessor, NOT a direct resolver call — replays the
// agent's answer and advances the activation, WITHOUT re-running the originating action.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("[When Digivolving] suspends under DeferredChoiceProvider, two ResolveChoice rounds resume it (no re-digivolve)", WhenDigivolveDeferredResume),
    ("[All Turns] reactivation suspends on a play, two ResolveChoice rounds resume it (no re-play)", AllTurnsDeferredResume),
    ("(brick 2b) BeforePayCost play suspends pre-payment; ResolveChoice resumes and finishes the play at the reduced cost", BeforePayCostDeferredPlay),
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

// LA-4 over LA-1: digivolving onto EX8_074 with a DeferredChoiceProvider suspends at the first
// [When Digivolving] choice; ResolveChoice (through the processor) resumes round by round.
async Task WhenDigivolveDeferredResume()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var processor = new MetadataActionProcessor();

    var baseDigimon = await Place(context, P1, "BASE", "BASE", ChoiceZone.BattleArea, dp: 3000, level: 4);
    var evolve = await PlaceEvolve(context, P1, "EX8_074", digivolveCost: 2);
    var ally = await Place(context, P1, "ALLY", "ALLY", ChoiceZone.BattleArea, dp: 4000, level: 4);
    var foe = await Place(context, P2, "FOE", "FOE", ChoiceZone.BattleArea, dp: 7000, level: 4);

    // 1) Digivolve through the processor. The [When Digivolving] suspend choice surfaces and suspends.
    ActionProcessResult digivolve = await processor.ProcessAsync(
        HeadlessActionFactory.Digivolve(P1, evolve, baseDigimon, memoryCost: 2), context);

    AssertTrue(digivolve.IsSuccess, $"digivolve action returned success ({digivolve.Message})");
    AssertTrue(context.ChoiceController.Current.IsPending, "first [When Digivolving] choice surfaced to the agent");
    AssertTrue(context.DeferredActivations.HasPending, "activation suspended (DeferredActivations holds it)");
    AssertEqual(EffectTiming.WhenDigivolving, context.DeferredActivations.Pending!.Timing, "suspended at the WhenDigivolving timing");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, evolve), "commit-once: EX8_074 is already the new top");
    AssertTrue(!IsSuspended(context, ally) && InZone(context, P2, ChoiceZone.BattleArea, foe), "nothing applied yet while suspended");

    // 2) ResolveChoice (suspend ALLY) through the processor -> re-invokes the activation, which re-suspends
    //    for the SECOND choice (delete). commit-once: the suspend is staged but not yet flushed.
    ActionProcessResult round1 = await processor.ProcessAsync(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(ally)), context);

    AssertTrue(round1.IsSuccess, $"first ResolveChoice succeeded ({round1.Message})");
    AssertTrue(context.ChoiceController.Current.IsPending, "second [When Digivolving] choice (delete) now pending");
    AssertTrue(context.DeferredActivations.HasPending, "activation still suspended between the two choices");
    // The Destroy step is staged until the activation completes (commit-once): the opponent is not deleted
    // until the delete choice is answered. (The Tap mutation upserts immediately, so the suspend is allowed
    // to have applied already — the meaningful "not done yet" signal is the still-pending activation.)
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, foe), "the opponent delete has NOT applied yet (still suspended)");

    // 3) ResolveChoice (delete FOE) through the processor -> activation completes; both effects apply once.
    ActionProcessResult round2 = await processor.ProcessAsync(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(foe)), context);

    AssertTrue(round2.IsSuccess, $"second ResolveChoice succeeded ({round2.Message})");
    AssertTrue(!context.DeferredActivations.HasPending, "activation cleared after it completes");
    AssertTrue(!context.ChoiceController.Current.IsPending, "no pending choice remains");
    AssertTrue(IsSuspended(context, ally), "[When Digivolving] suspended the chosen ally");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, foe), "[When Digivolving] deleted the chosen opponent");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, evolve), "EX8_074 stayed the top (no re-digivolve)");
}

// LA-4 over LA-3: an EX8_074 holder in the battle area reacts to ANOTHER Digimon being played. Under a
// DeferredChoiceProvider the [All Turns] reactivation (its [When Digivolving] effects) suspends; ResolveChoice
// through the processor resumes it round by round, and the once-per-turn guard prevents a second firing.
async Task AllTurnsDeferredResume()
{
    EngineContext context = Context();
    context.MemoryController.Set(10);
    var processor = new MetadataActionProcessor();

    var holder = await Place(context, P1, "EX8_074", "HOLDER", ChoiceZone.BattleArea, dp: 6000, level: 5);
    var ally = await Place(context, P1, "ALLY", "ALLY", ChoiceZone.BattleArea, dp: 4000, level: 4);
    var foe = await Place(context, P2, "FOE", "FOE", ChoiceZone.BattleArea, dp: 7000, level: 4);
    var played = await Place(context, P1, "PLAYED", "PLAYED", ChoiceZone.Hand, dp: 3000, level: 3, playCost: 3);

    // 1) Play another Digimon through the processor. The holder's [All Turns] reactivation suspends at the
    //    first re-activated [When Digivolving] choice (suspend).
    ActionProcessResult play = await processor.ProcessAsync(
        HeadlessActionFactory.PlayCard(P1, played, memoryCost: 3), context);

    AssertTrue(play.IsSuccess, $"play action returned success ({play.Message})");
    AssertTrue(context.ChoiceController.Current.IsPending, "[All Turns] reactivation surfaced its first choice");
    AssertTrue(context.DeferredActivations.HasPending, "reactivation suspended (DeferredActivations holds the holder)");
    AssertEqual(EffectTiming.OnEnterFieldAnyone, context.DeferredActivations.Pending!.Timing, "suspended at the [All Turns] (OnEnterFieldAnyone) timing");
    AssertEqual(holder.Value, context.DeferredActivations.Pending!.CardId.Value, "the suspended activation is the EX8_074 holder, not the played card");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, played), "commit-once: the played Digimon already entered");
    AssertTrue(!IsSuspended(context, ally) && InZone(context, P2, ChoiceZone.BattleArea, foe), "nothing applied yet while suspended");

    // 2) ResolveChoice (suspend ALLY) -> re-suspends for the delete choice.
    ActionProcessResult round1 = await processor.ProcessAsync(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(ally)), context);

    AssertTrue(round1.IsSuccess, $"first ResolveChoice succeeded ({round1.Message})");
    AssertTrue(context.DeferredActivations.HasPending, "reactivation still suspended between choices");

    // 3) ResolveChoice (delete FOE) -> completes; the re-activated [When Digivolving] applies once.
    ActionProcessResult round2 = await processor.ProcessAsync(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(foe)), context);

    AssertTrue(round2.IsSuccess, $"second ResolveChoice succeeded ({round2.Message})");
    AssertTrue(!context.DeferredActivations.HasPending, "reactivation cleared after it completes");
    AssertTrue(IsSuspended(context, ally), "[All Turns] re-activation suspended the chosen ally");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, foe), "[All Turns] re-activation deleted the chosen opponent");
}

// brick 2b: playing EX8_074 with a DeferredChoiceProvider suspends at the BeforePayCost "suspend 2 Digimon
// to reduce cost by 4" choice — the card is still in hand, nothing paid. ResolveChoice (through the processor)
// replays the 2-Digimon selection and FINISHES the play: cost 11 -> 7 paid, EX8_074 enters the battle area.
// At 0 memory only the reduced cost is affordable, so the play exists only because the reduction resolved.
async Task BeforePayCostDeferredPlay()
{
    EngineContext context = Context();
    context.MemoryController.Set(0);
    var processor = new MetadataActionProcessor();

    var ex8 = await PlaceDigimonInHand(context, P1, "EX8_074", playCost: 11);
    var s1 = await Place(context, P1, "S1", "S1", ChoiceZone.BattleArea, dp: 3000, level: 4);
    var s2 = await Place(context, P1, "S2", "S2", ChoiceZone.BattleArea, dp: 3000, level: 4);

    // 1) Play EX8_074. Its BeforePayCost suspend-2 choice surfaces and suspends — nothing paid/moved yet.
    ActionProcessResult play = await processor.ProcessAsync(
        HeadlessActionFactory.PlayCard(P1, ex8, memoryCost: 11), context);

    AssertTrue(play.IsSuccess, $"play action returned success ({play.Message})");
    AssertTrue(context.ChoiceController.Current.IsPending, "BeforePayCost suspend choice surfaced to the agent");
    AssertTrue(context.DeferredActivations.HasPending, "the play suspended (DeferredActivations holds it)");
    AssertEqual(EffectTiming.BeforePayCost, context.DeferredActivations.Pending!.Timing, "suspended at the BeforePayCost timing");
    AssertTrue(InZone(context, P1, ChoiceZone.Hand, ex8), "commit-once: EX8_074 is STILL in hand (not yet paid/moved)");
    AssertEqual(0, context.MemoryController.Current.Current, "no cost paid yet (memory still 0)");

    // 2) ResolveChoice selecting the 2 Digimon -> reduction resolves, play FINISHES at reduced cost (11-4=7).
    ActionProcessResult resume = await processor.ProcessAsync(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(s1, s2)), context);

    AssertTrue(resume.IsSuccess, $"ResolveChoice succeeded ({resume.Message})");
    AssertTrue(!context.DeferredActivations.HasPending, "activation cleared after the play finishes");
    AssertTrue(IsSuspended(context, s1) && IsSuspended(context, s2), "the 2 chosen Digimon were suspended to pay the reduction");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, ex8), "EX8_074 entered the battle area (play completed)");
    AssertTrue(!InZone(context, P1, ChoiceZone.Hand, ex8), "EX8_074 left the hand");
    AssertEqual(-7, context.MemoryController.Current.Current, "reduced cost 7 paid once: 0 -> -7 (no re-pay)");
}

// --- Helpers -------------------------------------------------------------

// A Digimon (dispatch-discoverable by card number) placed in hand with a play cost.
async Task<HeadlessEntityId> PlaceDigimonInHand(EngineContext context, HeadlessPlayerId owner, string cardNumber, int playCost)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 11000, ["level"] = 6 },
        CardType: "Digimon", PlayCost: playCost));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 913, deferredChoice: true);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string cardNumber, string tag, ChoiceZone zone, int dp, int level, int playCost = 0)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level }, CardType: "Digimon", PlayCost: playCost));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

// Evolve card in hand: EvolutionCondition=null matches any target; fixedDigivolutionCost sets the cost.
async Task<HeadlessEntityId> PlaceEvolve(EngineContext context, HeadlessPlayerId owner, string cardNumber, int digivolveCost)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 5, ["fixedDigivolutionCost"] = digivolveCost },
        CardType: "Digimon", EvolutionCondition: null));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = digivolveCost }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

bool IsSuspended(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null
    && r.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

static bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
