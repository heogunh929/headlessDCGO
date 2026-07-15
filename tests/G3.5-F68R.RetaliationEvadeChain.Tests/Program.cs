// (C-Del 3c-2b quarantine → GREEN) The Retaliation→Evade chain case from the pre-flip G3.5-F68 suite (the (C-8)
// scenario: a Retaliation attacker LOSES the battle, the retaliation flags the winning defender for deletion,
// and the defender's own would-be-deleted replacement — Evade — may fire, re-opening the window). Assertions
// preserved verbatim from G3.5-F68; separated because its RETALIATION half rides a different substrate status
// than the flip:
//   * The retaliation TRIGGER still fires through the RETAINED BattleResolver manual block (the HasRetaliationKey /
//     Permanent.HasRetaliation read, BattleResolver.cs:168-180) — the RD-CBTL-01 ADAPTATION. The AS-IS
//     OnDestroyedAnyone-window Retaliation firing (CanActivateRetaliation / RetaliationProcess) stays STOP: it
//     reads the live IBattle from the battle hashtable (WinnerPermanents_real / LoserPermanents_real, AS-IS
//     CardController.cs:4694-4700), which the mirror BattleResolver does not carry (battle-rehousing non-scope;
//     payload synthesis forbidden — the C-Btl decision). When the IBattle region-goal lands and the manual block
//     is retired, THIS suite is the chain's regression guard (it must keep passing through the window path).
//   * The defender's EVADE half is fully flipped: the retired gate marker fires nothing — the defender is the
//     REAL BT13_023 <Evade> registered through the dispatch, firing via the 3c-1b battle PRE cut-in window.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Retaliation: the targeted opponent may Evade the retaliation (window re-opens)", RetaliationOpponentCanEvade),
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

// (Assertions preserved VERBATIM from the pre-flip G3.5-F68 RetaliationOpponentCanEvade; the drive is updated
// only in that the defender is the REAL BT13_023 <Evade> card registered through the dispatch — the retired
// hasEvade gate marker no longer fires anything. The attacker keeps the BattleResolver HasRetaliationKey flag
// (the manual block that IS the current mirror firing path, RD-CBTL-01).)
async Task RetaliationOpponentCanEvade()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73, deferredChoice: true);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);
    (context.ChoiceProvider as DeferredChoiceProvider)?.CompleteResolution();

    // Attacker (P1) has Retaliation and LOSES the battle (5000 < 8000); defender (P2) wins but has Evade.
    HeadlessEntityId attacker = HandCard(match, P1, 1);
    HeadlessEntityId defender = HandCard(match, P2, 1);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, attacker, ChoiceZone.Hand, ChoiceZone.BattleArea));
    RetypeCard(context, defender, "BT13_023");
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, defender, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, attacker, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["dp"] = 5000,
        [BattleResolver.HasRetaliationKey] = true
    });
    SetMetadata(match, defender, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["dp"] = 8000,
        [DeletionReplacementGate.IsSuspendedKey] = false,
    });
    CardEffectRegistrar.RegisterCard(context, defender, P2);

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    await match.StepAsync();   // attacker loses → confirmed → Retaliation flags defender → Evade window opens

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "the retaliated opponent's Evade window opened");
    LegalAction activate = ResolveActions(match, P2).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // finalize

    AssertTrue(InZone(match, P1, ChoiceZone.Trash, attacker), "the retaliation holder (loser) is deleted");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, defender), "the opponent Evaded the retaliation and survives");
    AssertTrue(ReadFlag(match, defender, DeletionReplacementGate.IsSuspendedKey), "the opponent suspended as the Evade cost");
}

// --- Harness ---------------------------------------------------------------

void RetypeCard(EngineContext ctx, HeadlessEntityId card, string cardNumber)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"def:{cardNumber}");
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Blue" }, ["level"] = 5, ["dp"] = 5000 }, CardType: "Digimon"));
    if (!ctx.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"missing {card}");
    ctx.CardInstanceRepository.Upsert(record with { DefinitionId = defId });
}

IEnumerable<LegalAction> ResolveActions(DcgoMatch match, HeadlessPlayerId player) =>
    match.GetLegalActions(player).Where(a => a.ActionType == HeadlessActionTypes.ResolveChoice);

HeadlessEntityId HandCard(DcgoMatch match, HeadlessPlayerId player, int index)
{
    HeadlessEntityId[] hand = ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(player, ChoiceZone.Hand).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
    if (hand.Length < index) throw new InvalidOperationException($"hand short: {hand.Length} < {index}");
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
        throw new InvalidOperationException($"Missing {cardId}.");
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

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
