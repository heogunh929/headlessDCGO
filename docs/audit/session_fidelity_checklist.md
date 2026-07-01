# 세션 충실도 복원 체크리스트 (6 구획) — AS-IS 1:1 재검증

> 각 항목: **원본 `DCGO/` 메커니즘 확인 → 헤드리스가 그것을 미러하는지 → 동작을 단언하는 테스트 → green + RuleAudit 0**.
> (원본 확인을 첫 단계로 강제 — 반복 실수 교정. 참고: [[check-asis-before-implementing]])

## 종료 게이트 (공통)
- [x] `bash scripts/run-tests.sh` 전체 green (288 스위트)
- [x] `tools/RuleAudit` 위반 0
- [x] 각 구획 동작-단언 테스트 존재

---

## [x] FR — permanentCondition 술어 무시 21종 + defenderCondition
- **AS-IS**: 30+ 원본 팩토리가 `Func<Permanent,bool> permanentCondition`(임의 술어)로 대상 SET을 좁힘.
- **미러**: player-scope 연속효과가 뷰 계층(`CardSource`)으로 술어를 **평가**(enabler `ScopePredicateKey` + `EvaluateForCard`/`HasKeyword`). self→SET 승격은 sink에 EngineContext 스레딩. `defenderCondition`은 `CanNotAttackDefenderConditionEffect`+EvaluateAttack.
- **테스트**: G9-050(술어 평가·SET 삭제/서스펜드·defenderCondition). **커밋 `f384a186`**.
- **상태**: ✅ 완료·커밋됨.

## [x] FR2·M-1 — per-card 술어 3종
- **security-DP wrong-player**: AS-IS `ChangeSecurityDigimonCardDPPlayerEffect(cardCondition, …)` — cardCondition이 대상 **플레이어까지** 결정(LM_040: `cardSource.Owner == Enemy`). 포팅 owner-scope 하드코딩 = 버그 → `ScopeAnyPlayerKey`+술어 평가. **G9-052**.
- **UseRequirements**: AS-IS `CanUseCondition = HasMatchConditionOwnersPermanent/BreedingPermanent(PermanentCondition)` — owner가 매칭 Digimon/Tamer(배틀|브리딩) 보유 시에만 ignore-color 활성. → 게이트 폴딩 + `HasContinuousFlag` condition-aware. **G9-052**.
- **AddSelfDigivolution cardCondition**: AS-IS 대상 카드 집합 좁힘(기본 self). → player-scope+`TargetCardCondition`. **G9-052**.
- **상태**: ✅ 완료(미커밋).

## [x] M-2 — cardEffectCondition(상대효과 한정) 스레딩
- **AS-IS**: `CannotReturnToHand`/`CanNotBeTrashedBySkill`의 `Func<ICardEffect,bool> cardEffectCondition` — BT11_060 = `IsOpponentEffect(cardEffect, card)`(상대 효과로만 제약, 자기 효과는 허용). 대부분은 trivial.
- **미러**: 원인 효과의 owner만 필요 → sink `mutation.SourceEntityId`를 restriction 평가에 스레딩(`IsRestrictedFromCause`+`CausingEffectPredicateKey`), 인자 타입 `Func<CardSource,bool>`(원인 소스).
- **테스트**: G9-053(상대-발동 차단·자기-발동 허용·무조건 차단).
- **상태**: ✅ 완료(미커밋). (battle-condition은 유일 사용자 EX8_068 trivial=이미 1:1, G9-054.)

## [x] M-3 — 추가진화 비용(고정+동적 costEquation) 배선
- **AS-IS**: `AddDigivolutionRequirement`의 비용 = `costEquation != null ? costEquation() : digivolutionCost`(added 경로 자체 비용). 포팅은 binding에 미emit·미소비였음(printed 비용만).
- **미러**: `AddedEvolutionCostKey`/`AddedEvolutionCostEquationKey` emit + `DigivolveAction.TryGetAddedDigivolutionCost` + printed 실패 시 added 경로 비용 적용.
- **테스트**: G9-044(printed 2 거부·added 3 수락·동적 6).
- **상태**: ✅ 완료(미커밋). effectName(Func<string>)은 `SetEffectName` 표시 라벨=cosmetic, 무시 1:1.

## [x] M-5 — ChangeBaseDPGlobal (스코프+BaseDp seal 이중 버그)
- **AS-IS**: `ChangeBaseDPGlobalEffect`의 `PermanentCondition = permanentCondition(permanent) && !CanNotBeAffected` — **owner 스코프 없음**(양 플레이어). 그리고 base-DP를 실제로 조정.
- **미러**: (1) `scopeAnyPlayer`로 양 플레이어. (2) **BaseDp modifier를 아무도 소비 안 하던 seal** → `ContinuousDpGate.ResolveDp`가 BaseDp를 base에 먼저 fold.
- **테스트**: G9-052(양측 Lv5 +1000, Lv4 아님).
- **상태**: ✅ 완료(미커밋).

## [x] M-4 — Decoy 언실 · 링크 3종 언실
- **Decoy**: AS-IS `CanActivateDecoy`/`DecoyProcess`가 키워드를 **삭제 시점 라이브 평가**. 포팅은 죽은 `HasDecoyKey` 메타를 읽어 inert였음 → `FindDecoyRedirect`/`Candidates`가 **라이브 키워드(`HasKeyword`)** 인식(sink·timing에 registry 전달). **G9-055**.
- **링크 3종**: `ChangeSelfLinkMax`·`ChangeLinkMaxStatic`(linkedMaxDelta)·`GrantedReduceLinkCost`(linkCostDelta) — metric 없음+read 미반영 이중 seal → `NumericModifierMetric.LinkedMax/LinkCost`+emit+`LinkHelpers.ResolveLinkedMax`/`ResolveLinkCost`(fold)+context 스레딩. **G9-056**.
- **상태**: ✅ 완료(미커밋). ⚠️ **주의**: 나머지 삭제-치환 9종(Evade/Barrier/Save/Fortitude/Ascension/Decode/Partition/Scapegoat/Fragment)은 동일 seal이나 **미완**(사용자 대기 지시) — [fidelity_master_goals.md](fidelity_master_goals.md) 참고. Decoy 방식(라이브 키워드 읽기)이 충실한 정답, 메타 브릿지는 금지.

---

## 결론
6 구획 **전부 AS-IS 원본 확인 후 1:1 미러로 구현·테스트됨**. 288 green · RuleAudit 0. FR만 커밋됨(`f384a186`), 나머지 5 구획 + 테스트 + 문서는 미커밋(지시 대기).

---

## 부록: 삭제-치환 키워드 seal 상태 정정 + 트리거 불일치 (코드 검증, 2026-07-01)

> 이전에 "9종 sealed"라 한 건 **부정확**(doc 주석·기억 기반). 실제 코드 확인 결과 정정.

### 실제 seal 상태 (`|| HasKeyword` 라이브-인식 유무로 판정)
- **이미 언실됨(4)**: Decoy(이번 세션) · **Decode·Partition·ArmorPurge**(기존 GR-005 `|| HasKeyword`).
- **아직 SEAL(7)**: **Evade · Barrier · Save · Fortitude · Ascension · Scapegoat · Fragment** — 게이트가 메타 플래그만 읽고 라이브 키워드 미인식(프로덕션 무동작). ← M-4 잔여 = **7종**(9종 아님).

### 🔴 트리거 불일치 (언실/검증 시 반드시 교정)
- **Partition** — 원본: "leave the battle area **other than by one of your effects** or in battle". 헤드리스: `!DeletedByBattleKey`만 확인 = **"당신의 효과가 아닌" 제외 조건 누락** → 자기 효과로 떠나도 발동(원본은 제외). **게다가 엔진에 by-own-effect 구분 플래그 부재**(`DeletedByEffectKey`는 플레이어 구분 안 함) → 이 조건을 구현하려면 삭제 원인의 소유자 추적부터 필요. **실 fidelity 갭.**
- **Decode** — 원본: "leave other than in battle". 헤드리스: `!DeletedByBattleKey` = **일치**(battle 조건 갭 없음). 단 원본 "leave"는 삭제 외 이탈(바운스 등)도 포함 → 헤드리스가 삭제 경로에만 있으면 비-삭제 이탈 미커버 가능성(별도 모델링 확인 필요).
- **Save/Ascension/Fortitude** = 사후형([On Deletion] 등), Evade/Barrier/Fragment/Scapegoat = 생존형(삭제 취소). (앞서 Save를 생존형으로 잘못 분류한 것 정정.)

### 교훈
이번에도 doc 주석·기억으로 답하다 3연속 틀리고, **코드를 직접 읽자 매번 교정됨**. 언실 착수 시 각 키워드 원본 텍스트(DataBase.cs) ↔ 실제 게이트 코드 대조를 강제.
