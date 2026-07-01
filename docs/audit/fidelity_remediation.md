# 충실도 위반 리스트업 + 수정계획 (W3~W5 자기감사)

- 작성: 2026-07-01
- 계기: 특수플레이 재료 조건을 이름으로 뭉갠 1:1 위반 지적 → 동일 패턴 전수 감사.
- 핵심 위반 유형: **팩토리가 `Func<Permanent,bool> permanentCondition`(임의 술어)을 받아놓고 평가하지 않고 self/player-scope(전체)로 뭉갬.** = 가드-축소 FAIL.

---

## A. 침묵 단순화 (진짜 위반 — 술어 무시)

### A1 — self로 뭉갬 (9). 원본이 SET이면 틀림(self만 적용)
| 팩토리 | 구현 | 원본이 SET일 때 |
|---|---|---|
| `CanNotBeDestroyedStaticEffect` | ContinuousSelfRestriction(PreventDeletion) | self만 보호 (SET 틀림) |
| `ImmuneFromDPMinusStaticEffect` | ContinuousSelfRestriction(ImmuneFromDpMinus) | self만 |
| `InvertSAttackStaticEffect` | ContinuousSelfModifier(InvertSA) | self만 |
| `CantSuspendStaticEffect` | ContinuousSelfRestriction(CannotSuspend) | self만 |
| `CannotReturnToHandStaticEffect` | ContinuousSelfRestriction(CannotReturnToHand) | self만 |
| `CannotReturnToDeckStaticEffect` | ContinuousSelfRestriction(CannotReturnToDeck) | self만 |
| `CanNotBeDestroyedByBattleStaticEffect` | ContinuousSelfRestriction(PreventBattleDeletion) | self만 |
| `CanNotBeTrashedBySkillStaticEffect` | ContinuousSelfRestriction(ImmuneStackTrashing) | self만 |
| `CanNotAffectedStaticEffect` | ContinuousSelfRestriction(ImmuneFromEffects) | self만 |
| `CanNotAttackSelfStaticEffect` | ContinuousSelfRestriction(CannotAttack) | `defenderCondition` 무시 |

### A2 — player-scope(전체 owner Digimon)로 뭉갬 (11). 원본이 좁힌 조건이면 과다적용
| 팩토리 | 구현 | 좁힌 조건일 때 |
|---|---|---|
| `RushStaticEffect` | PlayerScopeKeyword(Rush) | 전체에 과다부여 |
| `RebootStaticEffect` | PlayerScopeKeyword(Reboot) | 〃 |
| `AllianceStaticEffect` | PlayerScopeKeyword(Alliance) | 〃 |
| `JammingStaticEffect` | PlayerScopeKeyword(Jamming) | 〃 |
| `CollisionStaticEffect` | PlayerScopeKeyword(Collision) | 〃 |
| `VortexCanAttackPlayersStaticEffect` | PlayerScopeKeyword(Vortex) | `attackerCondition` 무시 |
| `BlockerStaticEffect` | PlayerScopeKeyword(Blocker) | 〃 |
| `ChangeSAttackStaticEffect` | PlayerScopeModifier(SAttackDelta) | 〃 |
| `ChangeBaseDPGlobalEffect` | PlayerScopeModifier(BaseDpDelta) | 〃 (게다가 "global"=양측인데 owner만) |
| `ChangeLinkMaxStaticEffect` | PlayerScopeModifier(LinkedMaxDelta) | 〃 (+링크 소비자 latent) |
| `ChangeDPStaticEffect` (W3) | PlayerScopeModifier(DpDelta, scope"Digimon") | 좁힌 조건 무시 |

### ✅ 이미 수정 (참고)
- 특수플레이 재료 조건 → `SpecialPlayMaterial(Func<CardSource,bool>)` 술어 평가 (G9-049).
- `AddSelfDigivolutionRequirement` → 술어 평가 (G9-044). **이 둘은 올바른 패턴.**

---

## B. 문서화된-latent (preemptive-seal) — 침묵은 아니나 behavior 미배선
grant는 live(HasKeyword/쿼리)이나 동작 소비자 미마이그레이션. fidelity_debt.md에 기록됨.
- 링크: `ChangeSelfLinkMax`·`ChangeLinkMaxStatic`·`GrantedReduceLinkCost` (Enforce/LinkSelfEffect 소비자 latent)
- 키워드 동작-metadata: `Collision`·`Vortex`·`Ascension`·`TreatAsDigimon`·`MindLink` (동작 소비자가 metadata/타입 판정 사용)
- W2 seal 키워드(Barrier/Evade/Save 등 동작 latent)

## C. 근사/스코프 단순화
- `ChangeBaseDPGlobalEffect`: "global"(양 플레이어) → owner-scope만.
- `ReplaceBottomSecurity`: 바닥=security 리스트 마지막 원소 가정.
- `RevealLibraryClass`: 정보성 no-op(풀정보 모델).

## D. 명시적 NotSupported/STOP (위반 아님)
`~28곳` 명시적 NotSupported + 레시피 §4-b STOP-목록(AddSkill·AddEffectToPlayer·PlayOption·AddSelfLinkCondition·AddMaxTrashCountDigiXros·Jogress 임의조건·커스텀 coroutine). — 정직히 표시됨.

---

# 수정계획 (단계)

## P1 — enabler: player-scope 연속효과가 **임의 술어 평가**
- 바인딩 Context.Values에 `permanentPredicate: Func<Permanent,bool>` 실림.
- `PlayerScopeContinuousHelpers.CollectApplicable` / `ContinuousScopeEvaluation.EvaluateForCard`가 후보 카드를 `Permanent`로 만들어 술어 평가 → 통과분만 포함. (특수플레이 `TryMatchMaterials`와 동형.)
- 테스트: 좁힌 술어("Level==N"/트레잇)로 매칭, 비매칭 제외.

## P2 — A2(10) 팩토리를 predicate 경로로
- 각 `*StaticEffect`: `permanentCondition == null`이면 기존(전체), non-null이면 **player-scope + predicate**로.
- Keyword/Modifier player-scope 이펙트에 predicate 인자 추가(P1 소비).

## P3 — A1(9~10) self→SET 승격
- `permanentCondition == null` → self(현행). non-null → **player-scope-with-predicate** (제약/replacement의 player-scope 버전 필요).
- 제약(Cannot*)·replacement(PreventDeletion 등)의 player-scope 평가 경로 추가(ContinuousRestrictionGate/BattleDeletionGate가 player-scope+predicate도 읽게).

## P4 — 각 그룹 술어-평가 테스트
- "이름/전체 아닌 좁힌 술어"로 매칭됨 + 비매칭 제외를 단언(G9-050+).

## P5 — B(seal) 처리
- 링크/키워드-동작 소비자 마이그레이션은 별도(수요 시). 지금은 fidelity_debt에 정직히 유지(침묵 아님).

## 우선순위
1. **P1 enabler**(공통) → 2. **A2 player-scope predicate**(과다적용이 더 위험) → 3. **A1 self→SET** → 4. 테스트 일괄 → 5. seal는 유지/기록.
