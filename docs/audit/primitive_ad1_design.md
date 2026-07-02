# AD1 프리미티브 선행개발 설계 (설계만 — 구현 착수 전)

- 작성: 2026-07-02. 목적: AD1 세트(AD1_009/011/012/025 등)를 로컬모델 포팅 가능 상태로 만들기 위한 미개발 프리미티브의 AS-IS 대조 설계.
- **✅ 전체 구현 완료(같은 날, J→G→S→A)**: J `GetJogressConditionClass` 동명 팩토리 + **재료 매칭 greedy→백트래킹 교정**(AS-IS 순열 시맨틱, G9-048 +2); G `GainCanNotBeDeletedByBattle` 커먼즈 + BattleDeletionGate에 4-인자 술어 라이브 평가(G9-054 +4); S `CanNotSwitchAttackTargetClass`/`PermanentEffectFactory` 미러 + `AttackTargetSwitchGate` 초크포인트 + BlockTiming·RaidAttackSwitch 배선(G9-064 신설 5); A `AssemblyCondition`/`AddAssemblyConditionClass`/`SelectAssemblyClass` 1:1 + PlayCardAction 파라미터화 라이더(열거·검증·감소·bottom 스택·assemblyCount, G9-065 신설 4). 카탈로그 123종 재생성 + 레시피 4행. 축소 기록: Assembly 필드-대체(fidelity_debt).
- AS-IS probe: AD1_025/AD1_011 실카드 + `SelectAssemblyClass.cs`/`CardSource.cs`/`AttackProcess.cs`/`Permanent.cs`/`GiveEffect/CanNotBeDeletedByBattle.cs`/`CardEffectFactory.cs:752` 전수 인용 확보(아래 각 절).
- **재분류 결과**: 당초 "미개발 4건" 중 **1건(술어형 Jogress)은 이미 존재** — 실작업은 3건 + 매핑 문서화 1건.

| # | 항목 | 판정 | 규모 |
|---|---|---|---|
| J | 술어형 Jogress (`GetJogressConditionClass`) | **기존 `JogressEffect`로 표현 가능** — 동명 미러 팩토리(thin)만 신설 | 극소 |
| S | `CanNotSwitchAttackTargetEffect` | 미개발 (스켈레톤 7줄) | 소 |
| G | `GainCanNotBeDeletedByBattle` | 미개발 (연속형 팩토리는 존재 — 시한부 grant 커먼즈만 부재) | 소 |
| A | Assembly 특수플레이 | 미개발 (스켈레톤 7줄) | 중 |

---

## J. 술어형 Jogress — 동명 미러 팩토리 (극소)

**AS-IS** (`CardEffectFactory.cs:752-780`): `GetJogressConditionClass(permanentCondition1, description1, permanentCondition2, description2, card, cost = 0, canUseCondition = null)` → `AddJogressConditionClass` 반환. 내부 `GetJogressConditions`가 `JogressConditionElement[2]`(술어+설명)로 `JogressCondition(elements, cost)` 생성. **주의(원본 quirk)**: `cost` 파라미터는 `GetJogressConditions`에 전달되지 않아 **술어형은 항상 cost 0** (`DNADigivolveEffects.cs:630` 기본값). 소비: `CanPlayJogress`(`CardSource.cs:2747-2793`)가 배틀에리어 디지몬의 **순열**에 대해 element[0]↔재료0, element[1]↔재료1을 평가(+CanNotEvolve 게이트), 두 재료는 서로 달라야 함(`SelectJogressEffect.cs:325-334` — 첫 재료 선택 시 둘째 element를 만족하는 **다른** 후보 존재 요구). `AddJogressConditionClass.GetJogressCondition`(`:26-36`)이 각 술어를 `IsPermanentExistsOnOwnerBattleAreaDigimon`으로 래핑.

**포트 현황**: `JogressEffect(card, condition, params SpecialPlayMaterial[])` **이미 존재**(CardPortingFramework:4290) — 임의 술어 1:1, `SpecialPlayRecipe(DnaDigivolve, materials, MemoryCost: 0)`으로 등록. cost 0 하드코딩이 원본 quirk과 **일치**.

**설계**: 동명 thin 팩토리 신설(로컬모델이 원본 호출을 그대로 미러할 수 있도록):
```csharp
public static ICardEffect GetJogressConditionClass(
    Func<Permanent, bool> permanentCondition1, string description1,
    Func<Permanent, bool> permanentCondition2, string description2,
    CardSource card, int cost = 0, Func<bool>? canUseCondition = null) =>
    JogressEffect(card, canUseCondition,
        new SpecialPlayMaterial(cs => cs.IsDigimon && cs.Owner == card.Owner && permanentCondition1(new Permanent(cs.Context, cs.InstanceId, cs.Owner)), description1),
        new SpecialPlayMaterial(cs => cs.IsDigimon && cs.Owner == card.Owner && permanentCondition2(new Permanent(cs.Context, cs.InstanceId, cs.Owner)), description2));
```
- `Permanent` 술어→`CardSource` 술어 어댑터: 재료 후보는 배틀에리어 퍼머넌트의 탑카드이므로 인스턴스 id로 Permanent 뷰 생성해 평가(= AS-IS EvoRootCondition(permanent) 1:1). owner-스코프 래핑은 AS-IS `AddJogressConditionClass`의 `IsPermanentExistsOnOwnerBattleAreaDigimon` 미러.
- `cost` 파라미터는 **받고 무시**(원본 quirk 1:1 — 시그니처 충실도용; XML doc에 명기).
- **검증 항목(구현 시)**: 포트 DNA 열거가 (a) 순열 평가(elem0↔mat0/elem1↔mat1 교차), (b) 재료 상이 요구를 지키는지 — `SpecialPlayAction`의 DnaDigivolve 재료 매칭 로직 확인 후 어긋나면 그쪽 수정(별도 커밋).
- 테스트: 술어형 DNA 합법쌍 열거(레벨4 파랑+레벨4 초록), 동일 퍼머넌트 이중사용 불가, cost 0.

---

## S. CanNotSwitchAttackTarget — 공격대상 변경 잠금 (소)

**AS-IS 시맨틱** (probe 확정):
- 클래스(`Script/CardEffects/CanNotSwitchAttackTargetClass.cs`): `Func<Permanent,bool> PermanentCondition` 하나. `CanNotBeSwitchAttackTarget(permanent)` = 술어(permanent) — **공격자**를 인자로 받음.
- 소비자는 `Permanent.CanSwitchAttackTarget`(`Permanent.cs:3745-3792`, 필드+플레이어 효과 스캔) 단일 초크포인트이고, 이것이 **정확히 두 행동**을 게이트:
  1. `AttackProcess.SwitchDefender(cardEffect, isBlock, newDefender)`(`AttackProcess.cs:519`) — **블록 리다이렉트와 효과 재타게팅의 공용 루틴** 최상단에서 차단(isBlock 분기 이전) → 블록과 "공격 대상 변경" 효과 **양쪽** 차단.
  2. 블록 가능 판정(`Permanent.cs:2156`) — 공격자가 잠겨 있으면 블로커 후보 자체가 불가.
- 부여 형태(AD1_011:110-113): DNA 진화 시 `PermanentOfThisCard().UntilEachTurnEndEffects.Add(_ => PermanentEffectFactory.CanNotSwitchAttackTargetEffect(perm, activateClass))` — **UntilEachTurnEnd 시한부**. 팩토리(`PermanentEffectFactory.cs:109-127`): CanUse = 필드존재 && **IsOwnerTurn** && !CanNotBeAffected; 술어 = `permanent == targetPermanent`(자기 자신).
- 직접 생성 카드 12+장(AD1_012, EX8_025, BT20_026 …)은 자체 PermanentCondition을 넘김 → **술어는 실동작해야 함**(자기-한정으로 뭉개기 금지).

**포트 설계**:
1. **제한 키**: `RestrictionHelpers.CannotSwitchAttackTargetKey = "cannotSwitchAttackTarget"` — 연속 restriction 바인딩. 값에 술어(`Func<HeadlessEntityId,bool>` — 공격자 인스턴스 id, AS-IS Permanent 술어의 id-어댑터) + ConditionKey(CanUse 미러) 저장.
2. **초크포인트**: `ContinuousRestrictionGate.EvaluateAttackTargetSwitch(context, attackerId)` 신설(기존 EvaluateAttack/Block 형제) — 필드-와이드 바인딩 스캔, 술어(attackerId) && 조건 통과 시 Restricted. (AS-IS CanSwitchAttackTarget 미러; 시큐리티 faceup 스캔은 K3와 동일 census-후 확장.)
3. **소비 배선 (2곳, AS-IS와 동수)**:
   - `BlockTiming.RequestBlockChoice`: 공격자 잠금이면 블록 후보 열거 스킵(창 자체 불개) — AS-IS `:2156` 미러. **주의**: Collision 강제블록과 충돌 시 잠금이 우선(블록 자체가 불법이므로 후보 0 → 강제블록도 무효).
   - `RaidAttackSwitch.RequestChoice` + 향후 재타게팅 효과 공용 진입점: 공격 재타게팅은 현재 RaidAttackSwitch가 유일한 스위치 경로 — 잠금 시 창 불개. 설계 노트: 재타게팅 효과가 추가되면 반드시 이 게이트를 경유(공용 `AttackRetarget.TrySwitch` 헬퍼로 수렴 권장 — AS-IS SwitchDefender 미러 지점).
4. **카드-facing 미러**: `CanNotSwitchAttackTargetClass` 1:1(스켈레톤 교체 — SetUpICardEffect/SetUpCanNotSwitchAttackTargetClass/CanNotBeSwitchAttackTarget) + `PermanentEffectFactory.CanNotSwitchAttackTargetEffect(Permanent, ICardEffect)` 1:1(파일 Script/PermanentEffectFactory.cs 신설 미러; CanUse 3항 — IsOwnerTurn 포함 — 저장 condition으로). ToBinding → duration `UntilEachTurnEnd` 태그(AS-IS UntilEachTurnEndEffects 버킷 미러; `EffectDurationExpiry.ExpireTurnEnd` 기존 처리).
5. 테스트: (a) 잠긴 공격자에 블록 창 불개(+Collision 무효), (b) Raid 스위치 불개, (c) 턴 종료 시 만료, (d) IsOwnerTurn 게이트(상대 턴 공격엔 미적용), (e) 술어 비자기-한정 카드형(다른 퍼머넌트 조건) 통과.

---

## G. GainCanNotBeDeletedByBattle — 시한부 전투삭제 면역 grant (소)

**AS-IS** (`GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByBattle.cs:11-54`):
- 가드: target 필드존재 && activateClass/EffectSourceCard 유효.
- 기존 팩토리 `CanNotBeDestroyedByBattleStaticEffect`를 그대로 재사용해 빌드: `canNotBeDestroyedByBattleCondition`(호출자 4-인자 술어) + `permanentCondition = (attacker) => attacker == targetPermanent`(대상 잠금) + `CanUseCondition = 필드존재 && !target.CanNotBeAffected(activateClass)`.
- `AddEffectToPermanent(target, effectDuration, card, effect, EffectTiming.None)` — duration 버킷 등록. 소비는 `Permanent.CanBeDestroyedByBattle`(`Permanent.cs:3233-3304`) 기존 초크포인트.
- 실사용 duration: 조사된 전 호출자(AD1_011, BT20_022, BT14_028, BT19_023 …)가 `UntilOpponentTurnEnd`.

**포트 설계**:
1. **커먼즈 미러**: `CardEffectCommons.GainCanNotBeDeletedByBattle(EngineContext, HeadlessEntityId targetId, Func<Permanent,Permanent,Permanent,CardSource,bool>? cond, EffectDuration, CardSource sourceCard, string effectName)` — 기존 `ActivatedTargetBuffEffect.ApplyBuff` 패턴(대상에 duration-태그 바인딩 등록) 재사용:
   - 바인딩: `BattleDeletionGate.PreventBattleDeletionKey` restriction, Target = targetId, duration = effectDuration, 값에 4-인자 술어 + 대상잠금 술어 + CanUse(필드존재; CanNotBeAffected는 **부여 시점 1회** 체크 — AS-IS는 CanUse에 라이브 포함이므로 **라이브 미러**: ConditionKey에 `() => 필드존재(targetId)`, 면역은 grant 적용 시 sink의 ContinuousImmunityGate 경유 — 부여가 "효과 적용"이므로 grant 자체를 sink GrantKind…이 아닌 직접 등록으로 갈 경우 면역 체크를 등록 전에 1회+ConditionKey에 라이브로 넣는 이중화로 1:1).
2. **소비자 확인 항목(구현 시)**: `BattleDeletionGate`가 (a) TargetEntityIds로 직접 지정된 grant 바인딩을 매칭하는지, (b) 4-인자 술어 값을 평가하는지(EX8_068 라틴트에서 trivial 판정했던 그 키) — 미지원이면 게이트에 target-지정 매칭 분기 추가.
3. 시그니처는 코루틴이 아니라 동기 등록(모든 Gain류 포트 관례) — 레시피의 코루틴 의도표에 행 추가: `CardEffectCommons.GainCanNotBeDeletedByBattle(...)` → 동명 커먼즈.
4. 테스트: (a) 부여 후 전투 패배해도 생존(UntilOpponentTurnEnd 내), (b) 상대 턴 종료 후 만료 → 사망, (c) CanNotBeAffected 대상에 부여 무효, (d) 대상이 필드를 떠나면 실효(라이브 조건).

---

## A. Assembly 특수플레이 (중)

**AS-IS 규칙** (probe 전문 인용 확보 — `SelectAssemblyClass.cs`, `CardSource.cs:705-737/2575/3043-3065/4313-4358`, `CardController.cs:753-761/1250-1272/1630-1649`, `Permanent.cs:1133-1201/3843-3886`):
1. **선언**: timing None에 `AddAssemblyConditionClass`(thin — `Func<CardSource, AssemblyCondition>`). `AssemblyCondition` = `List<AssemblyConditionElement>` + **flat `reduceCost`**; element = `CardCondition`(CardSource 술어) + `ElementCount` + `selectMessage` + 선택적 `CanTargetCondition_ByPreSelecetedList`(기선택 연동 게이트) + `skipAllIfNoSelect`. `elementCount` = 합.
2. **발동 조건**: 카드를 **손에서 일반 플레이**할 때만(단일 카드, `!isEvolution`). 별도 액션이 아니라 플레이 플로우의 라이더. 가능성 판정 = **자기 트래시**에 요소별 술어를 전부 채울 수 있는가(`CanFulfillConditions` — 요소별 백트래킹).
3. **선택**: 요소별로 트래시에서 재료 선택(`Root.Trash`, 기선택 제외). 필드 퍼머넌트 대체는 `ICanSelectAssemblyEffect` 보유 시에만(별도 효과 — AD1 미사용).
4. **비용**: full set(`selected == elementCount`)일 때만 `Cost -= reduceCost`(`GetPayingCost`, `Owner.CanReduceCost` 게이트). 부분 선택 = 감소 없음.
5. **진입 후**: 선택 재료를 트래시에서 새 퍼머넌트의 **진화원 bottom**으로 이동(`AddDigivolutionCardsBottom`) — 트래시 잔류/파기 아님.
6. **트리거**: 전용 타이밍 없음 — 일반 On-Play(OnEnterField) 발화, `AssemblyCount` 해시 파라미터만 전달(현행 카드 소비자 **0**).

**포트 설계**:

*모델링 결정* — **PlayCardAction의 파라미터화 라이더**(옵션 B). 근거: (i) AS-IS가 일반 플레이 경로(진입 처리·On-Play 트리거 동일)이므로 SpecialPlayAction(fusion 진입)보다 PlayCardAction이 1:1; (ii) 재료를 액션 파라미터로 싣는 방식은 DigiXros 선례와 일관(RL 액션 공간 관례); (iii) in-flow 선택(옵션 A)은 비용 지불이 액션 시점에 선결정되는 포트 구조와 충돌.

1. **카드-facing 1:1 미러** (3 클래스, 기존 스켈레톤 교체):
   - `AssemblyCondition` / `AssemblyConditionElement` — `CardSource.cs:4313-4358` 1:1(두 ctor, 필드명 동일; 구형 단일-조건 ctor 포함).
   - `AddAssemblyConditionClass` — thin 래퍼 1:1(`SetUpAddAssemblyConditionClass(Func<CardSource, AssemblyCondition>)` + `GetAssemblyCondition`). `ToBinding` → timing-None 연속 바인딩, 값 `assembly.getCondition`에 Func 저장(직렬화 없음 — AS-IS EffectList(None) 스캔의 레지스트리 미러).
   - `CardSource.HasAssembly` / `assemblyCondition` 뷰 — 레지스트리에서 첫 사용가능 `assembly.getCondition` 평가(`CardSource.cs:2575/3043-3065` 미러; CanUse 게이트 포함).
2. **합법수 열거** (`HeadlessLegalActionDispatcher` 플레이 열거부):
   - 손패 카드가 HasAssembly && 트래시 feasibility 통과 시, 기존 일반 플레이 액션에 **추가로** assembly-변형 액션 제공: 파라미터 = 재료 인스턴스 id 목록(요소 순서), 비용 = `max(0, base - reduceCost)`(CanReduceCost 게이트 경유), 마커 `assembly=true`.
   - 재료 조합: 요소별 후보 × ElementCount — 조합 폭발 방지 위해 DigiXros와 동일한 열거 상한 정책 준수(기존 SpecialPlayAction 관례 재사용). `CanTargetCondition_ByPreSelecetedList`는 조합 생성 시 기선택 목록으로 평가(1:1).
   - full-set-only: 부분 재료 액션은 생성하지 않음(부분 선택 = 그냥 일반 플레이와 동일하므로 의미 없음 — AS-IS도 감소 미적용).
3. **실행** (`PlayCardAction.ProcessAsync` 라이더):
   - 일반 플레이 파이프라인 그대로(메모리 지불 = 감소 후 비용) → 진입 완료 후 재료를 트래시→진화원 bottom으로 이동(`DigivolutionStackHelpers`/MindLink에서 쓴 `AddSourcesBottomAsync` 재사용; 이동 전 재료가 여전히 트래시에 있는지 재검증 — 진입 트리거가 트래시를 건드린 경우 해당 재료만 스킵, AS-IS `isTrashCard` 가드 미러).
   - OnEnterField 트리거 값에 `assemblyCount` 추가(소비자 0이지만 해시 파라미터 1:1 — HashtableSetting:143 미러).
4. **명시적 축소(문서화)**: 필드 대체(`ICanSelectAssemblyEffect`/`CanSubstituteForAssemblyCondition`, `Permanent.cs:3843-3886`)는 **본 웨이브 제외** — 사용 카드가 AD1에 없고 별도 효과 프리미티브가 필요. fidelity_debt에 조건부 라틴트로 기록(해당 카드 등장 시).
5. **카드-facing 팩토리**(카탈로그 등재용): 원본은 팩토리 없이 클래스 직접 생성이므로 **클래스 직접 생성 표면**으로 카탈로그 문서화(PartitionCondition과 동일 취급) — 레시피에 AD1_025 형태의 미러 예시 추가.
6. 테스트: (a) 트래시에 WarGreymon+MetalGarurumon 있을 때 감소(-6) 플레이 액션 열거, (b) 한 쪽 부재 시 미열거(일반 플레이만), (c) 실행 후 두 재료가 진화원 bottom(순서 보존)·비용 감소 지불 확인, (d) On-Play 트리거 발화 + assemblyCount 값, (e) 요소 술어 실평가(이름 불일치 재료로 액션 위조 시 거부), (f) 진입 트리거가 재료를 먼저 소모한 경우 해당 재료 스킵.

---

## 구현 순서 제안 (착수 지시 시)

1. **J** (극소, 독립) → 2. **G** (소, 기존 게이트 확인 포함) → 3. **S** (소, 게이트 신설+2곳 배선) → 4. **A** (중, 열거+실행+뷰). 각 항목 green 게이트(테스트 + `run-tests.sh` + RuleAudit 0) 후 다음. 완료 시 카탈로그 재생성 + 레시피 행 추가(J/G/S/A 각 1행) + AD1 4장(009/011/012/025)을 파일럿 후보에 복귀.
