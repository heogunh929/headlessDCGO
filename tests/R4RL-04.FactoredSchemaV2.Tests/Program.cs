using System.Security.Cryptography;
using System.Text;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (R4RL-04 — B5-3, docs/audit/rl_env_design_2026-07-17.md §B5.4/§B5.5/§B5.7) FACTORED SCHEMA v2 +
// partial-selection observation witnesses. B5-2 (R4RL-03) flipped the dispatcher surface (toggle /
// Confirm / skip lanes live on the legal table); this suite pins the RL-LAYER contract on top of it:
//
//   * schema v2 = ONE Confirm slot appended last (599→600 at default capacities) — every v1 offset
//     is pinned ABSOLUTE here so any future reordering breaks loudly (기존 오프셋 안정성 지문);
//   * the 16 ResolveChoice candidate slots double as the session's toggle lanes (설계 핀 1) — the
//     "size-1 resolution" vs "session toggle" reading is decided deterministically by the pending
//     choice state (session-open condition), never by the action payload alone;
//   * the whole multi-select session table maps with ZERO unmapped actions at every session state
//     (the B5-2 flip had left toggles/Confirm unmapped(-1) in the v1 schema);
//   * the observation gains choice.candidate.{i}.selected (16 bits, 3088→3104 infoset) tracking the
//     chooser's partial picks; the perspective strip keeps them all-zero for the non-chooser;
//   * determinism: same seed + same factored index sequence = same observations (M4 계약).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Schema v2: version bump + Confirm slot appended last, every v1 offset pinned absolute", () => Pure(SchemaV2OffsetStability)),
    ("Multi-select session table maps FULLY (toggles→candidate lanes, Confirm→new slot, skip→skip slot; unmapped 0 at every state)", SessionTableFullyMapped),
    ("Factored indices drive the session end-to-end (toggle→toggle→Confirm by index, pick order preserved)", FactoredIndexDrivesSession),
    ("Single-select mapping keeps its v1 meaning on the preserved boundary (MaxCount<=1)", SingleSelectMeaningUnchanged),
    ("Observation: selected bits track the chooser's partial picks; non-chooser strip reads all-zero", SelectedBitsInObservation),
    ("Infoset observation size is 3104 (3088 + 16 selected bits)", InfosetObservationSize),
    ("Toggle beyond candidate capacity is surfaced as unmapped, never silently dropped", () => Pure(ToggleOverflowSurfaced)),
    ("Determinism: same seed + same factored index sequence = identical observation stream", DeterministicFactoredReplay),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

static Task Pure(Action body)
{
    body();
    return Task.CompletedTask;
}

// --- schema shape (오프셋 안정성 지문) ---------------------------------------------------------------

void SchemaV2OffsetStability()
{
    FactoredActionSchema s = FactoredActionSchema.Default;
    AssertEqual(2, FactoredActionSchema.Version, "schema version is 2");

    // v1 fingerprint — ABSOLUTE offsets at default capacities (16/16/16). These are the exact values
    // the v1 schema produced; the v2 append must not move ANY of them.
    AssertEqual(0, s.NoOpOffset, "NoOp @0");
    AssertEqual(1, s.PassOffset, "Pass @1");
    AssertEqual(2, s.AdvancePhaseOffset, "AdvancePhase @2");
    AssertEqual(3, s.EndTurnOffset, "EndTurn @3");
    AssertEqual(4, s.PlayCardOffset, "PlayCard @4..19");
    AssertEqual(20, s.ActivateOptionOffset, "ActivateOption @20..35");
    AssertEqual(36, s.DigivolveOffset, "Digivolve @36..291");
    AssertEqual(292, s.DeclareAttackOffset, "DeclareAttack @292..563");
    AssertEqual(564, s.ResolveChoiceOffset, "ResolveChoice @564..580 (16 candidates + skip)");
    AssertEqual(581, s.HatchDigitamaOffset, "HatchDigitama @581");
    AssertEqual(582, s.MoveBreedingOffset, "MoveBreeding @582");
    AssertEqual(583, s.SpecialPlayOffset, "SpecialPlay @583..598");

    // v2 append: the ONLY new slot, after everything v1 had.
    AssertEqual(599, s.ConfirmChoiceOffset, "ConfirmChoice appended @599 (= v1 TotalSize)");
    AssertEqual(600, s.TotalSize, "TotalSize 599→600");
}

// --- full mapping of the session table -------------------------------------------------------------

async Task SessionTableFullyMapped()
{
    // Forced pair with a joint validator (BT20_098 모양) — the W1 shape.
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 211, minCount: 2, maxCount: 2, canSkip: false,
        validatorFactory: _ => set => set.Count == 2);
    FactoredActionSchema schema = FactoredActionSchema.Default;

    // State A — zero picks: 4 toggle lanes, nothing else.
    FactoredActionMask mask = match.EncodeFactoredActionMask(schema);
    AssertEqual(0, mask.Unmapped.Count, "zero picks: nothing unmapped");
    AssertEqual(4, mask.Actions.Count, "zero picks: exactly the 4 toggle lanes");
    for (int i = 0; i < hand.Length; i++)
    {
        HeadlessEntityId id = hand[i];
        FactoredAction lane = mask.Actions.Single(a =>
            a.Action.ActionType == HeadlessActionTypes.ToggleChoiceCandidate && ReadCandidateId(a.Action) == id);
        AssertEqual(schema.ResolveChoiceOffset + i, lane.Index, $"toggle for candidate {i} on ITS candidate slot");
        AssertEqual("ToggleChoiceCandidate", lane.Lane, "lane label");
    }

    // State B — two picks: toggles + Confirm; the Confirm sits on the NEW dedicated slot.
    await ApplyListedToggleAsync(match, P1, hand[3]);
    await ApplyListedToggleAsync(match, P1, hand[1]);
    mask = match.EncodeFactoredActionMask(schema);
    AssertEqual(0, mask.Unmapped.Count, "two picks: nothing unmapped (Confirm included)");
    FactoredAction confirm = mask.Actions.Single(a => a.Lane == "ConfirmChoice");
    AssertEqual(schema.ConfirmChoiceOffset, confirm.Index, "Confirm on the appended slot");
    AssertSequence(new[] { hand[3], hand[1] }, ReadSelectedIds(confirm.Action), "Confirm carries the picks in pick order");

    // State C — optional session at zero picks: toggles + skip; skip keeps its v1 slot.
    (DcgoMatch optional, HeadlessEntityId[] optionalHand) = await PendingHandChoiceMatchAsync(
        seed: 223, minCount: 0, maxCount: 3, canSkip: true);
    FactoredActionMask optionalMask = optional.EncodeFactoredActionMask(schema);
    AssertEqual(0, optionalMask.Unmapped.Count, "optional session: nothing unmapped");
    FactoredAction skip = optionalMask.Actions.Single(a =>
        a.Action.Parameters.ContainsKey(HeadlessActionParameterKeys.ChoiceSkipped));
    AssertEqual(schema.ResolveChoiceOffset + schema.MaxChoice, skip.Index, "skip on its v1 slot (@580)");
    // MinCount=0 also lights the Confirm button at zero picks (empty Select — the dispatcher's
    // canEndNotMax mirror), so the zero-pick optional table is toggles + skip + empty Confirm.
    FactoredAction emptyConfirm = optionalMask.Actions.Single(a => a.Lane == "ConfirmChoice");
    AssertEqual(schema.ConfirmChoiceOffset, emptyConfirm.Index, "empty-set Confirm on the appended slot");
    AssertEqual(0, ReadSelectedIds(emptyConfirm.Action).Count, "empty Confirm carries no picks");
    AssertEqual(optionalHand.Length + 2, optionalMask.Actions.Count, "toggles + skip + empty Confirm, all placed");
}

// --- factored index round-trip ---------------------------------------------------------------------

async Task FactoredIndexDrivesSession()
{
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 227, minCount: 2, maxCount: 2, canSkip: false,
        validatorFactory: _ => set => set.Count == 2);
    FactoredActionSchema schema = FactoredActionSchema.Default;

    // Tap candidates 2 then 0 purely BY INDEX — the policy-side driving mode.
    foreach (int candidateSlot in new[] { 2, 0 })
    {
        FactoredActionMask mask = match.EncodeFactoredActionMask(schema);
        AssertTrue(
            mask.TryGetAction(schema.ResolveChoiceOffset + candidateSlot, out LegalAction toggle),
            $"candidate slot {candidateSlot} resolves to its toggle");
        StepResult applied = await match.ApplyActionAsync(toggle);
        AssertTrue(applied.Events.All(e => e.Type != GameEventType.InvalidAction), "indexed toggle accepted");
        await match.StepAsync();
    }

    AssertSequence(
        new[] { hand[2], hand[0] },
        match.Context.ChoiceController.Current.PendingSelectedIds,
        "picks accumulated in tap order");

    FactoredActionMask confirmMask = match.EncodeFactoredActionMask(schema);
    AssertTrue(confirmMask.TryGetAction(schema.ConfirmChoiceOffset, out LegalAction confirm), "Confirm slot resolves");
    await match.ApplyActionAsync(confirm);
    await match.StepAsync();

    HeadlessChoiceState resolved = match.Context.ChoiceController.Current;
    AssertTrue(resolved.IsResolved && !resolved.IsPending, "Confirm-by-index resolved the choice");
    AssertSequence(new[] { hand[2], hand[0] }, resolved.SelectedIds, "resolution preserves pick order");
}

// --- preserved boundary (v1 meaning of the candidate lanes) ----------------------------------------

async Task SingleSelectMeaningUnchanged()
{
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 229, minCount: 0, maxCount: 1, canSkip: true);
    FactoredActionSchema schema = FactoredActionSchema.Default;

    FactoredActionMask mask = match.EncodeFactoredActionMask(schema);
    AssertEqual(0, mask.Unmapped.Count, "single-select table fully mapped (as in v1)");
    AssertEqual(0, mask.Actions.Count(a => a.Lane == "ConfirmChoice"), "no Confirm-slot usage outside a session");
    AssertEqual(0, mask.Actions.Count(a => a.Action.ActionType == HeadlessActionTypes.ToggleChoiceCandidate),
        "no toggle lanes outside a session");

    // Each size-1 resolution occupies its candidate's slot — byte-for-byte the v1 lane meaning.
    for (int i = 0; i < hand.Length; i++)
    {
        HeadlessEntityId id = hand[i];
        FactoredAction pick = mask.Actions.Single(a =>
            a.Action.ActionType == HeadlessActionTypes.ResolveChoice &&
            ReadSelectedIds(a.Action).Count == 1 &&
            ReadSelectedIds(a.Action)[0] == id);
        AssertEqual(schema.ResolveChoiceOffset + i, pick.Index, $"size-1 resolution for candidate {i} on slot {i}");
    }

    FactoredAction skip = mask.Actions.Single(a =>
        a.Action.Parameters.ContainsKey(HeadlessActionParameterKeys.ChoiceSkipped));
    AssertEqual(schema.ResolveChoiceOffset + schema.MaxChoice, skip.Index, "skip slot unchanged");
}

// --- observation: selected bits --------------------------------------------------------------------

async Task SelectedBitsInObservation()
{
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 233, minCount: 2, maxCount: 3, canSkip: false,
        validatorFactory: _ => set => set.Count >= 2);

    ObservationEncodingOptions options = InfosetOptions(match);
    await ApplyListedToggleAsync(match, P1, hand[1]);
    await ApplyListedToggleAsync(match, P1, hand[3]);

    // Chooser: bits exactly on the picked candidates, pending count re-pointed to the partial set.
    EncodedObservation chooser = new ObservationEncoder(options).Encode(match.GetObservation(P1));
    for (int i = 0; i < hand.Length; i++)
    {
        double expected = i is 1 or 3 ? 1 : 0;
        AssertEqual(expected, FeatureValue(chooser, $"choice.candidate.{i}.selected"), $"chooser selected bit {i}");
    }

    for (int i = hand.Length; i < 16; i++)
    {
        AssertEqual(0d, FeatureValue(chooser, $"choice.candidate.{i}.selected"), $"empty candidate slot {i} reads 0");
    }

    AssertEqual(2d, FeatureValue(chooser, "choice.selectedIds.count"), "pending partial count visible to the chooser");

    // Non-chooser: the perspective strip (B5-1) empties PendingSelectedIds — every bit reads 0 and the
    // partial count is withheld, while the candidate COUNT survives (AS-IS local-variable non-exposure).
    EncodedObservation opponent = new ObservationEncoder(options).Encode(match.GetObservation(P2));
    for (int i = 0; i < 16; i++)
    {
        AssertEqual(0d, FeatureValue(opponent, $"choice.candidate.{i}.selected"), $"opponent selected bit {i} is 0");
    }

    AssertEqual(0d, FeatureValue(opponent, "choice.selectedIds.count"), "opponent does not see the partial count");
    AssertEqual((double)hand.Length, FeatureValue(opponent, "choice.candidateCount"), "candidate count survives the strip");

    // Resolved state: bits clear, count reports the FINAL selection again.
    LegalAction confirm = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    await match.ApplyActionAsync(confirm);
    await match.StepAsync();
    EncodedObservation resolved = new ObservationEncoder(options).Encode(match.GetObservation(P1));
    for (int i = 0; i < 16; i++)
    {
        AssertEqual(0d, FeatureValue(resolved, $"choice.candidate.{i}.selected"), $"resolved: bit {i} cleared");
    }

    AssertEqual(2d, FeatureValue(resolved, "choice.selectedIds.count"), "resolved: final selection count restored");
}

async Task InfosetObservationSize()
{
    (DcgoMatch match, _) = await PendingHandChoiceMatchAsync(seed: 239, minCount: 0, maxCount: 1, canSkip: true);
    ObservationEncodingOptions options = InfosetOptions(match);

    EncodedObservation encoded = new ObservationEncoder(options).Encode(match.GetObservation(P1));
    AssertEqual(3104, encoded.Length, "infoset obs size 3088→3104 (+16 selected bits)");
    AssertEqual(16, encoded.FeatureNames.Count(n => n.EndsWith(".selected", StringComparison.Ordinal)),
        "exactly 16 selected bits added");
}

// --- capacity guard --------------------------------------------------------------------------------

void ToggleOverflowSurfaced()
{
    // 17 candidates against a 16-slot candidate lane: the 17th toggle has no slot — it must surface as
    // unmapped (the FactoredActionMask no-silent-drop contract), exactly like the v1 overflow paths.
    HeadlessEntityId[] candidates = Enumerable.Range(0, 17).Select(i => new HeadlessEntityId($"c{i}")).ToArray();
    var positions = new FactoredPositionContext(
        static (_, _) => Array.Empty<HeadlessEntityId>(),
        candidates,
        multiSelectSessionActive: true);

    LegalAction inRange = HeadlessActionFactory.ToggleChoiceCandidate(P1, candidates[15], actionId: "t15");
    LegalAction overflow = HeadlessActionFactory.ToggleChoiceCandidate(P1, candidates[16], actionId: "t16");
    FactoredActionMask mask = FactoredActionEncoder.Encode(new[] { inRange, overflow }, positions);

    AssertEqual(1, mask.Actions.Count, "slot-15 toggle placed");
    AssertEqual(FactoredActionSchema.Default.ResolveChoiceOffset + 15, mask.Actions[0].Index, "last in-range slot");
    AssertEqual(1, mask.Unmapped.Count, "slot-16 toggle surfaced as unmapped");
    AssertEqual(overflow.Id, mask.Unmapped[0].Id, "the overflow action is the unmapped one");
}

// --- determinism -----------------------------------------------------------------------------------

async Task DeterministicFactoredReplay()
{
    string first = await RunOnceAsync();
    string second = await RunOnceAsync();
    AssertEqual(first, second, "seed-replay by factored indices: identical observation stream digest");

    async Task<string> RunOnceAsync()
    {
        (DcgoMatch match, _) = await PendingHandChoiceMatchAsync(
            seed: 241, minCount: 2, maxCount: 2, canSkip: false,
            validatorFactory: _ => set => set.Count == 2);
        FactoredActionSchema schema = FactoredActionSchema.Default;
        ObservationEncodingOptions options = InfosetOptions(match);
        var material = new StringBuilder();

        foreach (int index in new[]
        {
            schema.ResolveChoiceOffset + 1,
            schema.ResolveChoiceOffset + 1,   // un-tap (toggle round trip inside the stream)
            schema.ResolveChoiceOffset + 3,
            schema.ResolveChoiceOffset + 0,
            schema.ConfirmChoiceOffset,
        })
        {
            FactoredActionMask mask = match.EncodeFactoredActionMask(schema);
            AssertTrue(mask.TryGetAction(index, out LegalAction action), $"index {index} on the table");
            await match.ApplyActionAsync(action);
            await match.StepAsync();

            EncodedObservation obs = new ObservationEncoder(options).Encode(match.GetObservation(P1));
            material.Append(index).Append('>');
            foreach (double v in obs.ToVector())
            {
                material.Append(v.ToString("R")).Append(';');
            }

            material.Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString())));
    }
}

// --- fixtures --------------------------------------------------------------------------------------

async Task<(DcgoMatch Match, HeadlessEntityId[] Hand)> PendingHandChoiceMatchAsync(
    int seed,
    int minCount,
    int maxCount,
    bool canSkip,
    Func<HeadlessEntityId[], Func<IReadOnlyList<HeadlessEntityId>, bool>>? validatorFactory = null)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);

    StarterDecks.StarterDeck d1 = StarterDecks.Get("ST1");
    StarterDecks.StarterDeck d2 = StarterDecks.Get("ST2");
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions)
        },
        firstPlayerId: P1);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);

    var match = new DcgoMatch(context, new EngineTrace(), actionLegality: new LegalActionSetValidator());
    await match.InitializeAsync(config);

    IReadOnlyList<HeadlessEntityId> handZone = ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.Hand);
    AssertTrue(handZone.Count >= 4, "P1 has enough hand cards to choose from");
    HeadlessEntityId[] candidates = handZone.Take(4).ToArray();

    ChoiceRequest request = new(
        ChoiceType.HandCard, P1, "pick cards", minCount, maxCount, canSkip, ChoiceZone.Hand,
        candidates.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true)).ToArray())
    {
        SelectionValidator = validatorFactory?.Invoke(candidates),
    };
    context.ChoiceController.RequestChoice(request, new HeadlessEntityId("r4rl04:test-choice"));
    return (match, candidates);
}

ObservationEncodingOptions InfosetOptions(DcgoMatch match)
{
    CardVocabulary vocabulary = CardVocabulary.FromRepository(match.Context.CardRepository);
    return ObservationEncodingOptions.InformationSet(vocabulary);
}

async Task ApplyListedToggleAsync(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId candidateId)
{
    LegalAction toggle = match.GetLegalActions(player).Single(a =>
        a.ActionType == HeadlessActionTypes.ToggleChoiceCandidate &&
        ReadCandidateId(a) == candidateId);
    StepResult applied = await match.ApplyActionAsync(toggle);
    AssertTrue(
        applied.Events.All(e => e.Type != GameEventType.InvalidAction),
        $"listed toggle for {candidateId.Value} accepted by the legality boundary");
    await match.StepAsync();
}

static HeadlessEntityId ReadCandidateId(LegalAction action) =>
    action.Parameters.TryGetValue(HeadlessActionParameterKeys.ChoiceCandidateId, out object? raw) && raw is HeadlessEntityId id
        ? id
        : new HeadlessEntityId(raw?.ToString() ?? string.Empty);

static IReadOnlyList<HeadlessEntityId> ReadSelectedIds(LegalAction action) =>
    action.Parameters.TryGetValue(HeadlessActionParameterKeys.ChoiceSelectedIds, out object? raw) && raw is IEnumerable<HeadlessEntityId> ids
        ? ids.ToArray()
        : Array.Empty<HeadlessEntityId>();

static double FeatureValue(EncodedObservation encoded, string name)
{
    ObservationFeature? feature = encoded.Features.FirstOrDefault(f => f.Name == name);
    if (feature is null)
    {
        throw new InvalidOperationException($"Feature '{name}' not found in the encoded observation.");
    }

    return feature.Value;
}

static void AssertTrue(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException($"Assertion failed: {message}"); }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Assertion failed: {message} (expected: {expected}, actual: {actual})");
    }
}

static void AssertSequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
{
    T[] e = expected.ToArray();
    T[] a = actual.ToArray();
    if (!e.SequenceEqual(a))
    {
        throw new InvalidOperationException(
            $"Assertion failed: {message} (expected: [{string.Join(", ", e)}], actual: [{string.Join(", ", a)}])");
    }
}
