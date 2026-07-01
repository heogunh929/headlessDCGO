using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-3 Raid: when a <Raid> Digimon attacks, its controller may switch the attack onto the opponent's
// UNSUSPENDED Digimon with the highest DP (other than the current defender) — AS-IS RaidProcess →
// attackProcess.SwitchDefender. NOTE: Raid is a target-switch on attack, not a "direct security attack"
// subsystem. Consumption: RaidAttackSwitch.TryApply, called by AttackPipeline before block timing.

HeadlessPlayerId P1 = new(1);   // attacker side
HeadlessPlayerId P2 = new(2);   // defender side

var tests = new (string Name, Func<Task> Body)[]
{
    ("Raid offers a switch choice; selecting retargets to the highest-DP Digimon", RaidSwitchesDirectAttack),
    ("Raid ignores suspended Digimon when offering the switch", RaidIgnoresSuspended),
    ("Without Raid no switch choice is offered", NoRaidNoSwitch),
    ("Raid with no eligible enemy Digimon offers no switch", RaidNoEligibleTarget),
    ("Raid switches off the current defender to a higher-DP Digimon", RaidExcludesCurrentDefender),
    ("Declining the Raid choice leaves the attack unchanged", RaidDeclineKeepsAttack),
    ("AttackPipeline opens the Raid switch choice before block timing", PipelineOpensRaidSwitch),
    ("S1: Raid granted via the KEYWORD (metadata never set) opens the switch — un-sealed", RaidViaKeywordUnsealed),
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

async Task RaidViaKeywordUnsealed()
{
    Setup s = await NewMatch();
    // raid: false → the hasRaid METADATA flag is NOT set (production never sets it for keyword-granted Raid).
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false, raid: false);
    HeadlessEntityId high = await Establish(s, P2, dp: 8000, suspended: false, raid: false);
    // Grant Raid the way real cards do — the continuous KEYWORD (RaidSelfEffect → SelfKeywordByNameEffect).
    s.Match.Context.EffectRegistry.Register(
        CardEffectFactory.RaidSelfEffect(false, new CardSource(s.Match.Context, attacker, P1), null).ToBinding($"raid:{attacker.Value}"));

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    AssertTrue(RaidAttackSwitch.RequestChoice(s.Match.Context),
        "Raid via keyword opens the switch choice (un-sealed; hasRaid metadata never set)");
}

async Task RaidSwitchesDirectAttack()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false, raid: true);
    HeadlessEntityId low = await Establish(s, P2, dp: 3000, suspended: false, raid: false);
    HeadlessEntityId high = await Establish(s, P2, dp: 8000, suspended: false, raid: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    AssertTrue(RaidAttackSwitch.RequestChoice(s.Match.Context), "raid switch choice offered");
    RaidAttackSwitch.ResolveChoice(s.Match.Context, ChoiceResult.Select(high));

    HeadlessAttackState attack = s.Match.Context.AttackController.Current;
    AssertEqual(high, attack.TargetId, "attack now targets the selected highest-DP Digimon");
    AssertFalse(attack.IsDirectAttack, "switched attack is no longer direct");
    _ = low;
}

async Task RaidIgnoresSuspended()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false, raid: true);
    await Establish(s, P2, dp: 9000, suspended: true, raid: false);   // highest DP but suspended → ineligible
    HeadlessEntityId unsus = await Establish(s, P2, dp: 5000, suspended: false, raid: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    AssertTrue(RaidAttackSwitch.GetSwitchCandidates(s.Match.Context).Contains(unsus), "unsuspended Digimon is a candidate");
    AssertTrue(RaidAttackSwitch.RequestChoice(s.Match.Context), "raid switch choice offered");
    RaidAttackSwitch.ResolveChoice(s.Match.Context, ChoiceResult.Select(unsus));

    AssertEqual(unsus, s.Match.Context.AttackController.Current.TargetId, "switches to the highest UNSUSPENDED Digimon");
}

async Task NoRaidNoSwitch()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false, raid: false);
    await Establish(s, P2, dp: 8000, suspended: false, raid: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    AssertFalse(RaidAttackSwitch.RequestChoice(s.Match.Context), "no raid: no switch choice");
    HeadlessAttackState attack = s.Match.Context.AttackController.Current;
    AssertTrue(attack.IsDirectAttack, "attack stays direct");
    AssertEqual((HeadlessEntityId?)null, attack.TargetId, "attack stays targetless");
}

async Task RaidNoEligibleTarget()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false, raid: true);
    await Establish(s, P2, dp: 8000, suspended: true, raid: false);   // only enemy Digimon is suspended

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    AssertFalse(RaidAttackSwitch.RequestChoice(s.Match.Context), "no eligible (all suspended): no switch choice");
    AssertTrue(s.Match.Context.AttackController.Current.IsDirectAttack, "attack stays direct");
}

async Task RaidExcludesCurrentDefender()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false, raid: true);
    HeadlessEntityId mid = await Establish(s, P2, dp: 5000, suspended: true, raid: false);    // current defender
    HeadlessEntityId high = await Establish(s, P2, dp: 8000, suspended: false, raid: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: mid, isDirectAttack: false);
    AssertTrue(RaidAttackSwitch.RequestChoice(s.Match.Context), "raid switch choice offered");
    RaidAttackSwitch.ResolveChoice(s.Match.Context, ChoiceResult.Select(high));

    AssertEqual(high, s.Match.Context.AttackController.Current.TargetId, "switches off the current defender to the higher Digimon");
}

async Task RaidDeclineKeepsAttack()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false, raid: true);
    await Establish(s, P2, dp: 8000, suspended: false, raid: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    AssertTrue(RaidAttackSwitch.RequestChoice(s.Match.Context), "raid switch choice offered");
    RaidAttackSwitch.ResolveChoice(s.Match.Context, ChoiceResult.Skip());

    HeadlessAttackState attack = s.Match.Context.AttackController.Current;
    AssertTrue(attack.IsDirectAttack, "declined: attack stays direct");
    AssertEqual((HeadlessEntityId?)null, attack.TargetId, "declined: attack stays targetless");
}

async Task PipelineOpensRaidSwitch()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false, raid: true);
    HeadlessEntityId high = await Establish(s, P2, dp: 8000, suspended: false, raid: false);

    s.Match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    await new AttackPipeline().AdvanceAsync(s.Match.Context);   // Declared → opens the Raid switch choice

    AssertTrue(s.Match.Context.ChoiceController.Current.IsPending, "pipeline opened the Raid switch choice");
    RaidAttackSwitch.ResolveChoice(s.Match.Context, ChoiceResult.Select(high));
    AssertEqual(high, s.Match.Context.AttackController.Current.TargetId, "resolving the choice switched the defender");
}

// --- Harness -------------------------------------------------------------

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

async Task<HeadlessEntityId> Establish(Setup s, HeadlessPlayerId player, int dp, bool suspended, bool raid)
{
    int next = s.Used.TryGetValue(player.Value, out int n) ? n + 1 : 1;
    s.Used[player.Value] = next;

    HeadlessEntityId card = HandCard(s.Match, player, next);
    await s.Match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(player, card, ChoiceZone.Hand, ChoiceZone.BattleArea));

    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [BattleResolver.DpKey] = dp,
        [RaidAttackSwitch.IsSuspendedKey] = suspended
    };
    if (raid)
    {
        metadata[RaidAttackSwitch.HasRaidKey] = true;
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
        LegalAction advance = match.GetLegalActions(player)
            .Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
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

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon");

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

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

sealed record Setup(DcgoMatch Match, Dictionary<int, int> Used);
