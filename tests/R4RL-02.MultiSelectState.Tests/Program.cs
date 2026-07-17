using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (R4RL-02 — B5-1, docs/audit/rl_env_design_2026-07-17.md §B5.4/§B5.6/§B5.9) STATE-LEVEL witnesses for the
// multi-select partial-selection extension. B5-1 is BEHAVIOR-UNCHANGED by contract: the dispatcher does not
// enumerate toggle lanes yet (B5-2) and nothing observes PendingSelectedIds (B5-3) — these tests pin the
// dormant state machinery against its AS-IS anchors and prove the existing batch-ResolveChoice path is
// indifferent to partial state.
//
// AS-IS anchors (DCGO/Assets/Scripts/Script/…):
//   * toggle (tap/re-tap, pick order)  — SelectPermanentEffect.cs OnClickFieldPermanentCard :431-464
//                                        (Contains -> Remove, else Add in tap order; SelectHandEffect.cs
//                                        :271-311 same shape; pick order is semantic: SetSelectedIndexText
//                                        numbers picks 1..n, PutLibraryBottom consumes in pick order)
//   * replace-last at MaxCount         — SelectPermanentEffect.cs :457-463 (RemoveAt(Count-1) then Add(new),
//                                        inside an `if (Count > 0)` guard), SelectHandEffect.cs :303-310,
//                                        SelectCardPanel.cs :481-482
//   * session dies with the selection  — PreSelected lists are Activate() coroutine LOCALS
//                                        (SelectPermanentEffect.cs :362, SelectHandEffect.cs :253): nothing
//                                        outlives the confirm/skip, so resolve must clear the scratchpad
//   * unsatisfiable FORCED batch       — Hand/Panel surfaces have no open-time combination check
//                                        (SelectHandEffect.active() :147-160); the AI branch's bounded
//                                        search failing demotes to no-select (SetTargetHandCards(null) ->
//                                        _noSelect, :496-570 -> :595-608). The deterministic 200-cap
//                                        lexicographic search is the RD-R4P4-02 translation
//                                        (ScriptedChoiceProvider.CreateFallbackChoice :87-105).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(§B5.4/W4) toggle round trip preserves PICK order: a,b,c then untap b then tap d -> [a,c,d]", () => Pure(TogglePickOrderRoundTrip)),
    ("(§B5.4/W5) at MaxCount a fresh tap REPLACES THE LAST pick (AS-IS :457-463), order preserved", () => Pure(ToggleReplaceLastAtMax)),
    ("(§B5.4) MaxCount 0 tap is a no-op (AS-IS `if (Count > 0)` guard inside the replace branch)", () => Pure(ToggleMaxZeroIsNoOp)),
    ("(핀 3) deselect of a picked candidate is always a plain Remove, even at MaxCount (escape lane)", () => Pure(DeselectAlwaysRemoves)),
    ("(§B5.4) toggle guards: no pending choice / non-candidate id are rejected, state untouched", () => Pure(ToggleGuards)),
    ("(§B5.4) resolve clears the partial state: scratchpad never outlives the pending choice", () => Pure(ResolveClearsPartialState)),
    ("(§B5.7) batch ResolveChoice backward compat: identical resolved state with and without prior toggles", () => Pure(ResolveIsIndifferentToPartialState)),
    ("(§B5.7) skip resolve likewise clears the partial state", () => Pure(SkipResolveClearsPartialState)),
    ("(§B5.6) completability: a validator-passing pair exists -> completable, not demotable (+provider parity)", CompletabilitySatisfiable),
    ("(§B5.6/W2) completability: impossible-sum forced pair -> unsatisfiable forced choice (demote shape)", () => Pure(CompletabilityUnsatisfiableValidator)),
    ("(§B5.6) completability: pure count-forced shortfall (selectable < MinCount) demotes without any search", () => Pure(CompletabilityCountForced)),
    ("(§B5.6) demotion boundary: CanSkip / MinCount<=1 / Count-type shapes are never demoted", () => Pure(CompletabilityDemotionBoundary)),
    ("(§B5.6) 200-evaluation cap is deterministic: pass inside the cap found, pass beyond the cap not found", () => Pure(CompletabilityCapBoundary)),
    ("(§B5.5 보존 경계) live match: MaxCount<=1 table ignores toggles (bit-identical), and a listed resolve still lands", DispatcherTableUnchangedByToggles),
    ("(§B5.7 시점 필터) live match: PendingSelectedIds is chooser-only; non-chooser view gets it stripped", PendingSelectedIdsPerspectiveStrip),
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

// --- toggle transition (controller-level, AS-IS :431-464) ------------------------------------------

void TogglePickOrderRoundTrip()
{
    (InMemoryHeadlessChoiceController controller, HeadlessEntityId[] ids) = PendingChoice(maxCount: 3, candidateCount: 4);
    (HeadlessEntityId a, HeadlessEntityId b, HeadlessEntityId c, HeadlessEntityId d) = (ids[0], ids[1], ids[2], ids[3]);

    controller.ToggleCandidate(a);
    controller.ToggleCandidate(b);
    HeadlessChoiceState afterThree = controller.ToggleCandidate(c);
    AssertSequence(new[] { a, b, c }, afterThree.PendingSelectedIds, "taps append in pick order (AS-IS Add)");

    // Re-tap b: AS-IS `Contains -> Remove` — the list closes the gap, relative order of the rest survives.
    HeadlessChoiceState afterUntap = controller.ToggleCandidate(b);
    AssertSequence(new[] { a, c }, afterUntap.PendingSelectedIds, "re-tap removes exactly that pick");

    // Tap d with count(2) < max(3): plain append — [a,c,d], NOT re-sorted to candidate order.
    HeadlessChoiceState afterD = controller.ToggleCandidate(d);
    AssertSequence(new[] { a, c, d }, afterD.PendingSelectedIds, "pick order [a,c,d] preserved (W4 shape)");

    AssertTrue(controller.Current.IsPending, "toggling never resolves the choice (session ends only via confirm/skip)");
    AssertEqual(0, controller.Current.SelectedIds.Count, "resolved SelectedIds stays empty while pending");
}

void ToggleReplaceLastAtMax()
{
    (InMemoryHeadlessChoiceController controller, HeadlessEntityId[] ids) = PendingChoice(maxCount: 2, candidateCount: 3);
    (HeadlessEntityId a, HeadlessEntityId b, HeadlessEntityId c) = (ids[0], ids[1], ids[2]);

    controller.ToggleCandidate(a);
    controller.ToggleCandidate(b);
    AssertSequence(new[] { a, b }, controller.Current.PendingSelectedIds, "at MaxCount");

    // AS-IS :457-463: RemoveAt(Count-1) then Add(new) — b (the LAST pick, not the first) is replaced.
    HeadlessChoiceState afterC = controller.ToggleCandidate(c);
    AssertSequence(new[] { a, c }, afterC.PendingSelectedIds, "replace-last: [a,b] + tap c -> [a,c]");

    // Repeat at the boundary: tapping b again replaces c — still stable, still order-preserving.
    HeadlessChoiceState afterB = controller.ToggleCandidate(b);
    AssertSequence(new[] { a, b }, afterB.PendingSelectedIds, "replace-last again: [a,c] + tap b -> [a,b]");
}

void ToggleMaxZeroIsNoOp()
{
    (InMemoryHeadlessChoiceController controller, HeadlessEntityId[] ids) = PendingChoice(maxCount: 0, candidateCount: 1);

    // AS-IS: count(0) < max(0) fails, replace branch is inside `if (Count > 0)` — the tap changes nothing.
    HeadlessChoiceState after = controller.ToggleCandidate(ids[0]);
    AssertEqual(0, after.PendingSelectedIds.Count, "MaxCount 0 tap is a no-op, no throw (AS-IS guard)");
    AssertTrue(after.IsPending, "still pending");
}

void DeselectAlwaysRemoves()
{
    (InMemoryHeadlessChoiceController controller, HeadlessEntityId[] ids) = PendingChoice(maxCount: 2, candidateCount: 2);
    controller.ToggleCandidate(ids[0]);
    controller.ToggleCandidate(ids[1]);

    // Pin 3: the deselect lane is unconditional (AS-IS `Contains -> Remove` precedes every gate) — the
    // partial state can always be reconstructed, which is the whole no-deadlock argument for forced picks.
    HeadlessChoiceState after = controller.ToggleCandidate(ids[0]);
    AssertSequence(new[] { ids[1] }, after.PendingSelectedIds, "deselect at MaxCount removes the pick");
    HeadlessChoiceState empty = controller.ToggleCandidate(ids[1]);
    AssertEqual(0, empty.PendingSelectedIds.Count, "toggling everything off empties the scratchpad");
}

void ToggleGuards()
{
    var controller = new InMemoryHeadlessChoiceController();
    Throws<InvalidOperationException>(
        () => controller.ToggleCandidate(new HeadlessEntityId("cand:0")),
        "toggle without a pending choice is a contract violation");

    (controller, HeadlessEntityId[] ids) = PendingChoice(maxCount: 2, candidateCount: 2);
    controller.ToggleCandidate(ids[0]);
    Throws<ArgumentException>(
        () => controller.ToggleCandidate(new HeadlessEntityId("cand:ghost")),
        "a non-candidate id is rejected (parallel to ChoiceResult.Validate's non-candidate rejection)");
    AssertSequence(new[] { ids[0] }, controller.Current.PendingSelectedIds, "failed toggle left the state untouched");
}

// --- resolve compatibility (§B5.7: the pre-session batch path must be indifferent) -----------------

void ResolveClearsPartialState()
{
    (InMemoryHeadlessChoiceController controller, HeadlessEntityId[] ids) = PendingChoice(maxCount: 2, candidateCount: 3);
    controller.ToggleCandidate(ids[0]);
    controller.ToggleCandidate(ids[1]);

    // Resolve with a DIFFERENT set than the scratchpad — the batch answer is authoritative, the
    // scratchpad is discarded (AS-IS: the PreSelected local dies when the selection UI closes).
    HeadlessChoiceState resolved = controller.ResolveChoice(ChoiceResult.Select(new[] { ids[2] }));
    AssertTrue(resolved.IsResolved && !resolved.IsPending, "resolved");
    AssertSequence(new[] { ids[2] }, resolved.SelectedIds, "the batch answer landed verbatim");
    AssertEqual(0, resolved.PendingSelectedIds.Count, "partial state cleared on resolve");

    // And a fresh request starts with an empty scratchpad.
    controller.ClearChoice();
    (InMemoryHeadlessChoiceController fresh, _) = PendingChoice(maxCount: 2, candidateCount: 2);
    AssertEqual(0, fresh.Current.PendingSelectedIds.Count, "new request opens with an empty scratchpad");
}

void ResolveIsIndifferentToPartialState()
{
    // Same request, same batch answer; controller A toggles first, controller B does not.
    (InMemoryHeadlessChoiceController withToggles, HeadlessEntityId[] ids) = PendingChoice(maxCount: 2, candidateCount: 3);
    (InMemoryHeadlessChoiceController without, _) = PendingChoice(maxCount: 2, candidateCount: 3);

    withToggles.ToggleCandidate(ids[2]);
    withToggles.ToggleCandidate(ids[0]);

    ChoiceResult answer = ChoiceResult.Select(new[] { ids[0], ids[1] });
    HeadlessChoiceState a = withToggles.ResolveChoice(answer);
    HeadlessChoiceState b = without.ResolveChoice(answer);

    AssertEqual(b.IsResolved, a.IsResolved, "IsResolved identical");
    AssertEqual(b.IsSkipped, a.IsSkipped, "IsSkipped identical");
    AssertEqual(b.SelectedCount, a.SelectedCount, "SelectedCount identical");
    AssertSequence(b.SelectedIds, a.SelectedIds, "SelectedIds identical regardless of prior toggles");
    AssertEqual(0, a.PendingSelectedIds.Count, "toggled side cleared");
    AssertEqual(0, b.PendingSelectedIds.Count, "untoggled side trivially empty");
}

void SkipResolveClearsPartialState()
{
    (InMemoryHeadlessChoiceController controller, HeadlessEntityId[] ids) = PendingChoice(maxCount: 2, candidateCount: 2, canSkip: true);
    controller.ToggleCandidate(ids[0]);

    HeadlessChoiceState resolved = controller.ResolveChoice(ChoiceResult.Skip());
    AssertTrue(resolved.IsSkipped, "skipped");
    AssertEqual(0, resolved.PendingSelectedIds.Count, "partial state cleared on skip resolve");
}

// --- completability (§B5.6, the RD-R4P4-02 search reused as an open-time existence check) ----------

async Task CompletabilitySatisfiable()
{
    HeadlessEntityId[] ids = Candidates(4);
    // Joint validator in the BT20_098/W1 mold: only the specific pair {ids[1], ids[3]} "sums" right.
    ChoiceRequest request = ForcedPairRequest(ids, validator: set =>
        set.Count == 2 && set.Contains(ids[1]) && set.Contains(ids[3]));

    AssertTrue(ChoiceCompletability.HasCompletableSelection(request), "a passing pair exists");
    AssertTrue(!ChoiceCompletability.IsUnsatisfiableForcedChoice(request), "satisfiable -> never demoted");

    // Parity with the provider seat: the SAME deterministic search (ScriptedChoiceProvider :87-105)
    // must also find a passing set — 'completable' and 'auto-select succeeds' judge alike.
    ChoiceResult auto = await new ScriptedChoiceProvider().ChooseAsync(request);
    AssertTrue(request.SelectionValidator!(auto.SelectedIds), "provider fallback confirms a validator-passing set");
}

void CompletabilityUnsatisfiableValidator()
{
    // W2 shape: MinCount=MaxCount=2, canSkip:false, validator == "impossible sum" (never true).
    ChoiceRequest request = ForcedPairRequest(Candidates(4), validator: _ => false);

    AssertTrue(!ChoiceCompletability.HasCompletableSelection(request), "no combination passes");
    AssertTrue(ChoiceCompletability.IsUnsatisfiableForcedChoice(request),
        "forced + validator-dead = the no-select demotion shape (AS-IS null demotion, SelectHandEffect.cs :504-570)");
}

void CompletabilityCountForced()
{
    // 3 candidates but only ONE selectable: selectable(1) < MinCount(2). AS-IS analogue: the AI branch's
    // ValidCards.Count >= maxCount gate fails -> SetTargetHandCards(null). No search is needed or run —
    // a throwing validator proves the short-circuit.
    HeadlessEntityId[] ids = Candidates(3);
    var request = new ChoiceRequest(
        ChoiceType.HandCard, P1, "forced two", minCount: 2, maxCount: 2, canSkip: false, ChoiceZone.Hand,
        ids.Select((id, i) => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: i == 0)).ToArray())
    {
        SelectionValidator = _ => throw new InvalidOperationException("count shortfall must not reach the validator"),
    };

    AssertTrue(!ChoiceCompletability.HasCompletableSelection(request), "count shortfall -> incompletable");
    AssertTrue(ChoiceCompletability.IsUnsatisfiableForcedChoice(request), "count shortfall -> demote shape");

    // With no validator at all the same count gate is the whole judgment.
    ChoiceRequest bare = request with { SelectionValidator = null };
    AssertTrue(!ChoiceCompletability.HasCompletableSelection(bare), "validator-less count shortfall");
    AssertTrue(ChoiceCompletability.IsUnsatisfiableForcedChoice(bare), "validator-less demote shape");
}

void CompletabilityDemotionBoundary()
{
    HeadlessEntityId[] ids = Candidates(3);

    // CanSkip=true: optional selections are never demoted (the skip lane is the escape — 핀 2).
    var skippable = new ChoiceRequest(
        ChoiceType.HandCard, P1, "optional pair", minCount: 2, maxCount: 2, canSkip: true, ChoiceZone.Hand,
        ids.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true)).ToArray())
    {
        SelectionValidator = _ => false,
    };
    AssertTrue(!ChoiceCompletability.IsUnsatisfiableForcedChoice(skippable), "CanSkip shapes are out of scope");

    // MinCount<=1: the existing single-pick table (with the RD-S3D-01 filter) already covers these — the
    // demotion predicate must not creep into the preserved-trajectory boundary (§B5.5).
    var singleForced = new ChoiceRequest(
        ChoiceType.HandCard, P1, "forced one", minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.Hand,
        ids.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true)).ToArray())
    {
        SelectionValidator = _ => false,
    };
    AssertTrue(!ChoiceCompletability.IsUnsatisfiableForcedChoice(singleForced), "MinCount<=1 stays on the existing table");

    // Count-type requests have no candidate combinatorics at all.
    var countType = new ChoiceRequest(
        ChoiceType.Count, P1, "pick how many", minCount: 2, maxCount: 3, canSkip: false, ChoiceZone.None,
        Array.Empty<ChoiceCandidate>());
    AssertTrue(!ChoiceCompletability.IsUnsatisfiableForcedChoice(countType), "Count-type is out of scope");
}

void CompletabilityCapBoundary()
{
    // 25 selectable candidates, MinCount=MaxCount=2 -> C(25,2) = 300 lexicographic pairs, cap = 200
    // evaluations (the RD-R4P4-02/§B5 리스크 5 pinned value; same accounting as the provider loop).
    HeadlessEntityId[] ids = Candidates(25);

    // Pair (7,8) is evaluation #148 (24+23+...+18 pairs starting below 7, then (7,8) first) — inside the cap.
    ChoiceRequest findable = ForcedPairRequest(ids, validator: set =>
        set.Count == 2 && set.Contains(ids[7]) && set.Contains(ids[8]));
    AssertTrue(ChoiceCompletability.HasCompletableSelection(findable), "a pass inside the 200-cap is found");

    // Pair (23,24) is evaluation #300 — beyond the cap: deterministically NOT found (the bounded-search
    // translation judges 'no completion within the AS-IS budget', exactly like the AS-IS AI giving up).
    ChoiceRequest beyondCap = ForcedPairRequest(ids, validator: set =>
        set.Count == 2 && set.Contains(ids[23]) && set.Contains(ids[24]));
    AssertTrue(!ChoiceCompletability.HasCompletableSelection(beyondCap), "a pass beyond the 200-cap is not found");
    AssertTrue(ChoiceCompletability.IsUnsatisfiableForcedChoice(beyondCap), "beyond-cap forced shape demotes (documented cap semantics)");
}

// --- live-match behavior-unchanged witnesses (B5-1 계약: 디스패처/관측 무배선) ----------------------

async Task DispatcherTableUnchangedByToggles()
{
    // (B5-2 re-pin) The B5-1 version pinned "no lane reads PendingSelectedIds" for ANY shape; after the
    // B5-2 flip that contract holds exactly on the PRESERVED boundary (설계 §B5.5): MaxCount<=1 requests
    // keep the pre-session table and stay indifferent to partial state. MaxCount>1 session behavior is
    // witnessed by R4RL-03 (flip-level).
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(seed: 61, maxCount: 1);

    static string[] TableFingerprint(DcgoMatch m, HeadlessPlayerId p) =>
        m.GetLegalActions(p)
            .Select(a => $"{a.Id.Value}|{a.ActionType}|{string.Join(",", a.Parameters.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={FormatParam(kv.Value)}"))}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

    string[] before = TableFingerprint(match, P1);
    AssertTrue(before.Length > 0, "the pending choice exposes a resolution table");
    AssertTrue(
        before.All(row => !row.Contains(HeadlessActionTypes.ToggleChoiceCandidate)),
        "MaxCount<=1 never opens a session (no toggle lanes on the preserved boundary)");

    // Preserved boundary: the dispatcher does not read PendingSelectedIds for MaxCount<=1 — toggling must
    // not add, remove, or reshape ANY lane.
    match.Context.ChoiceController.ToggleCandidate(hand[0]);
    match.Context.ChoiceController.ToggleCandidate(hand[1]);
    string[] after = TableFingerprint(match, P1);
    AssertSequence(before, after, "legal table bit-identical under partial state");

    // And a listed batch resolve still lands exactly as before, clearing the scratchpad.
    LegalAction pick = match.GetLegalActions(P1).First(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    await match.ApplyActionAsync(pick);
    await match.StepAsync();
    AssertTrue(match.Context.ChoiceController.Current.IsResolved, "listed pick resolved through the existing path");
    AssertEqual(0, match.Context.ChoiceController.Current.PendingSelectedIds.Count, "scratchpad cleared by the resolve");
}

async Task PendingSelectedIdsPerspectiveStrip()
{
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(seed: 67);
    match.Context.ChoiceController.ToggleCandidate(hand[1]);
    match.Context.ChoiceController.ToggleCandidate(hand[0]);

    HeadlessChoiceState chooserView = match.GetObservation(P1).Choice;
    HeadlessChoiceState opponentView = match.GetObservation(P2).Choice;
    HeadlessChoiceState godView = match.GetObservation().Choice;

    // AS-IS: PreSelected is a local of the SELECTING client; the opponent only ever sees "The opponent is
    // selecting cards." (SelectPermanentEffect.cs :624) until the confirm RPC — chooser-only information.
    AssertSequence(new[] { hand[1], hand[0] }, chooserView.PendingSelectedIds, "chooser sees the picks in pick order");
    AssertEqual(0, opponentView.PendingSelectedIds.Count, "non-chooser gets PendingSelectedIds stripped");
    AssertEqual(0, opponentView.CandidateIds.Count, "existing candidate strip intact alongside");
    AssertSequence(new[] { hand[1], hand[0] }, godView.PendingSelectedIds, "null perspective (debug) keeps the full view");
}

// --- fixtures --------------------------------------------------------------------------------------

(InMemoryHeadlessChoiceController Controller, HeadlessEntityId[] Ids) PendingChoice(
    int maxCount, int candidateCount, bool canSkip = false)
{
    HeadlessEntityId[] ids = Candidates(candidateCount);
    var controller = new InMemoryHeadlessChoiceController();
    controller.RequestChoice(new ChoiceRequest(
        ChoiceType.HandCard, P1, "pick", minCount: 0, maxCount: maxCount, canSkip: canSkip, ChoiceZone.Hand,
        ids.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true)).ToArray()));
    return (controller, ids);
}

HeadlessEntityId[] Candidates(int count) =>
    Enumerable.Range(0, count).Select(i => new HeadlessEntityId($"cand:{i}")).ToArray();

ChoiceRequest ForcedPairRequest(HeadlessEntityId[] ids, Func<IReadOnlyList<HeadlessEntityId>, bool> validator) =>
    new ChoiceRequest(
        ChoiceType.HandCard, P1, "forced pair", minCount: 2, maxCount: 2, canSkip: false, ChoiceZone.Hand,
        ids.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true)).ToArray())
    {
        SelectionValidator = validator,
    };

async Task<(DcgoMatch Match, HeadlessEntityId[] Hand)> PendingHandChoiceMatchAsync(int seed, int maxCount = 2)
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

    IReadOnlyList<HeadlessEntityId> hand = ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.Hand);
    AssertTrue(hand.Count >= 3, "P1 has hand cards to choose from");
    HeadlessEntityId[] candidates = hand.Take(3).ToArray();

    context.ChoiceController.RequestChoice(new ChoiceRequest(
        ChoiceType.HandCard, P1, "discard one", minCount: 1, maxCount: maxCount, canSkip: false, ChoiceZone.Hand,
        candidates.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true)).ToArray()),
        new HeadlessEntityId("r4rl02:test-choice"));
    return (match, candidates);
}

static string FormatParam(object? value) => value switch
{
    null => "null",
    HeadlessEntityId[] ids => string.Join("+", ids.Select(i => i.Value)),
    System.Collections.IEnumerable seq and not string => string.Join("+", seq.Cast<object?>().Select(FormatParam)),
    _ => value.ToString() ?? string.Empty,
};

static Task Pure(Action body)
{
    body();
    return Task.CompletedTask;
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

static void Throws<TException>(Action body, string message)
    where TException : Exception
{
    try
    {
        body();
    }
    catch (TException)
    {
        return;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Assertion failed: {message} (threw {ex.GetType().Name} instead of {typeof(TException).Name})");
    }

    throw new InvalidOperationException($"Assertion failed: {message} (no exception thrown; expected {typeof(TException).Name})");
}
