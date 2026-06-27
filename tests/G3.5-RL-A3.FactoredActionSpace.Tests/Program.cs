using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-RL-A3: fixed factored action space (fixes P0-2). Same-type actions for different cards /
// targets / choice candidates must occupy DISTINCT indices in a fixed-size mask, unlike the
// type-only ActionEncoder where they collapse into one slot.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Schema lanes are contiguous and non-overlapping", () => Pure(SchemaLanesAreContiguous)),
    ("Different hand cards map to different PlayCard indices", () => Pure(PlayCardCardsAreDistinct)),
    ("Type-only encoder collapses what factored separates (regression contrast)", () => Pure(TypeOnlyCollapsesContrast)),
    ("Attacker x target pairs map to distinct attack indices", () => Pure(AttackPairsAreDistinct)),
    ("Direct attack uses a dedicated slot distinct from target attacks", () => Pure(DirectAttackHasOwnSlot)),
    ("Choice candidates and skip map to distinct indices", () => Pure(ChoiceCandidatesAreDistinct)),
    ("Mask vector length equals schema size with one hot per legal action", () => Pure(MaskVectorIsFixedSizeOneHot)),
    ("Out-of-capacity actions are surfaced as unmapped, not dropped silently", () => Pure(OverflowIsSurfaced)),
    ("Factored mask resolves indices back to actions end-to-end in a match", FactoredMaskRoundTripsInMatch),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"\n{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine($"\n{tests.Length} test(s) passed.");

static Task Pure(Action body)
{
    body();
    return Task.CompletedTask;
}

// --- Schema --------------------------------------------------------------

void SchemaLanesAreContiguous()
{
    var s = new FactoredActionSchema(maxHand: 4, maxField: 3, maxChoice: 5);
    AssertEqual(0, s.NoOpOffset, "noop offset");
    AssertEqual(4, s.PlayCardOffset, "playcard offset after 4 singletons");
    AssertEqual(4 + 4, s.ActivateOptionOffset, "option offset");
    AssertEqual(4 + 4 + 4, s.DigivolveOffset, "digivolve offset");
    AssertEqual(4 + 4 + 4 + (4 * 3), s.DeclareAttackOffset, "attack offset");
    // D-6: two single-slot breeding lanes appended after ResolveChoice.
    int resolveChoiceOffset = 4 + 4 + 4 + (4 * 3) + (3 * (3 + 1));
    AssertEqual(resolveChoiceOffset + (5 + 1), s.HatchDigitamaOffset, "hatch offset after resolve-choice lane");
    AssertEqual(resolveChoiceOffset + (5 + 1) + 1, s.MoveBreedingOffset, "move-breeding offset");
    int expectedTotal = 4 + 4 + 4 + (4 * 3) + (3 * (3 + 1)) + (5 + 1) + 1 + 1;
    AssertEqual(expectedTotal, s.TotalSize, "total size");
}

// --- Factored distinctness ----------------------------------------------

void PlayCardCardsAreDistinct()
{
    var pos = Positions(hand: new[] { "h0", "h1", "h2" });
    LegalAction a0 = HeadlessActionFactory.PlayCard(P1, Id("h0"), 1);
    LegalAction a2 = HeadlessActionFactory.PlayCard(P1, Id("h2"), 1);

    FactoredActionMask mask = FactoredActionEncoder.Encode(new[] { a0, a2 }, pos);

    int i0 = IndexOf(mask, a0);
    int i2 = IndexOf(mask, a2);
    AssertTrue(i0 >= 0 && i2 >= 0, "both play actions mapped");
    AssertTrue(i0 != i2, "different hand cards get different indices");
    AssertEqual(0, mask.Unmapped.Count, "nothing unmapped");
}

void TypeOnlyCollapsesContrast()
{
    // The legacy type-only encoder assigns both PlayCard actions the SAME action index slot...
    LegalAction a0 = HeadlessActionFactory.PlayCard(P1, Id("h0"), 1);
    LegalAction a1 = HeadlessActionFactory.PlayCard(P1, Id("h1"), 1);
    EncodedActionMask typed = new ActionEncoder().Encode(new ActionMask(new[] { a0, a1 }));
    int slot0 = typed.LegalActions.First(x => x.LegalAction.Id == a0.Id).ActionIndex;
    int slot1 = typed.LegalActions.First(x => x.LegalAction.Id == a1.Id).ActionIndex;
    AssertEqual(slot0, slot1, "type-only encoder collapses both into one slot (the bug)");

    // ...while the factored encoder keeps them apart.
    FactoredActionMask factored = FactoredActionEncoder.Encode(new[] { a0, a1 }, Positions(hand: new[] { "h0", "h1" }));
    AssertTrue(IndexOf(factored, a0) != IndexOf(factored, a1), "factored encoder separates them");
}

void AttackPairsAreDistinct()
{
    var pos = Positions(field: new[] { "atk0", "atk1" }, opponentField: new[] { "def0", "def1" });
    LegalAction a00 = Attack("atk0", "def0");
    LegalAction a01 = Attack("atk0", "def1");
    LegalAction a10 = Attack("atk1", "def0");

    FactoredActionMask mask = FactoredActionEncoder.Encode(new[] { a00, a01, a10 }, pos);
    int[] indices = { IndexOf(mask, a00), IndexOf(mask, a01), IndexOf(mask, a10) };
    AssertTrue(indices.All(i => i >= 0), "all attack pairs mapped");
    AssertEqual(3, indices.Distinct().Count(), "every (attacker,target) pair is a distinct index");
}

void DirectAttackHasOwnSlot()
{
    var pos = Positions(field: new[] { "atk0" }, opponentField: new[] { "def0" });
    LegalAction targetAttack = Attack("atk0", "def0");
    LegalAction directAttack = DirectAttack("atk0");

    FactoredActionMask mask = FactoredActionEncoder.Encode(new[] { targetAttack, directAttack }, pos);
    AssertTrue(IndexOf(mask, targetAttack) != IndexOf(mask, directAttack), "direct attack distinct from target attack");
    AssertEqual(0, mask.Unmapped.Count, "no unmapped attacks");
}

void ChoiceCandidatesAreDistinct()
{
    var pos = Positions(choiceCandidates: new[] { "c0", "c1", "c2" });
    LegalAction r0 = HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(Id("c0")), actionId: "r0");
    LegalAction r2 = HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(Id("c2")), actionId: "r2");
    LegalAction skip = HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Skip(), actionId: "skip");

    FactoredActionMask mask = FactoredActionEncoder.Encode(new[] { r0, r2, skip }, pos);
    int[] indices = { IndexOf(mask, r0), IndexOf(mask, r2), IndexOf(mask, skip) };
    AssertTrue(indices.All(i => i >= 0), "all choice actions mapped");
    AssertEqual(3, indices.Distinct().Count(), "candidates and skip are distinct indices");
}

void MaskVectorIsFixedSizeOneHot()
{
    var schema = new FactoredActionSchema(maxHand: 4, maxField: 3, maxChoice: 5);
    var pos = Positions(hand: new[] { "h0", "h1" });
    LegalAction a0 = HeadlessActionFactory.PlayCard(P1, Id("h0"), 1);
    LegalAction a1 = HeadlessActionFactory.PlayCard(P1, Id("h1"), 1);

    FactoredActionMask mask = FactoredActionEncoder.Encode(new[] { a0, a1 }, pos, schema);
    double[] vector = mask.ToMaskVector();
    AssertEqual(schema.TotalSize, vector.Length, "vector length equals schema size");
    AssertEqual(2, (int)vector.Sum(), "exactly two hot entries");
}

void OverflowIsSurfaced()
{
    // Hand has 3 cards but capacity is 2 -> the third position cannot be mapped.
    var schema = new FactoredActionSchema(maxHand: 2, maxField: 2, maxChoice: 2);
    var pos = Positions(hand: new[] { "h0", "h1", "h2" });
    LegalAction overflow = HeadlessActionFactory.PlayCard(P1, Id("h2"), 1);

    FactoredActionMask mask = FactoredActionEncoder.Encode(new[] { overflow }, pos, schema);
    AssertEqual(0, mask.Actions.Count, "overflow action is not placed");
    AssertEqual(1, mask.Unmapped.Count, "overflow action is surfaced as unmapped");
}

// --- End-to-end ----------------------------------------------------------

async Task FactoredMaskRoundTripsInMatch()
{
    DcgoMatch match = new();
    await match.InitializeAsync(BuildMatchConfig());
    await AdvanceToMainAsync(match);

    FactoredActionMask mask = match.EncodeFactoredActionMask();
    AssertTrue(mask.Actions.Count > 0, "match exposes at least one factored action (Pass)");

    // Every placed index resolves back to its action.
    foreach (FactoredAction fa in mask.Actions)
    {
        AssertTrue(mask.TryGetAction(fa.Index, out LegalAction back), $"index {fa.Index} resolves");
        AssertEqual(fa.Action.Id, back.Id, "round-trip identity");
    }

    double[] vector = mask.ToMaskVector();
    AssertEqual(mask.Schema.TotalSize, vector.Length, "match mask vector is fixed size");
    AssertEqual(mask.Actions.Count, (int)vector.Sum(), "hot count equals placed actions");
}

// --- Helpers -------------------------------------------------------------

FactoredPositionContext Positions(
    string[]? hand = null,
    string[]? field = null,
    string[]? opponentField = null,
    string[]? choiceCandidates = null)
{
    IReadOnlyList<HeadlessEntityId> handIds = (hand ?? Array.Empty<string>()).Select(Id).ToArray();
    IReadOnlyList<HeadlessEntityId> fieldIds = (field ?? Array.Empty<string>()).Select(Id).ToArray();
    IReadOnlyList<HeadlessEntityId> oppFieldIds = (opponentField ?? Array.Empty<string>()).Select(Id).ToArray();
    IReadOnlyList<HeadlessEntityId> candidates = (choiceCandidates ?? Array.Empty<string>()).Select(Id).ToArray();

    return new FactoredPositionContext(
        (player, zone) => (player == P1, zone) switch
        {
            (true, ChoiceZone.Hand) => handIds,
            (true, ChoiceZone.BattleArea) => fieldIds,
            (false, ChoiceZone.BattleArea) => oppFieldIds,
            _ => Array.Empty<HeadlessEntityId>()
        },
        candidates);
}

LegalAction Attack(string attacker, string target) =>
    HeadlessActionFactory.DeclareAttack(P1, Id(attacker), P2, Id(target), isDirectAttack: false);

LegalAction DirectAttack(string attacker) =>
    HeadlessActionFactory.DeclareAttack(P1, Id(attacker), P2, targetId: null, isDirectAttack: true);

static HeadlessEntityId Id(string value) => new(value);

static int IndexOf(FactoredActionMask mask, LegalAction action)
{
    FactoredAction? match = mask.Actions.FirstOrDefault(a => a.Action.Id == action.Id);
    return match?.Index ?? -1;
}

static MatchConfig BuildMatchConfig()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix)
{
    return new PlayerDeckSetup(
        playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());
}

async Task AdvanceToMainAsync(DcgoMatch match)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}
