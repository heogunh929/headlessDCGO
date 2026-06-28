using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-2 (Blitz): "When your opponent has 1 or more memory, this Digimon can attack." In the headless
// engine that window is the MemoryPass phase — after memory has handed to the opponent (opponent has
// >=1 memory) but before the turn is actually passed on EndTurn. Normally only EndTurn is offered there;
// a <Blitz> Digimon may instead declare an attack. Consumption lives in AttackPermanentAction's phase
// gate (Main-only, plus MemoryPass for Blitz) and HeadlessLegalActionDispatcher (exposes the action).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("A Blitz Digimon can attack during the memory-pass window", BlitzAttacksInMemoryPass),
    ("A non-Blitz Digimon cannot attack during the memory-pass window", NonBlitzCannotAttackInMemoryPass),
    ("Memory-pass dispatch still offers EndTurn alongside a Blitz attack", MemoryPassStillOffersEndTurn),
    ("A Blitz Digimon attacks normally during the main phase", BlitzAttacksInMainPhase),
    ("A non-Blitz Digimon still attacks normally during the main phase", NonBlitzAttacksInMainPhase),
    ("A Blitz attack declared in the memory-pass window is accepted", BlitzAttackProcessIsAccepted),
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

async Task BlitzAttacksInMemoryPass()
{
    DcgoMatch match = await BaseMatch();
    HeadlessEntityId attacker = await EstablishDigimon(match, P1, blitz: true);

    await PassAsync(match, P1);
    AssertEqual(HeadlessPhase.MemoryPass, match.GetObservation().Turn.Phase, "phase is memory pass");

    AssertTrue(HasDeclaration(match, P1, attacker), "Blitz attacker has an attack declaration in memory pass");
    AssertTrue(HasDeclareAttackAction(match, P1, attacker), "dispatcher exposes a DeclareAttack for the Blitz attacker");
}

async Task NonBlitzCannotAttackInMemoryPass()
{
    DcgoMatch match = await BaseMatch();
    HeadlessEntityId attacker = await EstablishDigimon(match, P1, blitz: false);

    await PassAsync(match, P1);
    AssertEqual(HeadlessPhase.MemoryPass, match.GetObservation().Turn.Phase, "phase is memory pass");

    AssertFalse(HasDeclaration(match, P1, attacker), "non-Blitz attacker produces no declaration in memory pass");
    AssertFalse(HasDeclareAttackAction(match, P1, attacker), "dispatcher exposes no DeclareAttack for the non-Blitz attacker");
}

async Task MemoryPassStillOffersEndTurn()
{
    DcgoMatch match = await BaseMatch();
    await EstablishDigimon(match, P1, blitz: true);

    await PassAsync(match, P1);
    AssertEqual(HeadlessPhase.MemoryPass, match.GetObservation().Turn.Phase, "phase is memory pass");

    AssertTrue(
        match.GetLegalActions(P1).Any(a => a.ActionType == HeadlessActionTypes.EndTurn),
        "memory pass still offers EndTurn even with a Blitz attacker present");
}

async Task BlitzAttacksInMainPhase()
{
    DcgoMatch match = await BaseMatch();
    HeadlessEntityId attacker = await EstablishDigimon(match, P1, blitz: true);

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "still main phase");
    AssertTrue(HasDeclaration(match, P1, attacker), "Blitz attacker can attack in the main phase too");
}

async Task NonBlitzAttacksInMainPhase()
{
    DcgoMatch match = await BaseMatch();
    HeadlessEntityId attacker = await EstablishDigimon(match, P1, blitz: false);

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "still main phase");
    AssertTrue(HasDeclaration(match, P1, attacker), "non-Blitz attacker attacks normally in the main phase (no regression)");
}

async Task BlitzAttackProcessIsAccepted()
{
    DcgoMatch match = await BaseMatch();
    HeadlessEntityId attacker = await EstablishDigimon(match, P1, blitz: true);
    await PassAsync(match, P1);

    LegalAction declare = HeadlessActionFactory.DeclareAttack(
        P1, attacker, P2, targetId: null, isDirectAttack: true);
    ActionProcessResult result = new AttackPermanentAction().Process(declare, match.Context);

    AssertTrue(result.IsSuccess, $"Blitz direct attack is accepted in memory pass ({result.Message})");
    AssertTrue(ReadFlag(match, attacker, "isSuspended"), "the Blitz attacker suspends on declaration");
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
    await match.ApplyActionAsync(HeadlessActionFactory.Pass(player));
    await match.StepAsync();
}

// --- Queries -------------------------------------------------------------

bool HasDeclaration(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId attackerId) =>
    new AttackPermanentAction()
        .GetAttackDeclarations(match.Context, player)
        .Any(declaration => declaration.AttackerId == attackerId);

bool HasDeclareAttackAction(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId attackerId) =>
    match.GetLegalActions(player).Any(a =>
        a.ActionType == HeadlessActionTypes.DeclareAttack &&
        ReadId(a.Parameters, HeadlessActionParameterKeys.AttackerId) == attackerId.Value);

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

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 91, setup: setup));
    await AdvanceToMainAsync(match, P1);
    return match;
}

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    for (var attempt = 0; attempt < 10 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(player)
            .Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
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

static string? ReadId(IReadOnlyDictionary<string, object?> p, string key)
{
    if (!p.TryGetValue(key, out object? raw) || raw is null) return null;
    return raw is HeadlessEntityId id ? id.Value : raw.ToString();
}

// --- Assertions ----------------------------------------------------------

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}
