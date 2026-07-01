# G12 후속 goal 리스트 (실루프 마무리)

- 작성일: 2026-06-30
- 출처: 4개 후속 항목(스위트 로그 / multi-choice e2e / cross-card 트리거 / 시큐리티 deferred). 2·3·4는 fidelity_debt.md "엔진 통합 갭" 잔여 3종 — 활성효과·트리거를 실게임 루프에서 완전히 굴리는 마무리.
- 공통 종료조건: 해당 동작을 **실제로 단언하는 테스트** + 전체 `bash scripts/run-tests.sh` green. 커밋은 사용자 지시 시.
- 권장 순서: G12-001 → G12-002 → G12-003 → G12-004 (문서/테스트만 → 엔진-통합).

| Goal | 내용 | 성격 | 난이도 |
|---|---|---|---|
| `G12-001` | latest_full_suite.md를 222/222 기준으로 갱신 | 문서 | 낮음 |
| `G12-002` | multi-choice activation e2e | 테스트만(배선 기존) | 낮음~중 |
| `G12-003` | ST3_01/ST3_04 live cross-card trigger | 신규 엔진+테스트 | 중~상 |
| `G12-004` | SecuritySkill deferred choice e2e | 신규 엔진+테스트 | 상 |

---

## `G12-001` — latest_full_suite.md 갱신
**현황**: `docs/test-results/latest_full_suite.md`가 `217/217 @ e1ba54f6`(G7 시절, 낡음)로 기록.
**작업**: 현재 `222/222 @ <push된 HEAD>`(날짜·커밋 해시)로 갱신. 직전 마일스톤 줄도 G10/G11/G12 반영.
**종료**: 문서가 최신 전체 스위트(222/222)와 일치.

## `G12-002` — multi-choice activation e2e
**현황**: 재개 훅이 re-suspend를 이미 처리(`MetadataActionProcessor.ResolveChoiceAsync` line ~613, "activation awaiting further choice")하고 `DeferredChoiceProvider`가 이전 답 누적 재생 — multi-choice 배선은 G11-002에 존재.
**작업**: 한 활성효과가 **2회 이상** choice를 요구하는 e2e — 활성→1차 ResolveChoice(여전히 pending)→2차 ResolveChoice→완료. **commit-once / no re-pay / 두 선택 모두 반영** 단언(HeadlessRlEnvironment 또는 DcgoMatch). 예: 선택을 2번 하는 옵션 효과(필요 시 테스트용 효과 또는 2-타깃 카드).
**종료**: multi-choice e2e green(배선 버그 있으면 수정). 전체 green.

## `G12-003` — ST3_01/ST3_04 live cross-card trigger
**문제**: OnDeletion 이벤트가 `sourceEntityId = 삭제 카드`를 세팅(`TriggerEventEmitter`)하고 `MatchesEvent`가 `effect.SourceEntityId != sourceEntityId` 리스너를 필터아웃(`AutoProcessingTriggerCollector:215`). → ST3_01/04(삭제된 *상대* 카드를 듣는, 자기 SourceEntityId가 다른 효과)는 **라이브에서 안 터짐**. (G11-004 enrich는 필터 통과 *후*에만 동작.)
**작업**: "Anyone" 타이밍(OnDestroyedAnyone 등)은 subject를 **필터-비대상 키**로 전달하도록 emit/match 수정(self-scoped 트리거 W4는 기존 sourceEntityId 필터 유지). 그러면 enrich가 삭제 subject를 트리거 컨텍스트에 실어 ST3_01/04 게이트가 라이브 평가됨. 실제 격파 플로우(0DP sweep / 전투 격파) → ST3_01 자신 DP+1000 / ST3_04 메모리+1 e2e.
**종료**: 상대 0DP 격파 시 ST3_01/04가 라이브 발동(자멸·비0DP엔 무) e2e. 전체 green.

## `G12-004` — SecuritySkill deferred choice e2e
**현황**: G11-002의 `DeferredActivations.Suspend`는 `OptionActivateAction`에만 배선. `SecurityResolver`는 `ActivatedEffectResolver`를 호출하나 deferred activation 미등록 → 시큐리티 활성효과가 suspend되면 재개 불가.
**작업**: SecurityResolver(시큐리티 체크 루프)에서 SecuritySkill 활성효과가 suspend될 때 deferred activation 등록(card/SecuritySkill/player) → ResolveChoice가 재개. 시큐리티-체크 루프가 전투 해소 *중*에 도므로 suspend→pause→resume 경계 처리 필요. 시큐리티에서 공개된 선택형 효과 e2e(예: ST2_14/ST3_15 [Security] 선택).
**종료**: 시큐리티 활성효과의 deferred suspend/resume e2e(재지불 없음). 전체 green.

## 진행 메모
- G12-002~004 완료 시 fidelity_debt.md "엔진 통합 갭" 트랙이 거의 닫힘(활성효과·트리거 실루프 완전 발동).
- G12-003은 트리거 emit/match의 self-scoped vs anyone-scoped 구분이 핵심 — 회귀 주의(전체 트리거 영향).
