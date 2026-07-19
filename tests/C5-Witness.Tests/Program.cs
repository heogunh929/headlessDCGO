using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-5 WITNESS CARDS — real-card integration for the security/field battle PRE would-be-deleted window and
// its keyword grants. Unlike tests/C5-SecurityPreWindow.Tests (metadata keyword flags), every keyword here
// comes from the REAL card class discovered by CardEffectDispatch (definition CardNumber = class name) and
// registered through the production enter-play path (RegisterEnteredCardEffects), so these tests witness:
//   BT14_035 — <Barrier> (BarrierSelfEffect at WhenPermanentWouldBeDeleted)
//   BT13_023 — <Evade> + [When Attacking] trash the BOTTOM digivolution card of 1 opponent Digimon
//   EX8_051  — <Fragment <3>> (+Collision/Piercing statics) + trash-source ESS "<De-Digivolve 1>" from TRASH
//   EX8_061  — <Scapegoat> (by-own-effect excluded) + alternate digivolution requirement (DS Lv4, cost 3)
//              + [When Attacking][Once Per Turn] / [On Deletion] trash-plays
//
// ── 수리-2 배치 잔여 red 정밀 마킹 (2026-07-19) ────────────────────────────────────────────────────────
// 재현·전구간 트레이스 결과, "실카드 인터랙티브 창 회귀"라는 기지 진단은 대부분 오진으로 판명됨. 효과 본체는
// 전부 발화하며 올바른 게임상태를 만든다(프로브·R4RL-03 W8 green으로 실증). 잔여 7 red의 근원별 판정:
//   [②군 수리됨] TrashSourceEss_TraitGate — CardSource 빈-controller 크래시(DigivolutionStackHelpers.IsTrashProtected
//     → CardSource.ctor) = 진짜 엔진 버그. RD-BCE-01 id-기반 BareCauseEffect 경로로 상환(CardEffectCommons
//     .IsTrashProtectedSource id-오버로드 신설). → GREEN.
//   [Root A · AS-IS 충실, 위트니스 노후] SelectPermanentEffect 강제선택(!canNoSelect && !canEndNotMax &&
//     pool==maxCount → EndSelect_RPC 직행, 창 미표면; AS-IS DCGO/…/SelectPermanentEffect.cs:366-399 축자):
//     WhenAttacking_TrashBottom / Scapegoat_NestedAllyOnDeletion / TrashSourceEss_DeDigivolve — 단일후보 강제선택이
//     AS-IS에서도 인터랙티브 창을 열지 않음. 게임상태는 정확(프로브 실증). 위트니스는 컷오버 前 per-pick 모델을 인코딩.
//   [Root B · AS-IS 충실, 위트니스 노후] Fragment_FieldBattle — Fragment<3> 소스픽은 min=max=3 다중선택 SESSION
//     (ToggleChoiceCandidate×3 + Confirm; HeadlessLegalActionDispatcher.cs:100/165). 동일 flip을 R4RL-03 W8이 green으로
//     구동. 회귀앵커 c5ff6ede(B5-2 다중선택 디스패처 flip). 위트니스는 per-pick ResolveChoice(retired ApplyFragmentSource)를 인코딩.
//   [Root C · AS-IS 충실, 위트니스 노후] WhenAttacking_TrashPlay_GateAndCap — [When Attacking] isOptional=true라
//     "Will you use…?" 프롬프트가 본체 SelectCard 앞에 선다(EX8_061.cs:72). 위트니스는 프롬프트를 수락하지 않고 곧장 dsPlay를 찾음.
//   [Root D · 문서화된 구조 블록, 강제 금지] Scapegoat_OwnEffect_NoOffer — PRE 컷인이 cardEffect=null로 열려
//     IsByEffect(IsOwnerEffect) by-cause 억제가 발화 불가(MatchStateMutationSink.cs:1270-1281, design item RD-3C2B-02 /
//     RD-C1-CARDEFFECT-IDTHREAD). 노트가 marker/합성 stand-in을 명시적으로 금지하고 live-cardEffect 스레딩에 블록. 회귀앵커 f7356fe7.
//   [Root E · 진짜 갭, 별도 스코프] OnDeletion_TrashPlay — raw MatchStateMutationSink DeleteKind 경로(ApplyDelete가
//     스텝 밖에서 동기 Flush)의 collect-before-removal OnDestroyedAnyone 리액터가 후속 bare StepAsync에 드레인되지 않음
//     (MatchStateMutationSink.cs:1355-1370). 동일 [On Deletion] 기제는 ScapegoatProcess→DestroyPermanentsClass 경로에선
//     정상(Scapegoat nested가 실증). 회귀앵커 8f155d02. 드레인-연결 정밀 규명은 이 배치 규모 초과 → 별도 골 마킹.
// 위트니스 재조준(Root A/B/C)과 Root D/E 상환은 각각 별도 배치 권고(강제 green 회피).
//
// ── 수리-3 배치: 재조준 실행 + Root A/C 판정 부분 반전 (2026-07-19) ─────────────────────────────────────
// 재조준을 실제로 구동하며 전구간 프로브(pending/zone/sourceIds/추가 step drain + CompleteResolution)로 각
// 케이스의 실 게임상태를 확인한 결과, 수리-2의 "효과 본체는 전부 발화" 진단이 3건에서 반증됨:
//   [Root B GREEN] Fragment_FieldBattle — min=max=3 세션(Toggle×3+Confirm)으로 재조준 완료. 3 소스 트래시+생존
//     실증. 2/3 픽 시 Confirm 미표면 음성대조 추가. 메타데이터-소스 트래시 primitive는 여기서 green으로 실증됨.
//   [Root A GREEN] Scapegoat_NestedAllyOnDeletion — 강제선택(단일 ally) 무창 자동 sacrifice로 재조준 완료.
//     holder 생존 + ally 트래시 + nested [On Deletion] Recovery +1(security 0→1) 실증. WhenPermanentWouldBeDeleted
//     교체 Process 경로는 본체를 완주함.
//   [Root A REAL-GAP, 반전] WhenAttacking_TrashBottom — 강제선택 무창은 맞으나 trash-bottom '본체' 미발화.
//     BT13_023 [When Attacking]는 ActivateClass(OnAllyAttack); ActivateCoroutine의 select 후 본체
//     (TrashDigivolutionCardsFromTopOrBottom)가 드레인되지 않아 defender가 두 소스를 유지. withoutTap true/false·
//     추가 step 무관. design item RD-C5W-ACTIVATEBODY.
//   [Root A REAL-GAP, 반전] TrashSourceEss_DeDigivolve — ESS De-Digivolve가 아예 미발화(pending 무·victim 무변),
//     CompleteResolution+6 step에도 불변. trash-resident unregistered-source dispatch 스캔의 리액터 미드레인 =
//     Root E와 동일 계열. design item RD-C5W-ESSTRASHSCAN.
//   [Root C REAL-GAP, 반전] WhenAttacking_TrashPlay_GateAndCap — 프롬프트+본체 select 표면은 정상 재조준(수락→
//     본체창→dsPlay 픽 수용)되나 select 후 PlayPermanentCards 본체가 미드레인(dsPlay가 trash 잔류). RD-C5W-ACTIVATEBODY.
// 공통 근원 가설: ActivateClass 트리거 효과(SetUpActivateClass+ActivateCoroutine; [When Attacking]/[On Deletion]/
// ESS)의 select-이후 본체 tail이 이 통합 하네스(raw AttackDeclarationCommons.Declare + bare StepAsync + deferredChoice)
// 에서 드레인되지 않음. 교체-Process 경로(Scapegoat/Fragment)는 정상. RD-C5W-ACTIVATEBODY / RD-C5W-ESSTRASHSCAN =
// 별도 구조골 스코프. 이 3건은 truthful RED로 유지(강제 green 금지, 수리 규율 rule 2/3).
//
// ── 수리 원장 배치2: 위 "공통 근원 가설(select-이후 본체 tail 미드레인)"이 전면 반증됨 (2026-07-19) ──────────
// 프로덕션 노출 판정 + 전구간 엔진 프로브(StackSkillInfos/GetSkillInfos/TriggeredSkillProcess/RunPickBody/
// CanActivate 계측) 결과: "본체 tail 미드레인" 가설은 4건 중 0건에서 성립. 재분류 —
//   [위트니스 오조준·엔진 AS-IS 정합] WhenAttacking_TrashBottom (BT13_023) / OnDeletion_TrashPlay (EX8_061):
//     둘 다 INHERITED 효과(SetIsInheritedEffect(true))인데 위트니스가 카드를 TOP에 배치. AS-IS
//     Permanent.EffectList_ForCard(Permanent.cs:2126-2145)는 계승효과를 SOURCE일 때만(`!isTopCard`) 수집 —
//     top-카드 자기 계승효과는 결코 수집 안 함(대조: MAIN 효과인 BT2_034 [On Deletion]은 top으로 발화=green).
//     프로브로 카드를 SOURCE로 재배치 시 동일 raw 하네스에서 본체 완주 실증. → 위트니스 재조준(둘 다 GREEN).
//     드레인 갭 아님, 엔진 정합.
//   [진짜 엔진 갭·별도 구조골] TrashSourceEss_DeDigivolve (RD-C5W-ESSTRASHSCAN 재분류): 본체 미드레인이 아니라
//     OnDigivolutionCardDiscarded 트리거 창 자체가 미개방(sink trash helper가 raw 이벤트만 emit; SkillWindowSupply
//     RDW-02 드롭; AS-IS ITrashDigivolutionCards:5215 인라인 StackSkillInfos 미이식). 미배선 timing = 트리거-수집
//     구조골. 본체창 마킹 참조.
//   [진짜 엔진 갭·별도 구조골] WhenAttacking_TrashPlay_GateAndCap (RD-C5W-ACTIVATEBODY 재분류): 본체 드레인이 아니라
//     [Once Per Turn] 캡이 deferred-choice REPLAY-resume에서 재게이트(RunPickBody + ActivateEffectProcess:1560의
//     CanActivate 재검사가 register-before-body로 이미 등록된 use를 isOverMaxCountPerTurn로 읽어 false). AS-IS는
//     단일 코루틴으로 CanActivate 1회 검사 후 재검사 없이 continue. 증명: UNCAPPED [On Deletion] 재조준은 동일
//     interactive-select resume로 green. 수리=OLD RegisterUseEffectThisTurn 캡을 suspend/resume-cycle-aware화(코어
//     resume 아키텍처) = 배치 초과. 본체창 마킹 참조.
// 결론: #1/#2 = 위트니스 재조준 GREEN; #3/#4 = 진짜 갭이나 각각 "미배선 timing"·"코어 resume 캡 아키텍처"로 배치
// 초과 → 정밀 재마킹 후 truthful RED 유지·STOP. Root D(RD-C1-CARDEFFECT-IDTHREAD)는 별도 배치 유지.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT14_035: registered <Barrier> grant survives a security battle loss (top own security paid)", Barrier_SecurityBattle),
    ("BT13_023: registered <Evade> grant suspends+survives a security battle loss and the check resumes", Evade_SecurityBattle_Resume),
    ("BT13_023: [When Attacking] trashes the BOTTOM digivolution card of the chosen opponent Digimon", WhenAttacking_TrashBottom),
    ("EX8_051: <Fragment <3>> pays exactly 3 sources through the PRE window and survives a field battle", Fragment_FieldBattle),
    ("EX8_051: only 2 sources -> Fragment unaffordable, NO offer, deleted outright", Fragment_Insufficient_NoOffer),
    ("EX8_051: trash-source ESS — effect-trashed from a [Mineral] host, De-Digivolves 1 opponent Digimon from the TRASH", TrashSourceEss_DeDigivolve),
    ("EX8_051: trash-source ESS does NOT fire off a non-[Mineral]/[Rock] host (trait gate)", TrashSourceEss_TraitGate),
    ("EX8_061: <Scapegoat> sacrifice routes the ally through the delete pipeline — the ally's [On Deletion] fires (nested)", Scapegoat_NestedAllyOnDeletion),
    ("EX8_061: <Scapegoat> is NOT offered when the deletion is by the owner's OWN effect", Scapegoat_OwnEffect_NoOffer),
    ("EX8_061: [When Attacking] memory>=1 gates the trash-play; [Once Per Turn] caps the second attack", WhenAttacking_TrashPlay_GateAndCap),
    ("EX8_061: [On Deletion] plays a matching Digimon from the trash after the real deletion", OnDeletion_TrashPlay),
    ("EX8_061: alternate digivolution (DS Lv4, cost 3) is read dispatch-first off the HAND card", AlternateDigivolution),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- BT14_035 <Barrier> ---------------------------------------------------

async Task Barrier_SecurityBattle()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId attacker = await PlaceWitness(match, P1, "BT14_035", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 3000, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 2 });
    HeadlessEntityId ownSecurity = await Place(match, P1, "PLAIN", "p1sec", ChoiceZone.Security);
    HeadlessEntityId sec1 = await Place(match, P2, "PLAIN", "p2sec1", ChoiceZone.Security, new Dictionary<string, object?> { ["dp"] = 9000 });
    HeadlessEntityId sec2 = await Place(match, P2, "PLAIN", "p2sec2", ChoiceZone.Security, new Dictionary<string, object?> { ["dp"] = 1000 });
    // Security stacks like AS-IS reveal top-first: last added is on top -> reorder so 9000 is checked FIRST.
    // (ZoneMover appends; SecurityResolver reveals index 0.) sec1 was added first (index 0) -> already first.

    AttackDeclarationCommons.Declare(match.Context, P1, attacker, P2, targetId: null, isDirectAttack: true);
    await match.StepAsync();   // security battle loss -> PRE window (Barrier via the REGISTERED grant)

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE would-be-deleted window is open");
    // (수리-2 re-aim) The torn-down invented "#barrier" gate id is gone; the current PRE would-be-deleted window
    // surfaces the replacement as an OptionalEffect candidate keyed by the holder's own instance id (the
    // Candidates[0].Id convention witnessed green by C-Del-3C1C). The rule assertions below (PRE window open →
    // printed <Barrier> fires → attacker survives, own top security paid, loop resumes) are all preserved.
    LegalAction activate = AcceptWindow(match, P1, attacker);
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertInZone(match, P1, ChoiceZone.BattleArea, attacker, "Barrier attacker survives the security battle");
    // (수리-2 re-aim) The invented DeletionReplacementGate.BarrieredKey marker is dead — it is never stamped by the
    // current SecurityResolver PRE cut-in window (the retired gate auto-path was the sole writer). Survival + own
    // top security paid + the loop resuming ARE the rule witness that the printed <Barrier> replacement fired.
    AssertInZone(match, P1, ChoiceZone.Trash, ownSecurity, "the attacker's own top security was paid");
    AssertInZone(match, P2, ChoiceZone.Trash, sec2, "the SECOND security card was checked (loop resumed)");
    AssertAttackEnded(match, "attack completed after the resumed check");
    _ = sec1;
}

// --- BT13_023 <Evade> + [When Attacking] ----------------------------------

async Task Evade_SecurityBattle_Resume()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId attacker = await PlaceWitness(match, P1, "BT13_023", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 3000, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 2 });
    HeadlessEntityId sec1 = await Place(match, P2, "PLAIN", "p2sec1", ChoiceZone.Security, new Dictionary<string, object?> { ["dp"] = 9000 });
    HeadlessEntityId sec2 = await Place(match, P2, "PLAIN", "p2sec2", ChoiceZone.Security, new Dictionary<string, object?> { ["dp"] = 1000 });

    // NOTE: the [When Attacking] trigger is collected at declare (canUse passes) but its canActivate half is
    // false — no opponent battle-area Digimon — so the window exhausts without a choice and the attack proceeds.
    // (MIG1) the mirror Attack() now faithfully SUSPENDS the attacker at declaration (AS-IS :158-167); Evade's
    // suspend cost needs an unsuspended attacker, so declare withoutTap (the Vortex-shape untapped attack).
    AttackDeclarationCommons.Declare(match.Context, P1, attacker, P2, targetId: null, isDirectAttack: true, withoutTap: true);
    await match.StepAsync();

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE would-be-deleted window is open");
    // (수리-2 re-aim) accept the OptionalEffect PRE window keyed by the holder's instance id (was the torn-down
    // "#evade" gate id); the rule assertions (survives, suspended as the Evade cost, loop resumes) are preserved.
    LegalAction activate = AcceptWindow(match, P1, attacker);
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertInZone(match, P1, ChoiceZone.BattleArea, attacker, "Evade attacker survives the security battle");
    // (수리-2 re-aim) IsSuspendedKey ("isSuspended") is a REAL state key (the Evade cost) and is preserved; the
    // invented DeletionReplacementGate.EvadedKey marker is dead (never stamped by the current window) and dropped.
    AssertTrue(ReadFlag(match, attacker, DeletionReplacementGate.IsSuspendedKey), "suspended as the Evade cost");
    AssertInZone(match, P2, ChoiceZone.Trash, sec2, "the SECOND security card was checked (loop resumed)");
    AssertAttackEnded(match, "attack completed after the resumed check");
    _ = sec1;
}

async Task WhenAttacking_TrashBottom()
{
    // ── 수리-3(배치2) RE-AIM (reverses the 수리-3 "RD-C5W-ACTIVATEBODY drain gap" verdict) ──────────────
    // The prior fixture placed BT13_023 as the TOP attacker and expected its [When Attacking] to fire. That is
    // AS-IS-IMPOSSIBLE: BT13_023's [When Attacking] is an INHERITED effect (BT13_023.cs:39 SetIsInheritedEffect
    // (true) — AS-IS BT13_023.cs:23), and AS-IS `Permanent.EffectList_ForCard` (Permanent.cs:2126-2145) collects
    // a card's inherited effects ONLY when it is a digivolution SOURCE (`IsInheritedEffect && !isTopCard`); a
    // TOP card's own inherited effect is never surfaced. So GetSkillInfos(OnAllyAttack) legitimately returned
    // ZERO for the old fixture (traced: `StackSkillInfos OnAllyAttack collected=0`) — no drain gap, the engine
    // is AS-IS-correct. Re-aim so BT13_023 is a SOURCE under an ordinary attacker: the inherited [When Attacking]
    // is then collected, the forced single-candidate SelectPermanentEffect (pool==maxCount, no window,
    // SelectPermanentEffect.cs:531) auto-selects the defender, and the body (TrashDigivolutionCardsFromTopOrBottom,
    // BT13_023.cs:108-115) runs to completion IN THIS SAME raw harness — proven by probe. The defender SURVIVES
    // (9000 vs the attacker's 500), so the trashed BOTTOM source can only be the [When Attacking] bottom-trash.
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId attacker = await Place(match, P1, "PLAIN", "p1atk", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 500, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 1 });
    // BT13_023 as a digivolution SOURCE of the attacker (never top) — the AS-IS home of an inherited effect.
    HeadlessEntityId inheritedSrc = await PlaceWitness(match, P1, "BT13_023", ChoiceZone.None);
    SetMetadata(match, attacker, new Dictionary<string, object?> { ["sourceIds"] = new[] { inheritedSrc.Value } });
    HeadlessEntityId defender = await Place(match, P2, "PLAIN", "p2def", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 9000, ["isSuspended"] = true });
    HeadlessEntityId srcTop = await Place(match, P2, "SRC", "p2src0", ChoiceZone.None);
    HeadlessEntityId srcBottom = await Place(match, P2, "SRC", "p2src1", ChoiceZone.None);
    SetMetadata(match, defender, new Dictionary<string, object?> { ["sourceIds"] = new[] { srcTop.Value, srcBottom.Value } });

    AttackDeclarationCommons.Declare(match.Context, P1, attacker, P2, targetId: defender, isDirectAttack: false);
    await match.StepAsync();   // [When Attacking] forced single-candidate select auto-trashes the bottom source.

    AssertInZone(match, P2, ChoiceZone.Trash, srcBottom, "the BOTTOM digivolution card was trashed");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, srcTop), "the TOP digivolution card stays (bottom-only)");
    AssertInZone(match, P2, ChoiceZone.BattleArea, defender, "the defender survived the battle");
}

// --- EX8_051 <Fragment <3>> + trash-source ESS -----------------------------

async Task Fragment_FieldBattle()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId attacker = await PlaceWitness(match, P1, "EX8_051", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 3000, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 1 });
    HeadlessEntityId defender = await Place(match, P2, "PLAIN", "p2def", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 9000, ["isSuspended"] = true });
    var sources = new List<HeadlessEntityId>();
    for (int i = 0; i < 3; i++)
    {
        sources.Add(await Place(match, P1, "SRC", $"p1src{i}", ChoiceZone.None));
    }

    SetMetadata(match, attacker, new Dictionary<string, object?> { ["sourceIds"] = sources.Select(s => s.Value).ToArray() });

    AttackDeclarationCommons.Declare(match.Context, P1, attacker, P2, targetId: defender, isDirectAttack: false);
    await match.StepAsync();   // field battle loss -> PRE window (Fragment via the REGISTERED grant)

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE window is open");
    // (수리-2 re-aim) accept the OptionalEffect PRE window keyed by the holder's instance id (was the torn-down
    // "#fragment" gate id); the per-pick source payment and survival rule assertions below are preserved.
    LegalAction activate = AcceptWindow(match, P1, attacker);
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    // (수리-3 re-aim) Fragment <3> source payment is a min=max=3 multi-select SESSION (FragmentProcess sets up the
    // AS-IS forced select — canNoSelect/canEndNotMax both false), surfaced as one ToggleChoiceCandidate lane per
    // source + a Confirm ResolveChoice that lights only once all 3 are picked (HeadlessLegalActionDispatcher.cs:
    // 100/165). This is the identical flip R4RL-03 W8 drives green; the retired per-pick ApplyFragmentSource model
    // is dropped. The 3-source payment + survival rule assertions below are preserved.
    ChoiceRequest sourcePick = match.Context.ChoiceController.PendingRequest!;
    AssertEqual(3, sourcePick.MinCount, "Fragment <3>: forced -> min=3");
    AssertEqual(3, sourcePick.MaxCount, "Fragment <3>: max=3");
    AssertFalse(sourcePick.CanSkip, "Fragment <3>: no skip (forced)");
    await ApplyToggle(match, P1, sources[0]);
    await ApplyToggle(match, P1, sources[1]);
    // (음성 대조 / false-green audit) With only 2 of the 3 forced sources picked the Confirm lane must NOT be
    // listed — a partial payment can never confirm. This proves the session's green is the FULL 3-source payment,
    // not a "Confirm always available" artifact.
    AssertFalse(ResolveActions(match, P1).Any(a => a.Id.Value.EndsWith(":confirm", StringComparison.Ordinal)),
        "2 of 3 picked (< min 3): no Confirm lane yet");
    await ApplyToggle(match, P1, sources[2]);
    LegalAction confirm = ResolveActions(match, P1).Single(a => a.Id.Value.EndsWith(":confirm", StringComparison.Ordinal));
    await match.ApplyActionAsync(confirm);
    await match.StepAsync();

    AssertInZone(match, P1, ChoiceZone.BattleArea, attacker, "Fragment attacker survives the field battle");
    // (The PRE-window fragment path pays per-pick via ApplyFragmentSource — it clears the pending deletion
    // rather than stamping the legacy auto-path 'fragmented' marker; the stack must be fully paid out.)
    AssertFalse(ReadFlag(match, attacker, GameFlowProcessor.PendingDeletionKey), "pending deletion cleared");
    foreach (HeadlessEntityId source in sources)
    {
        AssertInZone(match, P1, ChoiceZone.Trash, source, $"source {source.Value} paid to the trash");
    }

    AssertInZone(match, P2, ChoiceZone.BattleArea, defender, "the 9000 defender survived the battle");
}

async Task Fragment_Insufficient_NoOffer()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId attacker = await PlaceWitness(match, P1, "EX8_051", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 3000, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 1 });
    HeadlessEntityId defender = await Place(match, P2, "PLAIN", "p2def", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 9000, ["isSuspended"] = true });
    HeadlessEntityId s0 = await Place(match, P1, "SRC", "p1src0", ChoiceZone.None);
    HeadlessEntityId s1 = await Place(match, P1, "SRC", "p1src1", ChoiceZone.None);
    SetMetadata(match, attacker, new Dictionary<string, object?> { ["sourceIds"] = new[] { s0.Value, s1.Value } });

    AttackDeclarationCommons.Declare(match.Context, P1, attacker, P2, targetId: defender, isDirectAttack: false);
    await match.StepAsync();

    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no PRE window — Fragment <3> is unaffordable with 2 sources");
    AssertInZone(match, P1, ChoiceZone.Trash, attacker, "attacker deleted outright");
}

async Task TrashSourceEss_DeDigivolve()
{
    DcgoMatch match = await DriveTrashSourceEss(hostDefinition: "MINERAL");

    // ── 수리-3(배치2) REGURGE — RD-C5W-ESSTRASHSCAN reclassified: the ESS body does not "fail to drain", the
    //    OnDigivolutionCardDiscarded trigger WINDOW is never OPENED (larger structural, unwired timing) ─────────
    // Root cause (traced): DriveTrashSourceEss trashes EX8_051 off a [Mineral] host via the sink's
    // TrashDigivolutionCardsKind -> DigivolutionStackHelpers.RemoveSourcesAsync, which EMITS the raw
    // OnDigivolutionCardDiscarded event (DigivolutionStackHelpers.cs:452) but NEVER opens the window:
    // SkillWindowSupply drops that timing as UNHANDLED (SkillWindowSupply.cs:73-84, RDW-02 — the AS-IS inline
    // hashtable {CardEffect, Permanent, DiscardedCards} is not event-reconstructable), and unlike AS-IS
    // ITrashDigivolutionCards.TrashDigivolutionCards — which opens it INLINE via StackSkillInfos(…,
    // OnDigivolutionCardDiscarded) at CardController.cs:5215 — the mirror trash helper adds NO inline
    // StackSkillInfos. So GetSkillInfos(OnDigivolutionCardDiscarded) is never even reached and the trash-resident
    // ESS is never collected (traced: no OnDigivolutionCardDiscarded StackSkillInfos anywhere; the ActivateClass
    // Activate never runs). This is NOT the "ActivateClass-body drain" of the 수리-3 note — the body drains fine
    // once collected (proven by the BT13_023 / EX8_061 re-aims). Wiring the whole timing (an inline insert at the
    // sink's effect-trash seat that builds the AS-IS hashtable, AND supplying a non-null {CardEffect} for the ESS
    // CanUse's `cardEffect => cardEffect != null` gate — the RD-C1-CARDEFFECT-IDTHREAD live-effect residual) is a
    // trigger-collection structural goal beyond this repair batch. design item RD-C5W-ESSTRASHSCAN (RECLASSIFIED:
    // unwired OnDigivolutionCardDiscarded window, not a body drain). Truthful RED; NOT forced green (수리 규율 rule 2/3).
    // Assertions below are the preserved De-Digivolve rule witness (victim's top card → trash, under-source promoted).
    HeadlessEntityId victim = new("wit:victim");
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "forced single-victim De-Digivolve: no per-pick window");
    AssertInZone(match, P1, ChoiceZone.Trash, victim, "the victim's top card was de-digivolved to the trash");
    AssertInZone(match, P1, ChoiceZone.BattleArea, new HeadlessEntityId("wit:vicsrc"), "the victim's under-source was promoted");
}

async Task TrashSourceEss_TraitGate()
{
    DcgoMatch match = await DriveTrashSourceEss(hostDefinition: "PLAIN");   // no [Mineral]/[Rock] trait
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no ESS window — the host lacks the [Mineral]/[Rock] trait");
    AssertInZone(match, P1, ChoiceZone.BattleArea, new HeadlessEntityId("wit:victim"), "the victim is untouched");
}

// Shared driver: P2 host (hostDefinition) has EX8_051 as its digivolution source; an effect trashes that
// source (emitting OnDigivolutionCardDiscarded), and P1 has a De-Digivolve-able Digimon on the field.
async Task<DcgoMatch> DriveTrashSourceEss(string hostDefinition)
{
    DcgoMatch match = await CreateMatchAsync();
    EngineContext context = match.Context;
    HeadlessEntityId host = await Place(match, P2, hostDefinition, "host", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    // EX8_051 as a digivolution source of the host — NOT registered (sources never are); the ESS is found
    // by the dispatch-based bridge scan once the card reaches the trash.
    HeadlessEntityId ess = await Place(match, P2, "EX8_051", "ess", ChoiceZone.None);
    SetMetadata(match, host, new Dictionary<string, object?> { ["sourceIds"] = new[] { ess.Value } });

    HeadlessEntityId victim = await Place(match, P1, "PLAIN", "victim", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    HeadlessEntityId victimSource = await Place(match, P1, "SRC", "vicsrc", ChoiceZone.None);
    SetMetadata(match, victim, new Dictionary<string, object?> { ["sourceIds"] = new[] { victimSource.Value } });

    // An effect trashes the host's digivolution source (AS-IS ITrashDigivolutionCards — fires the
    // OnDigivolutionCardDiscarded window before the physical move).
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, log: null, context.ZoneMover, context.MemoryController,
        context.EffectRegistry, context.GameEventQueue, context: context);
    sink.Apply(new EffectMutation(
        MatchStateMutationSink.TrashDigivolutionCardsKind,
        new HeadlessEntityId("wit:cause"),
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = host.Value,
            [MatchStateMutationSink.CountKey] = 1,
            [MatchStateMutationSink.FromBottomKey] = true,
        }));
    await sink.FlushAsync();
    await match.StepAsync();

    AssertInZone(match, P2, ChoiceZone.Trash, ess, "EX8_051 was trashed from the host's digivolution cards");
    return match;
}

// --- EX8_061 <Scapegoat> + trash-plays + alternate digivolution ------------

async Task Scapegoat_NestedAllyOnDeletion()
{
    DcgoMatch match = await CreateMatchAsync();
    EngineContext context = match.Context;
    HeadlessEntityId holder = await PlaceWitness(match, P2, "EX8_061", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    // The sacrifice candidate is a REAL [On Deletion] card (BT2_034: security <= 3 -> Recovery +1), so the
    // nested window is witnessed by an observable state change (P2 security 0 -> 1).
    HeadlessEntityId ally = await PlaceWitness(match, P2, "BT2_034", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    HeadlessEntityId deleter = await Place(match, P1, "PLAIN", "deleter", ChoiceZone.BattleArea);

    ApplyDelete(match, source: deleter, target: holder);
    await match.StepAsync();   // PRE window (scapegoat, via the REGISTERED grant)

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE window is open (opponent-effect deletion)");
    // (수리-2 re-aim) accept the OptionalEffect PRE window keyed by the holder's instance id (was the torn-down
    // "#scapegoat" gate id); the ally-sacrifice pick and nested [On Deletion] rule assertions below are preserved.
    LegalAction activate = AcceptWindow(match, P2, holder);
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // forced single-candidate sacrifice: the lone ally is auto-selected and dies
                               // through the FULL delete pipeline -> its nested [On Deletion] fires.

    // (수리-3 re-aim) <Scapegoat> "by deleting 1 of your OTHER Digimon" targets the holder's only other Digimon
    // (the ally); with pool==maxCount==1 AS-IS forced-select auto-sacrifices it without surfacing a per-pick window
    // (SelectPermanentEffect.cs:531). The rule assertions — holder survives, the chosen ally is sacrificed, and the
    // sacrificed ally's nested [On Deletion] Recovery +1 fires — are preserved and read the forced-select outcome
    // directly. (The retired per-pick "pick the ally" ResolveChoice window is dropped.)
    AssertInZone(match, P2, ChoiceZone.BattleArea, holder, "the Scapegoat holder survives");
    AssertInZone(match, P2, ChoiceZone.Trash, ally, "the chosen ally was sacrificed");
    AssertEqual(1, SecurityCount(match, P2), "the sacrificed ally's [On Deletion] Recovery +1 fired (nested window)");
    _ = context;
}

async Task Scapegoat_OwnEffect_NoOffer()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId holder = await PlaceWitness(match, P2, "EX8_061", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    HeadlessEntityId ally = await Place(match, P2, "PLAIN", "ally", ChoiceZone.BattleArea);
    HeadlessEntityId ownDeleter = await Place(match, P2, "PLAIN", "owndeleter", ChoiceZone.BattleArea);

    // Deleted by the owner's OWN effect: AS-IS Scapegoat's by-cause clause (Scapegoat.cs:65-73
    // IsByEffect(IsOwnerEffect) -> no trigger) suppresses the offer entirely.
    ApplyDelete(match, source: ownDeleter, target: holder);
    await match.StepAsync();

    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no Scapegoat offer for an own-effect deletion");
    AssertInZone(match, P2, ChoiceZone.Trash, holder, "the holder was deleted outright");
    AssertInZone(match, P2, ChoiceZone.BattleArea, ally, "the ally was NOT sacrificed");
}

async Task WhenAttacking_TrashPlay_GateAndCap()
{
    DcgoMatch match = await CreateMatchAsync();
    EngineContext context = match.Context;
    HeadlessEntityId attacker = await PlaceWitness(match, P1, "EX8_061", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 9000, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 1 });
    HeadlessEntityId d1 = await Place(match, P2, "PLAIN", "p2d1", ChoiceZone.BattleArea, new Dictionary<string, object?> { ["dp"] = 1000, ["isSuspended"] = true });
    HeadlessEntityId d2 = await Place(match, P2, "PLAIN", "p2d2", ChoiceZone.BattleArea, new Dictionary<string, object?> { ["dp"] = 1000, ["isSuspended"] = true });
    HeadlessEntityId d3 = await Place(match, P2, "PLAIN", "p2d3", ChoiceZone.BattleArea, new Dictionary<string, object?> { ["dp"] = 1000, ["isSuspended"] = true });
    HeadlessEntityId dsPlay = await Place(match, P1, "DSPLAY", "ds1", ChoiceZone.Trash);
    HeadlessEntityId dsPlay2 = await Place(match, P1, "DSPLAY", "ds2", ChoiceZone.Trash);
    HeadlessEntityId noMatch = await Place(match, P1, "NOMATCH", "nm", ChoiceZone.Trash);

    // (1) memory 0 -> the "1 or more memory" gate fails: no window, the attack just resolves.
    context.MemoryController.Set(0);
    AttackDeclarationCommons.Declare(context, P1, attacker, P2, targetId: d1, isDirectAttack: false);
    await match.StepAsync();
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "memory 0: no trash-play window");
    AssertInZone(match, P1, ChoiceZone.Trash, dsPlay, "memory 0: nothing was played");

    // (2) memory 1 -> the optional [When Attacking] fires. (수리-3 re-aim) Because isOptional=true (EX8_061.cs:72)
    //     the FIRST window is the "Will you use…?" prompt, an OptionalEffect keyed by the holder's own instance id;
    //     ACCEPT it and the body SelectCard (trash-play) window then opens, with only trait+level matching cards as
    //     candidates. (The retired surface skipped the optional prompt and looked straight for dsPlay.)
    SetMetadata(match, attacker, new Dictionary<string, object?> { ["isSuspended"] = false });
    context.MemoryController.Set(1);
    AttackDeclarationCommons.Declare(context, P1, attacker, P2, targetId: d2, isDirectAttack: false);
    await match.StepAsync();
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "memory 1: the optional [When Attacking] prompt is open");
    LegalAction accept = AcceptWindow(match, P1, attacker);
    await match.ApplyActionAsync(accept);
    await match.StepAsync();
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "after accepting the prompt: the trash-play select is open");
    AssertFalse(ResolveActions(match, P1).Any(a => a.Id.Value.Contains(noMatch.Value, StringComparison.Ordinal)),
        "a non-[DS/Mollusk/Crustacean] trash card is NOT a candidate");
    LegalAction pick = ResolveActions(match, P1).Single(a => a.Id.Value.Contains(dsPlay.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();
    // ── 수리-3(배치2) REGURGE — RD-C5W-ACTIVATEBODY reclassified: NOT a generic "post-select body drain", it is
    //    a [Once Per Turn] cap re-gate on the deferred-choice REPLAY-resume (core resume-architecture) ──────────
    // The prompt+select surface ABOVE drives clean (optional prompt → accept → body SelectCard opens with only
    // trait/level-matching candidates, noMatch excluded → dsPlay pick accepted). Root cause of the stranded play
    // (traced): EX8_061's [When Attacking] is [Once Per Turn] (SetUpActivateClass(…, maxCountPerTurn:1, …),
    // EX8_061.cs:72). Its once-use is registered "register-before-body" (Activate_Execute, ICardEffect.cs:1136,
    // AS-IS :1116-1126) on the ACCEPT step — BEFORE the interactive SelectCard suspends. The mirror resume model
    // REPLAYS the whole body via RunPickBodyAsync(freshPick:false); that replay RE-CHECKS CanActivate at TWO seats
    // (MultipleSkills.RunPickBodyAsync + AutoProcessing.ActivateEffectProcess:1560), and CanActivate now returns
    // false because isOverMaxCountPerTurn (ICardEffect.cs:433) reads the already-registered use — so the body is
    // skipped and PlayPermanentCards never runs (probe: on the pick step RunPickBody freshPick=False
    // CanActivate=False; dsPlay stays in Trash). AS-IS is a SINGLE coroutine that checks CanActivate ONCE
    // (MultipleSkills.cs:366) and CONTINUES on resume without re-checking. Proof this is the [Once Per Turn]
    // re-gate and NOT a body-drain: the interactive-select resume completes fine for the UNCAPPED [On Deletion]
    // (OnDeletion_TrashPlay, now GREEN). Faithful repair = make the OLD RegisterUseEffectThisTurn/
    // isOverMaxCountPerTurn per-turn cap suspend/resume-cycle-aware (like the OnceFlags uniform cycle,
    // ActivatedEffectResolver.cs:508) OR bypass the CanActivate re-gate at every replay seat — a core resume
    // change affecting every [Once Per Turn] optional/interactive effect, beyond this repair batch. design item
    // RD-C5W-ACTIVATEBODY (RECLASSIFIED: once-per-turn resume re-gate, not a body drain). Truthful RED; NOT forced
    // green (수리 규율 rule 2/3).
    AssertInZone(match, P1, ChoiceZone.BattleArea, dsPlay, "the selected [DS] Digimon was played from the trash");

    // (3) same turn, third attack -> [Once Per Turn] (capHash PlayDigimon_EX8_061) suppresses the window.
    SetMetadata(match, attacker, new Dictionary<string, object?> { ["isSuspended"] = false });
    context.MemoryController.Set(1);
    AttackDeclarationCommons.Declare(context, P1, attacker, P2, targetId: d3, isDirectAttack: false);
    await match.StepAsync();
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "capped: no second trash-play this turn");
    AssertInZone(match, P1, ChoiceZone.Trash, dsPlay2, "capped: the second [DS] card stays in the trash");
    _ = d1;
}

async Task OnDeletion_TrashPlay()
{
    // ── 수리-3(배치2) RE-AIM (reverses the marking's "Root E collect-before-removal drain gap" verdict) ──
    // EX8_061's [On Deletion] is an INHERITED effect (EX8_061.cs:161 SetIsInheritedEffect(true) — AS-IS :144),
    // so, exactly like BT13_023's [When Attacking] above, it is AS-IS-only collected while EX8_061 is a
    // digivolution SOURCE (Permanent.EffectList_ForCard, Permanent.cs:2126-2145 `IsInheritedEffect && !isTopCard`).
    // The prior fixture deleted EX8_061 AS THE TOP and expected its own inherited [On Deletion] to fire — which
    // AS-IS never collects (traced: no OnDestroyedAnyone window for it), so it was not a drain gap. Contrast the
    // GREEN Scapegoat_NestedAllyOnDeletion, where BT2_034's [On Deletion] is a MAIN effect (no SetIsInheritedEffect,
    // BT2_034.cs:29-34) and so fires as the deleted TOP. Re-aim: EX8_061 is a SOURCE under an ordinary top holder;
    // deleting the holder trashes the whole stack, the [On Deletion] is collected (source, !isTopCard), fires its
    // optional prompt + interactive trash-play select, and plays the level-4 [DS] from trash — proven by probe in
    // this same raw ApplyDelete + bare-StepAsync harness (the "collect-before-removal" deletion drain works).
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId holder = await Place(match, P2, "PLAIN", "holder", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    // EX8_061 as a digivolution SOURCE of the holder (never top) — the AS-IS home of its inherited [On Deletion].
    HeadlessEntityId inheritedSrc = await PlaceWitness(match, P2, "EX8_061", ChoiceZone.None);
    SetMetadata(match, holder, new Dictionary<string, object?> { ["sourceIds"] = new[] { inheritedSrc.Value } });
    HeadlessEntityId dsPlay = await Place(match, P2, "DS4", "ds4", ChoiceZone.Trash);
    HeadlessEntityId deleter = await Place(match, P1, "PLAIN", "deleter", ChoiceZone.BattleArea);

    // The holder (PLAIN, no Scapegoat) is deleted outright; the whole stack goes to trash and EX8_061's
    // inherited [On Deletion] fires from the deletion window.
    ApplyDelete(match, source: deleter, target: holder);
    await match.StepAsync();

    AssertInZone(match, P2, ChoiceZone.Trash, holder, "the holder was deleted (no sacrifice available)");
    // [On Deletion] is isOptional=true (EX8_061.cs:160): the "will you use?" prompt opens first — accept it.
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "[On Deletion] optional prompt is open");
    LegalAction accept = AcceptWindow(match, P2, inheritedSrc);
    await match.ApplyActionAsync(accept);
    await match.StepAsync();

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "after accepting: the trash-play select is open");
    LegalAction pick = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(dsPlay.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();

    AssertInZone(match, P2, ChoiceZone.BattleArea, dsPlay, "the level-4 [DS] Digimon was played from the trash");
}

async Task AlternateDigivolution()
{
    DcgoMatch match = await CreateMatchAsync();
    EngineContext context = match.Context;
    context.MemoryController.Set(5);
    // A level-4 [DS] Digimon on the field — the added requirement's target.
    HeadlessEntityId target = await Place(match, P1, "DS4", "target", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    // EX8_061 in HAND — never registered; the added requirement must be read dispatch-first.
    HeadlessEntityId evo = await Place(match, P1, "EX8_061", "evo", ChoiceZone.Hand);

    // The printed requirement (Purple@5) fails against a DS Lv4 target; the printed cost is rejected too.
    var atPrinted = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);
    AssertFalse(atPrinted.IsSuccess, "printed path (Purple@5, cost 2) is rejected");

    var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 3), context);
    AssertTrue(result.IsSuccess, $"legal via the added DS-Lv4 path at cost 3 ({result.Message})");
    AssertInZone(match, P1, ChoiceZone.BattleArea, evo, "EX8_061 became the new top");

    // Control: a NON-DS level-4 target stays illegal.
    DcgoMatch control = await CreateMatchAsync();
    control.Context.MemoryController.Set(5);
    HeadlessEntityId plainTarget = await Place(control, P1, "PLAIN4", "target", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    HeadlessEntityId evo2 = await Place(control, P1, "EX8_061", "evo", ChoiceZone.Hand);
    var illegal = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo2, plainTarget, memoryCost: 3), control.Context);
    AssertFalse(illegal.IsSuccess, "a non-[DS] Lv4 target does not satisfy the added path");
}

// --- Harness ---------------------------------------------------------------

async Task<DcgoMatch> CreateMatchAsync()
{
    // deferredChoice: interactive ACTIVATED bodies (the trash-play / de-digivolve selects) surface as agent
    // ResolveChoice decisions instead of the enqueued-result fallback (G11-002 pattern).
    EngineContext context = EngineContext.CreateDefault(randomSeed: 75, deferredChoice: true);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Definition($"P1-M{index:D2}"));
        cards.Upsert(Definition($"P2-M{index:D2}"));
    }

    // Witness card definitions: CardNumber = ported class name (CardEffectDispatch resolves by it).
    cards.Upsert(Definition("BT14_035"));
    cards.Upsert(Definition("BT13_023"));
    cards.Upsert(Definition("EX8_051"));
    cards.Upsert(Definition("EX8_061", level: 6, playCost: 2, evolutionCondition: "Purple@5", extra: new Dictionary<string, object?> { ["fixedDigivolutionCost"] = 2 }));
    cards.Upsert(Definition("BT2_034"));
    // Support definitions.
    cards.Upsert(Definition("PLAIN"));
    cards.Upsert(Definition("PLAIN4", level: 4));
    cards.Upsert(Definition("SRC"));
    cards.Upsert(Definition("MINERAL", traits: new[] { "Mineral" }));
    cards.Upsert(Definition("DSPLAY", level: 3, traits: new[] { "DS" }));
    cards.Upsert(Definition("DS4", level: 4, traits: new[] { "DS" }));
    cards.Upsert(Definition("NOMATCH", level: 3, traits: new[] { "Machine" }));

    // (4b B6 re-pin) OLD `new DcgoMatch(context)` + AdvanceToMain (AdvancePhase currency) -> CreatePumpDriven +
    // pump reach-main. The pump owns the security deal, so the pump-dealt security/hand are cleared before the
    // test stages its own witness cards (all fixtures place their instances explicitly via Place()). This moves
    // the DRIVER off the retired step currency; every assertion (incl. the real-debt reds) is unchanged.
    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(P1, "P1"), BuildDeck(P2, "P2") },
        firstPlayerId: P1,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 75, setup: setup));
    await DriveUntilMainWait(match, P1);

    var reader = (IZoneStateReader)context.ZoneMover;
    foreach (HeadlessPlayerId owner in new[] { P1, P2 })
    {
        foreach (HeadlessEntityId dealt in reader.GetCards(owner, ChoiceZone.Security).ToArray())
        {
            await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, dealt, ChoiceZone.Security, ChoiceZone.Library));
        }
        foreach (HeadlessEntityId dealt in reader.GetCards(owner, ChoiceZone.Hand).ToArray())
        {
            await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, dealt, ChoiceZone.Hand, ChoiceZone.Library));
        }
    }

    return match;
}

static CardRecord Definition(
    string id, int level = -1, IReadOnlyList<string>? traits = null, int? playCost = null,
    string? evolutionCondition = null, IReadOnlyDictionary<string, object?>? extra = null)
{
    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (level >= 0)
    {
        metadata["level"] = level;
    }

    if (traits is not null)
    {
        metadata["traits"] = traits.ToArray();
    }

    if (extra is not null)
    {
        foreach (KeyValuePair<string, object?> pair in extra)
        {
            metadata[pair.Key] = pair.Value;
        }
    }

    return new CardRecord(new HeadlessEntityId(id), id, $"{id} Card", metadata,
        CardType: "Digimon", PlayCost: playCost, EvolutionCondition: evolutionCondition);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// Place a standalone instance of `definitionId` into `zone` (metadata optional).
async Task<HeadlessEntityId> Place(
    DcgoMatch match, HeadlessPlayerId owner, string definitionId, string tag, ChoiceZone zone,
    IReadOnlyDictionary<string, object?>? metadata = null)
{
    var id = new HeadlessEntityId($"wit:{tag}");
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(definitionId), owner));
    if (zone != ChoiceZone.None)
    {
        await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    }

    if (metadata is not null)
    {
        SetMetadata(match, id, metadata);
    }

    return id;
}

// Place a WITNESS card and register its ported effects through the production enter-play chokepoint.
async Task<HeadlessEntityId> PlaceWitness(
    DcgoMatch match, HeadlessPlayerId owner, string cardNumber, ChoiceZone zone,
    IReadOnlyDictionary<string, object?>? metadata = null)
{
    HeadlessEntityId id = await Place(match, owner, cardNumber, cardNumber.ToLowerInvariant() + ":" + owner.Value, zone, metadata);
    match.Context.RegisterEnteredCardEffects(id, owner);
    return id;
}

void ApplyDelete(DcgoMatch match, HeadlessEntityId source, HeadlessEntityId target)
{
    EngineContext context = match.Context;
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, log: null, context.ZoneMover, context.MemoryController,
        context.EffectRegistry, context.GameEventQueue, context: context);
    sink.Apply(new EffectMutation(
        MatchStateMutationSink.DeleteKind,
        source,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
    sink.FlushAsync().GetAwaiter().GetResult();
}

static async Task StepOnce(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static async Task ApplyAndStep(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
}

static bool AtMainWait(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

static async Task DriveUntilMainWait(DcgoMatch match, HeadlessPlayerId player)
{
    for (int i = 0; i < 96 && !AtMainWait(match, player); i++)
    {
        if (match.HasPendingChoice())
        {
            HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
            LegalAction? resolve;
            using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
            {
                resolve = match.GetLegalActions(chooser)
                    .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                        && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal))
                    ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
            }
            if (resolve is null) { await StepOnce(match); }
            else { await ApplyAndStep(match, resolve); }
        }
        else
        {
            await StepOnce(match);
        }
    }

    if (!AtMainWait(match, player))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump did not reach {player.Value}'s main wait — phase:{t.Phase}/{t.StepCursor} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

IEnumerable<LegalAction> ResolveActions(DcgoMatch match, HeadlessPlayerId player) =>
    match.GetLegalActions(player).Where(a => a.ActionType == HeadlessActionTypes.ResolveChoice);

// (수리-3) Drive one pick of a multi-select SESSION: apply the listed ToggleChoiceCandidate lane whose candidate
// id carries `source`, then step. The session table (per-candidate Toggle lanes + a Confirm ResolveChoice that
// lights only when the count/validator gate passes) is the AS-IS incremental selection loop as an action surface
// (HeadlessLegalActionDispatcher.cs:100/165); this is the same idiom R4RL-03 W8 uses to drive Fragment <3>.
async Task ApplyToggle(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId source)
{
    LegalAction toggle = match.GetLegalActions(player).Single(a =>
        a.ActionType == HeadlessActionTypes.ToggleChoiceCandidate
        && a.Id.Value.Contains(source.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(toggle);
    await match.StepAsync();
}

// (수리-2 re-aim) Accept the current OptionalEffect PRE would-be-deleted window: pick the non-skip candidate
// keyed by the replacement holder's own instance id (the OptionalEffect Candidates[0].Id convention that the
// green sibling C-Del-3C1C resolves against). This replaces the torn-down invented "#<keyword>" gate ids.
LegalAction AcceptWindow(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId holder) =>
    ResolveActions(match, player).Single(a =>
        a.Id.Value.Contains(holder.Value, StringComparison.Ordinal)
        && !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));

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

int SecurityCount(DcgoMatch match, HeadlessPlayerId player) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, ChoiceZone.Security).Count;

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    match.Context.ZoneMover is IZoneStateReader reader && reader.GetCards(player, zone).Contains(cardId);

void AssertInZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId, string label) =>
    AssertTrue(InZone(match, player, zone, cardId), label);

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

void AssertAttackEnded(DcgoMatch match, string label)
{
    HeadlessAttackState attack = match.Context.AttackController.Current;
    AssertTrue(attack.Phase == AttackPhase.None && !attack.IsPending, $"{label} (phase={attack.Phase}, pending={attack.IsPending})");
}

// --- Assertions ----------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}
