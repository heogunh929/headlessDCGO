using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-18 Alliance: when an <Alliance> Digimon attacks, its controller MAY suspend one OTHER owner
// battle-area Digimon (cost); if suspended, the attacker gains +DP (= that ally's DP) and +1 Security
// Attack, both UntilEndAttack. AS-IS AllianceProcess. Engine: AllianceAttackBoost (RequestChoice/
// ResolveChoice) consumed by AttackPipeline before block timing; grant GrantAlliance -> hasAlliance.
// Optional + the which-ally sub-selection are agent choices.

HeadlessPlayerId P1 = new(1);   // attacker side
HeadlessPlayerId P2 = new(2);   // defender side

var tests = new (string Name, Func<Task> Body)[]
{
    ("Alliance offers a choice when an unsuspended ally exists", AllianceOffersChoice),
    ("Alliance offers no choice without an eligible ally", AllianceNoEligibleAlly),
    ("Declining Alliance grants nothing", AllianceDeclineGrantsNothing),
    ("Selecting an ally suspends it and buffs the attacker (+DP, +1 SA)", AllianceSelectBuffsAttacker),
    ("Alliance buffs expire at attack end", AllianceBuffExpiresAtAttackEnd),
    ("Alliance +DP flips the battle outcome", AllianceFlipsBattle),
    ("AttackPipeline opens the Alliance choice before block timing", PipelineOpensAlliance),
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

async Task AllianceOffersChoice()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 3000, suspended: false, alliance: true);
    HeadlessEntityId ally = await Establish(s, P1, dp: 5000, suspended: false, alliance: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    AssertTrue(AllianceAttackBoost.RequestChoice(s.Match.Context), "alliance choice offered");
    IReadOnlyList<HeadlessEntityId> candidates = AllianceAttackBoost.GetAllyCandidates(s.Match.Context);
    AssertTrue(candidates.Contains(ally), "the unsuspended ally is a candidate");
    AssertFalse(candidates.Contains(attacker), "the attacker is not a candidate");
}

async Task AllianceNoEligibleAlly()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 3000, suspended: false, alliance: true);
    _ = await Establish(s, P1, dp: 5000, suspended: true, alliance: false);   // only ally is already suspended

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    AssertFalse(AllianceAttackBoost.RequestChoice(s.Match.Context), "no choice without a suspendable ally");
}

async Task AllianceDeclineGrantsNothing()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 3000, suspended: false, alliance: true);
    HeadlessEntityId ally = await Establish(s, P1, dp: 5000, suspended: false, alliance: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    AssertTrue(AllianceAttackBoost.RequestChoice(s.Match.Context), "alliance choice offered");
    AllianceAttackBoost.ResolveChoice(s.Match.Context, ChoiceResult.Skip());

    AssertFalse(ReadFlag(s.Match, ally, AllianceAttackBoost.IsSuspendedKey), "declining does not suspend the ally");
    AssertEqual(3000, ContinuousDpGate.ResolveDp(s.Match.Context, attacker, 3000), "attacker DP unchanged");
    AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(s.Match.Context, attacker, 1), "attacker SA unchanged");
}

async Task AllianceSelectBuffsAttacker()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 3000, suspended: false, alliance: true);
    HeadlessEntityId ally = await Establish(s, P1, dp: 5000, suspended: false, alliance: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    AssertTrue(AllianceAttackBoost.RequestChoice(s.Match.Context), "alliance choice offered");
    AllianceAttackBoost.ResolveChoice(s.Match.Context, ChoiceResult.Select(ally));

    AssertTrue(ReadFlag(s.Match, ally, AllianceAttackBoost.IsSuspendedKey), "the chosen ally is suspended (cost)");
    AssertEqual(8000, ContinuousDpGate.ResolveDp(s.Match.Context, attacker, 3000), "attacker gains +ally DP (3000+5000)");
    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(s.Match.Context, attacker, 1), "attacker gains +1 Security Attack");
}

async Task AllianceBuffExpiresAtAttackEnd()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 3000, suspended: false, alliance: true);
    HeadlessEntityId ally = await Establish(s, P1, dp: 5000, suspended: false, alliance: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    AllianceAttackBoost.RequestChoice(s.Match.Context);
    AllianceAttackBoost.ResolveChoice(s.Match.Context, ChoiceResult.Select(ally));
    AssertEqual(8000, ContinuousDpGate.ResolveDp(s.Match.Context, attacker, 3000), "buff applied before attack end");

    EffectDurationExpiry.ExpireAttackEnd(s.Match.Context.EffectRegistry);

    AssertEqual(3000, ContinuousDpGate.ResolveDp(s.Match.Context, attacker, 3000), "DP buff expired at attack end");
    AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(s.Match.Context, attacker, 1), "SA buff expired at attack end");
}

async Task AllianceFlipsBattle()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 3000, suspended: false, alliance: true);
    HeadlessEntityId ally = await Establish(s, P1, dp: 4000, suspended: false, alliance: false);
    HeadlessEntityId defender = await Establish(s, P2, dp: 6000, suspended: true, alliance: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: defender, isDirectAttack: false);
    AssertTrue(AllianceAttackBoost.RequestChoice(s.Match.Context), "alliance choice offered");
    AllianceAttackBoost.ResolveChoice(s.Match.Context, ChoiceResult.Select(ally));

    BattleResolutionResult result = await new BattleResolver().ResolveAsync(s.Match.Context);

    AssertTrue(result.DefenderDeleted, "attacker (3000+4000) beats the 6000 defender");
    AssertFalse(result.AttackerDeleted, "boosted attacker survives");
}

async Task PipelineOpensAlliance()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 3000, suspended: false, alliance: true);
    HeadlessEntityId ally = await Establish(s, P1, dp: 5000, suspended: false, alliance: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    await new AttackPipeline().AdvanceAsync(s.Match.Context);   // Declared → opens the Alliance choice

    AssertTrue(s.Match.Context.ChoiceController.Current.IsPending, "pipeline opened the Alliance choice");
    AssertEqual(ChoiceType.AllianceTarget, s.Match.Context.ChoiceController.PendingRequest!.Type, "choice type");
    AllianceAttackBoost.ResolveChoice(s.Match.Context, ChoiceResult.Select(ally));
    AssertTrue(ReadFlag(s.Match, ally, AllianceAttackBoost.IsSuspendedKey), "resolving suspended the ally");
}

// --- Harness (mirrors G3.5-C3) -------------------------------------------

async Task<Setup> NewMatch()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") },
        firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);
    return new Setup(match, new Dictionary<int, int>());
}

async Task<HeadlessEntityId> Establish(Setup s, HeadlessPlayerId player, int dp, bool suspended, bool alliance)
{
    int next = s.Used.TryGetValue(player.Value, out int n) ? n + 1 : 1;
    s.Used[player.Value] = next;

    HeadlessEntityId card = HandCard(s.Match, player, next);
    await s.Match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(player, card, ChoiceZone.Hand, ChoiceZone.BattleArea));

    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [BattleResolver.DpKey] = dp,
        [AllianceAttackBoost.IsSuspendedKey] = suspended
    };
    if (alliance)
    {
        metadata[AllianceAttackBoost.HasAllianceKey] = true;
    }

    SetMetadata(s.Match, card, metadata);
    return card;
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

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    for (var attempt = 0; attempt < 10 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(player).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

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

sealed record Setup(DcgoMatch Match, Dictionary<int, int> Used);
