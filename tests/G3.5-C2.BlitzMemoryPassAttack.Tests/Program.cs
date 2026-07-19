using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-2 (Blitz) — RETIRED phase gate (RD-CATK-BLITZ re-judgment, 2026-07-15). This suite previously asserted an
// INVENTED rule: a <Blitz> Digimon may declare an attack during the MemoryPass phase. AS-IS Blitz is NOT a phase
// permission — it is an [On Play] / [When Digivolving] ActivateClass that fires in the OnEnterFieldAnyone / OnPlay
// window and opens an immediate effect-driven attack (witnesses live in tests/C-Atk-Blitz.Tests).
//
// (4b B6 re-pin) The OLD step driver's MemoryPass WINDOW itself is retired with the AdvancePhase/EndTurn body
// (the pump's EndTurnCheck auto-ends the turn; no interactive memory-pass wait exists), so the three
// memory-pass-window tests are RETIRED WITH THEIR VERIFICATION TARGET:
//   - "A Blitz Digimon no longer has a memory-pass attack"      (asserted the OLD memory-pass legal table)
//   - "A non-Blitz Digimon cannot attack during the memory-pass window" (same OLD table, negative control)
//   - "Memory-pass dispatch still offers EndTurn"                (the OLD dispatcher's EndTurn offering itself)
// The pump exposes NO interactive step outside (Main, PhaseStart), so "nothing can attack in a memory-pass
// window" is vacuously enforced by the driver surface — the surviving real rule is "a manual attack outside
// the attacker's own Main phase is rejected", locked in below on the pump.
//
// The three MAIN-phase tests survive re-pinned onto DcgoMatch.CreatePumpDriven (assertions unchanged).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("A Blitz Digimon attacks normally during the main phase", BlitzAttacksInMainPhase),
    ("A non-Blitz Digimon still attacks normally during the main phase", NonBlitzAttacksInMainPhase),
    ("A manual attack outside the attacker's own Main phase is rejected (gate retired; Blitz included)", AttackOutsideOwnMainIsRejected),
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

async Task BlitzAttacksInMainPhase()
{
    DcgoMatch match = await BaseMatch();
    HeadlessEntityId attacker = await EstablishDigimon(match, P1, blitz: true);

    AssertTrue(match.GetObservation().Turn.IsMainPlayPhase, "still main phase");
    AssertTrue(HasDeclaration(match, P1, attacker), "Blitz attacker can attack in the main phase too");
}

async Task NonBlitzAttacksInMainPhase()
{
    DcgoMatch match = await BaseMatch();
    HeadlessEntityId attacker = await EstablishDigimon(match, P1, blitz: false);

    AssertTrue(match.GetObservation().Turn.IsMainPlayPhase, "still main phase");
    AssertTrue(HasDeclaration(match, P1, attacker), "non-Blitz attacker attacks normally in the main phase (no regression)");
}

async Task AttackOutsideOwnMainIsRejected()
{
    DcgoMatch match = await BaseMatch();
    HeadlessEntityId attacker = await EstablishDigimon(match, P1, blitz: true);

    // P1 passes -> the pump's EndTurnProcess flips the turn (no interactive memory-pass wait exists under
    // the pump). It is now P2's Main: P1's manual attack is outside P1's own Main phase.
    await PassAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));

    LegalAction declare = HeadlessActionFactory.DeclareAttack(
        P1, attacker, P2, targetId: null, isDirectAttack: true);
    ActionProcessResult result;
    using (AmbientMatchContext.Enter(match.Context))
    {
        result = new AttackPermanentAction().Process(declare, match.Context);
    }

    // RD-CATK-BLITZ: with the memory-pass firing-half retired, a manual attack outside the attacker's own
    // Main phase is illegal even for a hasBlitz Digimon.
    AssertFalse(result.IsSuccess, "retired: Blitz manual attack outside its own Main phase is rejected");
    AssertFalse(ReadFlag(match, attacker, "isSuspended"), "the rejected Blitz attacker did not suspend");
}

// --- Action drivers ------------------------------------------------------

async Task<HeadlessEntityId> EstablishDigimon(DcgoMatch match, HeadlessPlayerId player, bool blitz)
{
    // Move a hand card straight to the battle area: unlike PlayCardAction this does not stamp
    // enteredThisTurn, so the Digimon is established (not summoning-sick) and free to attack.
    HeadlessEntityId cardId = HandCard(match, player, index: 1);
    await match.Context.ZoneMover.MoveAsync(
        new ZoneMoveRequest(player, cardId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    if (blitz)
    {
        CardInstanceRecord record = Instance(match, cardId);
        Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal)
        {
            ["hasBlitz"] = true
        };
        match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    return cardId;
}

async Task PassAsync(DcgoMatch match, HeadlessPlayerId player)
{
    LegalAction pass;
    using (AmbientMatchContext.Enter(match.Context))
    {
        pass = match.GetLegalActions(player).Single(a => a.ActionType == HeadlessActionTypes.Pass);
    }
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(pass);
    await match.StepAsync();
    await match.StepAsync();
}

// --- Queries -------------------------------------------------------------

bool HasDeclaration(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId attackerId)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new AttackPermanentAction()
        .GetAttackDeclarations(match.Context, player)
        .Any(declaration => declaration.AttackerId == attackerId);
}

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key)
{
    CardInstanceRecord record = Instance(match, cardId);
    return record.Metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
}

CardInstanceRecord Instance(DcgoMatch match, HeadlessEntityId cardId)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    return record;
}

HeadlessEntityId HandCard(DcgoMatch match, HeadlessPlayerId player, int index)
{
    HeadlessEntityId[] hand = ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(player, ChoiceZone.Hand)
        .OrderBy(id => id.Value, StringComparer.Ordinal)
        .ToArray();
    if (hand.Length < index)
    {
        throw new InvalidOperationException($"Player '{player}' hand has {hand.Length} cards; needed index {index}.");
    }

    return hand[index - 1];
}

// --- Setup ---------------------------------------------------------------

async Task<DcgoMatch> BaseMatch()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 91);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 91, setup: setup));
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
    return match;
}

// --- Phase driving (pump auto-flow, F62/EXEMPLAR-T1 precedent, 4b B6) -----
static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

static async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingDriveAsync(match, skip: decline);
        }
        else await StepOnceDriveAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingDriveAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceDriveAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static CardRecord Digimon(string id)
{
    // PlayCost 0 keeps the card playable from an empty memory pool; the test moves cards onto the field
    // directly, so play/digivolve cost lines are irrelevant.
    return new CardRecord(
        new HeadlessEntityId(id),
        id,
        $"{id} Card",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = 0 },
        CardType: "Digimon",
        PlayCost: 0);
}

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// --- Assertions ----------------------------------------------------------

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}
