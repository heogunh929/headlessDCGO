# Phase 4 진행 전 체크리스트

- 작성일: 2026-06-26
- 목적: Phase 4(로컬 LLM이 카드 효과 3918개 포팅) **착수 전 반드시 정리할 항목**. LLM이 "효과 본문만" 작성하도록 엔진 배선·인프라를 미리 완료한다.
- 검증: 각 항목을 **실제 코드/저장소로 확인**(GPT 지적 7건 + 사전배선 조사). 본 문서 작성 시점에 소스 수정은 안 함(W1-1 트리거 배선만 선행 완료됨).
- 분류: 🔴엔진버그 / 🟠미배선(부품존재) / 🟡미구현(신규) / 🟢위생·인프라
- 관련 문서: [rl_gap_remediation_design.md](rl_gap_remediation_design.md)(RL 갭 A1~C2), [prephase4_wiring_plan.md](prephase4_wiring_plan.md)(W1~W7 배선)

## 현재 상태 (검증 시점, 2026-06-26)

- 빌드: `dotnet build src/HeadlessDCGO.Engine` — **오류 0**.
- 테스트: 전체 118개 프로젝트 중 **117 PASS / 1 FAIL**. 유일 실패는 선행 `G1E-001`(ChoiceType enum 9 vs 테스트 기대 7, 본 작업 무관·별도 칩 존재).
- 완료: RL 갭 A1~C2 + 인터페이스 검증 V + 배선 W1-1(트리거 타이밍 파생).
- 본 체크리스트 항목들은 **착수 전 미정리 상태**이며, 소스는 본 문서 작성 시점에 수정하지 않음.

---

## A. GPT 지적 7건 — 검증 결과 (전부 실제 증상 확인됨)

| # | 항목 | 검증 | 위치(근거) | 분류 |
|---|------|------|-----------|------|
| A1 | ~~DigivolutionStack 미통합~~ → **✅ 해결** | `DigivolutionStackReader`가 live `sourceIds`→타입드 스택 투영, `DigivolveAction`이 빌드·스탬프(stackDepth/stackBaseDp). 메타데이터=storage / 타입드 스택=view 결정 | `State/DigivolutionStackReader.cs`, `DigivolveAction.ProcessAsync` | 🟡→✅ B1b 완료 |
| A2 | ~~Security 효과/디지몬 전투 미구현~~ → **✅ 해결** | W4(효과 배선, subject-scoped OnSecurityCheck) + W5(시큐리티 디지몬 전투, DP 비교·공격자 삭제·StopSecurityCheck) | `SecurityResolver.cs` | 🟠+🟡 → ✅ W4·W5 완료 |
| A3 | ~~Blocker 선택 후 suspend 누락~~ → **✅ 해결** | `ResolveBlockChoice`가 `SelectBlocker` 후 블로커 suspend + OnBlockAnyone 발행 | `BlockTiming.SuspendBlocker`/`ResolveBlockChoice` | 🔴→✅ 엔진버그 수정 |
| A4 | ~~Strict effect gate 없음~~ → **✅ 해결** | `CardEffectSchedulerResolver.Create(strictUnbound:true)` → 미바인딩 효과를 Failure(strictUnbound 마커)로 | `CardEffectSchedulerResolver.cs` | 🟢→✅ strict 게이트 추가 |
| A5 | ~~factored mask가 기본 step result에 없음~~ → **✅ 해결** | `RlStepResult`에 `FactoredActionMask`/`FactoredActionMaskVector` 추가, `Encode`가 동일 legal-action 집합으로 빌드 | `RlStepResult.cs`, `HeadlessRlEnvironment.Encode` | 🟠→✅ RL 인터페이스 완성 |
| A6 | **Repository hygiene** | ✅실재 — `.gitignore`가 `/DCGO/`만 무시. **bin/obj 3709개 + .tmp 323개 = 산출물 4000+개 git 추적** | `.gitignore`(11B), `git ls-files` | 🟢위생 |
| A7 | **CI 없음** | ✅실재 — `.github` 디렉터리 **자체가 없음**(workflow 0). gh CLI 미설치 → 자동 검증 파이프라인 부재 | `.github` 없음 | 🟢인프라 |

---

## B. 사전 배선 로드맵 (효과 인프라, `prephase4_wiring_plan.md` 연동)

| # | 항목 | 상태 | 분류 |
|---|------|------|------|
| W1 | 트리거 타이밍 emission+매핑 | ◐ W1-1 완료(존전이/공격 파생). **남음: 턴시작/종료·진화완료·시큐리티체크 이벤트 발행** | 🟠 |
| W2 | Mutation sink 어휘 확장 (이동/DP/부여/제한 + 드롭 3종 Blitz/Retaliation/ArmorPurge) | ⬜ 미착수 | 🟠 |
| W3 | B-01 lookup player-1 하드코딩 제거 | ⬜ 미착수 (obsolete 오버로드 잔존) | 🔴 |
| W4 | 시큐리티 효과 배선 (=A2 일부) | ✅ 완료 — subject-scoped OnSecurityCheck | 🟠 |
| W5 | 시큐리티 디지몬 전투 (=A2 일부) | ⬜ | 🟡 |
| W6 | Counter 페이즈 | ⬜ | 🟡 |
| W7 | Effect-driven choice (DeferredChoiceProvider) | ⬜ | 🟠 |

---

## C. Phase 4 착수 게이트 — 우선순위별 체크리스트

### 🔴 MUST (안 하면 LLM이 엔진 구조를 건드려야 하거나, 포팅해도 발동 안 됨)
- [x] **W1 완성** — ✅ 완료(2026-06-26). W1-1 존전이/공격 파생 + W1-2 턴/진화/드로우/시큐리티 **이벤트 발행**. 카드가 OnPlay/OnDeletion/OnStartTurn/OnEndTurn/WhenDigivolving/OnDraw/OnSecurityCheck 등에 바인딩하면 자동 발화. (E2E 검증됨)
- [x] **W2 Mutation 어휘 확장** — ✅ 완료(2026-06-26). W2-core(DP/suspend/flag/키워드) + W2-follow(이동/드로우/메모리, FlushAsync). LLM이 emit하면 모두 상태 반영.
- [x] **W3 B-01 하드코딩 제거** — ✅ 완료(2026-06-26). player-1 하드코딩 오버로드 제거, 모든 lookup이 controllerId 명시.
- [x] **A1 DigivolutionStack 통합** — ✅ 완료(2026-06-26). 결정: **`sourceIds` 메타데이터가 저장소(storage), 타입드 `DigivolutionStack`이 그 위의 view**. 신규 `DigivolutionStackReader`가 live `sourceIds`를 타입드 스택(DigiEgg..Top 순서·BaseDp·UnderCards)으로 투영하고, `DigivolveAction`이 이를 빌드·검증해 `stackDepth`/`stackBaseDp`를 스탬프. 엔진/배틀/효과가 단일 타입드 API로 스택 접근.
- [x] **W4+A2 시큐리티 효과 배선** — ✅ 완료(2026-06-26). 공개된 시큐리티 카드의 `[Security]`(OnSecurityCheck) 효과가 공용 루프로 발동. `TriggerEventEmitter`가 **subject 카드로 타이밍 윈도우를 스코핑**(W4)해 공개된 카드의 효과만 발동(다른 카드의 OnSecurityCheck 효과는 휴면). WhenDigivolving도 진화한 카드로 스코핑됨.

### 🟠 SHOULD (정확성/완결성, Phase 4 중 카드가 의존)
- [x] **A3 Blocker suspend 버그 수정** — ✅ 완료(2026-06-26). 블록 시 블로커 **suspend** + **OnBlockAnyone** 타이밍 윈도우(블로커 스코프) 발행. AS-IS `SwitchDefender`(Tap + StackSkillInfos(OnBlockAnyone)) 충실 재현.
- [x] **W5 시큐리티 디지몬 전투** — ✅ 완료(2026-06-27). 공개된 시큐리티 디지몬이 공격자와 전투; 공격자 DP ≤ 시큐리티 DP면 공격자 삭제(equal=상호)·시큐리티 체크 중단(AS-IS StopSecurityCheck). `preventBattleDeletion`(Jamming) 보호. DP 미정의면 전투 스킵(BattleResolver와 동일). Jamming surface 활성화.
- [x] **W7 Effect-driven choice** — ✅ 완료(2026-06-27). `DeferredChoiceProvider` — 효과가 미응답 선택을 요청하면 ChoiceController에 pending 등록(A2로 surface) + `Suspended` 결과로 효과를 큐에 유지. 에이전트 응답 후 효과 재실행(누적 답변 replay)→완료. 다중 선택 지원. 계약: choose-then-apply.
- [x] **A5 factored mask를 RlStepResult에 포함** — ✅ 완료(2026-06-27). 모든 `RlStepResult`가 `FactoredActionMask`(+`FactoredActionMaskVector`) 보유. 타입기반 ActionMask와 동일 legal-action 집합으로 빌드(일관). 스키마는 `HeadlessRlEnvironmentOptions.FactoredActionSchema`로 설정. 별도 `EncodeFactoredActionMask()` 호출 불필요.

### 🟡 NICE (있으면 좋음, Phase 4와 병행/이후 가능)
- [x] **W6 Counter 페이즈** — ✅ 완료(2026-06-27). 공격당 1회 블록 전 `OnCounterTiming` 윈도우 발행(글로벌, AS-IS State=Counter→CounterTiming→Block). 카드가 OnCounterTiming 바인딩 시 발동.
- [x] **A4 Strict effect gate** — ✅ 완료(2026-06-27). `CardEffectSchedulerResolver.Create(strictUnbound: true)` 시 미바인딩 효과를 **Failure**(`strictUnbound` 마커)로 처리 → 커버리지 갭 즉시 검출. 기본(프로덕션)은 Unbound 드레인 유지.

### 🟢 위생/인프라 (Phase 4 전에 정리 권장 — LLM 작업 품질에 직접 영향)
- [x] **A6 .gitignore 정리** — ✅ 완료(2026-06-26). `bin/obj/.tmp/.dotnet/.vs/.idea` 추가, 추적 산출물 **8849개 untrack**(staged, working 파일 보존), `.gitattributes`로 줄바꿈 고정. 추적 22191→13346.
- [x] **A7 CI 추가** — ✅ 완료. `.github/workflows/ci.yml` **빌드 게이트**(엔진+테스트 전체 컴파일; DCGO gitignore라 실행은 로컬). 로컬 풀 스위트 러너 `scripts/run-tests.{sh,ps1}` 추가. *주의: 커밋·푸시 후 GitHub Actions 활성화돼야 동작.*
- [ ] **G1E-001 선행 실패 정리** — `ChoiceType` enum(9개) vs 테스트 기대(7개) 불일치 수정(별도 칩 존재).

---

## D. 권장 진행 순서

```
1. 위생 먼저 (A6 .gitignore, A7 CI)  ← LLM 작업 환경/안전망부터
2. W1완성 → W2 → W3              ← 트리거·mutation·바인딩 (효과 발동의 3대 토대)
3. A1(stack) → W4/A2 → A3(blocker) ← 진화/시큐리티/블록 규칙
4. W5 → W7 → A5                  ← 시큐리티전투/choice/RL결과
5. W6, A4                        ← 카운터/strict (병행 가능)
6. → Phase 4 카드 포팅 착수
```

**핵심**: 🔴MUST + 🟢위생을 끝내면 "LLM이 카드 효과 본문만 작성 → 자동 발동·반영·검증"이 성립합니다. 🟠/🟡는 해당 카드군 포팅 시점에 맞춰 채워도 됩니다.
