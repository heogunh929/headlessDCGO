# 충실도 복원 마스터 goal (통합 리스트업)

> **범위:** 카드-facing 팩토리가 원본의 조건/술어/값을 **받아놓고 뭉갠**(무시·평면화·과다적용) 것 전부. FR(permanentCondition)·FR2(그 외 인자)·seal·근사를 **하나의 백로그**로 통합.
> **공통 종료 기준·규율:** 항목별 **원본 `DCGO/` 1:1 확인(추측 금지)** → 소비 경로가 뷰 계층/문맥으로 술어·값을 평가 → `bash scripts/run-tests.sh` green + **술어-평가 테스트**(좁힌 조건 매칭 + 비매칭 제외) + `tools/RuleAudit` 0. 뭉개면 FAIL. 불가하면 STOP + 이 문서/ fidelity_debt에 기록("포팅 가능"이라 하지 말 것).
> **참고:** 상세 근거 [fidelity_remediation.md](fidelity_remediation.md) · FR 완료분 [fidelity_remediation_goals.md](fidelity_remediation_goals.md) · FR2 상세 [fidelity_remediation2_goals.md](fidelity_remediation2_goals.md).

---

## ✅ 완료 (이 세션)

| 구획 | 내용 | 검증 |
|---|---|---|
| 특수플레이 재료 | `SpecialPlayRecipe`를 `SpecialPlayMaterial(Func<CardSource,bool>)` 술어 기반으로 (이름 평면화 제거) | G9-048/049 |
| **FR-P1** enabler | player-scope 연속효과가 임의 술어 평가(`ScopePredicateKey` + EvaluateForCard/HasKeyword) | G9-050 |
| **FR-P2** (11) | Rush·Reboot·Alliance·Jamming·Collision·Vortex·Blocker·ChangeSAttack·ChangeBaseDPGlobal·ChangeLinkMax·ChangeDP → permanentCondition 술어 | G9-050 |
| **FR-P3** (10) | self→SET 승격 + registry-only sink/battle에 EngineContext 스레딩(`ApplicableEffects`) + CanNotAttackSelf(defenderCondition) | G9-050 |
| FR 회귀 2건 | `ActivatedEffectResolver` sink에 context 전달 · Delete/Prevent 파싱 복원 | G9-050 |
| **FR2-C** 특수플레이 condition | Blast·BlastDNA·Jogress×2 `condition`을 recipe가 평가(과다가용 제거) | G9-051 |
| **FR2-A** Gain1Memory | permanentCondition을 획득 게이트로 폴딩(무조건 획득 버그) | G9-051 |

---

## 🔲 잔여 백로그 (우선순위순)

> **✅ 2차 위반 감사 + 조치 완료(2026-07-02)**: M-4/M-5 완료 후 "임의 구현" 전수 감사에서 A급 4건 + B급 9건 + C급 10건 발견([**fidelity_violation_audit2.md**](fidelity_violation_audit2.md)) → 설계·AS-IS 매칭 검증([**fidelity_violation_fix_design.md**](fidelity_violation_fix_design.md)) → **A1~A4·B1~B5·C즉시군 전부 구현·green**. 잔여는 debt로 기록(fidelity_debt.md "위반 조치 구현" 절): 배틀-경로 키워드 스냅샷, B4 다중조건 패스, B5 다중공격자, C5 [Counter] 마커, C7/C8/C9(별건).

### M-1 — FR2-A: per-card 술어
- [x] **`ChangeSecurityDigimonCardDPStaticEffect(cardCondition)`** — 원본 확인: cardCondition이 대상 **플레이어까지** 결정(예: 적 security -DP). 기존 포팅은 owner-scope 하드코딩 = **wrong-player 버그**. → `ScopeAnyPlayerKey`(양 플레이어) + cardCondition을 scopePredicate로 평가. **G9-052**.
- [x] **`UseRequirements(cardCondition)`** — 원본 확인: ignore-color는 owner가 cardCondition 매칭 Digimon/Tamer(배틀|브리딩) 보유 시에만 활성(CanUseCondition). 기존은 무조건. → 게이트 폴딩 + `HasContinuousFlag`을 condition-aware(`ApplicableEffects`)로. **G9-052**.
- [x] **`AddSelfDigivolutionRequirementStaticEffect(cardCondition)`** — 원본 확인: cardCondition = 추가 진화요구가 적용되는 **대상 카드 집합**(기본 self; ST8_04는 "손패의 UlforceVeedramon"). `AddedDigivolutionRequirementPredicateEffect`에 player-scope+`TargetCardCondition` 모드 추가 + `MatchesAddedDigivolutionRequirement`을 `ApplicableEffects`(ScopePredicate 평가)로 전환 → 매칭 카드에만 도달. **G9-052**.
- [→ M-4] `DecoySelfEffect(permanentCondition)` — 원본 확인 결과, permanentCondition 이전에 **Decoy 키워드 grant가 redirect 메커니즘과 프로덕션에서 미연결**(`HasDecoyKey` 메타는 테스트에서만 설정, 키워드→메타 브릿지 부재). = per-card 술어가 아니라 **seal**(M-4). permanentCondition은 seal 배선과 함께 처리.

### M-2 — FR2-B: per-effect / per-battle 술어 (문맥 스레딩)
- [x] **`CannotReturnToHandStaticEffect(cardEffectCondition)`** + **`CanNotBeTrashedBySkillStaticEffect(cardEffectCondition)`** — 원본: 대부분 trivial(any effect)이나 BT11_060은 `IsOpponentEffect`. 원인 효과 owner만 필요 → sink `mutation.SourceEntityId`를 restriction 체크에 스레딩(`IsRestrictedFromCause`+`CausingEffectPredicateKey`), 타입 `Func<CardSource,bool>`(원인 소스)로 매핑. **G9-053**.
- [x] **`CanNotBeDestroyedByBattleStaticEffect(canNotBeDestroyedByBattleCondition)`** — 검증: **EX8_068이 전 게임 유일 사용자**(전수 grep). 두 조건 중 **permanentCondition(DS-trait SET)은 포팅이 강제**(G9-054: 매칭만 배틀 면역, 비매칭 아님 — condition-less 아님). 별도 4-arg battle-condition(`permanent == 공격자 || 방어자`)만 omit인데, "배틀로 삭제"는 참가자에게만 발생 → 삭제 검사 시점에 항상 참 = **trivial**(동작 동일). non-trivial 4-arg 형태를 쓰는 카드는 0장.

### M-3 — FR2-C: 동적/기타
- [x] **`AddSelfDigivolutionRequirementStaticEffect(costEquation)`** — 조사 결과 더 큰 갭: added 요구의 **비용 전체**(고정+동적)가 binding에 미emit·미소비였음. `AddedEvolutionCostKey`/`AddedEvolutionCostEquationKey` emit + `DigivolveAction.TryGetAddedDigivolutionCost` + **printed 실패 시 added 경로 비용 적용**(`costEquation() ?? digivolutionCost`). **G9-044**(printed 2 거부·added 3 수락·동적 6).
- [x] **`ChangeDPStaticEffect(effectName: Func<string>)`** — 원본 확인: `SetEffectName`(표시 라벨)만, 게임 로직 미사용 = **cosmetic**. 무시가 1:1(gameplay 무영향).

### M-4 — preemptive-seal (grant는 live, 동작 소비자 미배선) — 침묵 아님(문서화됨)
- [x] **Decoy 언실** (M-1에서 이동): `DeletionReplacementGate.FindDecoyRedirect`/`Candidates`가 `HasDecoy` 헬퍼로 **Decoy 키워드(라이브 grant)** 를 인식(메타 플래그 OR `ContinuousKeywordGate.HasKeyword(registry, ...)`); sink·timing 호출부에 `effectRegistry` 전달. Decoy가 이제 실제 작동. **G9-055**(키워드 홀더가 적-발동 삭제의 redirect로 인식·no-registry/no-decoy 대조). ~~잔여 refinement~~ → **✅ D1 완료(2026-07-02)**: grant에 술어 저장(`SelfKeywordByNameEffect(permanentCondition)` + `keyword.permanentCondition` 키) + `FindDecoyRedirect/Candidates`가 **보호 대상**에 라이브 평가(EngineContext 스레딩; sink defer는 문서화된 superset) + AS-IS 보호대상=디지몬 조건 폴딩. **G9-055 +4 테스트**. 상세: [fidelity_m4m5_design.md](fidelity_m4m5_design.md) D1.
- [x] **링크 3종 언실**: `ChangeSelfLinkMax`·`ChangeLinkMaxStatic`(linkedMaxDelta)·`GrantedReduceLinkCost`(linkCostDelta) — 이중 seal이었음(metric 없음 + read 미반영). `NumericModifierMetric.LinkedMax/LinkCost` 추가 + modifier emit + `LinkHelpers.ResolveLinkedMax`/`ResolveLinkCost`(EvaluateForCard fold) + EnforceLinkedMax/Link 지불에 context 스레딩. **G9-056**(max 1→3·cost 3→1·0 clamp).
#### 🔴 삭제-치환 키워드 10종 = 단일 아키텍처 seal (전수조사 2026-07-01)
`Evade·Barrier·Decoy·Fragment·Scapegoat·Save·Fortitude·Ascension·Decode·Partition` — **10종 전부** grant는 키워드(`SelfKeywordByNameEffect`)로 하는데, 모든 게이트가 **`Has*Key` 메타 플래그**를 읽음. 그 플래그는 **프로덕션에서 SET=0(테스트만), 키워드→메타 브릿지 없음** → 전부 inert였음.
- **충실한 수정 = 라이브 키워드 읽기** (게이트가 `ContinuousKeywordGate.HasKeyword`로 직접 확인 = AS-IS의 라이브 평가 `CanActivateDecoy`/`EvadeProcess`와 동일). **메타 동기화 브릿지는 AS-IS에 없어 채택 금지**(시도했다 제거함).
- **정정(코드 검증)**: 이미 언실된 것 = Decoy(이번)·**Decode·Partition·ArmorPurge**(기존 GR-005 `|| HasKeyword`). **실제 잔여 SEAL = 7종**: Evade·Barrier·Save·Fortitude·Ascension·Scapegoat·Fragment. (Scapegoat/Fragment는 게이트에 라이브-읽기 파라미터만 추가·호출부 미전달 = 실효 미완.)
- **🔴 Partition 트리거 갭**(언실과 별개, 검증 시 교정): 원본 "leave **other than by your own effects** or in battle"인데 헤드리스는 `!DeletedByBattleKey`만 = "당신의 효과 아닌" 제외 누락. **엔진에 by-own-effect 구분 플래그 부재** → 삭제 원인 소유자 추적 선결. (Decode 트리거는 원본과 일치.)
- 잔여 7종 언실 = 게이트를 라이브-읽기로 + 호출부 스레딩. 대기(사용자 지시).

#### 키워드-동작(전투 메커닉, 별개)
- [x] `Collision`·`Vortex`·`Ascension`·`TreatAsDigimon`·`MindLink`: **전부 배선 완료(2026-07-02)** — 설계·구현: [**fidelity_m4m5_design.md**](fidelity_m4m5_design.md) (D1 Decoy 술어 · K1 Vortex player-target un-flatten · K2 Ascension top-삽입 · K3 Collision 면역 가드 · K4 TreatAsDigimon 중앙 chokepoint · K5 MindLinkClass). 잔여 debt(CanAddSecurity 스켈레톤·security-scan 엣지·flip 축소 등)는 fidelity_debt.md.
  - 정정: 사전 조사에서 소비자 0은 MindLink·TreatAsDigimon 2종뿐이었음(나머지 3종은 검증 갭만).

### M-5 — 근사/스코프 단순화
- [x] **`ChangeBaseDPGlobalEffect`** — 이중 버그였음: (1) 원본 "global"=양 플레이어인데 owner-scope만 → `scopeAnyPlayer`. (2) **BaseDp modifier를 아무도 소비 안 함**(ContinuousDpGate가 Dp metric만 접음) = DP 무영향 seal → `ContinuousDpGate.ResolveDp`가 BaseDp modifier를 base에 먼저 fold. **G9-052**(양측 Lv5 +1000, Lv4 아님).
- [x] `ReplaceBottomSecurity` — 바닥=security 리스트 마지막 원소 가정. **AS-IS 검증 완료(2026-07-02): 원본도 `Last()`=bottom/`Insert(0)`=top → 가정 정확, 1:1.** 근거: [fidelity_m4m5_design.md §6](fidelity_m4m5_design.md).
- [x] `RevealLibraryClass` — 정보성 no-op(풀정보 모델). **AS-IS 검증 완료(2026-07-02): 원본도 순수 정보성(비파괴 read+UI/로그, 게임로직 소비자 0) → no-op이 1:1.** 근거: [fidelity_m4m5_design.md §6](fidelity_m4m5_design.md).

---

## 실행 대화문 (복붙용)
```
충실도 마스터 goal 진행. docs/audit/fidelity_master_goals.md 백로그 우선순위대로(M-1 per-card 술어 → M-2 문맥 스레딩 → M-3 → M-4 seal → M-5 근사).
각 항목: 원본 DCGO에서 그 조건/값이 실제로 무엇을 좁히는지 1:1 확인(추측 금지) → 소비 경로가 뷰 계층/문맥으로 평가하도록 배선 → bash scripts/run-tests.sh green + 술어-평가 테스트(좁힌 조건 매칭+비매칭 제외) + tools/RuleAudit 0. 이름/스칼라/스코프로 뭉개면 FAIL. 이전 항목 green 후 다음. 불가하면 STOP+fidelity_debt 기록. 커밋은 내가 지시할 때.
```
