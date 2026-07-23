# RD-J-01 "면역가드 제거" 상환분 전수 재검증

배경: `PermanentEffectFactory.CollisionEffect`가 "self-grant라 `CanNotBeAffected(activateClass)` 가드는
vacuous"라는 논리로 AS-IS 가드를 삭제했으나, 실제 AS-IS 호출부(EX8_070·BT21_077·EX11_063·EX10_032·EX10_008)는
**타 퍼머넌트**(select 대상)에 부여하고 있어 전제가 거짓 = 오삭제였다. 같은 논리로 삭제된 grant-time 면역/제약
가드가 더 있는지, 아래 3개 카테고리(총 31파일 + PermanentEffectFactory.cs 자체)를 전수 재검증했다.

판정 기준(과거 CollisionEffect 결함 실증으로 확립):
- "vacuous/self-grant" 주석은 근거로 인정하지 않음 — AS-IS 호출부 실측으로만 판정.
- AS-IS의 grant-time 가드는 두 층위로 나뉜다: (a) 부여되는 kind-class 자신의 **live CanUse/PermanentCondition에
  내장된** `CanNotBeAffected` 체크(매 평가 시 재확인되는 것 — 유지해야 함), (b) 부여 함수 자체의 UI 연출(예:
  `CreateDebuffEffect`/`CreateBuffEffect`)을 가리는 별도 gate(헤드리스에서 드롭 대상, RD-J-01이 정당하게 정리한
  부분). (a)가 사라지면 오삭제, (b)만 사라지면 정당.

---

## 1. `Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/` — CanNot*.cs 10종

대상: CanNotAttack, CanNotBeAttacked, CanNotBeBlocked, CanNotBeDeletedByBattle, CanNotBeDeletedByEffect,
CanNotBlock, CanNotReturnToHand, CanNotSuspend, CanNotUnsuspend, ImmuneFromDPMinus.

전건 동일 패턴 확인: AS-IS `Gain*` 함수는 `CanNotBeAffected(activateClass)`를 **두 곳**에서 참조한다 —
① 부여되는 kind-class의 live `CanUseCondition`(예: `CanNotAttack.cs:40` `if (!targetPermanent.TopCard
.CanNotBeAffected(activateClass)) return true;` 형태, 매 평가 시 재확인) — 이건 실제 제약 발동 여부를 가르는
핵심 게이트. ② `AddEffectToPermanent` 호출 **이후**의 UI 연출(`CreateDebuffEffect`/`CreateBuffEffect`)을
가리는 별도 if — 게임 상태에 영향 없는 비주얼 전용.

TO-BE 10종 전부: ①(live CanUseCondition 내장 체크)은 **보존**(`GainCanNot*Impl`의 `CanUseCondition` 내부에
`!targetPermanent.TopCard.CanNotBeAffected(cause)` 그대로), ②(UI 게이트)만 드롭 — 주석에도 "AS-IS grants
UNCONDITIONALLY — the only AS-IS CanNotBeAffected reads are the read-time CanUseCondition (kept) and the
dropped UI visual"로 명시. 이는 RD-J-01이 원래 노렸던 "발명된 grant-time 사전거부(가드가 걸리면 통째로
AddEffectToPermanent 자체를 스킵하던 로직)" 제거와 일치하고, 실제 제약을 발동시키는 라이브 체크는 전부 남아있다.

**판정: 10종 전부 정당(가드 보존 확인, 오삭제 아님).**

## 2. `Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/` — CanNot*.cs 7종

대상: CanNotAttack, CanNotBeDeletedByBattle, CanNotBlock, CanNotReturnToHand, CanNotSuspend, CanNotUnsuspend,
ImmuneFromDPMinus.

동일 검증: AS-IS는 각 파일당 `CanNotBeAffected` 1개소(대상 permanent의 live `PermanentCondition`/
`AttackerCondition` 내부, 예: `CanNotAttack.cs:26` `if (!attacker.TopCard.CanNotBeAffected(activateClass))`).
TO-BE 7종 전부 동일 체크를 `!attacker.TopCard.CanNotBeAffected(cause)` / `!permanent.TopCard
.CanNotBeAffected(cause)` 형태로 그대로 보존(줄 위치·조건 동일, `cause`=AS-IS `activateClass` 1:1 스레딩).

**판정: 7종 전부 정당(가드 보존 확인, 오삭제 아님).**

## 3. `Script/CardEffectFactory/` — CanNot*.cs 계열 14종

대상: CanNotAttack, CanNotBeAttacked, CanNotBeBlocked, CanNotBeDeleted, CanNotBeDeletedByBattle,
CanNotBeDeletedByEffect, CanNotBeRemoved, CanNotBeTrashedByEffect, CanNotBlock, CanNotDigivolve,
CanNotReturnToHand, CanNotSuspend, CanNotUnsuspend, ImmuneFromDPMinus.

12/14(CanNotAttack, CanNotBeDeleted, CanNotBeDeletedByBattle, CanNotBeDeletedByEffect, CanNotBeRemoved,
CanNotBeTrashedByEffect, CanNotBlock, CanNotDigivolve, CanNotReturnToHand, CanNotSuspend, CanNotUnsuspend,
ImmuneFromDPMinus): AS-IS의 `*StaticEffect` 팩토리가 만든 kind-class 자신의 `PermanentCondition`/
`AttackerCondition` 안에 `!permanent.TopCard.CanNotBeAffected(<자기자신 kind-class 인스턴스>)` 체크를 내장
(예: `CanNotBeDeleted.cs:30`). TO-BE 12종 전부 동일 위치·동일 조건으로 보존(`ADAPTATION` 주석은 오버로드
표기 방식 설명일 뿐, 실제 코드는 `canNotBeDestroyedClass` 인스턴스를 그대로 넘기는 동일 시그니처 —
`CanNotBeDeleted.cs:37` 확인).

나머지 2/14(CanNotBeAttacked, CanNotBeBlocked): AS-IS 원본을 열람한 결과 `CanNotBeAttackedSelfStaticEffect`
/ `CanNotBeBlockedStaticSelfEffect`는 애초에 `CanNotBeAffected` 체크 자체가 **존재하지 않음** — 대신
`attacker == card.PermanentOfThisCard()`로 self만 하드코딩된 별도 계열(면역 게이트가 아니라 self-lock
게이트). TO-BE도 동일하게 체크 없이 이식. AS-IS 원문(DCGO CardEffectFactory/CanNotBeAttacked.cs,
CanNotBeBlocked.cs) 확인 완료 — 애초에 없던 걸 없는 채로 이식한 것이므로 정당.

**판정: 14종 전부 정당(12종=가드 보존, 2종=AS-IS 원래 무가드 확인).**

## 4. `Script/PermanentEffectFactory.cs` — 면역-관련 kind 전체 재확인

파일 내 6개 함수 전수 대조(AS-IS `DCGO/Assets/Scripts/Script/PermanentEffectFactory.cs`):

| 함수 | AS-IS 가드 | TO-BE 상태 | 판정 |
|---|---|---|---|
| `DeleteSelfEffect` | `CanUseCondition` 내 `!permanent.TopCard.CanNotBeAffected(cardEffect)` (AS-IS :23) | 보존 (TO-BE :169) | 정당 |
| `DigimonEffectImmunity` | 없음(이 함수 자체가 면역을 **부여**하는 쪽; `CanNotBeAffected` 대신 `IsOpponentEffect`+`SkillCondition` 사용) | AS-IS 그대로(체크 대상 아님) | 정당 |
| `OptionEffectImmunity` | 없음(위와 동일 사유) | AS-IS 그대로 | 정당 |
| `AddDetailClass` | `CanUseCondition` 내 `!targetPermanent.TopCard.CanNotBeAffected(activateClass)` (AS-IS :159) | 보존 (TO-BE :220) | 정당 |
| `CollisionEffect` | `CanUseCondition` 내 `!targetPermanent.TopCard.CanNotBeAffected(activateClass)` (AS-IS :137-138) | **삭제**(`_ = activateClass;`로 무시, TO-BE :121-132) | **오삭제 확정(재확인)** |
| `CanNotSwitchAttackTargetEffect` | `CanUseCondition` 내 `!targetPermanent.TopCard.CanNotBeAffected(activateClass)` (AS-IS :120) | **삭제**(`_ = activateClass;`로 무시, TO-BE :22-37) | **오삭제(신규 발견)** — 상세는 아래 |

### 4-a. `CollisionEffect` — 확정 결함 재확인
AS-IS 호출 경로: `CardEffectCommons.GainCollision(targetPermanent, …)` → `PermanentEffectFactory
.CollisionEffect(targetPermanent, activateClass)`. `GainCollision`의 유일한 실호출부(grep 전수):
- `DCGO/Assets/Scripts/CardEffect/EX8/Black/EX8_070.cs:74` — `GainCollision(selectedPermanent, …)`
- `DCGO/Assets/Scripts/CardEffect/BT21/Purple/BT21_077.cs`
- `DCGO/Assets/Scripts/CardEffect/EX10/Black/EX10_032.cs`
- `DCGO/Assets/Scripts/CardEffect/EX10/Red/EX10_008.cs`
- `DCGO/Assets/Scripts/CardEffect/EX11/Green/EX11_063.cs`

EX8_070 확인 결과 `selectedPermanent`(select 대상, self 아님)에 부여 — self-grant 전제가 거짓. **결함 재확인.**
근거 카드: EX8_070, BT21_077, EX10_032, EX10_008, EX11_063.

또한 구조적 근거: `CollisionStaticEffect`(`CardEffectFactory/KeyWordEffects/Collision.cs`) 자체는 `condition`을
외부에서 주입받는 순수 팩토리로 면역 체크를 내장하지 않음 — 그 책임은 전적으로 `PermanentEffectFactory
.CollisionEffect`의 `CanUseCondition`에 있었는데 그게 삭제됨. TO-BE/AS-IS 양쪽 `CollisionStaticEffect` 대조로
이 구조 확인.

### 4-b. `CanNotSwitchAttackTargetEffect` — 신규 발견(경계 사례)
AS-IS 유일 호출부(grep 전수): `DCGO/Assets/Scripts/CardEffect/AD1/Blue/AD1_011.cs:112` —
```
card.PermanentOfThisCard().UntilEachTurnEndEffects.Add(_timing =>
    PermanentEffectFactory.CanNotSwitchAttackTargetEffect(card.PermanentOfThisCard(), activateClass));
```
`targetPermanent`와 `activateClass`의 소스 카드가 모두 `card`(this) — **이 함수는 실제로 self-only**
(CollisionEffect와 달리 "vacuous" 주장이 호출부 실측으로 참으로 확인됨. 다른 호출부는 전무).

다만 CollisionEffect 사건이 보여준 교훈("vacuous 논리 자체가 신뢰 불가")과 이 프로젝트의 1:1 미러 원칙
(단순화 금지)에 따르면: 현재 호출부가 self-only라는 사실이 앞으로도 self-only임을 보장하지 않고, AS-IS
소스 자체에는 이 체크가 명시적으로 존재한다. TO-BE가 `activateClass` 파라미터를 받고도 `_ = activateClass;`로
버린 것은 AS-IS 라이브 게이트 하나를 통째로 누락시킨 것 — 구조적으로는 오삭제와 동일한 패턴(파라미터
시그니처는 유지하되 그 안의 게이트 로직만 드롭). 현재 유일한 실제 호출부(AD1_011)가 self이므로 **현시점
관측 가능한 행동 차이는 없음**이나, 원칙(가드 판정은 "vacuous 주석"이 아니라 AS-IS 원문 그대로 이식해야 함)
에 따라 이것도 **오삭제로 판정**한다 — CollisionEffect보다 심각도는 낮음(현재 카드로는 무해)이지만 동일한
삭제 근거(뒤에 숨은 "self-grant라 vacuous" 논리)로 저질러진 동일 계열 결함.

---

## 종합

| 카테고리 | 파일 수 | 오삭제 결함 | 정당 |
|---|---|---|---|
| GiveEffectToPermanent CanNot*.cs | 10 | 0 | 10 |
| GiveEffectToPlayer CanNot*.cs | 7 | 0 | 7 |
| CardEffectFactory CanNot*.cs 계열 | 14 | 0 | 14 |
| PermanentEffectFactory.cs (면역-관련 kind 6개) | 6 | **2**(CollisionEffect, CanNotSwitchAttackTargetEffect) | 4 |
| **합계** | **37 판정 단위(31파일+6함수)** | **2** | **35** |

**오삭제 결함 전건(근거 카드):**
1. `PermanentEffectFactory.CollisionEffect` — AS-IS 라이브 `CanUseCondition`의 `!targetPermanent.TopCard
   .CanNotBeAffected(activateClass)` 삭제. 근거: EX8_070, BT21_077, EX10_032, EX10_008, EX11_063 (전부
   select-대상 부여, self-grant 아님). — 기존 감사에서 확정, 본 재검증으로 재확인.
2. `PermanentEffectFactory.CanNotSwitchAttackTargetEffect` — 동일 패턴으로 AS-IS 라이브 `CanUseCondition`의
   동일 가드 삭제. 유일 호출부(AD1_011)는 self-only라 현재 관측 가능한 행동 결함은 없으나, AS-IS 원문에
   가드가 존재하고 TO-BE가 이를 "vacuous" 논리로 침묵 삭제한 것은 CollisionEffect와 동일한 오류 패턴 —
   구조적 오삭제로 판정.

나머지 35개 판정 단위(GiveEffectToPermanent 10 + GiveEffectToPlayer 7 + CardEffectFactory 14 + Permanent
EffectFactory 4함수)는 전부 AS-IS 라이브 가드를 그대로 보존했거나(대다수), AS-IS 자체에 원래 그 가드가
없었음(CanNotBeAttacked/CanNotBeBlocked self-static 계열, DigimonEffectImmunity/OptionEffectImmunity)을
AS-IS 원문으로 확인 — 정당.
