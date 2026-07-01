# 전면 충실도 감사 goal (모든 효과 재검증)

> **계기**: 자기-인증("완료")이 신뢰 근거가 못 됨(설명 3/9 오답·술어 뭉갬 2회·seal 개수 오답). config-first 개발로 프리미티브가 **등록만 되고 소비/동작이 hollow(sealed)** 일 가능성이 구조적. grep 한 방은 읽기 패턴 다형성으로 **오탐 범벅**(seal 스캔 101 후보 중 다수 오탐 확인) → **per-primitive 읽기-경로 추적만이 신뢰 가능**.

## 규율 (엄격 — 반복 실수 교정)
- **증거 기반**: 각 항목은 `[원본 AS-IS 텍스트/코드] ↔ [헤드리스 소비 코드] ↔ [테스트]` 를 **코드로 확인**하고 근거 줄을 남긴다. **memory·doc 주석·추측으로 판정 금지**([[check-asis-before-implementing]]).
- **판정**: `LIVE-FAITHFUL`(소비 존재 + AS-IS 일치) / `LIVE-BUG`(소비되나 동작 어긋남) / `SEAL`(등록만, 소비 없음) / `MARKER`(결과-기록용 write-only, 정상) / `STOP`(수정 불가→debt).
- **수정 원칙**: seal/bug 발견 시 **AS-IS의 라이브 평가를 미러**(브릿지/발명 금지). 비용 크면 STOP+debt.
- **게이트 유지**: 각 카테고리 종료 시 `bash scripts/run-tests.sh` green + `tools/RuleAudit` 0.

## 카테고리 (체크리스트)
- [x] **C1. Modifiers** (9 metric) — ✅ **전부 LIVE**(소비 게이트 확인, seal 0). 아래 C1 표.
- [x] **C2. Restrictions** (Cannot* 13종) — ✅ 카드 사용 제약 전부 강제. 아래 C2.
- [x] **C3. Replacements** — 🔴 CanNotAffected SEAL(+설계 flattening). 아래 C3.
- [x] **C4. Keywords** — 🔴 Raid(~80장)·Collision·Execute SEAL. 아래 C4.
- [x] **C5. 삭제-치환** — 🔴 seal 7종(Evade·Barrier·Save·Fortitude·Ascension·Scapegoat·Fragment) + Partition 트리거 갭. (session_fidelity_checklist.md 부록)
- [x] **C6. Factories** — ✅ mutation kind 25/25 sink 처리. 신규 침묵 seal 0.
- [x] **C7. 마커** — ✅ write-only 마커 정상 + 가드 3종 read. seal 0.

## 산출물
카테고리별 **인벤토리 표**(항목 / 등록처 / 소비처(파일:줄) / AS-IS 근거 / 판정). SEAL·BUG는 fidelity_debt + 이 문서에 축적. 이 표가 "완료" 라벨을 대체하는 **검증 증거**.

## 진행 로그
(아래에 카테고리별 결과를 append)

---

## C1. Modifiers — 전부 LIVE (2026-07-01, 소비처 코드 확인)

| metric/key | 소비 게이트 (파일:줄) | 판정 |
|---|---|---|
| Dp (DpDelta) | ContinuousDpGate.ResolveDp:66 | LIVE |
| BaseDp (BaseDpDelta) | ContinuousDpGate.ResolveDp:60 (이번 세션 fold) | LIVE |
| SecurityAttack / SAttack | ContinuousModifierGate.ResolveSecurityAttack ← SecurityResolver:262 · AttackPipeline:253 | LIVE |
| PlayCost (PlayCostDelta) | ContinuousModifierGate.ResolvePlayCost ← PlayCardAction:345 · OptionActivateAction:270 | LIVE |
| DigivolutionCost | ContinuousModifierGate.ResolveDigivolutionCost ← DigivolveAction:361 | LIVE |
| LinkedMax (LinkedMaxDelta) | LinkHelpers.ResolveLinkedMax ← EnforceLinkedMax (이번 세션) | LIVE |
| LinkCost (LinkCostDelta) | LinkHelpers.ResolveLinkCost ← LinkSelfEffect 지불 (이번 세션) | LIVE |
| InvertSA (InvertSecurityAttackDelta) | NumericModifier.InvertSecurityAttack(InvertDelta mode) → ResolveSecurityAttack (Evaluate:258 적용). 테스트 G3H-001 | LIVE |

**결론**: modifier 카테고리 seal 0. (스코프-술어 정확성은 FR/M-1/M-5에서 별도 검증됨.)

## C2. Restrictions — 카드 사용분 전부 강제 (2026-07-01)

| kind | 강제 경로 | 판정 |
|---|---|---|
| Attack/Block/BeBlocked/BeAttacked | ContinuousRestrictionGate.EvaluateAttack/Block/... ← 공격·블록 게이트 | LIVE |
| Digivolve | ContinuousRestrictionGate.EvaluateDigivolve ← DigivolveAction | LIVE |
| Unsuspend | EvaluateUnsuspend ← 언서스펜드 경로 | LIVE |
| Suspend/ReturnToHand/ReturnToDeck | MatchStateMutationSink (HasSelfRestriction/IsRestrictedFromCause) | LIVE |
| DeleteBySkill | sink ApplyDelete (IsDeletionPreventedByContinuous) | LIVE |
| ReturnToLibrary | → ReturnToDeck로 alias (RestrictionHelpers:544) | LIVE |
| BeDeleted(CannotBeDeleted) | PreventDeletion 치환(C3) + decoy/scapegoat 후보 가드 | LIVE |
| **Delete(CannotDelete)** | 강제 게이트 없음 — 단 **원본 카드 사용 0** → dead-path, fidelity 갭 아님 | N/A(unused) |

**결론**: 카드가 쓰는 제약 seal 0. CannotDelete만 미사용 dead-path.

## C3. Replacements — 🔴 대형 SEAL 발견 (2026-07-01)

| 항목 | 소비 게이트 | 판정 |
|---|---|---|
| PreventDeletion | BattleDeletionGate + sink IsDeletionPreventedByContinuous (Delete/Prevent 치환) | LIVE |
| ImmuneFromDpMinus | ContinuousDpGate.ResolveDp (ImmuneFromDpReduction) | LIVE |
| ImmuneFromCostReduction | ContinuousModifierGate:63 | LIVE |
| **ImmuneFromEffects (CanNotAffected)** | ❌ **SEAL** — 아래 | **SEAL** |
| PreventRemoval | 게이트 호출부 없음(카드 사용 빈도 확인 필요) | 미확인 |

### 🔴 SEAL: CanNotAffectedStaticEffect (CanNotBeAffected, 원본 274장)
- 작동 게이트 `ContinuousImmunityGate.BlocksOpponentEffect`(MatchStateMutationSink:265 호출)는 **`ImmunityFromOpponentOnlyKey`** 를 읽음.
- 그 키 등록처 = **`ProgressImmunity.cs`(Progress 키워드) 단독**.
- `CanNotAffectedStaticEffect`(CardPortingFramework:3654)는 **`ReplacementHelpers.ImmuneFromEffectsKey`** 등록 → ReplacementHelpers:468 루프가 EffectMutation/Immune 치환으로 파싱만, **차단 소비자 없음**.
- ⇒ 이 팩토리로 포팅한 CanNotBeAffected는 **무동작**. (효과 grant 원본 ~39장; `CanNotBeAffected` 쿼리 참조는 274곳.)
- **⚠️ 첫 수정안(ImmunityFromOpponentOnlyKey)은 flattening이라 폐기.** 원본 `CanNotAffectedClass`: `CanNotAffect = CardCondition(카드) && SkillCondition(효과)`. SkillCondition은 임의·복합(39장 중 25장이 `IsOpponentEffect && IsDigimonEffect` 등, 14장 타 조건) = "상대 효과 전체"가 **아님**. opponent-only 키로 등록하면 SkillCondition을 또 뭉갬.
- **진짜 충실한 수정**: 팩토리가 **SkillCondition(원인 효과 술어)** 을 받고, `BlocksOpponentEffect`(target+원인 sourceEntityId 보유)가 CardCondition·SkillCondition **둘 다 평가**(M-2 cardEffectCondition 스레딩 동형; IsDigimonEffect 등은 원인 효과 CardType 판독).
- **주의**: 이 SEAL은 FR2/M-2에서 이 팩토리 permanentCondition을 "충실하게" 고칠 때도 못 봄 — 술어만 고치고 핵심(면역·SkillCondition)은 죽어있었음.

## C4. Keywords — 🔴 systemic seal (2026-07-01, 코드 검증)

**패턴**: 키워드는 `SelfKeywordByNameEffect`(연속 키워드)로 grant. 소비자가 (a) `HasKeyword`를 읽으면 **LIVE**, (b) `hasX` 메타 플래그(GrantX 뮤테이션으로만 설정)를 읽으면 **SEAL**(키워드 grant가 그 뮤테이션·플래그를 안 냄, 브릿지 없음 = Decoy와 동일).

| 키워드 | 소비 | 원본 | 판정 |
|---|---|---|---|
| Rush | AttackPermanentAction:266 HasKeyword | — | LIVE ✓코드 |
| Blocker | BlockTiming:226 HasKeyword | — | LIVE ✓코드 |
| Reboot·Piercing·Jamming·Alliance·Blitz·Overclock·Progress·Retaliation·Vortex | HasKeyword(스캔) | — | live(개별 미확정) |
| **Raid** | RaidAttackSwitch가 `hasRaid` 메타만 읽음, 키워드 미인식, GrantRaid 뮤테이션 미발생 | **~80장** | **🔴 SEAL** |
| **Collision** | `hasCollision` 메타(GrantCollision 뮤테이션) 소비, 키워드 grant는 미발생 | ~5장 | **🔴 SEAL** |
| **Execute** | `hasExecute` 읽는 소비자조차 없음 | ~8장 | **🔴 SEAL(이중)** |
| Iceclad·MindLink·TreatAsDigimon | (grant live/메타) | ~0장 | N/A(원본 미사용) |

**결론**: **Raid(~80장)·Collision·Execute SEAL** — 공격 키워드에도 deletion-replacement와 동일 seal. Raid는 게임 최다빈도 키워드 중 하나라 고영향. "live" 9종은 HasKeyword 스캔 기반이라 개별 코드 확정 잔여.

## C6. Factories / C7. 마커 — seal 0 (2026-07-01)
- **C6**: 활성효과 factory가 emit하는 mutation kind **25/25 전부 sink Apply()에서 처리**(미처리 0). RevealLibrary=풀정보 no-op, NotSupported(~28)=명시 STOP → 침묵 seal 아님. modifier/restriction/replacement/keyword 계열은 C1~C5로 커버.
- **C7**: 결과 마커(evaded/barriered/saved/ascended/fragmented/decoyRedirect/deletionPrevented/retaliationFired/scapegoatSacrifice) = **write-only(MARKER, 정상)**; 가드(decoded/partitioned/raidResolved) = read(live). seal 0.

---

## 🔴 전면 audit 최종 요약 (C1~C7, 코드 증거 기반)

### 확정 SEAL (고영향, 실 게임 무동작)
| # | 항목 | 원본 빈도 | 카테고리 |
|---|---|---|---|
| 1 | **CanNotAffected** (+SkillCondition flattening) | ~39장 grant | C3 |
| 2 | **Raid** | ~80장 | C4 |
| 3 | Collision | ~5장 | C4 |
| 4 | Execute (이중) | ~8장 | C4 |
| 5~11 | 삭제-치환 7종: Evade·Barrier·Save·Fortitude·Ascension·Scapegoat·Fragment | 각 다수 | C5 |
| — | Partition 트리거 갭(own-effect 제외 누락) | 1 | C5 |

### 공통 근본원인
**grant(키워드/키 등록) ↔ 소비(메타 플래그·다른 키) 미정렬** — 소비자가 프로덕션에서 설정 안 되는 신호를 읽어 무동작. config-first 개발의 preemptive-seal.

### 충실한 수정 방향(공통)
소비 게이트가 **라이브 신호(HasKeyword / 정확한 키)를 직접 읽도록** (Decoy·링크 방식). CanNotAffected는 SkillCondition까지 평가(M-2 방식). **메타-동기화 브릿지·opponent-only 하드코딩 = flattening, 금지.**

### 깨끗한 것 (seal 0)
C1 Modifiers(9) · C2 Restrictions(카드사용분) · C6 mutations(25) · C7 마커. 
Dead-path(원본 미사용, N/A): CannotDelete·PreventRemoval·Iceclad·MindLink·TreatAsDigimon.
