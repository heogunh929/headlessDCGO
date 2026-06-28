using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// S1 Effect-driven attack: the engine hub for an EFFECT initiating an attack (AS-IS SelectAttackEffect),
// unlocking C-20 Vortex / C-16 Overclock (attack part). No new pipeline — declaring the attack lets the
// existing AttackPipeline drive it. The target is an agent choice (rules-faithful). Options control the
// suspend cost (WithoutTap) and legal targets (player / Digimon / unsuspended).

HeadlessPlayerId P1 = new(1);   // attacker side (turn player)
HeadlessPlayerId P2 = new(2);   // defender side

var tests = new (string Name, Func<Task> Body)[]
{
    ("Initiate declares the effect-driven attack on the chosen target", InitiateDeclaresAttack),
    ("WithoutTap leaves the attacker unsuspended; default suspends it", WithoutTapControlsSuspend),
    ("GetTargets honours AllowDigimonTarget / AllowPlayerTarget", TargetsHonourAllowFlags),
    ("GetTargets excludes unsuspended Digimon unless TargetUnsuspended", TargetsHonourUnsuspended),
    ("Initiate refuses to nest inside a pending attack", InitiateRefusesNesting),
    ("Agent choice: selecting a target declares the attack", ChoiceSelectsTarget),
    ("Agent choice: declining initiates no attack", ChoiceDeclineNoAttack),
    ("Vortex options expose Digimon and player targets (unsuspended allowed)", VortexOptionsTargets),
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

async Task InitiateDeclaresAttack()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    HeadlessEntityId target = await Establish(s, P2, dp: 3000, suspended: true);

    var options = new EffectAttackOptions();
    AttackTargetCandidate pick = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, options)
        .Single(t => t.TargetId == target);
    AssertTrue(EffectDrivenAttack.Initiate(s.Match.Context, attacker, pick, options), "initiate succeeds");

    HeadlessAttackState attack = s.Match.Context.AttackController.Current;
    AssertTrue(attack.IsPending, "an attack is now pending");
    AssertEqual(attacker, attack.AttackerId, "attacker matches");
    AssertEqual(target, attack.TargetId, "target matches");
    AssertFalse(attack.IsDirectAttack, "a Digimon attack is not direct");
}

async Task WithoutTapControlsSuspend()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    _ = await Establish(s, P2, dp: 3000, suspended: true);

    AttackTargetCandidate player = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, new EffectAttackOptions())
        .Single(t => t.IsDirectAttack);
    EffectDrivenAttack.Initiate(s.Match.Context, attacker, player, new EffectAttackOptions(WithoutTap: true));
    AssertFalse(ReadFlag(s.Match, attacker, EffectDrivenAttack.IsSuspendedKey), "WithoutTap keeps the attacker unsuspended");

    Setup s2 = await NewMatch();
    HeadlessEntityId attacker2 = await Establish(s2, P1, dp: 4000, suspended: false);
    _ = await Establish(s2, P2, dp: 3000, suspended: true);
    AttackTargetCandidate player2 = EffectDrivenAttack.GetTargets(s2.Match.Context, attacker2, new EffectAttackOptions())
        .Single(t => t.IsDirectAttack);
    EffectDrivenAttack.Initiate(s2.Match.Context, attacker2, player2, new EffectAttackOptions(WithoutTap: false));
    AssertTrue(ReadFlag(s2.Match, attacker2, EffectDrivenAttack.IsSuspendedKey), "default suspends the attacker (cost)");
}

async Task TargetsHonourAllowFlags()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    _ = await Establish(s, P2, dp: 3000, suspended: true);

    var playerOnly = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, new EffectAttackOptions(AllowDigimonTarget: false));
    AssertTrue(playerOnly.All(t => t.IsDirectAttack), "AllowDigimonTarget=false leaves only the player target");
    AssertTrue(playerOnly.Any(t => t.IsDirectAttack), "the player target is present");

    var digimonOnly = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, new EffectAttackOptions(AllowPlayerTarget: false));
    AssertTrue(digimonOnly.All(t => !t.IsDirectAttack), "AllowPlayerTarget=false leaves only Digimon targets");
    AssertTrue(digimonOnly.Count > 0, "a Digimon target is present");
}

async Task TargetsHonourUnsuspended()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    HeadlessEntityId unsus = await Establish(s, P2, dp: 3000, suspended: false);

    var normal = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, new EffectAttackOptions(TargetUnsuspended: false, AllowPlayerTarget: false));
    AssertFalse(normal.Any(t => t.TargetId == unsus), "an unsuspended Digimon is not a normal target");

    var lifted = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, new EffectAttackOptions(TargetUnsuspended: true, AllowPlayerTarget: false));
    AssertTrue(lifted.Any(t => t.TargetId == unsus), "TargetUnsuspended lets the unsuspended Digimon be targeted");
}

async Task InitiateRefusesNesting()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    HeadlessEntityId target = await Establish(s, P2, dp: 3000, suspended: true);

    var options = new EffectAttackOptions();
    AttackTargetCandidate pick = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, options).Single(t => t.TargetId == target);
    AssertTrue(EffectDrivenAttack.Initiate(s.Match.Context, attacker, pick, options), "first initiate succeeds");
    AssertFalse(EffectDrivenAttack.Initiate(s.Match.Context, attacker, pick, options), "nested initiate is refused");
}

async Task ChoiceSelectsTarget()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    HeadlessEntityId target = await Establish(s, P2, dp: 3000, suspended: true);

    AssertTrue(EffectDrivenAttack.RequestChoice(s.Match.Context, attacker, new EffectAttackOptions()), "choice opened");
    AssertEqual(ChoiceType.EffectAttack, s.Match.Context.ChoiceController.PendingRequest!.Type, "choice type");
    AssertTrue(EffectDrivenAttack.ResolveChoice(s.Match.Context, ChoiceResult.Select(target)), "resolve succeeds");

    HeadlessAttackState attack = s.Match.Context.AttackController.Current;
    AssertTrue(attack.IsPending, "the attack was declared");
    AssertEqual(target, attack.TargetId, "declared on the chosen target");
}

async Task ChoiceDeclineNoAttack()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    _ = await Establish(s, P2, dp: 3000, suspended: true);

    AssertTrue(EffectDrivenAttack.RequestChoice(s.Match.Context, attacker, new EffectAttackOptions()), "choice opened");
    AssertTrue(EffectDrivenAttack.ResolveChoice(s.Match.Context, ChoiceResult.Skip()), "resolve (skip) succeeds");

    AssertFalse(s.Match.Context.AttackController.Current.IsPending, "declining initiates no attack");
}

async Task VortexOptionsTargets()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    HeadlessEntityId unsus = await Establish(s, P2, dp: 3000, suspended: false);

    // Vortex: attack Digimon + players, unsuspended allowed.
    var vortex = new EffectAttackOptions(AllowDigimonTarget: true, AllowPlayerTarget: true, TargetUnsuspended: true);
    var targets = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, vortex);
    AssertTrue(targets.Any(t => t.IsDirectAttack), "player target available");
    AssertTrue(targets.Any(t => t.TargetId == unsus), "unsuspended Digimon target available");
}

// --- Harness (mirrors G3.5-C3 / C18) -------------------------------------

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

async Task<HeadlessEntityId> Establish(Setup s, HeadlessPlayerId player, int dp, bool suspended)
{
    int next = s.Used.TryGetValue(player.Value, out int n) ? n + 1 : 1;
    s.Used[player.Value] = next;

    HeadlessEntityId card = HandCard(s.Match, player, next);
    await s.Match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(player, card, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(s.Match, card, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [BattleResolver.DpKey] = dp,
        [EffectDrivenAttack.IsSuspendedKey] = suspended
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
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
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
