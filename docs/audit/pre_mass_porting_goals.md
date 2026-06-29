# 대량 포팅 선결 Goal 스펙 (G8)

- 작성일: 2026-06-29
- 기준 커밋: `e1ba54f6` (G7 integration refinements)
- 출처: GPT P0/P1/P2 리스트 교차검증(전부 Confirmed / Partially, false positive 없음). 카드 대량 포팅 전에 막아야 할 정확성·통합 항목을 goal화.
- 공통 게이트: 각 goal 종료 시 `bash scripts/run-tests.sh` 전체 green + 통합 테스트 추가. 커밋은 사용자 지시 시.
- 관련: [integration_refinement_goals.md](integration_refinement_goals.md)(G7), [card_group_standard.md](card_group_standard.md).

## 요약 / 우선순위

| Goal | 제목 | 판정 | 우선순위 | 선행 |
|---|---|---|---|---|
| G8-001 | EvolutionCondition 파서 ↔ 실데이터 포맷 일관화 | Confirmed (잠재 버그) | 🔴 P0 최상 | — |
| G8-002 | Effect-driven PlayCard 자동 등록 | Confirmed | 🔴 P0 정확성 | — |
| G8-003 | OnStartBattle 동기 타이밍 윈도우 | Confirmed (emit-only) | 🔴 P0 정확성(잠복) | — |
| G8-004 | AddActivateMainOptionSecurityEffect 구현 | Confirmed (스텁) | 🟠 P0 완성도 | G7-004 |
| G8-005 | DeferredChoice action-level e2e (재실행 no-repay) | Confirmed | 🟠 P0 라이브/RL | G7-005 |
| G8-006 | SpecialPlay legal-action 열거 + 인코더 반영 | Confirmed | 🟠 P1 특수군 선결 | G7-002·G7-003 |
| G8-007 | GitHub Actions 최신 HEAD 확인 절차 | 정보성 | ⚪ P2 운영 | — |
| G8-008 | run-tests.sh 전체 green 로그 보관 | 사소 | ⚪ P2 운영 | — |

> 권장 순서: **G8-001 → G8-002 → G8-003**(정확성 3종) → G8-004 → G8-005 → G8-006 → G8-007/008.

---

## G8-001 — EvolutionCondition 파서 ↔ 실데이터 포맷 일관화  🔴 P0 최상
- **영역**: Runtime / 진화 검증
- **판정/현황**: Confirmed — 잠재 버그. `DigivolveAction.MatchesEvolutionCondition`(DigivolveAction.cs:361-386)은 토큰을 target의 **Id/CardNumber/CardType**과 비교(`from:Greymon` / `Digimon` 형식). 그러나 **G7-002 로더가 채운 `CardRecord.EvolutionCondition`은 `"Red@3:2;Blue@3:3"`(색@레벨:코스트)** → 파서가 못 맞춰 **모든 실데이터 진화가 거부**됨. 현 테스트는 EvolutionCondition을 비워서 가려져 있음.
- **실제 영향**: 실데이터로 카드를 진화시키면 전부 막힘 → **대량 포팅 즉시 차단**.
- **범위/산출물**:
  - `MatchesEvolutionCondition`를 `색@레벨` 형식 해석으로 확장: target의 `metadata["colors"]`에 해당 색이 있고 `metadata["level"]`이 조건 레벨과 일치하면 통과. 기존 `from:`/`definition:`/타입 토큰 경로도 유지(하위호환).
  - 진화 cost(`EvolutionCost`)와 condition을 같은 `EvoCosts`에서 일관 도출(선택한 from-색/레벨의 cost를 사용하도록 정합).
- **종료조건**: 통합 테스트 — 실데이터 카드(예: 적색 Lv4가 적색 Lv3 위로) 진화 성공, 색/레벨 불일치 진화 거부. 무회귀.

## G8-002 — Effect-driven PlayCard 자동 등록  🔴 P0 정확성
- **영역**: Effects / 효과 등록
- **판정/현황**: Confirmed — `MatchStateMutationSink.ApplyPlayCard`(:648)가 `RegisterCard`를 호출하지 않음. G6-001은 `PlayCardAction`/`DigivolveAction`/`SpecialPlayAction`만 배선.
- **실제 영향**: "이 카드를 트래시/패에서 플레이" 효과로 등장한 카드의 연속/트리거 효과가 자동 활성화 안 됨.
- **범위/산출물**: `ApplyPlayCard`가 카드 진입 후 그 카드의 효과를 등록(`CardEffectRegistrar.RegisterCard`). sink는 EngineContext가 없으므로 controller/owner를 instance에서 추출하거나 sink에 등록 훅(EffectRegistry + repos)을 전달. `PlayCardAction`과 동일한 enter-play 시맨틱.
- **종료조건**: 통합 테스트 — 효과로 플레이된 카드(예 ST7_10)의 연속/키워드가 게이트에 반영. 무회귀.

## G8-003 — OnStartBattle 동기 타이밍 윈도우  🔴 P0 정확성(잠복)
- **영역**: Runtime / 전투
- **판정/현황**: Confirmed — G7-006은 emit-only. `BattleResolver.cs:45-49`에서 OnStartBattle emit **직후 바로 `CompareBattleStats`**, 그 사이 drain/resolve 없음 → 전투 시작 효과가 DP를 바꿔도 비교에 미반영.
- **실제 영향**: "[전투 시] +DP" 류 효과가 전투 결과에 무영향(현재 그런 카드 미포팅이라 잠복).
- **범위/산출물**: emit 후 그 윈도우만 **동기 drain+resolve**(collector→scheduler.ResolveAll), 이후 참가자 DP를 `ContinuousDpGate.ResolveDp`로 **재계산**한 다음 `CompareBattleStats`. (BattleParticipant DP를 재읽기/재빌드.)
- **종료조건**: 통합 테스트 — OnStartBattle로 +DP 부여된 참가자가 비교에서 그 DP로 판정. 기존 전투 테스트 무회귀.

## G8-004 — AddActivateMainOptionSecurityEffect 구현  🟠 P0 완성도
- **영역**: 시큐리티 / 효과
- **판정/현황**: Confirmed — `CardPortingFramework.AddActivateMainOptionSecurityEffect`는 `DeferredCardEffect` 스텁. ST1_15/16의 [Security]가 동작 안 함.
- **실제 영향**: "[Security] [Main] 효과 발동" 패턴 카드(매우 흔함) 미동작.
- **범위/산출물**: G7-004(시큐리티 체크 시 `ActivatedEffectResolver`) 위에서, 카드의 `CardEffects(EffectTiming.OptionSkill, …)` 활성효과를 시큐리티 발동 경로에서 재사용하도록 연결(스텁 대체). 선택 필요 효과는 G7-005/G8-005 deferred 경로 활용.
- **종료조건**: 통합 테스트 — ST1_16 시큐리티 체크 시 [Main](상대 1삭제) 발동. 무회귀.

## G8-005 — DeferredChoice action-level e2e (재실행 no-repay)  🟠 P0 라이브/RL
- **영역**: 효과 해소 / 액션 루프
- **판정/현황**: Confirmed — G7-005는 resolver 레벨만. `OptionActivateAction`은 pending 반환만 하고, 액션 경계의 pending→`ResolveChoice`→resume(코스트/이동 비-재실행)·RL env 통합은 미배선.
- **실제 영향**: 대화형/RL 루프에서 옵션·시큐리티 선택 효과 풀-루프 미완. 즉답 provider는 동작.
- **범위/산출물**: 옵션/시큐리티 액션이 commit(코스트·이동)을 1회만 하고 effect-resolve만 재실행되도록 분리. `DcgoMatch`/`HeadlessRlEnvironment`에서 pending(`HasPendingChoice`)→드라이버 `ResolveChoice`→resume→적용 e2e.
- **종료조건**: 통합 테스트 — DeferredChoiceProvider로 옵션 활성→pending→응답→적용(코스트 1회). 무회귀.

## G8-006 — SpecialPlay legal-action 열거 + 인코더 반영  🟠 P1 특수군 선결
- **영역**: Runtime / 액션 / RL 인코딩
- **판정/현황**: Confirmed — `SpecialPlayAction.GetLegalActions`는 빈 배열, `FactoredActionEncoder`(:254-275)에 SpecialPlay 케이스 없음(`ActionEncoder`도). recipe 표현·후보 생성·인코더 미반영.
- **실제 영향**: DigiXros/DNA/Blast 카드군을 에이전트/RL이 합법수로 인식·인코딩 불가.
- **범위/산출물**: 카드별 recipe(이름/색/레벨/소재 요건) 데이터 모델 + `GetLegalActions` 후보 생성(배틀에어리어 매칭 소재 조합) + `FactoredActionEncoder`/`ActionEncoder`에 SpecialPlay 슬롯 + Validator 정합.
- **종료조건**: 통합 테스트 — DigiXros 가능 상황에서 `GetLegalActions`가 후보 제시 + 인코드/디코드 라운드트립. 무회귀.

## G8-007 — GitHub Actions 최신 HEAD 확인 절차  ⚪ P2 운영
- **영역**: CI / 운영
- **판정/현황**: 정보성 — 에이전트가 직접 불가(gh 미설치·UI 접근 불가). CI는 컴파일-only(패리티 테스트는 git-ignored `DCGO/` 필요).
- **범위/산출물**: gh CLI 설치 또는 connector로 `gh run list`/run 상태 확인 절차 문서화. (선택) `DCGO/` 비의존 테스트 서브셋만 CI 실행하도록 `ci.yml` 확장.
- **종료조건**: 최신 HEAD의 Actions run green 확인 가능. URL: https://github.com/heogunh929/headlessDCGO/actions

## G8-008 — run-tests.sh 전체 green 로그 보관  ⚪ P2 운영
- **영역**: 운영 / 기록
- **판정/현황**: 사소 — 회귀 게이트 결과 스냅샷 보관.
- **범위/산출물**: 최신 `bash scripts/run-tests.sh` 요약(`SUMMARY: PASS=N FAIL=0`)을 `docs/test-results/`에 커밋(날짜·커밋 해시 포함).
- **종료조건**: 최신 green 로그가 리포에 기록.

---

## 일괄 발주 블록 (복사용)
> "다음 goal들을 순서대로 진행해: G8-001 → G8-002 → G8-003 → G8-004 → G8-005 → G8-006 → G8-007 → G8-008. 각 goal은 docs/audit/pre_mass_porting_goals.md 스펙을 따르고, 종료 시 bash scripts/run-tests.sh 전체 green + 통합 테스트 추가, 커밋은 내가 지시할 때."

## Goal 인덱스 행 (docs/headless_goal_spec_index.csv 추가됨)
```
G8-001,Phase 8 - 대량 포팅 선결,진화,EvolutionCondition 파서 실데이터 포맷 일관화,docs/audit/pre_mass_porting_goals.md#g8-001,,없음
G8-002,Phase 8 - 대량 포팅 선결,효과등록,Effect-driven PlayCard 자동 등록,docs/audit/pre_mass_porting_goals.md#g8-002,,없음
G8-003,Phase 8 - 대량 포팅 선결,전투,OnStartBattle 동기 타이밍 윈도우,docs/audit/pre_mass_porting_goals.md#g8-003,,없음
G8-004,Phase 8 - 대량 포팅 선결,시큐리티,AddActivateMainOptionSecurityEffect 구현,docs/audit/pre_mass_porting_goals.md#g8-004,,G7-004
G8-005,Phase 8 - 대량 포팅 선결,효과해소,DeferredChoice action-level e2e,docs/audit/pre_mass_porting_goals.md#g8-005,,G7-005
G8-006,Phase 8 - 대량 포팅 선결,액션,SpecialPlay legal-action 열거+인코더,docs/audit/pre_mass_porting_goals.md#g8-006,,G7-003
G8-007,Phase 8 - 대량 포팅 선결,CI,GitHub Actions 최신 HEAD 확인,docs/audit/pre_mass_porting_goals.md#g8-007,,없음
G8-008,Phase 8 - 대량 포팅 선결,운영,run-tests.sh green 로그 보관,docs/audit/pre_mass_porting_goals.md#g8-008,,없음
```
