# 라이브 통합 Goal 스펙 (잔여 배선 6종)

- 작성일: 2026-06-29
- 목적: "효과 *로직*은 구현·검증됐으나 풀게임 루프에 자동 배선되지 않은" 잔여 항목 5개를 **한 번에 발주 가능한 goal**로 명세. 각 goal은 단독 실행 가능하며 done 조건은 테스트로 검증.
- 공통 게이트: 각 goal 종료 시 `bash scripts/run-tests.sh` 전체 green(무회귀) + 해당 통합 테스트 추가.
- 관련 문서: [card_porting_recipe.md](card_porting_recipe.md) §5(갭), [card_group_standard.md](card_group_standard.md), [development_roadmap.md](development_roadmap.md).

## 요약 / 우선순위 / 상태

| Goal | 제목 | 우선순위 | 선행 | 상태 |
|---|---|---|---|---|
| G6-001 | 런타임 자동 등록(enter-play) 배선 | 🔴 최우선 | — | ✅ 완료 (`tests/G6-001.AutoRegisterOnEnterPlay.Tests`) |
| G6-002 | Option/Security 활성효과 live choice 배선 | 🔴 최우선 | G6-001 | ✅ 완료 (`tests/G6-002.OptionActivatedChoice.Tests`) |
| G6-003 | CardBaseEntity → JSON 카드 데이터 로더 | 🟠 대량포팅 선결 | — | ✅ 완료 (`tools/CardDataConverter`, `tests/G6-003.CardDataLoader.Tests`) |
| G6-004 | 특수 플레이 액션(DigiXros/DNA/Blast) 연결 | 🟡 카드군 시 | G6-001·002 | ✅ 완료 (`tests/G6-004.SpecialPlayAction.Tests`) |
| G6-005 | emit-only 트리거 타이밍 실발화 | 🟡 카드군 시 | G6-001 | ✅ 완료 (`tests/G6-005.EmitOnlyTimings.Tests`) |

> **전부 완료(2026-06-29). 전체 스위트 209/209 green.** 잔여 정밀화: ① 활성효과 라이브 deferred suspend/resume(액션 경계) — 현재 `context.ChoiceProvider`(scripted/즉답) 경로는 동작; ② 특수 플레이 legal-action 열거(카드별 DigiXros/DNA 조건 데이터 필요); ③ OnAllyAttack 외 추가 emit-only 타이밍(카드군 요구 시); ④ 시큐리티 카드 DP게이트 실전 호출.

> 일괄 발주 권장 순서: **G6-001 → G6-002**(RL 셀프플레이 길목) → **G6-003**(대량 포팅 선결) → G6-004/005(해당 카드군 포팅 시 병행).

---

## G6-001 — 런타임 자동 등록(enter-play) 배선
- **영역**: Runtime / 효과 등록
- **현황**: 카드 효과는 수동 `CardEffectRegistrar.RegisterOnEnterPlay(...)` 호출로만 등록됨. 카드가 장에 들어올 때 자동 등록하는 훅이 게임 루프에 없음.
- **목표**: 카드가 배틀에어리어/진화로 진입할 때 그 카드의 `CEntity_Effect` 효과가 자동으로 `EffectRegistry`에 등록되고, 이탈 시 제거된다.
- **범위/산출물**
  - `CardEffectDispatch`(또는 동등) 레지스트리: `cardNumber → CEntity_Effect` 매핑. 포팅된 카드가 자기 자신을 등록(또는 중앙 등록 테이블).
  - `PlayCardAction`·`DigivolveAction`·기타 장-진입 경로에서 진입 직후 디스패치 조회 → `RegisterOnEnterPlay(context, effect, cardNumber, new CardSource(...))` 호출. **가드: 디스패치에 없는 카드는 무영향**(미포팅 카드 안전).
  - 장 이탈(삭제/바운스/진화 소멸) 시 해당 카드 바인딩 제거(`EffectRegistry.RemoveWhere`) + `EffectDuration` 수명 정합.
  - 활성효과(`IActivatedCardEffect`)는 자동 등록 제외(현 규칙 유지).
- **종료조건**: 통합 테스트 — `PlayCardAction`으로 카드를 내면 연속/트리거 효과가 자동 활성(예: ST7_10 Pierce/SA+1, ST1_03 상속 DP가 매치 상태에서 게이트로 반영). 카드 이탈 시 효과 사라짐. 전체 스위트 green.
- **선행**: 없음.

## G6-002 — Option/Security 활성효과 live choice 배선
- **영역**: 효과 해소 / 선택(Choice)
- **현황**: 활성효과(선택→삭제/버프; ST1_13/14/15/16, ST1_08)는 명령형(BuildRequest→scripted answer→Apply)으로만 검증. `IHeadlessCardEffect.ResolveAsync(context, mutations)`에 choice provider가 없어 대화형 풀-루프 미배선.
- **목표**: 옵션/시큐리티 활성효과가 해소 중 선택을 요청(suspend)하고, 드라이버/에이전트가 `Observation.Choice`로 고른 뒤 재개되어 적용되는 end-to-end.
- **범위/산출물**
  - 효과 본체에서 choice provider 접근 경로: `ResolveAsync`에 provider 주입(시그니처 확장) 또는 sink/context를 통한 접근, 혹은 활성효과 전용 resolve 경로. `DeferredChoiceProvider`/`IHeadlessChoiceController`·`DeferredChoicePendingException`(W7 suspend/resume)와 연동.
  - `ActivatedSelectEffect`/`ActivatedTargetBuffEffect`/`ActivatedPlayerScopeBuffEffect`를 `BuildRequest → ChooseAsync(suspend) → Apply/ApplyBuff`를 본체에서 수행하는 형태로 전환(또는 어댑터).
  - `OptionActivateAction`(옵션 카드 사용)·시큐리티 활성화 흐름(`SecurityDelayedTriggerHook`)이 카드의 활성효과를 enqueue→resolve하도록 연결.
- **종료조건**: 통합 테스트 — 옵션 카드 사용 → 선택 제시(`HasPendingChoice`/`Observation.Choice`) → 행동으로 선택 → 상대 디지몬 삭제(ST1_16) / 선택 디지몬 +3000DP(ST1_13) 적용. 시큐리티 발동 경로도 1건. 전체 green.
- **선행**: G6-001 권장.

## G6-003 — CardBaseEntity → JSON 카드 데이터 로더
- **영역**: 데이터 로딩 (로드맵 Phase 2)
- **현황**: `CardDatabase`는 수동 Upsert만. 카드 스탯/조건을 실데이터로 로딩하는 기능 없음(테스트가 CardRecord 손수 생성).
- **목표**: 전 카드의 스탯/조건을 실데이터로 로딩.
- **범위/산출물**
  - 원본 `DCGO/Assets/CardBaseEntity/.../*.asset`(CardName_ENG·EffectDiscription_ENG·color·level·DP·playCost·evolutionCost·trait·evolutionCondition) → 헤드리스 JSON 변환기(오프라인 스크립트).
  - `CardDatabase` JSON/파일 로더 구현(수동 Upsert 대체). `CardRecord` 필드 채움.
- **종료조건**: 전 카드 `CardRecord`가 실데이터로 로드되고, 기존 스위트 무회귀, 표본 카드 스탯 검증 테스트(이름/색/DP/코스트) green.
- **선행**: 없음.

## G6-004 — 특수 플레이 액션(DigiXros / DNA / Blast) 연결
- **영역**: Runtime / 액션
- **현황**: D-5(DNA·DigiXros)·D-6(Blast·Arts) 헬퍼는 구현·테스트됨. 플레이어가 그 방식으로 **플레이하는 legal-action/프로세서가 없음**.
- **목표**: DigiXros/DNA/Blast를 행동(legal action)으로 선택·실행.
- **범위/산출물**
  - DigiXros/DNA 플레이: legal-action 열거(소재 후보) + 프로세서(소재 선택 + 코스트 지불 + `FusionDigivolveHelpers.FuseAsync`).
  - Blast/Arts: `FreeDigivolveHelpers.DigivolveFreeAsync` 행동 배선.
  - validate + `GetLegalActions` 노출 + Observation 반영.
- **종료조건**: 통합 테스트 — 대표 카드(예 BT10_012 Shoutmon X4B, DigiXros)를 해당 방식으로 플레이해 소재가 밑으로 들어가고 코스트가 처리됨. 전체 green.
- **선행**: G6-001·G6-002 권장, G6-003(스탯) 권장.

## G6-005 — emit-only 트리거 타이밍 실발화
- **영역**: 트리거
- **현황**: 일부 타이밍이 정의만 되고 게임 루프가 emit하지 않음(예 `OnAllyAttack` — ST1_06 "[공격시] 메모리 -2"가 실전 미발화; 본체/조건은 직접 resolve로 검증).
- **목표**: 카드가 요구하는 미발화 타이밍을 해당 흐름에서 실제 `TriggerEventEmitter.Emit`.
- **범위/산출물**: OnAllyAttack, OnGetDamage, OnAttackTargetChanged, OnEndBlockDesignation, OnDetermineDoSecurityCheck, OnDeclaration, OnStartBattle(동기 윈도우) 등(로드맵 Phase 4 목록)을 카드군이 실제 요구할 때 배선.
- **종료조건**: 통합 테스트 — 해당 타이밍을 쓰는 카드(예 ST1_06)가 실매치에서 자동 발화. 전체 green.
- **선행**: G6-001.

---

## 일괄 발주 블록 (복사용)
> "다음 goal들을 순서대로 진행해: G6-001(런타임 자동 등록) → G6-002(활성효과 live choice) → G6-003(카드 데이터 로더) → G6-004(특수 플레이 액션) → G6-005(emit-only 타이밍). 각 goal은 docs/audit/live_integration_goals.md 스펙을 따르고, 종료 시 bash scripts/run-tests.sh 전체 green + 통합 테스트 추가, 커밋은 내가 지시할 때."

## Goal 인덱스 행 (docs/headless_goal_spec_index.csv 추가됨)
```
G6-001,Phase 6 - 라이브 통합/배선,런타임,런타임 자동 등록(enter-play) 배선,docs/audit/live_integration_goals.md#g6-001,,없음
G6-002,Phase 6 - 라이브 통합/배선,효과해소,Option/Security 활성효과 live choice 배선,docs/audit/live_integration_goals.md#g6-002,,G6-001
G6-003,Phase 6 - 라이브 통합/배선,데이터로딩,CardBaseEntity→JSON 카드 데이터 로더,docs/audit/live_integration_goals.md#g6-003,,없음
G6-004,Phase 6 - 라이브 통합/배선,액션,특수 플레이 액션(DigiXros/DNA/Blast) 연결,docs/audit/live_integration_goals.md#g6-004,,G6-001
G6-005,Phase 6 - 라이브 통합/배선,트리거,emit-only 트리거 타이밍 실발화,docs/audit/live_integration_goals.md#g6-005,,G6-001
```
