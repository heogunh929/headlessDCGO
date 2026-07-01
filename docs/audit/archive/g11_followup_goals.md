# G11 후속 goal 리스트 (검증·정밀화)

- 작성일: 2026-06-30
- 출처: 4개 후속 항목(합법수 경계·RL deferred e2e·진화비용·once/gate). 1·3은 마무리 검증, 2·4가 실질 신규(엔진-통합).
- 공통 종료조건: 해당 테스트 green + 전체 `bash scripts/run-tests.sh` green. 커밋은 사용자 지시 시.
- 권장 순서: G11-001 → G11-003 → G11-004 → G11-002 (쉬움/검증 → 엔진-통합 큰 것).

| Goal | 내용 | 성격 | 난이도 | 상태 |
|---|---|---|---|---|
| `G11-001` | SpecialPlay 합법수 경계 + 위조(crafted) 액션 차단 | 마무리 검증 | 낮음 | ✅ 완료 |
| `G11-003` | Red@3:2 / Blue@3:3 진화비용 케이스 | 테스트 보강 | 낮음 | ✅ 완료 |
| `G11-004` | Triggered once/gate 소비 순서 정밀화 | 신규(엔진-통합) | 중 | ✅ 완료 |
| `G11-002` | RL 환경 deferred-choice 풀 루프 e2e | 신규(엔진-통합, 활성화 루프) | 상 | ✅ 완료 |

## 진행 상태 (2026-06-30)
- **G11-001 완료**: `tests/G3.5-RL-A1.ActionLegality.Tests`에 `CraftedSpecialPlayRejectedWithoutStateChange` — 위조 SpecialPlay가 boundary에서 거부+상태 무변경. (221/221)
- **G11-003 완료**: `tests/G8-001.*`에 Red@3:2(2 legal/3 illegal)·Blue@3:3(3 legal/2 illegal) per-target 비용 케이스. (221/221)
- **G11-004 완료**: `GameFlowProcessor`가 트리거 request에 이벤트 subject를 enrich + **게이트(CanResolve) 통과 시에만 OnceFlag 소비**. `tests/G11-004.OnceGatePrecision.Tests` — 게이트 실패 시 once 미소진/캡 유지(실제 GameFlowProcessor 경유). (221/221)
- **G11-002 완료**: 활성화 풀 루프 실배선. `DeferredActivationController`(EngineContext)가 suspend된 activation(card/timing/player) 보관 → `OptionActivateAction`이 suspend 시 등록 → `MetadataActionProcessor.ResolveChoiceAsync` 기본 경로가 choice resolve 후 **`ActivatedEffectResolver.ResolveAsync`를 재호출**(OptionActivateAction 재실행 X = 재지불 X). `DeferredChoiceProvider`가 답 재생, sink는 suspend 시 미flush라 commit-once. `tests/G11-002.RlDeferredChoiceE2E.Tests` — ST2_16 옵션을 RL env에서 활성→pending(코스트 1회)→ResolveChoice→바운스 적용+재지불 없음. (222/222). **fidelity_debt.md "활성화 풀 루프" 최대 갭 해소**(옵션-활성 deferred 경로).

---

## `G11-001` — SpecialPlay 합법수 경계 + crafted 차단
**현황**: `LegalActionSetValidator.AgentFacingTypes`에 `NormalizedSpecialPlay` 이미 포함(커밋 610258ae). dispatcher도 Main phase에서 SpecialPlay 산출.
**작업**: **위조(crafted) SpecialPlay 차단**을 하드닝 — dispatcher가 산출하지 않은 가짜 SpecialPlay 액션을 apply 시 거부하고 **상태 무변경**임을 단언(기존 `tests/G3.5-RL-A1.ActionLegality.Tests` 패턴: InvalidAction 이벤트 + phase/legal-set 불변).
**종료**: 위조-SpecialPlay 거부 테스트 + 전체 green.

## `G11-003` — Red@3:2 / Blue@3:3 진화비용
**현황**: G8-001로 `색@레벨:코스트` 파서 구현·기본 검증(`DigivolveAction.MatchesEvolutionCondition` + `DigivolutionCostHelpers`).
**작업**: 두 구체 케이스 명시 추가 — (a) Red 레벨3 Digimon에서 진화조건 `Red@3:2` 매칭 + 코스트 2 해석, (b) Blue 레벨3에서 `Blue@3:3` 매칭 + 코스트 3. 색 불일치/레벨 불일치 음성 케이스 포함.
**종료**: 진화비용 케이스 테스트(`tests/G8-001.*` 보강 또는 신규) + 전체 green.

## `G11-004` — Triggered once/gate 소비 순서 정밀화
**문제(G10-002에서 플래그)**: 라이브 `GameFlowProcessor`가 `OnceFlags.TryActivate`를 효과의 `CanResolve`(0DP·상대 판정 등) **전에** 호출 → **게이트 미충족(매칭 안 되는 격파)에도 once-per-turn이 소진**될 수 있음. 원본은 "조건 충족 시에만 1회 카운트".
**작업**: once-per-turn 소비를 **효과의 전체 게이트 통과 시에만** 발생하도록 정밀화 — `GameFlowProcessor`가 `CanResolve`(또는 trigger-gate)를 먼저 평가 후 통과 시에만 `TryActivate`, 또는 효과가 게이트 통과 후 once를 등록. `TriggeredUnsuspendSelfEffect`/`TriggeredSelfDpBuffEffect`(+`TriggeredMemoryEffect`)에 적용.
**종료**: 테스트 — 같은 턴에 **매칭 안 되는 격파(비0DP/내 디지몬)는 once를 소진하지 않아**, 이어지는 매칭 격파가 정상 발동. + 전체 green.

## `G11-002` — RL 환경 deferred-choice 풀 루프 e2e
**갭(fidelity_debt.md 별도 트랙)**: 활성효과가 `IHeadlessCardEffect.ResolveAsync`에 choice provider 미주입이라 **실게임 루프에서 자동 발동 안 됨**(~15장). 현재 deferred suspend/resume는 resolver 레벨(명령형)만 검증(G7-005/G8-005).
**작업**: `HeadlessRlEnvironment.StepAsync` 관통 e2e — 옵션/선택 활성효과를 환경 액션으로 발동 → 환경이 **pending-choice** 상태 노출 → 에이전트가 `ResolveChoice` 제출 → 효과 재개. **commit-once / no re-pay / resume 정확성** 단언. 필요한 만큼 `ResolveAsync`에 provider 주입 또는 deferred-choice coordinator를 step 루프에 배선.
**종료**: `tests/G11-002.RlDeferredChoiceE2E.Tests`(신규) green + 전체 green. (이게 활성화 풀 루프의 핵심 — 완료 시 활성효과 다수가 실루프 발동 가능.)

## 진행 메모
- G11-002·004는 같은 뿌리(활성효과·트리거의 **실게임 루프** 정확 발동/카운트). 004 먼저 하면 002의 once-소비 정합도 자연히 맞물림.
- 1·3 완료 시 G8-006/G8-001가 "검증까지" 닫힘.
