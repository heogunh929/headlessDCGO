using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (R4RL-03 — B5-2 flip, docs/audit/rl_env_design_2026-07-17.md §B5.5/§B5.6/§B5.8) FLIP-LEVEL witnesses for
// the multi-select dispatcher session and the unsatisfiable-forced demotion. B5-1 (R4RL-02) pinned the
// dormant state machinery; this suite pins the BEHAVIOR CHANGE:
//   * a MaxCount>1 non-Count pending choice now opens a partial-selection SESSION on the legal table
//     (toggle lanes + Confirm + skip-at-empty) instead of the pre-flip table (MinCount>1 = EMPTY = stall;
//     up-to-N = size-1 lanes only);
//   * an unsatisfiable FORCED batch choice demotes to no-select AT OPEN TIME (never opens, never stalls)
//     with the unsatisfiableForcedChoice marking observable in the step's events/metadata;
//   * MaxCount<=1 requests keep the pre-flip table (보존 경계; bit-identity vs the OLD code is the shadow
//     suites' job — here the shape is pinned).
//
// AS-IS anchors (DCGO/Assets/Scripts/Script/…):
//   * toggle lanes / per-pick gate     — SelectHandEffect.cs OnClickHandCard :269-322 (Contains→Remove
//                                        unconditional; canTargetCondition_ByPreSelecetedList evaluated
//                                        with the last pick EXCLUDED when Count >= maxCount, :283-289;
//                                        replace-last :303-310)
//   * Confirm lane                     — CanEndSelect (count==max || canEndNotMax) && canEndSelectCondition,
//                                        re-evaluated after every tap (CheckEndSelect; SelectHandEffect.cs
//                                        :575-591)
//   * skip lane at empty only          — "No Selection" back button shown iff PreSelected empty && canNoSelect
//                                        (SelectHandEffect.cs :433-446 — 핀 2)
//   * unsatisfiable forced demotion    — the AI branch's bounded search failing -> SetTargetHandCards(null)
//                                        -> _noSelect (SelectHandEffect.cs :496-570 -> :595-608), translated
//                                        to open time (설계 §B5.6)

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(W1형 red→green) 합계형 joint validator, 강제 pair: 낱장 전멸 -> 토글×2+Confirm으로 해소(pre-flip이면 표가 비어 stall)", JointValidatorForcedPairRedToGreen),
    ("(W5) per-pick 게이트: 게이트 실패 후보는 토글 레인 부재 + at-max replace는 last-제외 리스트로 평가", PerPickGateAndReplaceLastBasis),
    ("(A1/게이트) 표 밖 토글(게이트 실패 후보) 제출은 Illegal — 상태 무변", OffTableToggleRejected),
    ("(§B5.5) Confirm 레인: 미달 시 부재 -> 충족 시 등장(픽 순서 탑재) -> 적용=resolve 성공", ConfirmLaneLifecycle),
    ("(핀 2) skip 레인: 0장일 때만 — 1장이면 소멸, 전부 해제하면 재등장, 적용=skip resolve", SkipLaneOnlyWhenEmpty),
    ("(W2/§B5.6) 불충족-강제: 개설 시점 no-select 강등 — 세션 미개설·빈 Select·이벤트/메타 관측(stall→진행)", UnsatisfiableForcedDemotesAtOpen),
    ("(§B5.6 경계) 만족-가능 강제는 강등되지 않고 세션이 열림(토글 레인 노출)", SatisfiableForcedOpensSession),
    ("(W7) up-to-N optional: 다중 픽 도달 가능(구 표는 size-1만) — 2장 Confirm resolve", UpToNOptionalMultiPick),
    ("(W9) 세션 중 표-밖 Confirm(부분 집합 불일치) 제출은 Illegal — 픽 상태 유지", OffTableConfirmRejected),
    ("(보존 경계) MaxCount==1 표는 세션 미개설: size-1 레인+B1 필터+skip 그대로, 토글 레인 없음", SingleSelectShapePreserved),
    ("(스모크+결정론) 랜덤 에이전트 풀게임: 다중-선택 세션 경유 자연 종결 + seed-replay 지문 일치", RandomAgentFullGameSmoke),
    ("(W8) 실카드 EX8_051 <Fragment <3>>: 발화 경로에서 min=max=3 세션(토글×3+Confirm) 완주 + AS-IS 결과 대조", FragmentRealCardSessionWitness),
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

// --- session red→green (W1형: BT20_098 합계 패턴) ---------------------------------------------------

async Task JointValidatorForcedPairRedToGreen()
{
    // Values a:1 b:2 c:1 d:3, target sum 5 — ONLY {b,d} passes; every singleton and every other pair fails.
    // Pre-flip table for MinCount>1 (old dispatcher: "Multi-select (MinCount > 1) ... only expose Skip when
    // allowed" and canSkip:false here): ZERO actions = agent stall. The flip must yield a working session.
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 71, minCount: 2, maxCount: 2, canSkip: false,
        validatorFactory: cards => set =>
        {
            int[] values = { 1, 2, 1, 3 };
            return set.Count == 2 && set.Sum(id => values[Array.IndexOf(cards, id)]) == 5;
        });

    IReadOnlyList<LegalAction> table = match.GetLegalActions(P1);
    AssertTrue(table.Count > 0, "red→green: the forced multi-select table is NOT empty any more");
    AssertEqual(0, table.Count(a => a.ActionType == HeadlessActionTypes.ResolveChoice),
        "no premature Confirm/size-1 lanes at zero picks");
    AssertEqual(4, table.Count(a => a.ActionType == HeadlessActionTypes.ToggleChoiceCandidate),
        "one toggle lane per selectable candidate");

    // Pick d then b THROUGH the validated action surface (A1 exercised on the toggle lanes).
    await ApplyListedToggleAsync(match, P1, hand[3]);
    AssertEqual(0, match.GetLegalActions(P1).Count(a => a.ActionType == HeadlessActionTypes.ResolveChoice),
        "one pick (sum 3): count gate unmet -> no Confirm lane");
    await ApplyListedToggleAsync(match, P1, hand[1]);

    LegalAction confirm = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    AssertSequence(
        new[] { hand[3], hand[1] },
        ReadSelectedIds(confirm),
        "the Confirm lane carries the partial set in PICK order (d then b)");

    await match.ApplyActionAsync(confirm);
    await match.StepAsync();
    HeadlessChoiceState resolved = match.Context.ChoiceController.Current;
    AssertTrue(resolved.IsResolved && !resolved.IsPending, "listed Confirm resolved the choice (표에 뜬다=실행 가능)");
    AssertSequence(new[] { hand[3], hand[1] }, resolved.SelectedIds, "resolved ids preserve pick order");
    AssertEqual(0, resolved.PendingSelectedIds.Count, "scratchpad cleared by the resolve");
}

// --- per-pick gate (W5: last-제외 평가) -------------------------------------------------------------

async Task PerPickGateAndReplaceLastBasis()
{
    // Gate: candidates 0 (a) and 2 (c) are mutually exclusive — the AS-IS
    // canTargetCondition_ByPreSelecetedList shape. maxCount 2.
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 73, minCount: 0, maxCount: 2, canSkip: true,
        gateFactory: cards => (basis, cand) =>
            !(cand == cards[2] && basis.Contains(cards[0])) &&
            !(cand == cards[0] && basis.Contains(cards[2])));

    HeadlessEntityId a = hand[0], b = hand[1], c = hand[2], d = hand[3];

    string[] ToggleLanes() => match.GetLegalActions(P1)
        .Where(x => x.ActionType == HeadlessActionTypes.ToggleChoiceCandidate)
        .Select(x => ReadCandidateId(x).Value)
        .ToArray();

    AssertSequence(new[] { a.Value, b.Value, c.Value, d.Value }, ToggleLanes(), "empty basis: every candidate gated in");

    await ApplyListedToggleAsync(match, P1, a);
    AssertSequence(new[] { a.Value, b.Value, d.Value }, ToggleLanes(),
        "basis [a]: c fails the gate -> its toggle lane is NOT listed (a stays as the deselect lane)");

    await ApplyListedToggleAsync(match, P1, b);   // picks [a,b] — at MaxCount.
    AssertSequence(new[] { a.Value, b.Value, d.Value }, ToggleLanes(),
        "at max with picks [a,b]: replace-last basis is [a] (last pick b excluded) -> c still gated out");

    // Rebuild as [b,a] so the last pick is a: now the replace-last basis [b] lets c in — the W5 core:
    // the gate is evaluated on the list MINUS the pick that would be replaced (AS-IS :283-289).
    await ApplyListedToggleAsync(match, P1, a);   // untap a -> [b]
    await ApplyListedToggleAsync(match, P1, a);   // tap a  -> [b,a] — at max, last = a
    AssertSequence(new[] { b, a }, match.Context.ChoiceController.Current.PendingSelectedIds, "picks rebuilt as [b,a]");
    AssertSequence(new[] { a.Value, b.Value, c.Value, d.Value }, ToggleLanes(),
        "at max with picks [b,a]: replace-last basis is [b] -> c passes the gate and is listed");

    await ApplyListedToggleAsync(match, P1, c);   // replace-last: [b,c]
    AssertSequence(new[] { b, c }, match.Context.ChoiceController.Current.PendingSelectedIds,
        "the gated-in tap replaced the last pick (AS-IS :303-310)");
}

async Task OffTableToggleRejected()
{
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 79, minCount: 0, maxCount: 2, canSkip: true,
        gateFactory: cards => (basis, cand) => !(cand == cards[2] && basis.Contains(cards[0])));

    await ApplyListedToggleAsync(match, P1, hand[0]);
    IReadOnlyList<HeadlessEntityId> before = match.Context.ChoiceController.Current.PendingSelectedIds;

    // Manufacture the gated-out toggle (candidate c while a is picked) and submit it: the A1 boundary must
    // reject it (it is not in the dispatcher table), leaving the partial state untouched — the apply-time
    // enforcement of the enumeration-time per-pick gate.
    LegalAction offTable = HeadlessActionFactory.ToggleChoiceCandidate(
        P1, hand[2], actionId: $"togglechoice:{P1.Value}:{hand[2].Value}");
    StepResult rejected = await match.ApplyActionAsync(offTable);
    AssertTrue(
        rejected.Events.Any(e => e.Type == GameEventType.InvalidAction),
        "off-table toggle rejected at the legality boundary");
    AssertSequence(before, match.Context.ChoiceController.Current.PendingSelectedIds, "partial state unchanged");
}

// --- Confirm / skip lanes --------------------------------------------------------------------------

async Task ConfirmLaneLifecycle()
{
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 83, minCount: 1, maxCount: 3, canSkip: false);

    AssertEqual(0, match.GetLegalActions(P1).Count(a => a.ActionType == HeadlessActionTypes.ResolveChoice),
        "0 picks < MinCount 1 and no skip: no ResolveChoice lane at all");

    await ApplyListedToggleAsync(match, P1, hand[2]);
    LegalAction confirmOne = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    AssertSequence(new[] { hand[2] }, ReadSelectedIds(confirmOne), "Confirm appears at MinCount, carrying the pick");

    await ApplyListedToggleAsync(match, P1, hand[0]);
    LegalAction confirmTwo = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    AssertSequence(new[] { hand[2], hand[0] }, ReadSelectedIds(confirmTwo), "Confirm tracks the growing set in pick order");

    await match.ApplyActionAsync(confirmTwo);
    await match.StepAsync();
    AssertSequence(new[] { hand[2], hand[0] }, match.Context.ChoiceController.Current.SelectedIds,
        "applying the listed Confirm resolves with exactly that set");
}

async Task SkipLaneOnlyWhenEmpty()
{
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 89, minCount: 0, maxCount: 2, canSkip: true);

    bool HasSkip() => match.GetLegalActions(P1).Any(a =>
        a.ActionType == HeadlessActionTypes.ResolveChoice &&
        a.Parameters.TryGetValue(HeadlessActionParameterKeys.ChoiceSkipped, out object? v) && v is true);

    AssertTrue(HasSkip(), "empty selection: the skip lane (AS-IS back button) is listed");

    await ApplyListedToggleAsync(match, P1, hand[1]);
    AssertTrue(!HasSkip(), "1 pick: the skip lane disappears (핀 2 — 접으려면 먼저 전부 해제)");

    await ApplyListedToggleAsync(match, P1, hand[1]);
    AssertTrue(HasSkip(), "toggled empty again: the skip lane returns");

    LegalAction skip = match.GetLegalActions(P1).Single(a =>
        a.ActionType == HeadlessActionTypes.ResolveChoice &&
        a.Parameters.ContainsKey(HeadlessActionParameterKeys.ChoiceSkipped));
    await match.ApplyActionAsync(skip);
    await match.StepAsync();
    AssertTrue(match.Context.ChoiceController.Current.IsSkipped, "the listed skip resolves as a skip");
}

// --- unsatisfiable forced demotion (§B5.6) ---------------------------------------------------------

async Task UnsatisfiableForcedDemotesAtOpen()
{
    (DcgoMatch match, EngineContext context) = await DeferredMatchAsync(seed: 97);
    HeadlessEntityId[] ids = Candidates(4);

    ChoiceRequest impossible = new(
        ChoiceType.HandCard, P1, "forced impossible pair", minCount: 2, maxCount: 2, canSkip: false,
        ChoiceZone.Hand, ids.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true)).ToArray())
    {
        SelectionValidator = _ => false, // 합계==불가능값 모양: 어떤 조합도 통과 불가
    };

    // OPEN seat: the provider demotes instead of registering — the request that WOULD have stalled the
    // agent surface (pre-flip: pending + empty table) never opens at all, and the effect-side caller
    // receives the AS-IS no-select currency (selected.Count==0 -> noSelect=true downstream).
    ChoiceResult demoted = await context.ChoiceProvider.ChooseAsync(impossible);
    AssertTrue(!demoted.IsSkipped && demoted.SelectedIds.Count == 0,
        "demotion currency = EMPTY Select (not Skip — Validate가 !CanSkip skip을 거부)");
    AssertTrue(!context.ChoiceController.Current.IsPending, "no session/pending choice was opened (progress, not stall)");

    // Observability (리스크 4): the demotion is stamped on the step it drained in — message event + the
    // step's action metadata when an action was processed.
    await match.ApplyActionAsync(HeadlessActionFactory.NoOp(P1));
    StepResult step = await match.StepAsync();
    AssertTrue(
        step.Events.Any(e => e.Message.Contains("Unsatisfiable forced choice demoted", StringComparison.Ordinal)),
        "demotion surfaced in the game-event stream");
    GameEvent processed = step.Events.Single(e => e.Type == GameEventType.ActionProcessed);
    AssertTrue(
        processed.Metadata.TryGetValue(HeadlessActionParameterKeys.UnsatisfiableForcedChoice, out object? marker) && marker is true,
        "unsatisfiableForcedChoice: true stamped on the step's action metadata (D6 수확 채널)");

    // And the drain is once-only: the next step carries no stale marking.
    await match.ApplyActionAsync(HeadlessActionFactory.NoOp(P1));
    StepResult clean = await match.StepAsync();
    AssertTrue(
        clean.Events.All(e => !e.Metadata.ContainsKey(HeadlessActionParameterKeys.UnsatisfiableForcedChoice)),
        "the demotion marking does not leak into later steps");
}

async Task SatisfiableForcedOpensSession()
{
    (DcgoMatch match, EngineContext context) = await DeferredMatchAsync(seed: 101);
    HeadlessEntityId[] ids = Candidates(4);

    ChoiceRequest satisfiable = new(
        ChoiceType.HandCard, P1, "forced pair with a passing combo", minCount: 2, maxCount: 2, canSkip: false,
        ChoiceZone.Hand, ids.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true)).ToArray())
    {
        SelectionValidator = set => set.Count == 2 && set.Contains(ids[1]) && set.Contains(ids[3]),
    };

    // The demotion must NOT fire for a completable request: the provider registers + suspends as before.
    bool suspended = false;
    try { await context.ChoiceProvider.ChooseAsync(satisfiable); }
    catch (DeferredChoicePendingException) { suspended = true; }
    AssertTrue(suspended, "satisfiable forced request suspends (no demotion — 강등은 완성-불가능 판정 시에만)");
    AssertTrue(context.ChoiceController.Current.IsPending, "the choice is pending");
    AssertEqual(4, match.GetLegalActions(P1).Count(a => a.ActionType == HeadlessActionTypes.ToggleChoiceCandidate),
        "and the session is on the table (toggle lanes)");

    StepResult step = await match.StepAsync();
    AssertTrue(
        step.Events.All(e => !e.Message.Contains("Unsatisfiable forced choice demoted", StringComparison.Ordinal)),
        "no demotion event for a satisfiable request");
}

// --- up-to-N optional (W7) -------------------------------------------------------------------------

async Task UpToNOptionalMultiPick()
{
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 103, minCount: 0, maxCount: 3, canSkip: true);

    // Pre-flip: MinCount<=1<MaxCount requests only ever offered size-1 resolutions (§B5.3 실측 갭) — the
    // session lifts that: reach a size-2 selection and confirm it.
    await ApplyListedToggleAsync(match, P1, hand[0]);
    await ApplyListedToggleAsync(match, P1, hand[2]);

    LegalAction confirm = match.GetLegalActions(P1).Single(a =>
        a.ActionType == HeadlessActionTypes.ResolveChoice &&
        !a.Parameters.ContainsKey(HeadlessActionParameterKeys.ChoiceSkipped));
    AssertSequence(new[] { hand[0], hand[2] }, ReadSelectedIds(confirm), "multi-pick Confirm reachable (표현력 결손 상환)");

    await match.ApplyActionAsync(confirm);
    await match.StepAsync();
    AssertEqual(2, match.Context.ChoiceController.Current.SelectedIds.Count, "resolved with BOTH picks (old ceiling was 1)");
}

// --- A1 boundary (W9) ------------------------------------------------------------------------------

async Task OffTableConfirmRejected()
{
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 107, minCount: 2, maxCount: 2, canSkip: false);

    await ApplyListedToggleAsync(match, P1, hand[1]);

    // [hand1, hand3] WOULD pass ChoiceResult.Validate (both selectable, size 2) — but it does not match the
    // current partial set, so no such Confirm is on the table and A1 must reject the synthetic action.
    LegalAction synthetic = HeadlessActionFactory.ResolveChoice(
        P1, ChoiceResult.Select(new[] { hand[1], hand[3] }), actionId: $"resolvechoice:{P1.Value}:confirm");
    StepResult rejected = await match.ApplyActionAsync(synthetic);
    AssertTrue(rejected.Events.Any(e => e.Type == GameEventType.InvalidAction), "off-table Confirm rejected (합성 액션 거부)");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "choice still pending");
    AssertSequence(new[] { hand[1] }, match.Context.ChoiceController.Current.PendingSelectedIds, "toggle state untouched");
}

// --- preserved boundary ----------------------------------------------------------------------------

async Task SingleSelectShapePreserved()
{
    // MaxCount==1 + validator: the pre-flip table shape — one size-1 ResolveChoice per VALIDATOR-PASSING
    // candidate (B1/RD-S3D-01 filter) + the skip lane; no toggle lanes.
    (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoiceMatchAsync(
        seed: 109, minCount: 0, maxCount: 1, canSkip: true,
        validatorFactory: cards => set => set.Count == 1 && set[0] != cards[2]);

    IReadOnlyList<LegalAction> table = match.GetLegalActions(P1);
    AssertEqual(0, table.Count(a => a.ActionType == HeadlessActionTypes.ToggleChoiceCandidate),
        "no session lanes on the preserved boundary");
    LegalAction[] picks = table
        .Where(a => a.ActionType == HeadlessActionTypes.ResolveChoice &&
            !a.Parameters.ContainsKey(HeadlessActionParameterKeys.ChoiceSkipped))
        .ToArray();
    AssertEqual(3, picks.Length, "size-1 lanes for the 3 validator-passing candidates (B1 filter drops hand[2])");
    AssertTrue(picks.All(a => ReadSelectedIds(a).Count == 1), "each lane is a complete size-1 resolution");
    AssertTrue(table.Any(a => a.Parameters.ContainsKey(HeadlessActionParameterKeys.ChoiceSkipped)), "skip lane intact");
}

// --- random-agent full-game smoke + seed replay ----------------------------------------------------

async Task RandomAgentFullGameSmoke()
{
    string first = await RunSessionGameAsync(policySeed: 11);
    string second = await RunSessionGameAsync(policySeed: 11);
    AssertEqual(first, second, "seed-replay: the full trajectory fingerprint (session included) is identical");

    async Task<string> RunSessionGameAsync(int policySeed)
    {
        // (4b B6 re-pin) Phase 2's full-game walk is TURN-FLOW-driven, so this smoke runs on the PUMP:
        // the OLD AdvancePhase micro-step driver is physically retired. The synthetic forced-pair session
        // is staged at P1's first main wait (hand dealt by the pump's own StartGame).
        (DcgoMatch match, HeadlessEntityId[] hand) = await PendingHandChoicePumpMatchAsync(
            seed: 113, minCount: 2, maxCount: 2, canSkip: false,
            validatorFactory: _ => set => set.Count == 2);

        var random = new Random(policySeed);
        var applied = new List<string>();

        // Phase 1 — the random agent must escape the multi-select session (no toggle-loop trap): with the
        // Confirm lane appearing whenever 2 picks are held, a uniform pick over the table terminates fast.
        int sessionSteps = 0;
        while (match.Context.ChoiceController.Current.IsPending)
        {
            AssertTrue(++sessionSteps <= 200, "random agent escaped the toggle session within 200 steps");
            IReadOnlyList<LegalAction> table = match.GetLegalActions(P1);
            AssertTrue(table.Count > 0, "the session table is never empty (탈출 경로 보장 — 핀 3)");
            LegalAction pick = table[random.Next(table.Count)];
            applied.Add(pick.Id.Value);
            await match.ApplyActionAsync(pick);
            await match.StepAsync();
        }

        AssertEqual(2, match.Context.ChoiceController.Current.SelectedIds.Count,
            "the forced pair resolved with two picks (only exit — no skip existed)");

        // Phase 2 — the game continues to a NATURAL end under random legal play (the session did not wedge
        // the match). Pump driver: no lanes between action waits — step the auto-flow forward; generous cap.
        int steps = 0;
        while (!match.IsTerminal() && steps < 4000)
        {
            steps++;
            LegalAction[] actions = match.GetLegalActions(P1).Concat(match.GetLegalActions(P2)).ToArray();
            if (actions.Length == 0)
            {
                await match.StepAsync();
                continue;
            }

            LegalAction pick = actions[random.Next(actions.Length)];
            applied.Add(pick.Id.Value);
            await match.ApplyActionAsync(pick);
            await match.StepAsync();
        }

        AssertTrue(match.IsTerminal(), "full game reached a natural terminal under random play");
        applied.Add($"terminal:{match.GetResult().WinnerId?.Value.ToString() ?? "none"}:{steps}");
        return string.Join("|", applied);
    }
}

// --- W8: real-card EX8_051 <Fragment <3>> (설계 §B5.8 W8, B5-4) ------------------------------------
//
// The ONE live multi-select shape in the current 209-card pool (§B5.3): the <Fragment <3>> keyword
// (Fragment.cs FragmentProcess — SelectCardEffect maxCount:3, canNoSelect:()=>false, canEndNotMax:false
// over permanent.DigivolutionCards) fires on EX8_051's WhenPermanentWouldBeDeleted window and posts a
// FORCED min=max=3 batch Card request. Pre-B5-2 the dispatcher emitted ZERO actions for it (MinCount>1,
// no skip) = agent stall (the B4 campaign never reached this path — §B4.5 #4); post-flip it must open a
// session the agent completes as toggle×3 + Confirm, and the resolved effect must land the AS-IS
// FragmentProcess result: the 3 picked sources trashed (ITrashDigivolutionCards), the unpicked source
// still attached, the top NOT deleted (willBeRemoveField=false — the sweep spares the survivor).
// Firing path (minimal reachable, same as tests/G3.5-F68 FragmentTwoStep): real ported EX8_051 on the
// battle area + 4 attached sources + an effect deletion opening the PRE cut-in window; step 1 activates
// the window's optional Fragment, step 2 is the session under test. G3.5-F68 resolves step 2 by writing
// the controller directly (pre-flip necessity); W8 is the agent-surface completion of the same request.

async Task FragmentRealCardSessionWitness()
{
    (DcgoMatch match, EngineContext ctx) = await RealCardMatchAsync(seed: 127);
    HeadlessEntityId top = await PlaceRealCardAsync(match, ctx, P2, "EX8_051");
    HeadlessEntityId s1 = MakeSource(ctx, P2, "w8-fs1");
    HeadlessEntityId s2 = MakeSource(ctx, P2, "w8-fs2");
    HeadlessEntityId s3 = MakeSource(ctx, P2, "w8-fs3");
    HeadlessEntityId s4 = MakeSource(ctx, P2, "w8-fs4");
    SetCardMetadata(match, top, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.SourceIdsKey] = new[] { s1.Value, s2.Value, s3.Value, s4.Value }
    });

    // FIRE: an effect deletes EX8_051 -> deferred (pendingDeletion) -> the PRE cut-in window parks the
    // printed <Fragment <3>> as the owner's optional replacement choice.
    await DeleteByEffectAsync(match, ctx, top);
    AssertTrue(ReadCardFlag(match, top, GameFlowProcessor.PendingDeletionKey), "deletion deferred behind the window");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "the Fragment window choice is open");
    AssertEqual(P2, match.Context.ChoiceController.PendingRequest!.PlayerId, "the owner decides");

    // Step 1 — activate Fragment through the listed action surface.
    LegalAction activate = match.GetLegalActions(P2)
        .Single(a => a.ActionType == HeadlessActionTypes.ResolveChoice && !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    // Step 2 — the FragmentProcess source pick: the exact §B5.3 stall shape, now a session.
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "the source-pick choice is open");
    ChoiceRequest sourcePick = match.Context.ChoiceController.PendingRequest!;
    AssertEqual(3, sourcePick.MinCount, "Fragment <3>: forced (canNoSelect false, canEndNotMax false) -> min=3");
    AssertEqual(3, sourcePick.MaxCount, "Fragment <3>: maxCount=3");
    AssertTrue(!sourcePick.CanSkip, "Fragment <3>: no skip (forced)");
    AssertEqual(4, sourcePick.Candidates.Count, "candidates = the permanent's 4 digivolution cards");

    IReadOnlyList<LegalAction> table = match.GetLegalActions(P2);
    AssertTrue(table.Count > 0, "red→green: the table that stalled every pre-flip agent is NOT empty");
    AssertEqual(4, table.Count(a => a.ActionType == HeadlessActionTypes.ToggleChoiceCandidate),
        "session open: one toggle lane per source");
    AssertEqual(0, table.Count(a => a.ActionType == HeadlessActionTypes.ResolveChoice),
        "0 picks < min 3 and no skip: no ResolveChoice lane yet");

    // Toggle the first three sources through the validated lanes (pick order s1, s2, s3).
    HeadlessEntityId CandidateOf(HeadlessEntityId source) =>
        sourcePick.Candidates.Single(c => c.Id.Value.Contains(source.Value, StringComparison.Ordinal)).Id;
    await ApplyListedToggleAsync(match, P2, CandidateOf(s1));
    await ApplyListedToggleAsync(match, P2, CandidateOf(s2));
    AssertEqual(0, match.GetLegalActions(P2).Count(a => a.ActionType == HeadlessActionTypes.ResolveChoice),
        "2 picks < min 3: still no Confirm");
    await ApplyListedToggleAsync(match, P2, CandidateOf(s3));

    LegalAction confirm = match.GetLegalActions(P2).Single(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    AssertSequence(
        new[] { CandidateOf(s1), CandidateOf(s2), CandidateOf(s3) },
        ReadSelectedIds(confirm),
        "the Confirm lane carries the 3 picks in pick order");

    await match.ApplyActionAsync(confirm);
    await match.StepAsync();

    // AS-IS FragmentProcess result contrast (Fragment.cs:65-80): the 3 selected sources are trashed
    // (ITrashDigivolutionCards -> mirror TrashSpecificSourcesAsync), the unselected source stays attached,
    // and cardsTrashed -> willBeRemoveField=false so the top is NOT deleted.
    AssertTrue(InZoneOf(match, P2, ChoiceZone.BattleArea, top), "the top survives on the battle area (deletion cancelled)");
    AssertTrue(!InZoneOf(match, P2, ChoiceZone.Trash, top), "the top was NOT trashed");
    AssertTrue(
        InZoneOf(match, P2, ChoiceZone.Trash, s1) && InZoneOf(match, P2, ChoiceZone.Trash, s2) && InZoneOf(match, P2, ChoiceZone.Trash, s3),
        "the 3 picked sources are trashed as the Fragment cost");
    AssertTrue(!InZoneOf(match, P2, ChoiceZone.Trash, s4), "the unpicked source is NOT trashed");
    AssertTrue(AttachedSourceIds(match, top).Contains(s4.Value), "the unpicked source stays attached under the top");
    AssertTrue(!AttachedSourceIds(match, top).Intersect(new[] { s1.Value, s2.Value, s3.Value }).Any(),
        "the trashed sources are detached from the stack");
    AssertTrue(!ReadCardFlag(match, top, "willBeRemoveField"), "willBeRemoveField cleared (AS-IS cardsTrashed branch)");
    AssertTrue(!ReadCardFlag(match, top, GameFlowProcessor.PendingDeletionKey), "pendingDeletion cleared for the survivor");
    AssertTrue(!match.Context.ChoiceController.Current.IsPending, "no dangling choice after the session completed");
}

// W8 fixture helpers (same harness shape as tests/G3.5-F68 — legacy driver + deferredChoice so the PRE
// cut-in window parks as a pending choice; the dispatcher's choice branch is driver-independent).

async Task<(DcgoMatch Match, EngineContext Ctx)> RealCardMatchAsync(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed, deferredChoice: true);
    var cards = (CardDatabase)ctx.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(PlainDigimon($"P1-M{index:D2}"));
        cards.Upsert(PlainDigimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(ctx, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { PlainDeck(P1, "P1"), PlainDeck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup));
    // Drive the pump's natural auto-flow to P1's Main wait; the OLD AdvancePhase step currency is retired
    // (4b B6 gate). Breeding/Mulligan declined; DeferredChoiceProvider mid-resolution is then completed.
    await AdvanceToMainAsync(match, P1);

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advanced to Main");
    (ctx.ChoiceProvider as DeferredChoiceProvider)?.CompleteResolution();
    return (match, ctx);
}

// --- Phase driving (pump auto-flow, F62/C1-Witness precedent, 4b B3 RL re-aim) ---
static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, player));
}

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
            await ResolvePendingDriveAsync(match, skip: decline);
        }
        else await StepOnceDriveAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingDriveAsync(DcgoMatch match, bool skip)
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

static async Task StepOnceDriveAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

async Task<HeadlessEntityId> PlaceRealCardAsync(DcgoMatch match, EngineContext ctx, HeadlessPlayerId owner, string cardNumber)
{
    HeadlessEntityId card = ((IZoneStateReader)ctx.ZoneMover)
        .GetCards(owner, ChoiceZone.Hand).OrderBy(id => id.Value, StringComparer.Ordinal).First();
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"def:{cardNumber}");
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Black" }, ["level"] = 5, ["dp"] = 5000 }, CardType: "Digimon"));
    if (!ctx.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"missing {card}");
    }

    ctx.CardInstanceRepository.Upsert(record with { DefinitionId = defId });
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, card, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetCardMetadata(match, card, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.IsSuspendedKey] = false });
    CardEffectRegistrar.RegisterCard(ctx, card, owner);
    return card;
}

HeadlessEntityId MakeSource(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"def:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Black" }, ["level"] = 4, ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}-{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    return id;
}

async Task DeleteByEffectAsync(DcgoMatch match, EngineContext ctx, HeadlessEntityId cardId)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, log: null, ctx.ZoneMover, memory: null, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("w8-deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value }));
    await sink.FlushAsync();
    await match.StepAsync();
}

void SetCardMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing {cardId}.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) { metadata[pair.Key] = pair.Value; }
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

bool InZoneOf(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadCardFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

string[] AttachedSourceIds(DcgoMatch match, HeadlessEntityId cardId) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(DeletionReplacementGate.SourceIdsKey, out object? raw) && raw is IEnumerable<string> ids
        ? ids.ToArray() : Array.Empty<string>();

static CardRecord PlainDigimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup PlainDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// --- fixtures --------------------------------------------------------------------------------------

async Task<(DcgoMatch Match, HeadlessEntityId[] Hand)> PendingHandChoiceMatchAsync(
    int seed,
    int minCount,
    int maxCount,
    bool canSkip,
    Func<HeadlessEntityId[], Func<IReadOnlyList<HeadlessEntityId>, bool>>? validatorFactory = null,
    Func<HeadlessEntityId[], Func<IReadOnlyList<HeadlessEntityId>, HeadlessEntityId, bool>>? gateFactory = null)
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
        PartialPickGate = gateFactory?.Invoke(candidates),
    };
    context.ChoiceController.RequestChoice(request, new HeadlessEntityId("r4rl03:test-choice"));
    return (match, candidates);
}

// (4b B6) Pump twin of the fixture above for the TURN-FLOW smoke: CreatePumpDriven owns the deal
// (hand 0 until StartGame), so the match is driven to P1's first main wait (mulligan declined —
// deterministic for the seed-replay fingerprint) BEFORE the synthetic forced-pair session is staged.
async Task<(DcgoMatch Match, HeadlessEntityId[] Hand)> PendingHandChoicePumpMatchAsync(
    int seed,
    int minCount,
    int maxCount,
    bool canSkip,
    Func<HeadlessEntityId[], Func<IReadOnlyList<HeadlessEntityId>, bool>>? validatorFactory = null,
    Func<HeadlessEntityId[], Func<IReadOnlyList<HeadlessEntityId>, HeadlessEntityId, bool>>? gateFactory = null)
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

    var match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    await match.InitializeAsync(config);
    await match.StepAsync();
    await AdvanceToMainAsync(match, P1);

    IReadOnlyList<HeadlessEntityId> handZone = ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.Hand);
    AssertTrue(handZone.Count >= 4, "P1 has enough hand cards to choose from");
    HeadlessEntityId[] candidates = handZone.Take(4).ToArray();

    ChoiceRequest request = new(
        ChoiceType.HandCard, P1, "pick cards", minCount, maxCount, canSkip, ChoiceZone.Hand,
        candidates.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true)).ToArray())
    {
        SelectionValidator = validatorFactory?.Invoke(candidates),
        PartialPickGate = gateFactory?.Invoke(candidates),
    };
    context.ChoiceController.RequestChoice(request, new HeadlessEntityId("r4rl03:test-choice"));
    return (match, candidates);
}

async Task<(DcgoMatch Match, EngineContext Context)> DeferredMatchAsync(int seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed, deferredChoice: true);
    var match = new DcgoMatch(context, new EngineTrace());
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed);
    await match.InitializeAsync(config);
    return (match, context);
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

HeadlessEntityId[] Candidates(int count) =>
    Enumerable.Range(0, count).Select(i => new HeadlessEntityId($"cand:{i}")).ToArray();

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
