# BT1–BT3 선결 프리미티브 goal (공유 기반)

> **목표:** BT1·BT2·BT3 카드 포팅(원본 250장 = BT1 88 + BT2 91 + BT3 71, 현재 전부 7줄 스텁)에 **공통으로 필요한 카드-facing 프리미티브를 한 번에 신설**한다. 색상별 카드 포팅(`bt1_porting_goals.md` 등)의 *공유 선결 게이트*. 세트마다 따로 만들지 않고 BT1–3 수요의 **합집합**을 먼저 깐다.
>
> **근거(probe):** BT1–3 원본의 `CardEffectFactory.*` / `new XClass(` 사용을 전수 스캔 → 헤드리스 존재 여부 대조. 대부분 "엔진 메커니즘은 있고 카드-facing 등록 경로만 없음"(반복 패턴).

## 공통 종료 기준
- `bash scripts/run-tests.sh` 전체 green + 각 프리미티브 **격리/픽스처 테스트**(활성화·정적 동작 단언) + `tools/RuleAudit` 0.
- **AS-IS 미러**: 원본 이름·시그니처 그대로(`CardEffectFactory.<X>` / `<X>Class`). 카드-facing 술어는 엔티티-id 관용. 가드 완화·추측 = FAIL.
- **probe-first**: 각 프리미티브 신설 전 엔진에 이미 메커니즘 있는지 확인(아래 "엔진 대응"란). `Headless/**` change-control.
- 신설 위치: 카드-facing은 `CardPortingFramework.cs`/`CardEffectFactory/`에 **원본 이름으로**, 엔진 배관은 `Headless/`. 픽스처 `TestFixtures/Tfx*.cs`.

## 기준 / 현황
- 기준 HEAD `d8b9daba`(+ 미커밋 EX8_074 fix·brick 2b). 전체 **246 green, RuleAudit 0**.
- ✅ **이미 재사용 가능**(신설 불필요): `ActivateClass`(259회)→`ActivatedSelectEffect`, `ChangeSelfDPStaticEffect`/`ChangeSelfSAttackStaticEffect`, `PlaySelfTamerSecurityEffect`, `SuspendPermanentsClass`, `PierceSelfEffect`, `ChangeCostClass`, `RebootSelfStaticEffect`, `JammingSelfStaticEffect`, `BlockerSelfStaticEffect`, `DisableEffectClass`, `ChangeCardColorClass`, `CanSuspendByDigisorptionClass`, `AddSkillClass`, `ChangeBaseDPClass`, `CannotAddMemoryClass`, `CanAttackTargetDefendingPermanentClass`, `ChangeSecurityDigimonCardDPStaticEffect`, `ChangeDPStaticEffect`.

---

## 신설 선결 프리미티브 (BT1–3 합집합, 우선순위 = 사용 빈도순)

각 항목 = 1 서브goal(또는 batch). "엔진 대응" 있으면 위임, 없으면 최소 배관 신설.

### 배치 A — 액션형 (엔진 메커니즘 존재, 카드-facing 래퍼만 신설)
| 프리미티브 | 사용 | 엔진 대응 | 비고 |
|---|---|---|---|
| **`DrawClass`** | 27 | `DrawCards` 액션 / ZoneMover(Deck→Hand) | N장 드로우. 덱 고갈 규칙 기존 처리 확인 |
| **`SimplifiedSelectCardConditionClass`** | 20 | `SelectPermanentEffect`/`RevealAndSelect` | 조건부 카드 선택(존 지정) 래퍼 |
| **`DestroyPermanentsClass`** | 6 | delete/destroy 뮤테이션(`Mode.Destroy`) | 직접 삭제(비-select N) |
| **`PlayCardClass`** | 1 | `PlayCardAction` | 지정 카드 플레이 |
| **`HatchDigiEggClass`** | 1 | `HatchDigitama` 액션 | 디지타마 부화 |

### 배치 B — 키워드 self-static (기존 키워드 팩토리 관용, 신규 키워드)
| 프리미티브 | 사용 | 엔진 대응 | 비고 |
|---|---|---|---|
| **`RetaliationSelfEffect`** | 5 | 배틀 해소 훅 | <Retaliation> 키워드 |
| **`CanNotBeBlockedStaticSelfEffect`** | 4 | `BlockTiming` 게이트 | 차단 불가 |
| **`JammingStaticEffect`** | 1 | `JammingSelfStaticEffect`(self만 존재) | 대상-지정(비-self) 변형 |
| **`CanNotBeDestroyedBySkillStaticEffect`** | 1 | `CanNotBeDestroyedBySkillClass`(존재) | 정적-효과 팩토리 래퍼만 |
| **`CanNotAttackSelfStaticEffect`** | 1 | 공격 선언 게이트 | 자기 공격 불가 |

### 배치 C — 코스트/스탯/메모리 정적 팩토리
| 프리미티브 | 사용 | 엔진 대응 | 비고 |
|---|---|---|---|
| **`SetMemoryTo3TamerEffect`** | 8 | `MemoryController.Set` | 테이머 "메모리 3으로"(BT1 `SetMemoryTo`도 같은 계열로 흡수) |
| **`ChangeDigivolutionCostStaticEffect`** | 3 | `ContinuousModifierGate`(코스트) | 진화 코스트 ± |
| **`ChangeSAttackStaticEffect`** | 2 | `ChangeSelfSAttackStaticEffect`(self만) | 대상-지정 변형 |
| **`EoTLose3Memory`** | 2 | `OnEndTurn` + 메모리 delta | 턴종료 메모리 −3 |
| **`AddSelfDigivolutionRequirementStaticEffect`** | 1 | 진화 조건 게이트 | 진화 요건 추가 |
| **`PlaySelfDigimonAfterBattleSecurityEffect`** | 1 | 시큐리티 + `PlayThisCardToBattleEffect` | 배틀 후 시큐리티 자기-플레이 |

> 합계 **16 신설**(배치 A 5 · B 5 · C 6). 다수가 기존 self-변형/래퍼라 비용 낮음. BT1 단독 문서(`bt1_porting_goals.md`)의 BT1-0은 이 문서로 **대체**(Draw·SetMemoryTo·EoTLose·CanNotBeBlocked·SimplifiedSelect가 여기 합집합에 포함됨).

---

## 서브goal 순서 (빈도·의존 기반)
1. **BT-PRE-A** 액션형 5종 (Draw 최우선 — 27장)
2. **BT-PRE-B** 키워드 5종
3. **BT-PRE-C** 코스트/스탯/메모리 6종

각 배치 green 게이트 후 다음. 전부 끝나면 **BT1·BT2·BT3 색상 포팅이 이 프리미티브를 공유**하여 진행(per-set 카드 goal은 별도 문서: `bt1_porting_goals.md`, 추후 `bt2_/bt3_`).

## 진행 요약
- [ ] **BT-PRE-A** Draw · SimplifiedSelect · Destroy · PlayCard · HatchDigiEgg
- [ ] **BT-PRE-B** Retaliation · CanNotBeBlocked · Jamming(target) · CanNotBeDestroyedBySkill(factory) · CanNotAttackSelf
- [ ] **BT-PRE-C** SetMemoryTo(3)Tamer · ChangeDigivolutionCost · ChangeSAttack(target) · EoTLose(3)Memory · AddSelfDigivolutionRequirement · PlaySelfDigimonAfterBattleSecurity
- 완료 시: BT1–3 250장 포팅의 프리미티브 선결 종료 → 색상 goal 진입.

## 세트 규모 (참고)
| 세트 | 원본 .cs | 스텁 | 색상 분포(대략) |
|---|---|---|---|
| BT1 | 88 | 88 | R22 B22 Y20 G23 W1 |
| BT2 | 91 | 91 | (포팅 goal 작성 시 분해) |
| BT3 | 71 | 71 | (포팅 goal 작성 시 분해) |

## /goal 연동
`/goal BT-PRE-A` → `/goal BT-PRE-B` → `/goal BT-PRE-C`. 각 배치는 이 문서 스펙대로(원본 1:1 확인 → probe → 미러 → green + 격리 테스트 + RuleAudit 0).
