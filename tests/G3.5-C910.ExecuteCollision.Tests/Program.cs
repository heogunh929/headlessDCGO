using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-9 Execute / C-10 Collision — finishing C-group3.
//   * Execute   — an effect makes this Digimon attack (able to hit UNSUSPENDED Digimon via the existing
//                 canAttackUnsuspendedDigimon flag) and, at end of that attack, deletes itself (AS-IS
//                 UntilEndAttack DeleteSelfEffect). New consumption: deleteSelfAtEndOfAttack in
//                 AttackPipeline.AdvanceEndAttackAsync.
//   * Collision — when this Digimon attacks the opponent must block if able and any Digimon may block
//                 (AS-IS no-skip forced block). Consumption already lives in BlockTiming (hasCollision);
//                 here we exercise the grant→consume path end to end.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
Dictionary<int, int> used = new();   // per-player count of established battle-area cards

var tests = new (string Name, Func<Task> Body)[]
{
    ("Execute: a self-delete-at-end-of-attack Digimon is trashed after its attack", ExecuteSelfDeletesAfterAttack),
    ("Execute: without the flag the attacker survives its attack", NoSelfDeleteSurvives),
    ("Execute: canAttackUnsuspendedDigimon lets it declare an attack on an unsuspended Digimon", ExecuteCanAttackUnsuspended),
    ("Collision: GrantCollision forces the opponent to block with any Digimon", CollisionForcesBlock),
    ("S4: Collision granted via the KEYWORD (no metadata) forces block — un-sealed", CollisionViaKeywordUnsealed),
    ("(K3) a CanNotAffected(opponent-Digimon) defender is NOT forced by Collision (per-defender guard)", CollisionImmuneDefenderNotForced),
    ("S5: Execute granted via the KEYWORD self-deletes after its attack — un-sealed", ExecuteViaKeywordSelfDeletes),
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

// --- Execute -------------------------------------------------------------

async Task ExecuteSelfDeletesAfterAttack()
{
    DcgoMatch match = await NewMatch();
    HeadlessEntityId attacker = await Establish(match, P1, dp: 9000, suspended: false,
        flag: ("deleteSelfAtEndOfAttack", true));
    HeadlessEntityId defender = await Establish(match, P2, dp: 3000, suspended: true, flag: null);

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    await DriveAttackAsync(match);

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, defender), "defender deleted in battle");
    AssertTrue(InZone(match, P1, ChoiceZone.Trash, attacker), "execute attacker self-deleted at end of attack");
    AssertFalse(InZone(match, P1, ChoiceZone.BattleArea, attacker), "attacker left the battle area");
}

async Task NoSelfDeleteSurvives()
{
    DcgoMatch match = await NewMatch();
    HeadlessEntityId attacker = await Establish(match, P1, dp: 9000, suspended: false, flag: null);
    HeadlessEntityId defender = await Establish(match, P2, dp: 3000, suspended: true, flag: null);

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    await DriveAttackAsync(match);

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, defender), "defender deleted in battle");
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, attacker), "attacker without self-delete survives");
}

async Task ExecuteCanAttackUnsuspended()
{
    DcgoMatch withFlag = await NewMatch();
    HeadlessEntityId att1 = await Establish(withFlag, P1, dp: 5000, suspended: false,
        flag: ("canAttackUnsuspendedDigimon", true));
    HeadlessEntityId def1 = await Establish(withFlag, P2, dp: 4000, suspended: false, flag: null); // unsuspended
    AssertTrue(
        new AttackPermanentAction().GetAttackDeclarations(withFlag.Context, P1)
            .Any(d => d.AttackerId == att1 && d.TargetCandidates.Any(c => c.TargetId == def1)),
        "with canAttackUnsuspendedDigimon the unsuspended Digimon is a legal target");

    DcgoMatch noFlag = await NewMatch();
    HeadlessEntityId att2 = await Establish(noFlag, P1, dp: 5000, suspended: false, flag: null);
    HeadlessEntityId def2 = await Establish(noFlag, P2, dp: 4000, suspended: false, flag: null); // unsuspended
    AssertFalse(
        new AttackPermanentAction().GetAttackDeclarations(noFlag.Context, P1)
            .Any(d => d.AttackerId == att2 && d.TargetCandidates.Any(c => c.TargetId == def2)),
        "without the flag an unsuspended Digimon is not a legal target");
}

// --- Collision -----------------------------------------------------------

async Task CollisionForcesBlock()
{
    DcgoMatch match = await NewMatch();
    HeadlessEntityId attacker = await Establish(match, P1, dp: 6000, suspended: false, flag: null);
    HeadlessEntityId blocker = await Establish(match, P2, dp: 4000, suspended: false, flag: null); // plain, no hasBlocker

    // Grant Collision to the attacker via the mutation sink (grant path), then confirm the flag is set.
    MatchStateMutationSink sink = new(match.Context.CardInstanceRepository, log: null,
        match.Context.ZoneMover, memory: null, match.Context.EffectRegistry);
    sink.Apply(new EffectMutation("GrantCollision", new HeadlessEntityId("granter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = attacker.Value }));
    await sink.FlushAsync();
    AssertTrue(ReadFlag(match, attacker, BlockTiming.HasCollisionKey), "GrantCollision set hasCollision");

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    BlockTimingResult result = new BlockTiming().RequestBlockChoice(match.Context);

    AssertTrue(result.IsSuccess, "block timing opened");
    AssertTrue(result.Candidates.Any(c => c.BlockerId == blocker), "collision makes a plain Digimon a forced blocker");
    AssertFalse(match.Context.ChoiceController.Current.CanSkip, "collision block cannot be skipped");
}

async Task ExecuteViaKeywordSelfDeletes()
{
    DcgoMatch match = await NewMatch();
    used.Clear();
    HeadlessEntityId attacker = await Establish(match, P1, dp: 9000, suspended: false, flag: null);
    HeadlessEntityId defender = await Establish(match, P2, dp: 3000, suspended: true, flag: null);
    // Grant Execute via the KEYWORD (ExecuteSelfEffect), NOT the deleteSelfAtEndOfAttack metadata flag.
    match.Context.EffectRegistry.Register(
        CardEffectFactory.ExecuteSelfEffect(false, new CardSource(match.Context, attacker, P1), null).ToBinding($"exec:{attacker.Value}"));

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    await DriveAttackAsync(match);

    AssertTrue(InZone(match, P1, ChoiceZone.Trash, attacker), "keyword Execute attacker self-deleted at end of attack (un-sealed)");
    AssertFalse(InZone(match, P1, ChoiceZone.BattleArea, attacker), "attacker left the battle area");
}

async Task CollisionViaKeywordUnsealed()
{
    DcgoMatch match = await NewMatch();
    used.Clear();   // fresh match → reset the shared hand-index counter
    HeadlessEntityId attacker = await Establish(match, P1, dp: 6000, suspended: false, flag: null);
    HeadlessEntityId blocker = await Establish(match, P2, dp: 4000, suspended: false, flag: null);

    // Grant Collision via the KEYWORD (CollisionStaticEffect → player-scope Collision keyword), NOT the mutation.
    match.Context.EffectRegistry.Register(
        CardEffectFactory.CollisionStaticEffect(null, false, new CardSource(match.Context, attacker, P1), null).ToBinding($"col:{attacker.Value}"));
    AssertFalse(ReadFlag(match, attacker, BlockTiming.HasCollisionKey), "hasCollision metadata is NOT set (keyword-granted)");

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    BlockTimingResult result = new BlockTiming().RequestBlockChoice(match.Context);

    AssertTrue(result.IsSuccess, "block timing opened");
    AssertTrue(result.Candidates.Any(c => c.BlockerId == blocker), "keyword Collision forces a plain Digimon to be a blocker (un-sealed)");
    AssertFalse(match.Context.ChoiceController.Current.CanSkip, "keyword Collision block cannot be skipped");
}

// (K3) AS-IS Permanent.HasBlocker: the forced-Blocker grant is guarded per-defender by
// `!CanNotBeAffected(fakeCollisionClass)` with source = the ATTACKER — an immune defender is not forced.
async Task CollisionImmuneDefenderNotForced()
{
    DcgoMatch match = await NewMatch();
    used.Clear();
    HeadlessEntityId attacker = await Establish(match, P1, dp: 6000, suspended: false, flag: null);
    HeadlessEntityId immune = await Establish(match, P2, dp: 4000, suspended: false, flag: null);
    HeadlessEntityId plain = await Establish(match, P2, dp: 4000, suspended: false, flag: null);

    match.Context.EffectRegistry.Register(
        CardEffectFactory.CollisionStaticEffect(null, false, new CardSource(match.Context, attacker, P1), null).ToBinding($"col:{attacker.Value}"));
    // The immune defender cannot be affected by the OPPONENT's Digimon effects (AS-IS SkillCondition shape).
    match.Context.EffectRegistry.Register(
        CardEffectFactory.CanNotAffectedStaticEffect(
            permanentCondition: null,
            skillCondition: src => src.Owner != P2 && src.IsDigimon,
            isInheritedEffect: false, card: new CardSource(match.Context, immune, P2), condition: null).ToBinding($"cna:{immune.Value}"));

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    BlockTimingResult result = new BlockTiming().RequestBlockChoice(match.Context);

    AssertTrue(result.IsSuccess, "block timing opened");
    AssertTrue(result.Candidates.Any(c => c.BlockerId == plain), "the plain defender is still forced");
    AssertFalse(result.Candidates.Any(c => c.BlockerId == immune),
        "a CanNotAffected(opponent-Digimon-effects) defender is NOT forced by Collision");
}

// --- Harness -------------------------------------------------------------

async Task DriveAttackAsync(DcgoMatch match)
{
    var pipeline = new AttackPipeline();
    for (int i = 0; i < 12 && match.Context.AttackController.Current.Phase != AttackPhase.None; i++)
    {
        await pipeline.AdvanceAsync(match.Context);
    }

    AssertEqual(AttackPhase.None, match.Context.AttackController.Current.Phase, "attack ran to completion");
}

async Task<DcgoMatch> NewMatch()
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
    return match;
}

async Task<HeadlessEntityId> Establish(DcgoMatch match, HeadlessPlayerId player, int dp, bool suspended, (string Key, bool Value)? flag)
{
    int next = used.TryGetValue(player.Value, out int n) ? n + 1 : 1;
    used[player.Value] = next;

    HeadlessEntityId card = HandCard(match, player, next);
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(player, card, ChoiceZone.Hand, ChoiceZone.BattleArea));

    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [BattleResolver.DpKey] = dp,
        ["isSuspended"] = suspended
    };
    if (flag is { } f)
    {
        metadata[f.Key] = f.Value;
    }

    SetMetadata(match, card, metadata);
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

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

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
