# 프리미티브 선행개발 마스터 백로그 (전 DCGO)

> **개발 롤(재정의):** 카드 포팅은 추후 **로컬 모델**이 수행한다. 로컬 모델의 작업이 "기계적 채우기 = 확정적"이 되도록, **카드가 호출하는 카드-facing 프리미티브를 미리(선행) 전부 만들어 둔다.** 카드별 on-demand가 아니라, 원본 전체 수요를 스캔한 **유한 집합을 앞에서 소진**한다.
>
> **근거(전수 스캔):** `DCGO/Assets/Scripts/CardEffect/**`(BT1–25·EX1–12·ST1–24·P·LM·RB1·AD1 = **3918 카드**)의 `CardEffectFactory.*` / `new *Class(` 사용을 전수 집계 → **고유 프리미티브 157종**. 헤드리스 대조: **EXISTS 67 / MISSING 90**. 이 90종이 백로그다. (대조는 휴리스틱 — 개발 시 각 항목 probe로 확정.)

## 종료 기준(각 프리미티브 공통)
- `bash scripts/run-tests.sh` 전체 green + **격리/픽스처 테스트로 동작 단언** + `tools/RuleAudit` 0.
- **AS-IS 미러**: 원본 이름·시그니처 그대로. 카드-facing 술어는 엔티티-id 관용. 가드 완화·추측 = FAIL.
- **probe-first**: 엔진 메커니즘 재사용(대부분 "엔진 있음 / 카드-facing 없음"). `Headless/**` change-control. 없는 메커니즘은 **BT/실 카드 수요 범위만**, 나머지 분기는 NotSupported 명시.
- 신설 위치: 카드-facing은 `CardPortingFramework.cs` / `CardEffectFactory`(원본 이름), 엔진 배관은 `Headless/`. 픽스처 `TestFixtures/Tfx*.cs`.

## ✅ 전 웨이브 완료 (2026-07-01)
> **MISSING 90 소진 완료 — 로컬 모델 카드 포팅 진입 가능.** 전체 **274 green, RuleAudit 0**.
>
> | 웨이브 | 프리미티브 | 문서 |
> |---|---|---|
> | BT-PRE-A | 5 (액션형) | (archive/bt_pre_a_goals.md) |
> | W1 | 6 (진화 기반) | [primitive_w1_goals.md](primitive_w1_goals.md) |
> | W2 | 20 (고빈도) | [primitive_w2_goals.md](primitive_w2_goals.md) |
> | W3 | 27 (중빈도) | [primitive_w3_goals.md](primitive_w3_goals.md) |
> | W4 | 30 (저빈도+FW/타이밍) | [primitive_w4_goals.md](primitive_w4_goals.md) |
> | **누적** | **88** | |
>
> 분류: behavior-live 다수 · preemptive-seal(grant live, 소비자 latent) 일부 · 분류-제외 2(DigiXros 데이터 config·ExtendActivate per-card). 상세: [fidelity_debt.md](fidelity_debt.md).

## 기준 / 현황 (이력)
- HEAD `d8b9daba`(+ 미커밋: run-tests 튜닝·EX8_074 fix·brick2b·BT 문서·BT-PRE-A). 전체 **251 green, RuleAudit 0**.
- ✅ **완료**: BT-PRE-A 5종(Draw·SimplifiedSelect·Destroy·Hatch·PlayCard) — 아래 목록에서 제외.

---

## MISSING 90 — 카테고리별 (사용빈도 = 우선순위 신호)

### G1. 진화 조건/코스트 (최우선 — 거의 모든 디지몬)
| 프리미티브 | 사용 |
|---|---|
| **AddSelfDigivolutionRequirementStaticEffect** | **1282** |
| AddSelfLinkConditionStaticEffect | 70 |
| ChangeDigivolutionCostStaticEffect | 49 |
| CanNotDigivolveClass / CanNotDigivolveStaticSelfEffect / …StaticEffect | 14 / 13 / 2 |
| AddDigivolutionRequirementClass / …StaticEffect / GetJogressConditionClass | 3 / 1 / 3 |

### G2. 배틀 키워드 (self-static)
| 프리미티브 | 사용 |
|---|---|
| Retaliation·Raid·Barrier·Rush(Self/Static) | 74·64·61·43+11 |
| Collision(Self/Static)·Fortitude·Evade·Partition·Blitz·Scapegoat | 32+1·27·20·18·17·17 |
| Iceclad·Decoy·Fragment·Execute·Progress·Ascension | 12·12·7·7·6·2 |
| BlockerStaticEffect(비-self)·AllianceStaticEffect·JammingStaticEffect·VortexCanAttackPlayers | 25·5·3·1 |

### G3. 특수 진화 (Digivolve 계열)
| 프리미티브 | 사용 |
|---|---|
| BlastDigivolveEffect · BlastDNADigivolveEffect · ArtsDigivolveEffect | 75 · 7 · 6 |

### G4. Link / DigiXros / AppFuse / MindLink
| 프리미티브 | 사용 |
|---|---|
| LinkEffect · AddAppfuseMethodByName · MindLinkClass | 70 · 26 · 11 |
| GrantedReduceLinkCostClass · ChangeSelfLinkMax · ChangeLinkMax | 9 · 8 · 2 |
| DigiXrosEffectFromNames · PlayMindLinkTamerFromDigivolutionCards | 1 · 1 |

### G5. 메모리/코스트
| 프리미티브 | 사용 |
|---|---|
| SetMemoryTo3TamerEffect · Gain1MemoryTamerOpponentDigimon · Gain2MemoryOptionDelay | 83 · 17 · 13 |
| UseRequirements · Gain1MemoryTamerOwnerConditional · EoTLose3Memory | 16 · 3 · 2 |

### G6. 시큐리티/플레이
| 프리미티브 | 사용 |
|---|---|
| PlaceSelfDelayOptionSecurityEffect · PlaySelfDigimonAfterBattleSecurityEffect | 54 · 35 |
| ReplaceBottomSecurityWithFaceUpOption(Main/—) · ReplaceTopSecurity… | 7+7 · 1 |

### G7. 공격/차단/파괴 제약 (static)
| 프리미티브 | 사용 |
|---|---|
| CanNotAttackSelf / CanNotAttack | 20 / 6 |
| CanNotBeBlocked · CanNotBlock(Self/—) · CanNotBeAttackedSelf | 9 · 2+2 · 3 |
| CanNotBeDestroyedBySkill / ByBattle / (일반) / CanNotBeTrashedBySkill | 6 / 1 / 2 / 1 |
| CantUnsuspend · CantSuspend · ImmuneFromDPMinus · ImmuneStackTrashing | 7 · 1 · 2 · 2 |
| ChangeSAttackStaticEffect(비-self) · InvertSAttack · TreatAsDigimon · ChangeBaseDPGlobal | 10 · 1 · 1 · 1 |

### G8. 바운스/덱/공개
| 프리미티브 | 사용 |
|---|---|
| DeckBottomBounceClass · ReturnToLibraryBottomDigivolutionCardsClass | 36 · 15 |
| CannotReturnToHand / CannotReturnToDeck · RevealLibraryClass | 2 / 2 · 2 |

### G9. Save/Purge/Training/기타 효과
| 프리미티브 | 사용 |
|---|---|
| ArmorPurgeEffect · SaveEffect · TrainingEffect · MaterialSaveEffect · AceOverflowClass | 45 · 39 · 19 · 13 · 4 |

### G10. 프레임워크/타이밍/공유
| 프리미티브 | 사용 |
|---|---|
| ActivateClassesForSharedEffects · SelectCardConditionClass(리치 select) | 88 · 30 |
| ExtendActivateClass · WhenMovingClass · WhenDigivolvingClass | 1 · 1 · 1 |
| StartOfYourTurnClass · StartOfYourMainPhaseClass · OnDeletionClass | 1 · 1 · 1 |

---

## 선행개발 웨이브 — goal 문서 (각 문서에 실행 대화문 포함)
- **W1** 진화 기반 → `docs/audit/primitive_w1_goals.md`
- **W2** 고빈도(20+) → `docs/audit/primitive_w2_goals.md`
- **W3** 중빈도(6–19) → `docs/audit/primitive_w3_goals.md`
- **W4** tail(1–5)+프레임워크/타이밍 → `docs/audit/primitive_w4_goals.md`

## 선행개발 웨이브 (권장 순서 — 임팩트/의존)
- **W1 — 진화 기반(G1)**: `AddSelfDigivolutionRequirementStaticEffect`(1282) 단독으로 거의 모든 디지몬의 진화 조건 해금. 진화 코스트/제약 포함. **최우선 단일 임팩트 최대.**
- **W2 — 고빈도 키워드(G2 상위) + 특수진화(G3) + 링크(G4 상위) + 메모리(G5 상위) + 시큐리티(G6)**: Retaliation/Raid/Barrier/Rush/Blast/Link/SetMemoryTo3Tamer/PlaceSelfDelayOptionSecurity 등 20–90회대.
- **W3 — 중빈도 키워드·제약(G2 하위·G7)** + 바운스(G8) + Save/Purge(G9).
- **W4 — 프레임워크/타이밍(G10) + 저빈도 tail(1–5회)**.
- (BT-PRE-B/C는 이 백로그의 부분집합 — G2/G7의 CanNotBeBlocked·Retaliation·CanNotAttackSelf 등, G5의 SetMemoryTo3Tamer/EoTLose, G1의 ChangeDigivolutionCost 등. 이 마스터로 흡수.)

## 진행 추적
- 완료: **BT-PRE-A 5/90+67**. 남은 MISSING **90**.
- 각 웨이브 green 게이트. 완료분은 이 표에서 ✅ 처리하고 `fidelity_debt`/핸드오프에 반영.

## /goal 연동
`/goal <프리미티브명>` 또는 웨이브 단위 대화문으로 실행. 각 항목: 원본 위치 확인(`CardController.cs`/`CardEffectFactory/`/`CardEffectCommons/` 등) → probe(엔진 seam) → 미러 → green + 격리 테스트 + RuleAudit 0.
