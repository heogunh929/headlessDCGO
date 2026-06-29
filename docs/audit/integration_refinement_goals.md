# 통합 정밀화 Goal 스펙 (교차검증 7건)

- 작성일: 2026-06-29
- 기준 커밋: `2c3e2d8c` (G6 live integration)
- 출처: GPT 지적 7건 교차검증(전부 Confirmed / Partially True, false positive 없음). 영향도 순으로 goal화.
- 공통 게이트: 각 goal 종료 시 `bash scripts/run-tests.sh` 전체 green + 통합 테스트 추가. 커밋은 사용자 지시 시.
- 관련: [live_integration_goals.md](live_integration_goals.md)(G6), [card_group_standard.md](card_group_standard.md).

## 요약 / 우선순위

| Goal | 제목 | 판정 | 우선순위 | 선행 |
|---|---|---|---|---|
| G7-001 | 장 이탈 effect unregister 전 경로 적용 | Confirmed | 🔴 정확성 | — |
| G7-002 | 카드 데이터 로더 확장(trait/진화조건/복수색) | Partially | 🟠 선결 | — |
| G7-003 | SpecialPlay action pipeline 편입(+legal-action 열거) | Confirmed | 🟠 | G7-002 |
| G7-004 | SecuritySkill 활성효과 security 경로 연결 | Confirmed | 🟠 | — |
| G7-005 | 활성효과 live deferred suspend/resume(액션 경계) | Confirmed | 🟡 | — |
| G7-006 | 추가 emit-only 전투 타이밍 배선 | Partially | 🟡 카드군 시 | — |
| G7-007 | P1 임시 테스트 → 그룹기준 통합 | Confirmed | ⚪ 정리 | — |

> 권장 순서: **G7-001(정확성 버그)** → G7-002(데이터 선결) → G7-003 → G7-004 → G7-005 → G7-006 → G7-007.

---

## G7-001 — 장 이탈 시 effect unregister 전 경로 적용
- **영역**: Runtime / 효과 등록 수명
- **판정/현황**: Confirmed. unregister(`RemoveWhere(SourceEntityId==target)`)가 `MatchStateMutationSink.cs:551`의 **Delete 핸들러에만** 존재. 바운스(ReturnToHand)·덱복귀(ReturnToDeckTop/Bottom)·시큐리티행(AddToSecurity)·비-delete Trash 경로엔 없음.
- **실제 영향**: 카드가 그 경로로 장을 떠나도 연속/트리거 바인딩이 레지스트리에 잔존 → **유령 효과(없는 카드의 DP버프/키워드)** 적용. **정확성 결함.**
- **범위/산출물**: 이탈성 zone-move kind 핸들러(ReturnToHand/ReturnToDeckTop/ReturnToDeckBottom/AddToSecurity/비-delete Trash)에 `CardEffectRegistrar.UnregisterCard`(또는 동등 RemoveWhere) 추가 — 또는 ZoneMover의 "배틀에어리어 이탈" 단일 choke point에서 일괄 처리(권장: 중복 방지). 진화 소재(under-card)는 의도적 유지(예외).
- **종료조건**: 통합 테스트 — 효과 가진 카드를 바운스/덱복귀/시큐리티행 → 해당 효과가 게이트에서 사라짐. Delete는 기존대로. 전체 green.
- **선행**: 없음.

## G7-002 — 카드 데이터 로더 확장(trait / 진화조건 / 복수색)
- **영역**: 데이터 로딩
- **판정/현황**: Partially True. 현재 로드: 타입·단일색·level·playCost·evolutionCost(첫 MemoryCost 1개)·dp·효과텍스트. **누락**: trait(`Type_ENG/Attribute_ENG/Form_ENG`), 진화조건(`EvoCosts` 색+레벨 요건 — `CardRecord.EvolutionCondition` 미채움), 복수 색상(`cardColors` 플래그).
- **실제 영향**: 진화 합법성 판정, trait 조건 효과, 다색 카드 처리 불가.
- **범위/산출물**: `tools/CardDataConverter`에 trait(Type/Attribute/Form)·`EvoCosts` 전체 리스트(색·레벨·코스트)·`cardColors` 다색 파싱 추가 → `cards.json` 확장 → `CardBaseEntityLoader`가 `CardRecord.EvolutionCondition` + metadata(traits/colors) 채움. cards.json 재생성.
- **종료조건**: 표본 카드 검증 — 다색 카드의 colors, trait 보유, 진화조건(색/레벨/코스트) 로드. 무회귀.
- **선행**: 없음(G7-003의 선결).

## G7-003 — SpecialPlay action pipeline 편입 (+ legal-action 열거)
- **영역**: Runtime / 액션
- **판정/현황**: Confirmed. `SpecialPlayAction`이 `HeadlessLegalActionDispatcher`·`MetadataActionProcessor` 어디에도 없고 `GetLegalActions`도 없음(`Create`+`ProcessAsync`만).
- **실제 영향**: 에이전트/RL이 DigiXros·DNA·Blast를 합법수로 보지 못함 → 자동 선택 불가.
- **범위/산출물**: ① `SpecialPlayAction.GetLegalActions`(카드별 DigiXros/DNA 조건→배틀에어리어 매칭 소재 조합 열거; trait/이름/레벨/색 = G7-002 데이터 활용) ② `HeadlessLegalActionDispatcher`에 concat ③ `MetadataActionProcessor`에 `HeadlessActionTypes.SpecialPlay` 라우팅(+Normalized 매핑).
- **종료조건**: 통합 테스트 — DigiXros 가능 상황에서 `GetLegalActions`가 특수플레이를 제시 + 디스패처/프로세서로 실행. 무회귀.
- **선행**: G7-002(조건 데이터).

## G7-004 — SecuritySkill 활성효과 security 경로 연결
- **영역**: 효과 해소 / 시큐리티
- **판정/현황**: Confirmed. `ActivatedEffectResolver`는 `OptionActivateAction`(OptionSkill)에서만 호출. `SecurityDelayedTriggerHook.Process`는 호출 안 함.
- **실제 영향**: ST1_12/13/14/15/16 등의 **[Security] 효과가 시큐리티 체크 시 발동 안 됨**(현재 스텁/미연결).
- **범위/산출물**: 시큐리티 체크/뒤집힘 흐름(`SecurityDelayedTriggerHook` 또는 호출부)에서 카드의 `CardEffects(EffectTiming.SecuritySkill, …)`를 `ActivatedEffectResolver`로 해소(`PlaySelfTamerSecurityEffect`/`AddActivateMainOptionSecurityEffect` 등 실제 동작으로 대체). 선택 필요한 시큐리티 효과는 G7-005 의존.
- **종료조건**: 통합 테스트 — 시큐리티에서 뒤집힌 카드의 [Security] 효과(예 ST1_13 광역 SA+1) 발동. 무회귀.
- **선행**: 선택형 시큐리티 효과는 G7-005 권장.

## G7-005 — 활성효과 live deferred suspend/resume (액션 경계)
- **영역**: 효과 해소 / 선택
- **판정/현황**: Confirmed. `DeferredChoicePendingException` catch는 효과-스케줄러 경로에만(`CardEffectSchedulerResolver` 등). 활성효과/`OptionActivateAction` 경로엔 catch도 재개 루프도 없음 → 즉답 provider만 동작.
- **실제 영향**: 사람 대화형(보류→관전 노출→응답→재개) 미배선. RL 즉답은 OK.
- **범위/산출물**: 옵션/시큐리티 액션을 "선택 대기" 상태로 반환 + `Observation.Choice` 응답 후 그 액션을 재개·완료하는 액션-레벨 suspend/resume(W7를 액션 경계로 확장). `ActivatedEffectResolver`가 `DeferredChoicePendingException`을 액션 결과로 변환.
- **종료조건**: 통합 테스트 — DeferredChoiceProvider로 옵션 활성 → 선택 보류(HasPendingChoice) → 응답 → 적용 완료. 무회귀.
- **선행**: 없음.

## G7-006 — 추가 emit-only 전투 타이밍 배선
- **영역**: 트리거
- **판정/현황**: Partially. OnAttack/OnAllyAttack은 G6-005로 배선. 전투-세부 타이밍 `OnGetDamage·OnStartBattle·OnAttackTargetChanged·OnEndBlockDesignation·OnDeclaration·OnUseDigiburst`는 **상수 정의도 emit도 없음**(설계상 보류).
- **실제 영향**: 해당 타이밍 쓰는 카드군 포팅 불가(현재 막힌 카드만).
- **범위/산출물**: 해당 타이밍을 요구하는 카드군 포팅 시 — `TriggerTimings` 상수 정의 + 전투/블록/피해 흐름에 `TriggerEventEmitter.Emit` + 테스트(점진).
- **종료조건**: 대상 타이밍을 쓰는 카드가 실매치에서 자동 발화. 무회귀.
- **선행**: 해당 카드군 식별(점진).

## G7-007 — P1 임시 테스트 → 그룹기준 통합
- **영역**: 테스트 정리
- **판정/현황**: Confirmed(커버리지 중복은 아님). `tests/P1-ST710.Port` + `P1-ST1.Red{Wave1,Triggers,Activated,TimedBuff}` 5개 잔존, `tests/CardEffect.*.Tests` 0개.
- **실제 영향**: 기능/회귀 무해. 그룹기준 미준수(프로젝트 수↑ → 빌드 비용↑).
- **범위/산출물**: 5개 P1 프로젝트를 `tests/CardEffect.ST1.Red.Tests`(ST1 적색 12장) + `tests/CardEffect.ST7.Red.Tests`(ST7_10)로 통합, P1 프로젝트 제거. (card_group_standard.md §6.)
- **종료조건**: 그룹기준 프로젝트로 동일 케이스 green + P1 제거. 전체 green.
- **선행**: 없음.

---

## 일괄 발주 블록 (복사용)
> "다음 goal들을 순서대로 진행해: G7-001 → G7-002 → G7-003 → G7-004 → G7-005 → G7-006 → G7-007. 각 goal은 docs/audit/integration_refinement_goals.md 스펙을 따르고, 종료 시 bash scripts/run-tests.sh 전체 green + 통합 테스트 추가, 커밋은 내가 지시할 때."

## Goal 인덱스 행 (docs/headless_goal_spec_index.csv 추가됨)
```
G7-001,Phase 7 - 통합 정밀화/마감,런타임,장 이탈 effect unregister 전 경로 적용,docs/audit/integration_refinement_goals.md#g7-001,,없음
G7-002,Phase 7 - 통합 정밀화/마감,데이터로딩,카드 데이터 로더 확장(trait/진화조건/복수색),docs/audit/integration_refinement_goals.md#g7-002,,없음
G7-003,Phase 7 - 통합 정밀화/마감,액션,SpecialPlay action pipeline 편입,docs/audit/integration_refinement_goals.md#g7-003,,G7-002
G7-004,Phase 7 - 통합 정밀화/마감,시큐리티,SecuritySkill 활성효과 security 경로 연결,docs/audit/integration_refinement_goals.md#g7-004,,없음
G7-005,Phase 7 - 통합 정밀화/마감,효과해소,활성효과 live deferred suspend/resume,docs/audit/integration_refinement_goals.md#g7-005,,없음
G7-006,Phase 7 - 통합 정밀화/마감,트리거,추가 emit-only 전투 타이밍 배선,docs/audit/integration_refinement_goals.md#g7-006,,없음
G7-007,Phase 7 - 통합 정밀화/마감,테스트,P1 임시 테스트 그룹기준 통합,docs/audit/integration_refinement_goals.md#g7-007,,없음
```
