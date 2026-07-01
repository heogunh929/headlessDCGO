# PRIM-W4 — 저빈도 tail + 프레임워크/타이밍 (선행개발 웨이브 4, 사용 1–5)

> **위치:** `primitive_backlog.md`의 W4. 저빈도(1–5회) 프리미티브 + 프레임워크/타이밍 트리거. W3 완료 후. 완료 시 **MISSING 90 전부 소진** → 로컬 모델 카드 포팅 진입 가능.
> **공통 종료 기준·규율:** W1~W3와 동일. 저빈도라도 AS-IS 미러·green 게이트 동일 적용. 다수가 기존 프리미티브의 변형(비-self/정적/조건부)이라 저비용.

## 서브goal
### 저빈도 키워드/제약/효과 (사용 2–5)
| 프리미티브 (사용) | 원본 위치 |
|---|---|
| AllianceStaticEffect (5) | `KeyWordEffects/Alliance.cs` |
| AceOverflowClass (4) | `Script/CardController.cs` |
| JammingStaticEffect (3) | `KeyWordEffects/Jamming.cs` |
| Gain1MemoryTamerOwnerDigimonConditionalEffect (3) | `CardEffectFactory` (probe) |
| CanNotBeAttackedSelfStaticEffect (3) | `CardEffectFactory/CanNotBeAttacked.cs` |
| RevealLibraryClass (2) | `CardEffectCommons/RevealLibrary.cs` |
| ImmuneStackTrashingClass (2) | `Script/CardEffects/ImmuneFromStackTrashingClass.cs` |
| ImmuneFromDPMinusStaticEffect (2) | `CardEffectFactory/ImmuneFromDPMinus.cs` |
| EoTLose3Memory (2) | `CardEffectFactory` (probe) |
| ChangeLinkMaxStaticEffect (2) | `CardEffectFactory/ChangeLinkMax.cs` |
| CannotReturnToHandStaticEffect (2) | `CardEffectFactory/CanNotReturnToHand.cs` |
| CannotReturnToDeckStaticEffect (2) | `CardEffectFactory/CanNoReturnToDeck.cs` |
| CanNotBlockStaticSelfEffect / CanNotBlockStaticEffect (2/2) | `CardEffectFactory/CanNotBlock.cs` |
| CanNotBeDestroyedStaticEffect (2) | `CardEffectFactory/CanNotBeDeleted.cs` |
| AscensionSelfEffect (2) | `KeyWordEffects/Ascension.cs` |

### 단일사용 (1) — 키워드/제약/특수
| 프리미티브 | 원본 위치 |
|---|---|
| VortexCanAttackPlayersStaticEffect | `CardEffectFactory/VortexCanAttackPlayers.cs` |
| TreatAsDigimonStaticEffect | `CardEffectFactory/TreatAsDigimon.cs` |
| InvertSAttackStaticEffect | `CardEffectFactory/ChangeSAttack.cs` |
| CollisionStaticEffect | `KeyWordEffects/Collision.cs` |
| ChangeBaseDPGlobalEffect | `CardEffectFactory/ChangeOriginDP.cs` |
| CantSuspendStaticEffect | `CardEffectFactory/CanNotSuspend.cs` |
| CanNotBeTrashedBySkillStaticEffect | `CardEffectFactory/CanNotBeTrashedByEffect.cs` |
| CanNotBeDestroyedByBattleStaticEffect | `CardEffectFactory/CanNotBeDeletedByBattle.cs` |
| DigiXrosEffectFromNames | `CardEffectFactory` (probe) |
| PlayMindLinkTamerFromDigivolutionCards | `CardEffectFactory` (probe) |
| ReplaceTopSecurityWithFaceUpOptionMainEffect | `CardEffectFactory` (probe) |

### 프레임워크/타이밍 (1) — 트리거 창 신설 가능성
| 프리미티브 | 원본 위치 | 비고 |
|---|---|---|
| ExtendActivateClass | `CardEffect/EX2/Blue/EX2_057.cs` | ActivateClass 확장 |
| WhenMovingClass · WhenDigivolvingClass | `CardEffectFactory` (probe) | 타이밍 트리거(이동/진화 시) |
| StartOfYourTurnClass · StartOfYourMainPhaseClass | `CardEffectFactory` (probe) | 턴/메인 시작 트리거 |
| OnDeletionClass | `CardEffectFactory` (probe) | 삭제 시 트리거 |

> 타이밍 트리거(Start/WhenMoving/OnDeletion)는 **엔진 타이밍 창 신설**이 필요할 수 있음 — probe 후 기존 `EffectTiming`/트리거 emit 경로에 없으면 신설(EX8 OnEndTurn 신설 선례).

## 진행 요약
- [ ] 저빈도 변형(2–5): 기존 프리미티브의 비-self/정적/조건부 변형 위주 — 저비용 일괄
- [ ] 단일사용(1): 키워드·제약 나머지
- [ ] 프레임워크/타이밍: ExtendActivate·타이밍 트리거 4종(신설 판단)
- ✅ 완료 시 **MISSING 90 전부 소진** → 로컬 모델 카드 포팅 진입.

---

## 실행 대화문 (복붙용)
```
PRIM-W4 진행. docs/audit/primitive_w4_goals.md 스펙대로: (1) 저빈도 변형(2–5) 일괄 → (2) 단일사용(1) → (3) 프레임워크/타이밍(ExtendActivate·WhenMoving·WhenDigivolving·StartOfYourTurn·StartOfYourMainPhase·OnDeletion).
각 항목: 구현 전 원본(표 위치)에서 1:1 확인(추측 금지) → probe(대부분 기존 프리미티브의 비-self/정적/조건부 변형; 타이밍 트리거는 기존 EffectTiming/emit 경로 있는지 먼저 확인, 없으면 EX8 OnEndTurn 선례처럼 신설) → 미러 → bash scripts/run-tests.sh green + 격리/픽스처 테스트 + tools/RuleAudit 0. 이전 항목 green 후 다음. 중앙 게이트 재구현 금지, 범위 밖 NotSupported. AS-IS 불명확하면 중단·확인. 커밋은 내가 지시할 때.
```
