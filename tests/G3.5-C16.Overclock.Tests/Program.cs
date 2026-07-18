using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-16 Overclock (S3 trait + S1 attack): at end of turn, an Overclock Digimon MAY delete one OTHER owner
// battle-area Digimon that is a token or carries the required trait; if deleted, this Digimon makes an
// UNTAPPED player-only attack. AS-IS OverclockProcess. Engine: OverclockEffect (trait-ally select +
// SacrificeAsync) -> EffectDrivenAttack (untapped, player only); grant GrantOverclock -> hasOverclock.

HeadlessPlayerId P1 = new(1);   // Overclock controller (turn player)
HeadlessPlayerId P2 = new(2);
const string Trait = "Dragon";

var tests = new (string Name, Func<Task> Body)[]
{
    ("Candidates include token + trait allies, exclude non-trait + self", CandidatesFilterByTraitAndToken),
    ("No trait/token ally offers no Overclock choice", NoEligibleAllyNoChoice),
    ("Selecting an ally deletes it and opens the untapped player attack", SelectDeletesAndOpensAttack),
    ("Declining Overclock deletes nothing and opens no attack", DeclineDoesNothing),
    ("The opened attack is player-only (no Digimon target)", AttackIsPlayerOnly),
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

async Task CandidatesFilterByTraitAndToken()
{
    Setup s = await NewMatch();
    HeadlessEntityId source = await Source(s);
    HeadlessEntityId dragon = await Ally(s, traits: new[] { Trait });
    HeadlessEntityId token = await Ally(s, token: true);
    HeadlessEntityId plain = await Ally(s);

    var candidates = OverclockEffect.GetTraitAllyCandidates(s.Match.Context, source);

    AssertTrue(candidates.Contains(dragon), "trait ally is a candidate");
    AssertTrue(candidates.Contains(token), "token ally is a candidate");
    AssertFalse(candidates.Contains(plain), "non-trait non-token ally is excluded");
    AssertFalse(candidates.Contains(source), "the Overclock source excludes itself");
}

async Task NoEligibleAllyNoChoice()
{
    Setup s = await NewMatch();
    HeadlessEntityId source = await Source(s);
    _ = await Ally(s);   // a plain ally, no trait, not a token

    AssertFalse(OverclockEffect.RequestChoice(s.Match.Context, source), "no choice without a trait/token ally");
}

async Task SelectDeletesAndOpensAttack()
{
    Setup s = await NewMatch();
    HeadlessEntityId source = await Source(s);
    HeadlessEntityId dragon = await Ally(s, traits: new[] { Trait });

    AssertTrue(OverclockEffect.RequestChoice(s.Match.Context, source), "Overclock choice offered");
    AssertEqual(ChoiceType.OverclockTarget, s.Match.Context.ChoiceController.PendingRequest!.Type, "choice type");
    AssertTrue(await OverclockEffect.ResolveChoice(s.Match.Context, ChoiceResult.Select(dragon)), "resolve succeeds");

    AssertTrue(InZone(s.Match, P1, ChoiceZone.Trash, dragon), "the chosen trait ally is deleted");
    AssertTrue(s.Match.Context.ChoiceController.Current.IsPending, "an attack choice follows the deletion");
    AssertEqual(ChoiceType.EffectAttack, s.Match.Context.ChoiceController.PendingRequest!.Type, "the follow-up is the effect attack");
}

async Task DeclineDoesNothing()
{
    Setup s = await NewMatch();
    HeadlessEntityId source = await Source(s);
    HeadlessEntityId dragon = await Ally(s, traits: new[] { Trait });

    AssertTrue(OverclockEffect.RequestChoice(s.Match.Context, source), "Overclock choice offered");
    AssertTrue(await OverclockEffect.ResolveChoice(s.Match.Context, ChoiceResult.Skip()), "resolve (skip) succeeds");

    AssertFalse(InZone(s.Match, P1, ChoiceZone.Trash, dragon), "declining deletes nothing");
    AssertFalse(s.Match.Context.ChoiceController.Current.IsPending, "declining opens no attack");
}

async Task AttackIsPlayerOnly()
{
    Setup s = await NewMatch();
    HeadlessEntityId source = await Source(s);
    HeadlessEntityId dragon = await Ally(s, traits: new[] { Trait });
    _ = await Establish(s, P2, suspended: true);   // an enemy Digimon that must NOT be attackable via Overclock

    OverclockEffect.RequestChoice(s.Match.Context, source);
    await OverclockEffect.ResolveChoice(s.Match.Context, ChoiceResult.Select(dragon));

    AssertTrue(s.Match.Context.ChoiceController.Current.IsPending, "the untapped attack choice is open");
    IReadOnlyList<AttackTargetCandidate> targets = EffectDrivenAttack.GetTargets(
        s.Match.Context, source, new EffectAttackOptions(WithoutTap: true, AllowPlayerTarget: true, AllowDigimonTarget: false, TargetUnsuspended: false));
    AssertTrue(targets.All(t => t.IsDirectAttack), "Overclock attack targets the player only (no Digimon)");
    AssertTrue(targets.Any(t => t.IsDirectAttack), "the player target is present");
}

// --- Harness -------------------------------------------------------------

async Task<HeadlessEntityId> Source(Setup s)
{
    HeadlessEntityId source = await Establish(s, P1, suspended: false);
    SetMetadata(s.Match, source, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [OverclockEffect.HasOverclockKey] = true,
        [OverclockEffect.OverclockTraitKey] = Trait
    });
    return source;
}

async Task<HeadlessEntityId> Ally(Setup s, string[]? traits = null, bool token = false)
{
    HeadlessEntityId ally = await Establish(s, P1, suspended: false);
    if (traits is not null)
    {
        SetMetadata(s.Match, ally, new Dictionary<string, object?>(StringComparer.Ordinal) { ["traits"] = traits });
    }

    if (token && s.Match.Context.CardInstanceRepository.TryGetInstance(ally, out CardInstanceRecord? record) && record is not null)
    {
        s.Match.Context.CardInstanceRepository.Upsert(record with { IsToken = true });
    }

    return ally;
}

async Task<Setup> NewMatch()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") },
        firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);
    return new Setup(match, new Dictionary<int, int>());
}

async Task<HeadlessEntityId> Establish(Setup s, HeadlessPlayerId player, bool suspended)
{
    // Cards leave the hand as they are established, so always take the first remaining hand card.
    HeadlessEntityId card = HandCard(s.Match, player, 1);
    await s.Match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(player, card, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(s.Match, card, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [BattleResolver.DpKey] = 3000,
        ["isSuspended"] = suspended
    });
    return card;
}

HeadlessEntityId HandCard(DcgoMatch match, HeadlessPlayerId player, int index)
{
    HeadlessEntityId[] hand = ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(player, ChoiceZone.Hand).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
    if (hand.Length < index) throw new InvalidOperationException($"Player '{player}' hand has {hand.Length}; needed {index}.");
    return hand[index - 1];
}

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, player));

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}

// --- Phase driving (pump auto-flow, F62/alpha/EXEMPLAR-T1 precedent) ------
// Drive the pump's natural Active->Draw->Breeding->Main auto-flow to the player's main wait; the OLD
// AdvancePhase step currency is retired. Breeding/Mulligan decisions are declined; observed Main arrival
// is asserted (assertion strength unchanged).
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
            await ResolvePendingAsync(match, skip: decline);
        }
        else await StepOnceAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingAsync(DcgoMatch match, bool skip)
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

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

sealed record Setup(DcgoMatch Match, Dictionary<int, int> Used);
