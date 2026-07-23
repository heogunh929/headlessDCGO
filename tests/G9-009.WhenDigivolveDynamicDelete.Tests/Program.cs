using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-009 (EX8-1): the [When Digivolving] dynamic-threshold suspend+delete. The delete target cap is
// 8000 + 3000 * (other suspended Digimon) — evaluated when the delete step builds its candidate list,
// AFTER the suspend has applied. Verified via the fixture's real effects (white-box on the candidate
// predicate) and end-to-end through ActivatedEffectResolver.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Threshold is 8000 with no suspended Digimon: a 10000 DP opponent is NOT deletable", BaseThreshold8000),
    ("Each other suspended Digimon adds 3000: with 1 suspended, the 10000 opponent IS deletable", PlusThreePerSuspended),
    ("Only the opponent's Digimon are delete candidates (your own are not)", OnlyOpponentDeletable),
    ("E2E: suspend 1 then delete — suspending raises the cap so the 10000 opponent is deleted", SuspendThenDeleteE2E),
    // (이연③-b RETIRED) the "ReuseWhenDigivolvingEffect re-runs [When Digivolving]" case was removed with the
    // retired marker class. The re-activation rule is covered live by G9-012.LiveAllTurnsReactivation (real
    // EX8_074 through the play-window pump). This suite keeps the WhenDigivolving suspend+delete assertions.
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

async Task BaseThreshold8000()
{
    EngineContext context = Context();
    var self = await PlaceDigimon(context, P1, "SELF", dp: 5000, suspended: false, cardNumber: "TfxWhenDigivolveDelete");
    var big = await PlaceDigimon(context, P2, "BIG", dp: 10000, suspended: false);

    ChoiceRequest delete = DeleteRequest(context, self);
    AssertTrue(!Offered(delete, big), "a 10000 DP opponent is NOT deletable at the base 8000 threshold");
}

async Task PlusThreePerSuspended()
{
    EngineContext context = Context();
    var self = await PlaceDigimon(context, P1, "SELF", dp: 5000, suspended: false, cardNumber: "TfxWhenDigivolveDelete");
    await PlaceDigimon(context, P1, "ALLY", dp: 5000, suspended: true); // 1 other suspended -> +3000 -> 11000
    var big = await PlaceDigimon(context, P2, "BIG", dp: 10000, suspended: false);

    ChoiceRequest delete = DeleteRequest(context, self);
    AssertTrue(Offered(delete, big), "with 1 suspended Digimon the cap is 11000, so the 10000 opponent IS deletable");
}

async Task OnlyOpponentDeletable()
{
    EngineContext context = Context();
    var self = await PlaceDigimon(context, P1, "SELF", dp: 5000, suspended: false, cardNumber: "TfxWhenDigivolveDelete");
    var ownLow = await PlaceDigimon(context, P1, "OWN", dp: 3000, suspended: false);
    var foeLow = await PlaceDigimon(context, P2, "FOE", dp: 3000, suspended: false);

    ChoiceRequest delete = DeleteRequest(context, self);
    AssertTrue(Offered(delete, foeLow), "a low-DP opponent is deletable");
    AssertTrue(!Offered(delete, ownLow), "your own Digimon is NOT a delete candidate");
}

async Task SuspendThenDeleteE2E()
{
    EngineContext context = Context();
    var self = await PlaceDigimon(context, P1, "SELF", dp: 5000, suspended: false, cardNumber: "TfxWhenDigivolveDelete");
    var ally = await PlaceDigimon(context, P1, "ALLY", dp: 5000, suspended: false);
    var big = await PlaceDigimon(context, P2, "BIG", dp: 10000, suspended: false);

    // Suspend ALLY (raises cap to 11000), then delete the 10000 opponent.
    var provider = (ScriptedChoiceProvider)context.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(ally));
    provider.Enqueue(ChoiceResult.Select(big));

    int resolved = await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.WhenDigivolving);

    AssertEqual(2, resolved, "both [When Digivolving] effects (suspend + delete) resolved");
    AssertTrue(IsSuspended(context, ally), "the chosen ally was suspended");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, big), "the 10000 opponent was deleted (cap was raised to 11000 by the suspend)");
}

// (이연③-b RETIRED) `ReuseWhenDigivolving()` test removed with the retired marker class — see the array
// comment above; the re-activation rule is covered live by G9-012.LiveAllTurnsReactivation.

// --- Helpers -------------------------------------------------------------

// (F4 companion change) TfxWhenDigivolveDelete's delete step was re-ported old-model ActivatedEffect +
// ActivatedSelectEffect -> new-model inline ActivateClass (a GManager SelectPermanentEffect component
// configured directly inside the ActivateCoroutine, no separately-inspectable Body object anymore). This
// helper used to white-box-cast effects[1] to pull out its ChoiceRequest; it now reconstructs the SAME
// candidate predicate (verbatim from the fixture's CanDelete/DeletionMaxDp) and asks a SelectPermanentEffect
// configured identically (maxCount 1 / canNoSelect true / Mode.Destroy) for its request — same thresholds,
// same offered/not-offered outcomes, only the inspection route changed.
ChoiceRequest DeleteRequest(EngineContext context, HeadlessEntityId selfId)
{
    var card = new CardSource(context, selfId, P1);
    var probeActivate = new ActivateClass();
    int DeletionMaxDp() => new Player(context, P1).MaxDP_DeleteEffect(
        8000 + 3000 * CardEffectCommons.MatchConditionPermanentCount(card,
            permanent => CardEffectCommons.IsBattleAreaDigimon(card, permanent.InstanceId)
                && CardEffectCommons.IsSuspended(card, permanent.InstanceId)
                && permanent.InstanceId != card.InstanceId),
        probeActivate);
    bool CanDelete(Permanent permanent) =>
        CardEffectCommons.IsOpponentBattleAreaDigimon(card, permanent.InstanceId) && CardEffectCommons.CurrentDp(card, permanent.InstanceId) <= DeletionMaxDp();

    // GManager.instance resolves from the AmbientMatchContext AsyncLocal scope (null outside one) — the
    // production ActivateCoroutine always runs inside ActivatedEffectResolver's own `AmbientMatchContext.Enter`
    // (ActivatedEffectResolver.cs); this white-box helper calls the GManager component directly, so it must
    // open the same scope itself.
    using var ambientScope = AmbientMatchContext.Enter(context);
    var select = GManager.instance!.GetComponent<SelectPermanentEffect>();
    select.SetUp(
        selectPlayer: P1,
        canTargetCondition: CanDelete,
        canTargetCondition_ByPreSelecetedList: null,
        canEndSelectCondition: null,
        maxCount: 1,
        canNoSelect: true,
        canEndNotMax: false,
        selectPermanentCoroutine: null,
        afterSelectPermanentCoroutine: null,
        mode: SelectPermanentEffect.Mode.Destroy,
        cardEffect: null!);
    return select.BuildRequest((IZoneStateReader)context.ZoneMover, new[] { P1, P2 });
}

static bool Offered(ChoiceRequest req, HeadlessEntityId id) =>
    req.Candidates.Any(c => c.Label.Contains(id.Value, StringComparison.Ordinal));

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (F4 companion change) Initialize() starts the match at HeadlessPhase.Setup (AS-IS pre-mulligan), which
    // makes TurnStateMachine.DoneStartGame false. The new-model ActivateClass's base ICardEffect.CanTrigger
    // gates ALL activation on DoneStartGame (AS-IS: no effect may trigger before the start-game sequence
    // completes) — a gate the old-model ActivatedEffect this fixture previously used did not consult. A
    // [When Digivolving] trigger is by definition mid-game, so advancing past Setup here is a correctness fix
    // for the harness, not a behavior change to what is being tested.
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, int dp, bool suspended, string? cardNumber = null)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId(cardNumber ?? $"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 5 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = suspended }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
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
