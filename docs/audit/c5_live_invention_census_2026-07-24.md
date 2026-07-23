# C5 라이브 발명 계통 — 소비 지도·해체 설계 census (2026-07-24)

- 기준 커밋: `db0b01f8` (Script TOBE_ONLY 청산 C0~C4), 워킹트리 clean, 조사 전용(수정·커밋 없음).
- 근거 원장: `docs/audit/manifest/verdicts_tobe_only.csv` (전 수치 재실측; 오계수 다수 발견 — 아래 표기).
- 스코프 규약:
  - **LIVE**(런타임) = `src/HeadlessDCGO.Engine/Headless` + `src/HeadlessDCGO.Engine/Assets` + `src/HeadlessDCGO.Rl` (항상 `/obj/`·`/bin/`·`/.claude/worktrees/` 제외).
  - **TEST** = `./tests/` (별도 `.Tests` 프로젝트).
  - **AS-IS** = `DCGO/Assets/Scripts` (Unity 원본; 런타임 빌드 비포함). grep은 항상 `--binary-files=text`.
- 전칭 주장에는 grep 명령 병기. 빈도 가중 없음(전수 리스트/분류만).

---

## 요약 판정 (계통별)

| # | 계통 | 실측 규모 | AS-IS 대응 | 해체 난도 | 상태 |
|---|------|----------|-----------|----------|------|
| 1 | ContinuousEffectEvaluator + 연속-게이트 클러스터 | 라이브 신호 **0**(전량 inert) | Permanent/CardSource getter + NewModelContinuousScan | 中(4 콜사이트 union 절단 → 원자삭제) | **재분류: LOAD-BEARING 아님** |
| 2 | EffectChoiceHelpers | 라이브 심볼 4개(빌더)뿐, ~280/388줄 사문 | AS-IS 대응 없음(포팅-발명 빌더층; substrate=Headless/Choices) | 低 | 부분 삭제 |
| 3 | NumericModifier 파이프라인(ModifierHelpers) | 진짜 라이브 좌석 **1**(LinkHelpers) | AS-IS interface fold 직접(모디파이어 레코드 없음) | 低(좌석 1 절단 = ResolveLinkCost 전례) | seat-by-seat 은퇴 |
| 4 | KeywordBaseBatch1/2 | 컴파일-용접 1개(14 파샬 Block A)만 잔존 | AS-IS 대응 없음(구모델 Kind-dispatch) | 中(R6-Db 동승) | 조건부 삭제(precond 대부분 완료) |
| 5 | NewModelContinuousScan | **46 참조/33 파일**(테스트 포함 72) — C2 "109/75"는 **과계수** | Permanent.cs/CardSource.cs getter(이미 존재) | 高(33 소비 retarget, 계약 차이 주의) | REHOUSED(재배선→삭제) |
| 6 | 소형 잔여(Conditions/CardRequirementHelpers/ContinuousAndRestrictionEffects) | 전부 라이브 소비 **0**(사문/툼스톤) | 각 항목별(아래) | 低 | 즉시 삭제 가능 |

핵심 재측정(오계수 시정):
- **System 1은 "LOAD-BEARING"이 아니다.** 원장 line 22의 "modifier read carries live continuous-DP signal"은 반증됨 — 아래 §1 참조.
- **System 5의 "109참조/75파일"은 과계수.** 실측 46 참조 라인 / 33 라이브 파일(자기 제외), 테스트 포함 72 파일.
- **System 6 Conditions.cs의 "TfxDigivolveCostGate+BT23 소비"는 부분문자열 오탐**(`TfxDigivolve**CostGate**`); `DigivolveCost` enum 소비자 전무.

---

## System 1 — ContinuousEffectEvaluator + 연속-게이트 클러스터

### 실존 게이트 전수 (grep 확정)
`grep -rln "class Continuous\|class BattleDeletionGate\|ContinuousScopeEvaluation" src/.../Headless`:
- `Headless/Runtime/ContinuousScopeEvaluation.cs`
- `Headless/Runtime/ContinuousModifierGate.cs`
- `Headless/Runtime/ContinuousRestrictionGate.cs`
- `Headless/Runtime/BattleDeletionGate.cs`
- `Headless/Runtime/ContinuousKeywordGate.cs`
- `Headless/Runtime/ContinuousFieldMembership.cs`
- (W3a REVERT 잔당인 `ContinuousDpGate`는 **존재하지 않음** — grep 0; 주석 참조만)

### 소비 지도 및 inert 증명 (파일:라인)
클러스터의 데이터 진입점은 `ContinuousScopeEvaluation.ApplicableEffects` (`ContinuousScopeEvaluation.cs:57-72`)인데, **`Array.Empty<EffectRequest>()`를 반환**(:71). 즉 레지스트리 연속-수집 arm은 생산자 0(주석 :63-68). 따라서:

- `ContinuousScopeEvaluation.EvaluateForCard` (:36-51) → `ContinuousEffectEvaluator.Evaluate`에 **빈 effect 리스트** + card/instance metadata + `state:null` 전달.
- `ContinuousEffectEvaluator.Evaluate` (`ContinuousEffectEvaluator.cs:105-133`) → `ModifierHelpers.ReadModifiers` / `RestrictionHelpers.ReadRestrictions` / `ReplacementHelpers.ReadReplacements`. 이 셋은 card.Metadata·instance.Metadata·state.Modifiers·effectRequests에서 키를 읽음(`ModifierHelpers.cs:385-426`).
- **numeric-modifier 키 생산자 census = 0**: `grep '"dpDelta"\|"linkedMaxDelta"\|"sAttackDelta"\|"securityAttackDelta"\|"numericModifiers"'` (LIVE, ModifierHelpers.cs 제외) → **0 히트**. `state`는 게이트 경로에서 항상 `null`. LinkHelpers 주석이 이를 확증("linkCostDelta … WRITTEN by nothing", `LinkHelpers.cs:98-99`).
- 결론: `ContinuousEffectEvaluator`의 modifier/restriction/replacement 산출은 **프로덕션에서 항상 빈 집합**.

`EvaluateForCard` 라이브 콜사이트 4곳 — 전부 빈 결과를 받은 뒤 **NewModelContinuousScan로 union**되어 무력화:
1. `Headless/Runtime/LinkHelpers.cs:71-74` — `result.Modifiers`(빈) fold 후 `NewModelContinuousScan.FoldLinkedMax`가 진짜 값 산출(:79).
2. `Headless/Effects/MatchStateMutationSink.cs:1927`(`ScopedResult`) — 빈 restriction/replacement, 실제 판정은 NewModelContinuousScan 스캔.
3. `Headless/Runtime/BattleDeletionGate.cs:48-55` — `result.Replacements`(빈) 순회 후 `NewModelContinuousScan.HasCanNotBeDestroyed`/`HasCanNotBeDestroyedByBattle`(:61-71)가 라이브 경로.
4. `Headless/Runtime/ContinuousRestrictionGate.cs:50-52`(`Evaluate`) — 빈 `result.Restrictions` 반환. **이 `Evaluate` 메서드는 소비자 0**(`grep "ContinuousRestrictionGate.Evaluate\b"` → 0); 라이브 제약은 `EvaluateAttack/Block/Digivolve/…`가 `JointResult`→`NewModelContinuousScan.IsRestrictedNewModel`로 처리(:67-108).

`ContinuousModifierGate` (`ContinuousModifierGate.cs`): 연속-모디파이어 평가는 이미 **은퇴**(W3c-final, :70-79). 잔존 `ResolvePlayCost`/`ResolveDigivolutionCost`는 `CardSource.GetPayingCostWithBaseCost`로의 **thin delegate**(:27-68). 라이브 콜사이트 **0**, 테스트 전용(10개 파일: G3.5-D8/G9-003r/BT23.PrimTranche3/G9-021/G3.5-F17/FAILa-05/BT1.StopRemainder/PRIM-P0.CannotReduceCost 등).

`ContinuousKeywordGate.HasKeyword`(:98-107)·`IsDigimon`(:114-137) → `NewModelContinuousScan.HasKeyword`로 위임. (System 4·5 참조.)

### AS-IS 대응 구조
AS-IS는 연속 계산을 **Permanent.DP/CardSource getter 인라인 스캔**으로 흩어놓음(원장 line 22 앵커). 미러도 동일: `Permanent.cs`에 `DP`(:381)·`HasBlocker`(:825)·`HasJamming`(:917)·`HasPierce`(:1021)·`HasCollision`(:1502) getter가 이미 존재. `ContinuousEffectEvaluator`/values-dict 모델은 AS-IS 무대응 발명. W3c-1에서 `CanNotAffected` flip 성공(BlockTiming 소비-측 재배선, `BlockTiming.cs:285-288`) — 게이트가 잔존한 이유는 게이트 자체가 아니라 **빈 union 비계가 4 콜사이트에 아직 배선**돼 있어서(삭제 대기), 로직 이유 아님.

### 해체 배치안
- **B1 (원자, 저위험)**: `ContinuousRestrictionGate.Evaluate`(registry, 소비 0) 삭제.
- **B2 (콜사이트 4 union 절단, 다이제스트/shadow 검증)**: LinkHelpers:71-74 legacy fold 제거(FoldLinkedMax 유지) — `ResolveLinkCost`가 이미 밟은 RD-P6B-16 전례와 동형(bit-identical 기대); MatchStateMutationSink.ScopedResult 빈-분기화; BattleDeletionGate replacement 순회 제거(interface 스캔 유지); ApplicableEffects 소비 잔재 정리.
- **B3 (원자 삭제)**: B2 후 `ContinuousScopeEvaluation`·`ContinuousEffectEvaluator`(+Factory)·`ModifierHelpers.ReadModifiers`·`RestrictionHelpers.ReadRestrictions`·`ReplacementHelpers.ReadReplacements` 무참조화 → 삭제.
- **보존**: `CalculateOrder` enum(ICardEffect.cs:1428로 이미 rehoused, 진짜 AS-IS)·`RestrictionHelpers`의 `CannotXKey` const 10종(라이브 kind 키 — NewModelContinuousScan/작은 키파일로 재하우징)·`ReplacementHelpers.ImmuneFrom`(라이브 1).
- `ContinuousModifierGate` cost 래퍼: 테스트 10파일을 `CardSource.GetPayingCostWithBaseCost` 직접 호출로 retarget 후 삭제(또는 얇은 래퍼 유지 = 저우선).

### 위험
빈 결과의 union 절단이므로 **행동 중립이 가설**. B2는 다이제스트(연속-DP/제약/전투삭제 시나리오)로 union 전후 동일 확인 필수. RestrictionHelpers const 키를 옮길 때 참조 동기화(repair DoD).

---

## System 2 — EffectChoiceHelpers

파일: `.../CardEffectCommons/EffectChoiceHelpers.cs` (388줄).

### 소비 지도
- **라이브 심볼 4개만**: `Candidate`(9 사이트), `CreateCardRequest`(2: SelectCardEffect.cs:113, Permanent.cs:3896), `CreatePermanentRequest`(5: SelectPermanentEffect.cs:153/663, Raid.cs:107, MindLink.cs:83, TfxPlayOption), `CreateCountRequest`(2: SelectCountEffect.cs:66/209).
- 소비 카드/이펙트: SelectCardEffect(:102,113), SelectAttackEffect(:277,290 — Candidate만, request 인라인), SelectCountEffect(:66,209), SelectPermanentEffect(:143/153/658/663/714), Permanent.cs(:3893,3896), Raid.cs(:107,113), MindLink.cs(:81,83).
- **전부 빌더로만 사용** — 실제 해소는 `context.ChoiceProvider.ChooseAsync(...)` / `ChoiceController.RequestChoice(...)`(Headless/Choices). `EffectChoiceHelpers.ResolveAsync`/`ApplyResult`/`EffectChoiceResolution`/`EffectChoiceHelperFactory`/14 const 키 = **라이브 소비 0**, 전용 테스트 `tests/G3K-001`만.
- grep: `grep -rn 'EffectChoiceHelpers' <LIVE>` / 심볼별 `grep '\.<Method>('`.

### AS-IS 대응
AS-IS 무대응. AS-IS `Select*Effect`는 `MonoBehaviourPunCallbacks` 코루틴+Photon RPC+UI 패널(`DCGO/.../SelectPermanentEffect.cs:242 Activate→:428 OnClick→:579 EndSelect_RPC→:601 RPC`). 포팅 substrate = `Headless/Choices`(ChoiceRequest/Candidate/IChoiceProvider). EffectChoiceHelpers는 그 위의 발명 빌더 편의층(AS-IS 소스라인 없음).

### 해체 배치안 (低위험)
1. 라이브 빌더 4개 유지(로직 있는 `CreateCountRequest`) 또는 ~9 콜사이트에 `new ChoiceCandidate/ChoiceRequest(...)` 인라인.
2. 해소 엔진 삭제: `EffectChoiceResolution`·`ApplyResult`·`ResolveAsync`·`WithValues`·`RequestValues`·`ResultValues`·`Key`·`CandidatesFromIds`·`CreateRequest`·14 const·`EffectChoiceHelperFactory` (~280줄). 프로덕션 참조 파손 0.
3. `tests/G3K-001` 슬림화(삭제 표면 커버 제거, 생존 빌더만).

### 위험
낮음. 빌더 인라인 시 substrate 타입 시그니처 동일 확인만.

---

## System 3 — NumericModifier 파이프라인 (ModifierHelpers.cs)

파일: `.../CardEffectCommons/ModifierHelpers.cs` (819줄).

### 소비 지도 (실측 — 원장 "15+"는 과대)
`ModifierHelpers`/`NumericModifier` 참조 라이브 파일 = **10** (자기 2 포함). 진짜 라이브 좌석:
- **1개**: `Headless/Runtime/LinkHelpers.cs:72-74` — `ModifierHelpers.Evaluate(NumericModifierRequest(LinkedMax, baseMax, result.Modifiers))`. `result.Modifiers`는 System 1에서 **빈 집합** → 이 fold도 inert, 실제 값은 `NewModelContinuousScan.FoldLinkedMax` union(:79).
- 나머지 참조는 전부 사문/주석: `ContinuousModifierGate.cs`(RETIRED 주석), `CardSource.cs:1473-1475`(RETIRED 주석), `MatchStateMutationSink.cs:1933`(`Array.Empty<NumericModifier>()`만), `BT25_104.cs:294`·`BT25_075.cs:12`(설계아이템 주석), `ContinuousEffectEvaluator.cs`(사문 체인).

**`CalculateOrder` enum은 파이프라인과 별개**: AS-IS `ICardEffect.cs:940`의 정본을 미러 `ICardEffect.cs:1428`로 rehoused. 진짜 AS-IS fold가 소비(`Permanent.cs:2710`, `ChangeSAttackClass.cs`, `ChangeLinkMaxClass.cs`, `NewModelContinuousScan.cs:265`). **보존 대상.**

### AS-IS 대응
AS-IS는 `ICardEffect` interface(IChangeSAttackEffect/IChangeLinkMaxEffect/IImmuneFromDPMinusEffect)를 **직접 fold**(`Permanent.cs:295-310/1872-1930/975-1000`). NumericModifier 레코드/Request/Result/Evaluate/ReadX/메타-딕셔너리 리더는 무대응 발명.

### 해체 배치안 (seat-by-seat, 低위험)
- LinkHelpers.ResolveLinkedMax의 legacy fold 절단(FoldLinkedMax 유지) — **ResolveLinkCost가 이미 밟은 RD-P6B-16 전례와 동형**(bit-identical 기대). = System 1 B2와 동일 콜사이트.
- 그 후 `NumericModifier`/`NumericModifierRequest`/`NumericModifierResult`/`NumericModifierMetric`/`Evaluate`/`ReadModifiers`/`ReadSimpleModifiers`/*Key/*Metric 삭제.
- `CalculateOrder`·`NumericModifier.Add/Set/InvertSecurityAttack`(만약 라이브 좌석 잔존 시 확인) 취급 분리 보존.

### 위험
LinkHelpers 절단은 빈-fold 제거 → 다이제스트로 LinkedMax 시나리오 union 전후 동일 확인.

---

## System 4 — KeywordBaseBatch1/2

파일: `.../KeyWordEffects/KeywordBaseBatch1.cs`(356), `KeywordBaseBatch2.cs`(422).

### 소비 지도
- 발명 장치(`KeywordBaseBatch1/2Kind` enum, `*Timings`/`*Scopes` const, `*Factory`, `Create/CreateAll/ToBinding/RegisterBaseBatch1/2`) = **라이브·테스트 생성자 0**. `RegisterBaseBatch1/2`·`ToBinding`·`QueryRole`·`IHeadlessCardEffect`는 이미 **삭제됨**(RD-GC2-01, 파일 내 확인).
- 잔존 용접 = **파샬 클래스 컴파일 엣지 1개**: `KeywordBaseBatch1Effect`(sealed partial) ← 4 파샬(Blocker/Jamming/Pierce/Reboot); `KeywordBaseBatch2Effect` ← 10 파샬(Rush/Blitz/Retaliation/ArmorPurge/Decode/Alliance/Vortex/Overclock/Partition/Progress). 이 14 파일의 **Block A**(`CanResolveX` + `*ContextKeys` 소비)만 배치파일을 참조. **Block B**(라이브 `CardEffectCommons` AS-IS 포트)는 배치파일 참조 0.
- `ContinuousKeywordGate`와는 **컴파일 엣지 없음**(양방향 주석만: :49, :96). 게이트는 키워드-이름 const 권위 형제일 뿐. → 원장의 "게이트 컴파일-의존"은 과장.
- `.../CardEffectFactory/KeyWordEffects/`(32 라이브 팩토리)는 배치 참조 0.

### AS-IS 대응
없음(구모델 Kind-dispatch 발명). 진짜 키워드 로직은 Block B + Factory 디렉토리(AS-IS `AddCardEffect`/`GetCardEffects` 라이브 쿼리).

### "R6-Db weld cut" 실체 (rebuild 원장 대조)
- `docs/audit/keyword_rehoming_design_2026-07-15.md:45` — A군 후 배치가 test-only 아님 판명(구모델 binding-rule/`ToBinding` + 16 파샬 Kind-dispatch 용접) → **R3-W3 레지스트리-삭제 지역골로 이관**.
- `docs/audit/registry_teardown_investigation_2026-07-16.md:20,35,38` — precond (c)=Kind-dispatch 해체; **R3-W3b**(등록 반쪽 `RegisterBaseBatch1/2`/`ToBinding`)=**완료**; **R6-Db**="인라인 6장 re-port + Tfx 18 은퇴 + **corpus 삭제**".
- `docs/audit/registry_probe_census_2026-07-20.md:152`·`r6da_prime_design_2026-07-21.md:175,255` — corpus 삭제가 R6-Db에 동승, 순서 `…→corpus 삭제(R6-Db)→W3c-final`.
- 구조적 의미: 세 용접 중 (i)등록 반쪽=완료, (iii)ContinuousAndRestrictionEffects 참조=HEAD에 **부재**(0), **(ii)14 파샬 Block A만 잔존**. weld cut = 그 마지막 kind-dispatch 엣지 절단(R6-Db corpus 자기-삭제 동승).

### 해체 배치안 (~ -778줄 + Block A 제거)
1. 14 파샬 파일의 **Block A만 삭제**(Block B 라이브 포트 유지) → `*ContextKeys`·파샬 신원 마지막 소비 제거.
2. `KeywordBaseBatch1.cs`+`KeywordBaseBatch2.cs` 원자 삭제.
3. (선택) 댕글링 주석 정리(MatchStateMutationSink.cs:257, ContinuousKeywordGate.cs:49/96, 테스트 3파일).
- 순서: **R6-Db "corpus 삭제" 동승**, `Da′-1…Da′-6` + 레지스트리 read-half 은퇴 후, W3c-final 종착. 배치 생성자 0이므로 독립 테스트 위험 없음(corpus 배치 외).

### 위험
낮음(컴파일-용접 절단). Block A/B 분리 삭제 시 파일별 정밀 제거(Block B 훼손 금지).

---

## System 5 — NewModelContinuousScan (C2 BLOCKED)

파일: `.../CardEffectCommons/NewModelContinuousScan.cs` (1861줄, public static 메서드 46개).

### 실측 재확인 (C2 "109참조/75파일" = 과계수)
- 라이브 참조 라인(자기 제외) = **46**; 라이브 파일 = **33**; 테스트 포함 파일 = **72**.
  - grep: `grep -rn "NewModelContinuousScan" <LIVE> | grep -v NewModelContinuousScan.cs | wc -l` → 46 라인 / `-rln … | wc -l` → 33 파일.
- 소비 파일 분류: 런타임 6(Headless/Runtime 5 + Headless/Effects 1), 미러 코어 4(Permanent.cs/CardSource.cs/CardEffectCommons.cs/CardEffectFactory.cs), 미러 kind-class ~10(CanNotAttack/CanNotBlock/CanNotBeAttacked/CanNotBeBlocked/CanNoReturnToDeck/CanNotReturnToHand/CanNotBeDeletedByEffect/ChangeCardDP/Alliance 등), 카드 3.
- 런타임 소비 메서드 빈도: HasKeyword·HasCanNotBeDestroyed·CanNotSuspend(각 2), IsRestrictedNewModel·IsRestrictedByCauseNewModel·HasCanNotBeDestroyedByBattle·FoldLinkedMax·FoldLinkCost·FoldCardDp(각 1).

### AS-IS 대응 — **getter가 이미 미러 Permanent.cs/CardSource.cs에 존재**
NewModelContinuousScan.Has*/Fold*/CanNot*는 (context,cardId)→bool/int **어댑터 형태로 미러 getter를 중복**. 미러 `Permanent.cs`에 이미: `DP`(:381), `HasBlocker`(:825), `HasJamming`(:917), `HasPierce`(:1021), `HasReboot`(:1055), `HasCollision`(:1502) 등. 즉 REHOUSED 종점은 "본문 이전"이 아니라 **소비자를 `new Permanent(context,id).HasX`/CardSource getter로 retarget 후 NewModelContinuousScan 원자 삭제**(BlockTiming.cs:247·336가 이미 이 패턴).

### HasBlocker AS-IS Collision 선행영역 갭 — **판정: 라이브 갭 아님(seam artifact)**
- AS-IS `Permanent.HasBlocker`(`DCGO/.../Permanent.cs:2397-2483`)는 3-tier IBlockerEffect 스캔 **앞에** Collision 선행영역(:2401-2418): 공격 중 & 배틀에어리어 & 공격자 TopCard.Owner≠수비자 Owner & `attackingPermanent.HasCollision` & `!TopCard.CanNotBeAffected(fakeCollisionClass)` → 수비자를 HasBlocker=true로 강제.
- 미러 `NewModelContinuousScan.HasBlocker`(:547-592)는 이 선행영역을 **생략**(3-tier 스캔만).
- **커버 확인**: 원장 note의 추정("separate HasCollision가 커버")은 **틀림** — `HasCollision(X)`는 "X가 Collision 키워드 보유"로 무관. 실제 커버는 **두 곳**에 1:1 존재:
  1. 미러 `Permanent.HasBlocker` getter(`src/.../Permanent.cs:825-846`)가 선행영역을 **완전 포함**(:829-846, AS-IS 1:1). 카드 소비자(BT1_023:46, BT1_079:44, BT1_094:51, BT1_110:96, BT1_082:48)와 BlockTiming.cs:247이 이 getter를 읽으므로 선행영역 획득.
  2. `Headless/Runtime/BlockTiming.cs`가 Collision 강제-블록을 독립 구현: `HasBlocker(...,attackerHasCollision)`(:269-294)=`attackerHasCollision && !CannotBeAffectedByCollision && !DefenderIsCollisionImmune`; `DefenderIsCollisionImmune`(:302-318)=synthetic `fakeCollisionClass`(공격자 TopCard 소스)+`blockerCard.CanNotBeAffected` — AS-IS :2411-2415 1:1; `CanSkipBlock`(:320-337)도 강제.
  - 블록윈도 판정(`TryCreateCandidate` :244)=`BlockTiming.HasBlocker(…collision arm…) || Permanent(blockerId).HasBlocker` → 두 항의 union이 AS-IS `Permanent.HasBlocker` = [Collision 선행] OR [3-tier]를 정확 재구성.
- **∴ `NewModelContinuousScan.HasBlocker`의 생략은 설계상 의도(키워드-존재 질의 = HasKeyword("Blocker") 소스, :1830). 라이브 행동 갭 없음.**

### 해체 배치안 ((a)union 은퇴 →(b)앵커별 트랜치 →(c)트랜치별 다이제스트)
- **(a) 선행조건**: System 1 클러스터(빈 union 비계) 은퇴 완료 — NewModelContinuousScan는 그 후 유일 연속 경로.
- **(b) 앵커별 트랜치** (소비자를 이미 존재하는 Permanent/CardSource getter로 retarget):
  - T5-DP/Cost: `FoldCardDp`/`FoldLinkedMax`/`FoldLinkCost`/`FoldPlayCost` → `Permanent.DP`/`CardSource` cost getter. 소비=LinkHelpers·PlayCostHelpers·DigivolutionCostHelpers·CardEffectCommons.
  - T5-Keyword: `Has<Keyword>` ~30 → `ContinuousKeywordGate.HasKeyword` 유지하되 dispatch를 Permanent 키워드 getter로. **주의: HasBlocker/HasCollision 등은 Permanent getter가 attack-context(선행영역/전투) 포함 → 키워드-존재 의미와 계약 상이.** HasKeyword 경로는 키워드-스캔 전용 변형 필요(현 NewModelContinuousScan.HasBlocker 축소본을 keyword-only 헬퍼로 잔치). = 트랜치 최대 위험 지점.
  - T5-Restriction: `CanNotAttack/CanNotBlock/CanNotBeAttacked/CanNotBeBlocked/CanNotDigivolve/IsRestricted*` → `Permanent.CanX` getter/joint. 소비=ContinuousRestrictionGate joint·MatchStateMutationSink·BattleResolver·kind-class.
  - T5-Deletion: `HasCanNotBeDestroyed`/`ByBattle`/`BySkill`/`HasCannotReturnToHand`/`Library` → `Permanent.CanBeDestroyed*`. 소비=BattleDeletionGate·MatchStateMutationSink.
- **(c) 트랜치별 다이제스트**: 각 트랜치 flip 전후 골든 시나리오(블록/전투삭제/DP fold/제약) union 동일 확인 + 해당 witness 테스트(SA3/DPB/FAILd/PRIM-W3~4/C-Del 계열).
- **종점**: NewModelContinuousScan 무참조 → 원자 삭제(1861줄).

### 위험
高. 33 소비 retarget는 계약-보존 필요(특히 HasBlocker/HasCollision keyword-vs-getter 이중 의미). 트랜치당 적대리뷰 + shadow-run 케이던스.

---

## System 6 — 소형 잔여

### 6a. Conditions.cs — `enum DigivolveCost` — **완전 사문(오탐 시정)**
- 파일 16줄, `enum DigivolveCost{Free,Normal,Reduced,Fixed}`만.
- 소비: `grep "DigivolveCost\.(Free|Normal|Reduced|Fixed)" src+tests` → **0**. 원장의 "TfxDigivolveCostGate+BT23 소비"는 부분문자열 오탐(`TfxDigivolve**CostGate**`); `TfxDigivolveCostGate.cs`는 `ChangeCostClass`(CardEffectFactory.ChangeDigivolutionCostStaticEffect, :41) 사용, enum 미참조. BT23 3히트도 fixture 카드명 문자열.
- AS-IS 대응: enum 아닌 **평문 파라미터** — `payCost:bool`/`reduceCostTuple:`/`fixedCostTuple:`(예 `DCGO/.../BT9/Purple/BT9_071.cs:143-145`, `ST16/Purple/ST16_09.cs:35,112`). Free→payCost:false, Normal→payCost:true, Reduced→reduceCostTuple, Fixed→fixedCostTuple.
- 처분: **즉시 삭제**(재배선 불요; fixture는 이미 ChangeCostClass 파라미터 사용).

### 6b. CardRequirementHelpers.cs — **라이브 소비 0(TargetFilterHelpers 삭제 후)**
- 573줄. 유일 소비였던 `TargetFilterHelpers.cs`는 **C0 삭제**(ls 부재 확인). 현 라이브 소비 = **0**; 전용 테스트 `tests/G3D-002`만(~30 콜사이트 + 자기-텍스트 self-assert :270-280).
- AS-IS 대응: 무대응 발명. AS-IS는 `CardSource.cs` 인라인 술어 — `ContainsTraits`(`DCGO/.../CardSource.cs:1701`), Bird→Avian 그룹 `HasBirdTraits`(:1713→ContainsTraits("Avian"):1719/"Bird":1724), Dragon(:1801), Angel(:1866). `TraitGroupValues`(CardRequirementHelpers.cs:542-565)는 그 체인의 테이블 재인코딩.
- 처분(G3D-002 처분안): 발명 API + G3D-002 테스트 삭제. 그룹 참조 필요 시 CardSource.cs:1713+ 1줄 노트로 대체.

### 6c. ContinuousAndRestrictionEffects.cs — **툼스톤 100%(라이브 타입 0)**
- 211줄. `grep class/enum/interface/struct/record` → **0** — 원장의 "3 live types" 전제는 stale. 세 타입 모두 이미 이전됨(파일 내 :134-136/:163-164/:188-189 기록):
  - `CanNotMoveEffect` → `src/.../CardEffects/RestrictionCarriers.cs:84`(생산=CardEffectFactory.cs:511; read=ICanNotMoveEffect 스캔 Permanent.cs:3158/3170/3184 + BT1_089.cs:105). AS-IS=`CanNotMoveClass.cs`.
  - `CanNotSelectBySkillEffect` → `RestrictionCarriers.cs:20`(생산=CardEffectFactory.cs:500; read=Permanent.cs:2932/2936 + SelectPermanentEffect.cs:402). AS-IS=`CanNotSelectBySkillClass.cs`.
  - `BareCauseEffect` → `Headless/Bridge/BareCauseEffect.cs:32`(**43 실사용**/9 파일: CardEffectCommons.cs 23, MatchStateMutationSink 6, CardEffectFactory 5, SkillWindowSupply 2, CardController/Player/SelectPermanentEffect/DigivolutionStackHelpers 각 1). AS-IS 무대응(발명 substrate cause-stub, ActivatedHashtableBridge.CauseStub 이웃 정착 = 정합).
- 툼스톤 제거 선행조건: 원자 registry-scan 삭제(B군 레지스트리 청산)와 연동 — 툼스톤은 이미 inert 텍스트, 감사 트레일로 유지 중. 이 파일이 무엇도 블록하지 않음.
- 처분: rehousing은 **이미 완료**. registry-scan 원자삭제 착지 시 파일 원자 삭제(코드 없음), 또는 지금 원장 이관 후 삭제 가능(참조 0). 행동 위험 없음.

---

## 캠페인 순서 권고 (의존 반영)

의존 그래프: System 1(빈 union 은퇴) → System 5(a)선행조건. System 3(LinkHelpers 절단)은 System 1 B2와 **동일 콜사이트**(병합 처리). System 4는 R6-Db corpus 삭제 동승(독립 트랙). System 2·6은 무의존(선착 가능).

권고 순서:
1. **C5-0 즉시삭제 트랜치(저위험, 무의존)**: System 6a(Conditions)·6b(CardRequirementHelpers+G3D-002)·System 2 해소엔진(EffectChoiceHelpers ~280줄+G3K-001 슬림). — 참조 파손 0, 다이제스트 불요.
2. **C5-1 빈-union 은퇴(System 1 B1+B2+B3, System 3 좌석절단 병합)**: ContinuousRestrictionGate.Evaluate 삭제 → 4 콜사이트 legacy fold 절단(LinkHelpers=System3 동시) → ContinuousScopeEvaluation/ContinuousEffectEvaluator/ReadX/NumericModifier 파이프라인 삭제. — **다이제스트/shadow 필수**(연속-DP/제약/전투삭제/LinkedMax union 전후 동일). CalculateOrder·CannotXKey·ImmuneFrom 보존.
3. **C5-2 NewModelContinuousScan REHOUSED(System 5)**: C5-1 후 착수. 앵커별 4 트랜치(DP/Cost·Keyword·Restriction·Deletion) retarget→삭제. 트랜치당 적대리뷰+witness+shadow. **HasBlocker/HasCollision keyword-vs-getter 계약 분리 주의.**
4. **C5-3 KeywordBaseBatch1/2(System 4)**: R6-Db corpus 삭제 동승(레지스트리 read-half 은퇴 + 인라인6/Tfx18 re-port 후). 14 파샬 Block A 제거 + 배치 2파일 삭제.
5. **C5-4 툼스톤 소거(System 6c)**: B군 registry-scan 원자삭제 착지 시 ContinuousAndRestrictionEffects.cs 삭제(또는 참조0이므로 조기 삭제 가능).
6. **ContinuousModifierGate cost 래퍼**: 테스트 10파일 retarget 후 삭제(저우선, 무의존).

## 총 규모 추정
- 삭제/은퇴 후보 14 파일 합계 **4,851줄**(사문·비계·중복 포함). 순삭제 대략:
  - 즉시(C5-0): Conditions 16 + CardRequirementHelpers 573 + EffectChoiceHelpers ~280(부분) ≈ **~870줄**.
  - C5-1: ContinuousEffectEvaluator 196 + ContinuousScopeEvaluation 97 + ModifierHelpers 대부분(~700, CalculateOrder 제외) + RestrictionHelpers/ReplacementHelpers ReadX 반쪽(~800/731 중 상당) ≈ **~1,800줄**.
  - C5-2: NewModelContinuousScan **1,861줄**(retarget 후) + 게이트 3(ContinuousScope/Modifier/Restriction/BattleDeletion 잔부).
  - C5-3: KeywordBaseBatch1/2 **778줄** + 14 파샬 Block A(~수백줄).
  - C5-4: ContinuousAndRestrictionEffects **211줄**.
- 개괄 **~5,500~6,000줄 순삭제** + 33 NewModelContinuousScan 소비 retarget + 10 cost-wrapper 테스트 retarget. 행동-변경 위험 집중 구간 = C5-1(빈-union 절단)·C5-2 Keyword 트랜치(계약 분리).
