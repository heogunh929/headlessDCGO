# FR2 — 무시된 조건/술어 인자 복원 goal (permanentCondition 외)

> **계기:** FR(permanentCondition) 완료 후 재감사에서, **다른 인자로 퍼진 같은 계열의 뭉갬**(받아놓고 본문 미사용) 다수 발견. Func<> 인자 전수 스캔(scratchpad).
> **공통 종료 기준·규율:** 구현 전 **원본 1:1 확인(추측 금지)** → 소비 경로가 뷰 계층/문맥으로 술어를 평가 → `bash scripts/run-tests.sh` green + 술어-평가 테스트(좁힌 조건 매칭+비매칭 제외) + `tools/RuleAudit` 0. 뭉개면 FAIL. 불가하면 STOP+fidelity_debt.

## ✅ 이미 수정 (이 세션)
- **내 FR 회귀 2건**: `ActivatedEffectResolver`가 sink에 EngineContext 미전달(프로덕션 SET형 무효) → 전달. Delete/Prevent를 플래그-스캔으로 축소 → `EvaluateForCard` 파싱 복원(BattleDeletionGate·sink). 
- **C 특수플레이 condition**: `BlastDigivolve·BlastDNA·Jogress×2`의 `condition`을 `SpecialPlayRecipe.Condition`으로 실어 GetLegalActions가 평가(과다가용 제거). **G9-051**.
- **A 최상위 기능버그**: `Gain1MemoryTamerOwnerDigimonConditionalEffect(permanentCondition)` — 무조건 메모리 획득이었음 → permanentCondition을 `OwnerControlsMatchingDigimon` 게이트로 폴딩. **G9-051**.

## 잔여 서브goal

### FR2-A — per-card 술어 (뷰 계층으로 평가 가능)
| 팩토리(인자) | 소비 경로 | 원본 확인 필요 |
|---|---|---|
| `ChangeSecurityDigimonCardDPStaticEffect(cardCondition)` | security DP 게이트 | 어느 security 카드에 적용되는지 |
| `UseRequirements(cardCondition)` | DigivolveAction(ignore-color) | ignore-color 적용 대상/상황 |
| `DecoySelfEffect(permanentCondition)` | Decoy 키워드 | self 전용인지(vestigial?) SET인지 |
| `AddSelfDigivolutionRequirementStaticEffect(cardCondition)` | DigivolveAction | 주 permanentCondition은 이미 1:1; 이 2차 조건 역할 |

### FR2-B — per-effect / per-battle 술어 (문맥 스레딩 선결)
| 팩토리(인자) | 필요 문맥 |
|---|---|
| `CannotReturnToHandStaticEffect(cardEffectCondition: Func<ICardEffect,bool>)` | 되돌리는 **원인 ICardEffect** — 현재 sink 미보유 |
| `CanNotBeTrashedBySkillStaticEffect(cardEffectCondition)` | 〃 |
| `CanNotBeDestroyedByBattleStaticEffect(canNotBeDestroyedByBattleCondition: Func<Perm,Perm,Perm,CardSource,bool>)` | **전투 참가자**(attacker/defender) — BattleDeletionGate가 cardId만 받음 |

### FR2-C — 동적/기타
| 팩토리(인자) | 판정 |
|---|---|
| `AddSelfDigivolutionRequirementStaticEffect(costEquation: Func<int>)` | 동적 비용 무시(고정값 사용) — 실질 |
| `ChangeDPStaticEffect(effectName: Func<string>)` | cosmetic 라벨 — 무시 허용(기록만) |

## 우선순위
1. **FR2-A**(뷰 계층으로 바로 평가 가능, 원본 확인 후) → 2. **FR2-B**(문맥 스레딩: ICardEffect를 sink에, 전투참가자를 BattleDeletionGate에) → 3. **FR2-C**(costEquation).

## 실행 대화문 (복붙용)
```
FR2 진행. docs/audit/fidelity_remediation2_goals.md 스펙대로 A(per-card 술어)→B(문맥 스레딩)→C.
각 항목: 원본 DCGO에서 그 조건이 실제로 무엇을 좁히는지 1:1 확인(추측 금지) → 소비 경로가 술어를 평가하도록 배선(뷰 계층/문맥) → green + 술어-평가 테스트 + RuleAudit 0. 뭉개면 FAIL. 불가하면 STOP+fidelity_debt. 커밋은 내가 지시할 때.
```
