using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons; // Permanent / CardSource / ICardEffect
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;        // CanNotUnsuspendClass (AS-IS kind-class)
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// N-9: the original breeding-area unsuspend loop unsuspends UNCONDITIONALLY — it does not consult the
// CanUnsuspend gate (which governs field permanents). The port's turn-start Unsuspend now bypasses the
// gate for the breeding area while STILL honouring it on the battle area.
//
// (4b A-1) RE-POINTED to the pump-dispatch seat: the OLD step-cadence driver (HeadlessEarlyPhaseFlow's
// phase-step arm) is retired. Under the cutover pump the turn-start unsuspend runs INSIDE
// TurnStateMachine.ActivePhaseAsync (AS-IS ActivePhase :586-624) as the pump auto-flows Active→Draw→Breeding;
// there is no stoppable "Unsuspend phase" step, so the fixture DRIVES the pump past the Active/Unsuspend
// sub-step to the turn-1 Main wait (ExpDriveUntil on TurnController.Current.Phase) and then reads the card's
// suspend state — exactly the state the OLD driver observed after its step into the Unsuspend sub-step.
//
// TRANSLATION (setup currency, not an assertion): the OLD `canUnsuspend=false` INSTANCE-METADATA flag is a
// RETIRED-scaffold gate — only HeadlessEarlyPhaseFlow.TryUnsuspend ever read it. The pump's real unsuspend
// gate is `Permanent.CanUnsuspend`, the AS-IS-literal ICanNotUnsuspendEffect scan over field permanents'
// EffectLists. So "this card cannot unsuspend" is now staged in the pump's OWN currency: a self-scoped
// CanNotUnsuspendClass attached to the card's live EffectList (the AS-IS BT25_061 idiom —
// UntilOwnerTurnEndEffects.Add of a CanNotUnsuspendClass whose PermanentCondition matches the card). Game
// intent is identical (the gate is CLOSED for this card); only the carrier moves from the dead metadata flag
// to the live effect the pump actually consults. No OLD phase-step/end-of-turn action currency remains.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Breeding card unsuspends at turn start even with canUnsuspend=false", BreedingIgnoresGate),
    ("Battle-area card with canUnsuspend=false stays suspended (gate still applies)", FieldHonoursGate),
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

async Task BreedingIgnoresGate()
{
    DcgoMatch match = await SetupAtFirstPause();
    HeadlessEntityId card = StageSuspended(match, P1, "p1:N9-breed", ChoiceZone.BreedingArea);
    GiveCanNotUnsuspend(match, card, P1); // gate CLOSED, yet the breeding-area loop must still unsuspend it.

    await RunUnsuspendPhase(match);

    AssertFalse(ReadBool(match, card, "isSuspended"), "breeding card unsuspended despite canUnsuspend=false");
}

async Task FieldHonoursGate()
{
    DcgoMatch match = await SetupAtFirstPause();
    HeadlessEntityId card = StageSuspended(match, P1, "p1:N9-field", ChoiceZone.BattleArea);
    GiveCanNotUnsuspend(match, card, P1); // gate CLOSED — the battle area must honour it and leave it suspended.

    await RunUnsuspendPhase(match);

    AssertTrue(ReadBool(match, card, "isSuspended"), "battle-area card stays suspended (gate applies)");
}

// --- Drivers -------------------------------------------------------------

async Task RunUnsuspendPhase(DcgoMatch match)
{
    // (TRANSLATION of the OLD `AssertTrue(...IsUnsuspendPhase, "reached Unsuspend phase")`) — the OLD step-model
    // could park AT the (Active, Unsuspending) sub-step; the pump instead auto-flows Active→Draw→Breeding, so the
    // turn-start UnsuspendForTurnPlayer / breeding bypass (TurnStateMachine.ActivePhaseAsync) is not a stoppable
    // pause. Drive the pump past it to the turn-1 Main wait (auto-skipping the StartGame mulligan and the breeding
    // decision); reaching Main is the pump-equivalent proof the Active/Unsuspend step has executed.
    await ExpDriveUntil(match, m => ExpAtMainWait(m, P1));
    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "reached Main (turn-start Unsuspend step ran)");
}

async Task<DcgoMatch> SetupAtFirstPause()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 9);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 9, setup: setup));

    // (preserved) the pre-game setup state — former HeadlessPhase.Setup, now (None, Starting) — holds right after
    // InitializeAsync, before the pump's first step runs StartGame.
    AssertTrue(match.GetObservation().Turn.IsSetupPhase, "starts at Setup");

    await ExpStepOnce(match);

    // Drive to the first pump pause (StartGame Mulligan). This is BEFORE turn-1's Active/Unsuspend step, so a card
    // staged here is on the board when the natural unsuspend runs.
    for (int i = 0; i < 32 && !match.HasPendingChoice() && !ExpAtMainWait(match, P1); i++)
    {
        await ExpStepOnce(match);
    }

    return match;
}

// Stage a fresh, suspended synthetic Digimon directly into a zone (mirrors the FAILd-03 pump-staging idiom).
HeadlessEntityId StageSuspended(DcgoMatch match, HeadlessPlayerId owner, string idText, ChoiceZone zone)
{
    var cards = (CardDatabase)match.Context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{idText}");
    cards.Upsert(new CardRecord(defId, idText, idText,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));

    var instId = new HeadlessEntityId(idText);
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(instId, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = true }));
    match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, instId, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    return instId;
}

// TRANSLATION of the OLD `canUnsuspend=false` metadata flag into the pump's REAL gate: the AS-IS-literal
// ICanNotUnsuspendEffect scan (Permanent.CanUnsuspend). Attach a self-scoped CanNotUnsuspendClass to the card's
// live EffectList exactly as AS-IS BT25_061 does (:209-212). The pump's Active-phase unsuspend then reads
// Permanent.CanUnsuspend == false for this card — the same "cannot unsuspend" state the metadata flag stood for.
void GiveCanNotUnsuspend(DcgoMatch match, HeadlessEntityId cardId, HeadlessPlayerId owner)
{
    var permanent = new Permanent(match.Context, cardId, owner);
    var restriction = new CanNotUnsuspendClass();
    restriction.SetUpICardEffect("Can't Unsuspend", _ => true, permanent.TopCard);
    restriction.SetUpCanNotUntapClass(candidate => candidate.InstanceId == cardId);
    permanent.UntilOwnerTurnEndEffects.Add(_ => restriction);
}

bool ReadBool(DcgoMatch match, HeadlessEntityId cardId, string key)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing instance '{cardId}'.");
    return record.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;
}

// --- Harness (pump) ------------------------------------------------------

static bool ExpAtMainWait(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

static async Task ExpStepOnce(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static async Task ExpApply(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task ExpDriveUntil(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
            LegalAction? resolve;
            using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
            {
                resolve = match.GetLegalActions(chooser)
                    .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                        && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal))
                    ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
            }
            if (resolve is null) { await ExpStepOnce(match); }
            else { await ExpApply(match, resolve); }
        }
        else
        {
            await ExpStepOnce(match);
        }
    }

    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"EXP drive did not reach the main wait — phase:{t.Phase}/{t.StepCursor} " +
            $"pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
