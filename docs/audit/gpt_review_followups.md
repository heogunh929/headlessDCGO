# GPT 검수 후속 조치 리스트 (보류 — 미수정)

- 작성일: 2026-06-27
- 상태: **리스트업만. 코드 수정 보류** (사용자 요청: 자체 원본 대조 검증 먼저)
- 출처: Phase 4 사전 배선 커밋 후 GPT 재검수. 각 항목 **실제 코드로 검증 완료**(아래 검증란).

## 우선순위별

| # | 항목 | 검증 | 의미/영향 | 심각도 | 상태 |
|---|------|------|----------|--------|------|
| 1 | ~~`ValueEquals` 배열 deep-equality 없음~~ → ✅ **수정 완료(2026-06-27)** | `ValueEquals`에 `SequenceValueEquals`(element-wise, 문자열 제외) 추가. 컬렉션 파라미터(ChoiceSelectedIds)를 내용으로 비교 → 비후보 선택 거부. 신규 `tests/G3.5-GPT1.ValidatorDeepEquality.Tests` 3/3 PASS(비후보 c3 거부·후보 c1/c2 수락). 회귀(A1·A2·A3·V) 0. | 🔴→✅ | ✅ |
| 2 | ~~RL observation에 `randomSeed` 포함~~ → ✅ **수정 완료(2026-06-27)** | `ObservationEncodingOptions.IncludeRandomSeed`(기본 off) 추가, 시드 피처 2개를 게이팅 → 기본 관측에서 제외(나머지 runtime flags 유지). 절대 관측 길이/시드 피처를 단언하는 테스트 없어 회귀 0. 신규 `tests/G3.5-GPT2.ObservationNoSeed.Tests` 4/4 PASS(기본 제외·opt-in 포함·정확히 +2·기타 플래그 유지). | 🟠→✅ | ✅ |
| 3 | ~~flow `MaxIterationsExceeded` 상태 없음~~ → ✅ **수정 완료(2026-06-27)** | `FlowProcessStatus.MaxIterationsExceeded` 추가. `RunToStableAsync`가 `reachedStable` 플래그로 진짜 fixpoint(무진행/터미널) vs cap 절단을 구분, cap 시 경고 로그 + 해당 상태 반환. `HeadlessGameLoop`도 진단 메시지. 신규 `tests/G3.5-GPT3.MaxIterationsExceeded.Tests` 3/3 PASS(런어웨이→exceeded@cap·정상→Stable·상태 구분). 회귀(004·005·V) 0. *seam: AttackPipeline unseal+virtual.* | 🟡→✅ | ✅ |
| 4 | ~~`DcgoMatch` 기본 생성자 `actionLegality=null`~~ → ✅ **수정 완료(2026-06-27)** | 생성자 XML 문서로 "unguarded(스크립팅/엔진) vs agent-validated" 의도 명시 + `EnforcesActionLegality` 속성 + `DcgoMatch.CreateValidated(...)` 팩토리(LegalActionSetValidator 부착) 추가. 동작 변화 없음(기본은 종전대로 unguarded). | 🟡→✅ | ✅ |
| 신1 | ~~`strictUnbound`가 default profile 미연결~~ → ✅ **수정 완료(2026-06-27)** | `EngineContext.CreateDefault(strictUnbound: false)` 파라미터 추가, `CardEffectSchedulerResolver.Create`로 전달. 기본 lenient 유지, `CreateDefault(strictUnbound:true)`로 strict 프로파일 노출. (#4와 공통 테스트 `tests/G3.5-GPT4.MatchValidationAndStrictProfile.Tests` 5/5 PASS) | 🟡→✅ | ✅ |
| 신2 | `DigivolutionStack` = projection(storage 아님) | ✅사실 + GPT도 "지금 OK" | de-digivolve/source-reorder 본격 구현 시 병목 가능. 문서를 "완전 모델"이 아닌 "typed read facade 확보"로 기록 | ⚪ 기록 | ⬜ |
| 신3 | CI 실행 이력 없음 | ⚠️부분 — 파일은 커밋됨(`.github/workflows/ci.yml`), GitHub Actions RUN 이력만 없음(push 후 활성화 필요, gh 미설치) | 외부 자동검증 미가동 | 🟡 인프라 | ⬜ |

## 권장 처리 순서 (수정 재개 시)
1. 🔴 #1 ValueEquals — 배열/리스트 element-wise 비교 + 회귀 테스트
2. 🟠 #2 randomSeed — `IncludeRandomSeed` 기본 off (관측 차원 테스트 동반 갱신)
3. 🟡 #3 MaxIterationsExceeded 상태 / 신1 strict 테스트 프로파일 / #4·신2 문서·API 명확화
4. 🟡 신3 — push 후 GitHub Actions 활성화 확인

> 본 항목들은 **자체 원본 대조 검증(아래 별도 문서) 이후** 일괄 처리 예정.

---

## 라운드 2 — GPT 재검수 후속 (2026-06-27, 작성자 코드 검증 완료)

> 라운드 1(#1~#4·신1) 수정 후 GPT가 재검수한 5건. **전부 사실로 확인**(아래 검증란). 리스트업만, 미수정.

| # | 항목 | 검증 결과 | 의미/연계 | 심각도 | 상태 |
|---|------|-----------|-----------|--------|------|
| R2-1 | ~~Security Digimon battle의 Jamming 처리 경로 불명확~~ → ✅ **소비측 배선 완료(2026-06-27)** | 신규 `BattleDeletionGate`(ContinuousRestrictionGate 자매)가 전투 삭제 결정에서 **연속 `Delete/Prevent` replacement를 조회** → `BattleResolver`·`SecurityResolver` 둘 다 정적 플래그 외에 이를 존중. 신규 `tests/G3.5-R2-1.BattleDeletionReplacement.Tests` 3/3(필드/시큐리티 전투에서 prevent-deletion이 패자 구제, 컨트롤=삭제). 회귀(C2·W5·D1·G2G-003·G1F-006·G3I-001) 0. **남음(Phase 4 생산측)**: Jamming 키워드가 연속 replacement로 등록(또는 battle-delete check에서 정적 플래그 set) + 키 정렬(`PreventBattleDeletion`↔`preventDeletion`). | =N-2 삭제방지 슬라이스 소비측 ✅ / 생산측 Phase 4 | 🔴→◑ | ◑ |
| R2-2 | ~~optional trigger 자동 발동~~ → ✅ **해소(2026-06-27)** | per-effect `IsOptional` 재분류 + `OptionalPromptQueue` 루프/A2 배선으로 **선택발동은 에이전트 결정(활성/스킵)**, 강제발동만 즉시. `tests/G3.5-OPT2` 3/3. (1차 audit #2 참조) | 원본대로 닫음 | 🟠→✅ | ✅ |
| R2-3 | MaxIterationsExceeded가 RL 결과 필드로 미노출 | ✅사실 — `FlowProcessStatus.MaxIterationsExceeded` + `HeadlessGameLoop` 로그만. `RlStepResult`/`StepResult`에 해당 필드 **없음**(grep 0). trainer가 step 결과에서 직접 못 읽음 | GPT #3의 follow-up — 상태는 추가됐으나 RL 표면 미노출 | 🟡 | ⬜ |
| R2-4 | strictUnbound 옵션 있으나 기본 학습/포팅 profile factory 부재 | ✅사실 — `CreateDefault(strictUnbound)` + `CreateValidated`는 있지만, **strict+validated를 묶은 "Phase 4 포팅/학습 프로파일" 단일 팩토리 없음**. 호출자가 매번 조합해야 함 | GPT 신1의 follow-up — 편의 팩토리 부재 | 🟡 | ⬜ |
| R2-5 | CI 여전히 없음 | ⚠️부분 — `.github/workflows/ci.yml` **커밋됨**(a2b6c832). 그러나 GitHub Actions **실행 이력 없음**(push 후 활성화 필요, gh 미설치로 원격 확인 불가). 외부 자동검증 실질 미가동 | 라운드 1 신3과 동일 — 운영 단계(push) | 🟡 인프라 | ⬜ |

### 정리
- **R2-1**은 신규(라운드 1엔 없던 구체 지적)이고 **pass2 감사의 N-2(지속/대체 미배선)와 동일 뿌리** → N-2 수정 시 함께 해소.
- **R2-2**는 1차 audit에서 이미 "수용한 한계"로 문서화한 항목.
- **R2-3/R2-4**는 라운드 1 수정의 마감 부족분(RL 표면 노출 / 편의 팩토리) — 작은 follow-up.
- **R2-5**는 코드가 아니라 push·Actions 활성화 운영 건.
