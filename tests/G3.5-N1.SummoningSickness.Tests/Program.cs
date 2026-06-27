using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// N-1 (summoning sickness): a Digimon that entered the field this turn cannot attack until its
// controller's next turn unless it has Rush. The engine now SETS this on play (PlayCardAction),
// INHERITS it on digivolve (DigivolveAction keeps the existing permanent's status), and CLEARS it at
// the controller's Unsuspend step (HeadlessEarlyPhaseFlow). Previously the consumption check existed
// (AttackPermanentAction) but nothing set the flag, so freshly played Digimon could attack instantly.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Playing a Digimon marks it summoning-sick and blocks its attack", PlayMarksSickAndBlocksAttack),
    ("A played Digimon with Rush can attack the same turn", RushBypassesSickness),
    ("The summoning-sickness flag is cleared at the controller's next turn", NextTurnClearsSickness),
    ("Digivolving onto an established Digimon inherits not-sick and can attack", DigivolveInheritsNotSick),
    ("Digivolving onto a freshly played Digimon stays sick and cannot attack", DigivolveInheritsSick),
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

async Task PlayMarksSickAndBlocksAttack()
{
    DcgoMatch match = await BaseMatch();
    HeadlessEntityId cardId = HandCard(match, P1, index: 1);

    await PlayAsync(match, P1, cardId);

    AssertTrue(ReadFlag(match, cardId, "enteredThisTurn"), "played card is marked entered-this-turn");
    AssertFalse(HasDeclaration(match, P1, cardId), "summoning-sick Digimon produces no attack declaration");
}

async Task RushBypassesSickness()
{
    DcgoMatch match = await BaseMatch(rushDefinitions: true);
    HeadlessEntityId cardId = HandCard(match, P1, index: 1);

    await PlayAsync(match, P1, cardId);

    AssertTrue(ReadFlag(match, cardId, "enteredThisTurn"), "rush card still entered this turn");
    AssertTrue(HasDeclaration(match, P1, cardId), "rush Digimon can attack the same turn");
}

async Task NextTurnClearsSickness()
{
    DcgoMatch match = await BaseMatch();
    HeadlessEntityId cardId = HandCard(match, P1, index: 1);
    await PlayAsync(match, P1, cardId);
    AssertFalse(HasDeclaration(match, P1, cardId), "sick on the turn it was played");

    // End P1's turn, play out P2's turn, and return to P1 — the Unsuspend step clears the flag.
    await EndTurnAsync(match, P1);
    await AdvanceToMainAsync(match, P2);
    await EndTurnAsync(match, P2);
    await AdvanceToMainAsync(match, P1);

    AssertFalse(ReadFlag(match, cardId, "enteredThisTurn"), "flag cleared at the controller's next turn");
    AssertTrue(HasDeclaration(match, P1, cardId), "no longer summoning-sick next turn");
}

async Task DigivolveInheritsNotSick()
{
    DcgoMatch match = await BaseMatch();
    // Under-card has been on the field since a prior turn (no entered-this-turn flag).
    HeadlessEntityId underCard = HandCard(match, P1, index: 1);
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, underCard, ChoiceZone.Hand, ChoiceZone.BattleArea));

    HeadlessEntityId evolving = HandCard(match, P1, index: 2);
    await DigivolveAsync(match, P1, evolving, underCard);

    AssertFalse(ReadFlag(match, evolving, "enteredThisTurn"), "evolved Digimon inherits not-sick");
    AssertTrue(HasDeclaration(match, P1, evolving), "evolved established Digimon can attack");
}

async Task DigivolveInheritsSick()
{
    DcgoMatch match = await BaseMatch();
    // Under-card was played THIS turn, so it is summoning-sick; digivolving inherits that.
    HeadlessEntityId underCard = HandCard(match, P1, index: 1);
    await PlayAsync(match, P1, underCard);
    AssertTrue(ReadFlag(match, underCard, "enteredThisTurn"), "under-card sick after play");

    HeadlessEntityId evolving = HandCard(match, P1, index: 2);
    await DigivolveAsync(match, P1, evolving, underCard);

    AssertTrue(ReadFlag(match, evolving, "enteredThisTurn"), "evolved Digimon inherits sick");
    AssertFalse(HasDeclaration(match, P1, evolving), "evolved freshly played Digimon cannot attack");
}

// --- Action drivers ------------------------------------------------------

async Task PlayAsync(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId cardId)
{
    LegalAction play = match.GetLegalActions(player)
        .Single(a => a.ActionType == HeadlessActionTypes.PlayCard &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.CardId) == cardId.Value);
    await match.ApplyActionAsync(play);
    await match.StepAsync();
}

async Task DigivolveAsync(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId cardId, HeadlessEntityId targetCardId)
{
    LegalAction digivolve = match.GetLegalActions(player)
        .Single(a => a.ActionType == HeadlessActionTypes.Digivolve &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.CardId) == cardId.Value &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.TargetCardId) == targetCardId.Value);
    await match.ApplyActionAsync(digivolve);
    await match.StepAsync();
}

async Task EndTurnAsync(DcgoMatch match, HeadlessPlayerId player)
{
    await match.ApplyActionAsync(HeadlessActionFactory.EndTurn(player));
    await match.StepAsync();
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

// --- Queries -------------------------------------------------------------

bool HasDeclaration(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId attackerId) =>
    new AttackPermanentAction()
        .GetAttackDeclarations(match.Context, player)
        .Any(declaration => declaration.AttackerId == attackerId);

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    return record.Metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
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

async Task<DcgoMatch> BaseMatch(bool rushDefinitions = false)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 91);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}", rushDefinitions));
        cards.Upsert(Digimon($"P2-M{index:D2}", rush: false));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 91, setup: setup));
    await AdvanceToMainAsync(match, P1);
    return match;
}

static CardRecord Digimon(string id, bool rush)
{
    Dictionary<string, object?> metadata = new(StringComparer.Ordinal)
    {
        ["fixedDigivolutionCost"] = 0
    };
    if (rush)
    {
        metadata["hasRush"] = true;
    }

    // PlayCost 0 keeps every card playable from an empty memory pool; EvolutionCondition null matches
    // any digivolution target so the digivolve drivers do not depend on printed evolution lines.
    return new CardRecord(
        new HeadlessEntityId(id),
        id,
        $"{id} Card",
        metadata,
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
