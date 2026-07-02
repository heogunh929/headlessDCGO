# 카드 포팅 충실도 부채 레저 (fidelity debt)

- 작성일: 2026-06-30
- 방법: **원본 `DCGO/.../<id>.cs` ↔ 포팅 `src/.../<id>.cs` 카드별 코드 대조** (추정 없음, 병렬 감사 에이전트 3 + 직접 1). 기준: [[asis-structure-mirror-rule]] = 카드-facing 로직 **1:1**.
- 판정:
  - **PASS** — 원본과 1:1 (형태 매핑 차이는 허용: `Func<Permanent,bool>`→`Func<HeadlessEntityId,bool>`, inline ActivateClass→팩토리 헬퍼).
  - **PARTIAL** — 일부 타이밍 분기는 1:1, 다른 분기는 미구현(주로 테이머 [Security] = `PlaySelfTamerSecurityEffect` stub).
  - **FAIL** — 카드 코드 자체가 원본과 다름(게이트/조건/임계값 누락·단순화).
  - **STUB** — 카드 전체가 무동작(`DeferredCardEffect`).
- **구분**: "활성효과가 실루프에서 자동 발동 안 됨(imperative 검증만)"은 **엔진 통합 갭**이지 카드-코드 실패가 아님 → 별도 열.
- **PASS 기준 (엄격)**: 원본 가드를 뺐으면 **발동 빈도·중복성·"희귀 엣지"는 PASS 근거가 아님 = FAIL.** 단, 그 가드와 **동일한 동작을 엔진이 다른 방식으로 강제함을 *검증*한 경우에 한해** PASS(추측 금지). 예: ST3_05 `CanAddMemory` → `InMemoryHeadlessMemoryController.Clamp`(상한 강제, 검증); ST3_09 `Library>=1` → `MoveFromZoneTop`이 가용분 클램프(빈 덱 no-op, 검증).

## 요약 (포팅 파일 35장)

| 판정 | 장수 | 비율 |
|---|---|---|
| **PASS (1:1)** | **35** | 100% |
| PARTIAL | 0 | 0% |
| FAIL | 0 | 0% |
| STUB | 0 | 0% |

**진짜 1:1 = 35/35 (100%).** 부채 = **0** — G10 전부 상환.
**복원 진행**: G10-001(ST2_11 once-per-turn) · G10-002(ST3_01·ST3_04 0DP-격파 전제+once) · G10-003(테이머 [Security] 플레이) · G10-004(protected-source) · G10-005(동적 삭제 임계값) · G10-006(배틀-페어링) · G10-007(ST2_15 play-from-under `PlayDigivolutionAsDigimon`) 완료. ✅ ST1/ST2/ST3 진짜 1:1 35/35.
별칭(파일 없음): ST2_07·ST3_07 → `ST1_06`(PASS) 재사용 — dispatch 배선(G9-001) 전제.

---

## 카드별 레저

### ST1/Red (12 PASS · 0 PARTIAL · 0 FAIL)
| 카드 | 판정 | 카드-코드 부채 | 엔진통합 노트 |
|---|---|---|---|
| ST1_01·03·07·11 | PASS | — | — |
| ST1_06 | PASS | — | OnAllyAttack 메모리 트리거 plumbing 완화(recipe §5) |
| ST1_08·13·14·16 | PASS | — | 활성/선택 imperative 해소(미배선) |
| ST1_09 | PASS | — | OnBlockAnyone 트리거 plumbing |
| ST1_12 | **PASS** | (G10-003) [Security] `PlaySelfTamerSecurityEffect` 실구현 → 테이머를 배틀에리어로 플레이(+효과 자동등록) | — |
| ST1_15 | **PASS** | (G10-005) 동적 삭제 임계값 `MaxDpDeleteThreshold(card, 4000)`(기본+상승 누적) 복원 | 선택→삭제 imperative |

### ST2/Blue (11 PASS · 0 PARTIAL · 0 FAIL · 0 STUB) + 별칭 ST2_07
| 카드 | 판정 | 카드-코드 부채 | 엔진통합 노트 |
|---|---|---|---|
| ST2_03 | **PASS** | (G10-004) protected-source 가드 복원: `HasTrashableDigivolutionCards`(보호 안 된 전위카드 ≥1) + `TopCardHasLevel` | 활성 imperative |
| ST2_06 | PASS | — (원본 select=상대 디지몬만, 포팅 일치) | 활성 imperative |
| ST2_09 | **PASS** | (G10-004) protected-source 가드 복원: `HasTrashableDigivolutionCards` | 활성 imperative |
| ST2_08 | PASS | — | — |
| ST2_13·14·16 | PASS | — | 활성/시큐리티 imperative |
| ST2_07 | PASS(별칭) | — (→ST1_06) | effectClass dispatch 전제 |
| ST2_01 | **PASS** | (G10-006) 배틀-페어링 복원: `CurrentBattleOpponent`(AttackController.Current)로 싸우는 특정 적의 무전위 판정 | — |
| ST2_11 | **PASS** | (G10-001) once-per-turn 복원: `maxCountPerTurn=1`+`hash="Unsuspend_ST2_11"` → 라이브 트리거 루프 `OnceFlagController` 게이트가 강제 | — |
| ST2_12 | **PASS** | (G10-003) [Security] 테이머 플레이 실구현 (OnStartTurn 메모리도 1:1) | — |
| ST2_15 | **PASS** | (G10-007) play-from-under 실구현: 밑 Digimon 전위카드를 배틀에리어로 cost-free 플레이(`ActivatedPlayFromUnderEffect`+`PlayDigivolutionAsDigimon` 뮤테이션) | 활성 imperative |

### ST3/Yellow (11 PASS · 0 PARTIAL · 0 FAIL) + 별칭 ST3_07
| 카드 | 판정 | 카드-코드 부채 | 엔진통합 노트 |
|---|---|---|---|
| ST3_05 | PASS | `CanAddMemory` 누락이나 **메모리 Clamp 상한이 엔진에서 강제됨(검증)** → 동일 동작 | — |
| ST3_08·11 | PASS | — | 활성 imperative |
| ST3_09 | PASS | `Library>=1`/`CanAddSecurity` 누락이나 **`MoveFromZoneTop` 가용분 클램프=빈 덱 no-op(검증)** → 동일 동작 | — |
| ST3_13·14·15·16 | PASS | — | 활성/시큐리티 imperative |
| ST3_07 | PASS(별칭) | — (→ST1_06) | effectClass dispatch 전제 |
| ST3_01 | **PASS** | (G10-002) 0DP-격파 전제(`IsDPZeroDelete`+`CanTriggerOnPermanentDeleted` 상대디지몬) + once-per-turn(`maxCountPerTurn=1`+hash) 복원 | ✅ (G12-003) 라이브 cross-card 발동 해소 — OnDestroyedAnyone이 삭제 subject를 cross-card 리스너에 브로드캐스트 |
| ST3_04 | **PASS** | (G10-002) ST3_01과 동일 게이트 복원(메모리+1판) | ✅ (G12-003) 위와 동일 |
| ST3_12 | **PASS** | (G10-003) [Security] 테이머 플레이 실구현 ([All Turns] 시큐리티존 +2000도 1:1) | — |

### ST7/Red
| 카드 | 판정 | 부채 |
|---|---|---|
| ST7_10 | PASS | — (원본과 동일) |

### EX8/Green (1 PASS · 0 FAIL — 나머지 74장은 스켈레톤 스텁, 미포팅)
| 카드 | 판정 | 부채 |
|---|---|---|
| EX8_074 | PASS | — 6 region 전부 라이브 + deferred resume. **수정 이력**: #1 [When Would be Played] suspend-2 코스트감소 술어가 owner-only(`IsOwnerBattleAreaDigimon`)로 좁혀져 있던 것을 원본 `IsPermanentExistsOnBattleAreaDigimon`(any-owner)에 맞춰 `IsBattleAreaDigimon`으로 교정 — 상대 디지몬도 서스펜드 코스트 대상/감소 게이트 카운트에 포함(원본 일치). 회귀 테스트 G9-014(상대 카운트로 0메모리 플레이 가능) + G9-005 교정. 원본의 `CanNotBeAffected`는 sink 중앙화(ContinuousImmunityGate)·`CanSuspend`는 엔진 미모델링(스텁)이라 술어 비추가가 정답. |

> ⚠️ EX8 완성도: 효과 로직 기준 **1/75**(EX8_074만 실구현, 나머지 74장은 `// TODO: Skeleton only` 7줄 스텁). 데이터(JSON)·dispatch 배선은 전 카드 존재.

---

## 부채 — 갚는 법 (유형별)

| 유형 | 카드 | 필요 작업 | 난이도 |
|---|---|---|---|
| **once-per-turn** | ~~ST2_11, ST3_01, ST3_04~~ **전부 완료**(G10-001/002) | `CardEffectDefinition.maxCountPerTurn`+hash → 라이브 `OnceFlagController` 게이트 | 낮음 |
| **트리거 전제(0DP격파·상대디지몬)** | ~~ST3_01, ST3_04~~ **완료**(G10-002) | `IsDPZeroDelete`(DPZero 마커)/`CanTriggerOnPermanentDeleted`(TriggerEntityId) 술어 복원 | 중 |
| **배틀-페어링** | ~~ST2_01~~ **완료**(G10-006) | `PermanentView.TopInstanceId`+`CurrentBattleOpponent`(AttackController.Current) | 중~상 |
| **동적 삭제 임계값** | ~~ST1_15~~ **완료**(G10-005) | `MaxDpDeleteScope`+`MaxDpDeleteThreshold`(상승 바인딩 합산) | 중 |
| **보호 전위카드(protected source)** | ~~ST2_03, ST2_09~~ **완료**(G10-004) | `TrashProtectedKey` 메타 + `TrashableDigivolutionCount`/`HasTrashableDigivolutionCards`/`TopCardHasLevel` | 중 |
| **테이머 시큐리티 플레이** | ~~ST1_12, ST2_12, ST3_12~~ **완료**(G10-003) | `PlayThisCardToBattleEffect`(PlayCard 뮤테이션)+ActivatedEffectResolver 디스패치 | 중~상 |
| **play-from-under** | ~~ST2_15~~ **완료**(G10-007) | `DigivolutionStackHelpers.PlaySpecificSourceAsync`+`PlayDigivolutionAsDigimonKind`+`ActivatedPlayFromUnderEffect` | 상 |

## 엔진 통합 갭 (카드-코드 실패 아님, 별도 트랙)
- **활성화 풀 루프 — 해소(G11-002/G12-002/G12-004)**: 옵션-활성 deferred 경로가 RL env에서 풀 루프로 발동(활성→suspend→ResolveChoice→재개, 재지불 없음, commit-once; `DeferredActivationController`+`ResolveChoiceAsync` 재호출). ✅ 다중-choice 활성(G12-002, 2-라운드) + ✅ 시큐리티-스킬 deferred 재개(G12-004, `SecurityResolver`가 suspend 시 `DeferredActivations` 등록→체크 일시정지→ResolveChoice가 재개) 모두 e2e로 단언.
- **트리거 plumbing(G11-002/004 + G12-003)**: `GameFlowProcessor`가 트리거 request에 이벤트 subject(TriggerEntityId) enrich + 게이트-우선 once-소비. ✅ (G12-003) "Anyone" 트리거의 cross-card subject 전달 해소 — `AutoProcessingTriggerCollector.MatchesEvent`가 브로드캐스트 타이밍(OnDestroyedAnyone)에서 sourceEntityId/cardId/playerId 필터를 우회 + `EnrichWithEventSubject`가 cardId에서도 subject 판독 → ST3_01/04 라이브 cross-card 발동(자기-삭제 음성 포함).

## 권장 처리 순서 (수요 기반)
1. **once-per-turn 3장** — `OnceFlagHelpers` 연결, 가장 싸고 "실제보다 강함" 버그 제거.
2. **ST3_01/04 트리거 전제** — 0DP격파 컨텍스트(같은 작업으로 2장).
3. **테이머 시큐리티 3장** — `PlaySelfTamerSecurityEffect` 한 번 구현 → 3장 동시 해결.
4. **ST1_15 임계값 / ST2_01 배틀페어링** — 해당 메커니즘 실제 필요 시.
5. **ST2_15 play-from-under** — 특수플레이 서브시스템 착수 시.

---

## 프리미티브 preemptive-seal 부채 (PRIM-W3)

> **정의**: grant/등록은 live(쿼리 가능)이나 behavior-consumer가 아직 미마이그레이션 = 기존 코드베이스의 "preemptive seal" 패턴(키워드 grant Raid/Barrier 등과 동일). 카드 포팅 시 config-only로 호출은 되나, 아래 소비자 배선 전까지 실제 게임 동작은 미반영.

| 프리미티브 | 등록(live) | 소비자(latent) | 상환 방법 |
|---|---|---|---|
| ~~MindLink~~ | — | — | **✅ 해소(K5, 2026-07-02)**: 키워드가 아니라 프로세스였음 — `MindLinkClass` 신설(G9-060). 키워드 grant는 표시용 |
| ~~ChangeSelfLinkMax~~ | — | — | **✅ 해소(G9-056)**: LinkedMax metric + Enforce 스레딩 |
| ~~GrantedReduceLinkCost~~ | — | — | **✅ 해소(G9-056)**: LinkCost metric + 지불부 폴딩 |

**behavior-live(소비자 배선 완료, 실제 동작함)**: 키워드 grant 9종·Rush/Reboot/CanNotAttack static·Gain1/Gain2·CantUnsuspend·CanNotBeBlocked·CanNotBeDestroyedBySkill·ChangeSAttackStatic·ReturnToLibraryBottom·ReplaceBottomSecurity·Training·MaterialSave·**UseRequirements(ignore-color, DigivolveAction consult 배선)**·Arts/BlastDNA(subsume). = W3 24/27 behavior-live, 3/27 seal.

## PRIM-W4 preemptive-seal + deferred

| 항목 | 상태 | 소비자(latent)/사유 |
|---|---|---|
| Collision / Vortex StaticEffect | seal | BlockTiming.hasCollision / Vortex 공격 소비자가 metadata flag를 읽음 (grant는 HasKeyword로 live) |
| TreatAsDigimon | seal | 카드타입 판정 소비자 (grant HasKeyword live) |
| Ascension | seal | DeletionReplacementGate.hasAscension (grant HasKeyword live) |
| ChangeLinkMax / ChangeSelfLinkMax | seal | LinkHelpers.EnforceLinkedMaxAsync (registry 미보유) |
| DigiXrosEffectFromNames | config | DigiXros 특수플레이 존재; by-names 레시피는 데이터 config |
| ExtendActivateClass | per-card | EX2_057 nested 클래스, 공유 프리미티브 아님 |
| AceOverflowClass | ✅ done | 중앙 규칙으로 구현(AceOverflowGate + sink 필드-이탈 hook, G9-042) |

**W4 behavior-live 21/32 · seal 5 · already-supported 4(타이밍) · 분류-제외 2(DigiXros config·ExtendActivate per-card).**

## W5 특수플레이/제약 — 1:1 위반 자기감사 (2026-07-01)

세션 중 커버리지를 충실도보다 우선해 **가드-축소 위반**을 저질렀고, 지적받아 수정함. 기록:

| 항목 | 위반 | 조치 |
|---|---|---|
| DigiXros/Blast/DNA/Jogress 재료 조건 | 원본 임의 `CanSelectCardCondition(CardSource)` 술어를 **평면 카드-이름으로 뭉갬** | ✅ **수정**: `SpecialPlayRecipe`를 `SpecialPlayMaterial(Func<CardSource,bool> Matches, Label)` 술어 기반으로 재설계. `TryMatchMaterials`가 술어 평가. 이름형은 `DigiXrosEffectFromNames`(이름-일치 술어), 임의형은 `DigiXrosEffect(params SpecialPlayMaterial)`. **G9-049**(Lv3 술어 1:1 평가 검증). |
| `CanNotBeDestroyedStaticEffect` | `permanentCondition`을 받아놓고 **무시**(self 전용) | ⚠️ **문서화**: self형("이 디지몬 삭제불가")은 1:1. ~~SET형은 player-scope prevent 미구현 → STOP~~ → **✅ 해소(FR2 시기 구현 확인, 2026-07-02 재검증)**: non-null 술어는 `ContinuousPlayerScopeRestrictionEffect(PreventDeletionKey, ScopePred)` — SET형도 1:1. |

**원칙 재확인**: 술어를 받는 팩토리는 그 술어를 **평가**해야 함(뷰 계층으로 가능). 이름/스칼라로 뭉개면 FAIL. 발견-배선(on-demand 등록)은 원본 라이브평가의 브릿지이나, 재료 조건 자체는 이제 1:1.

## FR 복원 진행 (2026-07-01) — permanentCondition 술어 무시

- **수정 완료(술어 1:1 평가)**: A2 player-scope 11종(Rush·Reboot·Alliance·Jamming·Collision·Vortex·Blocker·ChangeSAttack·ChangeBaseDPGlobal·ChangeLinkMax·ChangeDP) + A1 predicate-aware 2종(ImmuneFromDPMinus·InvertSAttack). enabler: player-scope 바인딩이 `scopePredicate`를 실어 EvaluateForCard/HasKeyword가 후보를 CardSource로 평가. **G9-050**.
- **✅ SET형 완료**: registry-only sink/battle 게이트 소비 항목 — `CanNotBeDestroyed`·`CanNotBeDestroyedByBattle`·`CanNotBeTrashedBySkill`·`CantSuspend`·`CannotReturnToHand`·`CannotReturnToDeck`·`CanNotAffected` — **EngineContext를 MatchStateMutationSink에 스레딩**(`ContinuousScopeEvaluation.ApplicableEffects` 노출 + EngineContext.cs 배선) + BattleDeletionGate가 ApplicableEffects 사용 → SET형이 player-scope 술어로 매칭 세트에만 적용. self형도 그대로 1:1. **G9-050**(SET-form 삭제/서스펜드).
- **✅ defenderCondition 완료**: `CanNotAttackSelfStaticEffect(defenderCondition)` — 신규 `CanNotAttackDefenderConditionEffect` + `RestrictionHelpers.DefenderPredicateKey`, `EvaluateAttack`가 defender를 CardSource로 평가해 매칭 방어자만 제약(과다제약 제거). **G9-050**.
- **결론**: FR permanentCondition 술어 무시 위반(21종) + defenderCondition(1종) **전부 1:1 술어 평가로 복원**. 282 green, RuleAudit 0. 상세: [fidelity_remediation_goals.md](fidelity_remediation_goals.md).

## FR2/M-1 진행 (2026-07-01) — per-card 술어

- **✅ ChangeSecurityDigimonCardDPStaticEffect(cardCondition)** — 원본은 cardCondition이 대상 플레이어까지 결정(예: `cs.Owner == card.Owner.Enemy` = 적 security). 기존 포팅 owner-scope 하드코딩 = **wrong-player 버그**. `PlayerScopeContinuousHelpers.ScopeAnyPlayerKey`(양 플레이어 스코프) + cardCondition을 scopePredicate로 1:1 평가. **G9-052**.
- **✅ UseRequirements(cardCondition)** — 원본 CanUseCondition: owner가 cardCondition 매칭 Digimon/Tamer(배틀|브리딩) 보유 시에만 ignore-color 활성. 기존은 무조건 grant. 게이트로 폴딩 + `DigivolveAction.HasContinuousFlag`을 condition-aware(`ApplicableEffects`)로 전환(ignore-digivolution-req 플래그도 이제 condition 존중). **G9-052**.
- ~~STOP DecoySelfEffect(permanentCondition)~~ → **✅ 해소(D1, 2026-07-02)**: grant에 술어 저장 + 보호 대상에 라이브 평가(G9-055). 주의: 술어는 **보호 대상**(홀더 아님)에 걸린다.
- ~~STOP AddSelfDigivolutionRequirementStaticEffect(cardCondition, costEquation)~~ → **✅ 해소(G9-052/G9-044)** + **level/min/max 게이트도 A2(2026-07-02)로 해소** — 이 팩토리는 전 인자 실동작.

## FR2/M-2 진행 (2026-07-01) — per-effect / per-battle 술어

- **✅ cardEffectCondition** (`CannotReturnToHand`·`CanNotBeTrashedBySkill`) — 원본 대부분 trivial(any effect)이나 BT11_060 = `IsOpponentEffect`(상대 효과로만 제약). 원인 효과 owner만 필요 → sink `mutation.SourceEntityId`를 restriction 평가에 스레딩(`MatchStateMutationSink.IsRestrictedFromCause` + `RestrictionHelpers.CausingEffectPredicateKey`), 인자 타입을 `Func<CardSource,bool>`(원인 소스)로 매핑. self/player-scope 양 경로 지원. **G9-053**(상대-발동 차단·자기-발동 허용·무조건 차단).
- **canNotBeDestroyedByBattleCondition** — 유일 사용자 EX8_068 조건이 `permanent==공격자||permanent==방어자`(참가자에겐 항상 참=trivial) → 무조건 배틀 면역 포팅이 1:1. non-trivial 형태는 BattleDeletionGate에 전투 참가자(attacker/defender) 스레딩 필요하나 해당 카드 없음.

## FR2/M-3·M-5 진행 (2026-07-01)

- **✅ M-3a AddSelf costEquation/added-cost** — 조사 결과 added 진화요구의 **비용 전체**(고정+동적)가 binding 미emit·미소비였음(printed 비용만 씀). `AddedEvolutionCostKey`/`AddedEvolutionCostEquationKey` emit + `DigivolveAction.TryGetAddedDigivolutionCost` + printed 실패 시 added 경로 비용 적용(`costEquation() ?? digivolutionCost`). **G9-044**(printed 2 거부·added 3·동적 6).
- **✅ M-3b ChangeDP effectName** — `SetEffectName`(표시 라벨)만, gameplay 미사용 = cosmetic. 무시 1:1.
- **✅ M-5 ChangeBaseDPGlobal** — 이중 버그: (1) "global"=양 플레이어인데 owner-scope만 → `scopeAnyPlayer`. (2) **BaseDp modifier를 아무도 소비 안 함**(ContinuousDpGate가 Dp metric만) = DP 무영향 seal → `ContinuousDpGate.ResolveDp`가 BaseDp를 base에 먼저 fold. **G9-052**.
- **RevealLibraryClass** — `InformationalRevealEffect`(no-op). 풀정보 엔진에서 reveal은 숨은 정보가 없어 1:1(단 "공개 시" 트리거 소비자는 미존재).
- ~~ReplaceBottomSecurity 심층 검증 대기~~ → **✅ 해소(M-5, 2026-07-02)**: AS-IS도 `Last()`=bottom/`Insert(0)`=top — 1:1 확인.

## FR2/M-4 진행 (2026-07-01) — preemptive-seal 언실

- **✅ Decoy 언실** — Decoy 키워드 grant(`DecoySelfEffect` → `ContinuousKeywordGate.Decoy`)가 redirect 메커니즘과 프로덕션 미연결이었음(`HasDecoyKey` 메타는 테스트에서만 설정). `DeletionReplacementGate.FindDecoyRedirect`/`Candidates`에 `HasDecoy` 헬퍼(메타 OR `HasKeyword(registry)`) + sink·DeletionReplacementTiming 호출부에 `effectRegistry` 전달 → Decoy 실제 작동. **G9-055**. 잔여: `DecoySelf(permanentCondition)`의 target-narrowing(grant에 술어 저장 + context 평가) — permanentCondition=null 다수는 현재 1:1.
- ~~잔여 seal~~ → **✅ 전부 해소**: 링크 3종(G9-056) · 키워드-동작 5종(K1~K5, 2026-07-02) · W2 seal(S3/G9-058).

## FR2/M-4 전수조사 (2026-07-01) — 삭제-치환 키워드 10종 단일 seal

**스캔 방법**: 모든 `Has*Key` 삭제-치환 메타 플래그의 프로덕션 SET 여부 + 키워드 HasKeyword 소비자 수.

**결과**: `Evade·Barrier·Decoy·Fragment·Scapegoat·Save·Fortitude·Ascension·Decode·Partition` 10종 전부 —
- grant는 키워드(`SelfKeywordByNameEffect(ContinuousKeywordGate.X)`).
- 소비자(게이트)는 `Has*Key` 메타 플래그를 읽음.
- 그 플래그는 **프로덕션 SET=0**(테스트에서만 설정), **키워드→메타 브릿지 없음** → 프로덕션에서 전부 inert.

**근본 원인**: 키워드 모델과 메타-플래그 모델이 연결된 적 없음(config-first 개발로 소비 배선 이연 = preemptive seal).

**충실한 수정 원칙 (중요)**:
- ✅ **게이트가 라이브 키워드를 직접 읽기**(`ContinuousKeywordGate.HasKeyword`) = **AS-IS 미러**(원본은 `CanActivateDecoy`/`EvadeProcess`가 키워드/효과를 삭제 시점에 라이브 평가). Decoy가 이 방식(G9-055).
- ❌ **메타 동기화 브릿지**(키워드→Has*Key 복사)는 **AS-IS에 없는 헤드리스 워크어라운드** → 채택 금지(시도 후 제거).

**진행**: Decoy ✅ → ~~잔여 7종 대기~~ → **✅ 전부 해소(S3/G9-058, 2026-07-02)**: 게이트 라이브-읽기 + 호출부 스레딩 완료.

## 참고: 이번 세션 발견·수정한 seal (4건)
등록은 되나 소비자 없음 = 무동작이던 것들: **BaseDp**(ContinuousDpGate Dp metric만) · **linkedMax/linkCost**(metric 없음+read 미반영) · **Decoy**(키워드↔메타 미연결). 전부 라이브-소비 경로로 배선(정적 감사로 발견). 나머지 삭제-치환 9종은 동일 패턴, 대기.

## S5 Execute 부분 완료 (2026-07-02)
- ✅ 공격 대상 확장(상대 미서스펜드 공격 가능) + 종료 시 self-delete: `AttackPermanentAction`/`AttackPipeline`가 `HasKeyword(Execute)` 인식. G3.5-C910.
- ~~STOP: Execute 파트1(턴종료 공격창)~~ → **✅ 해소(Execute-1, 2026-07-02)**: `EndOfTurnEffectAttack`에 Execute 배선 — 플레이어+미서스펜드 디지몬 타깃(AS-IS ExecuteProcess `canAttackPlayer: ()=>true` + isExecute), 소환둔화 미우회(Permanent.cs:2244 — Rush/isVortex만 우회), self-delete는 **창-공격에만 per-attack 플래그로 arm**(선언 시 스탬프, ResolveChoice). **부수 교정 2건**: (1) S5의 블랭킷 `HasKeyword(Execute)` self-delete가 일반 메인페이즈 공격에도 적용되던 과대적용 제거(AS-IS는 ExecuteProcess 내 UntilEndAttack DeleteSelfEffect만); (2) self-delete가 직접 AddToTrash → **sink Delete 경유**로 교체(would-be-deleted 치환 응답·P1 leave-play 정리·삭제 트리거 적용 — AS-IS DeleteSelfEffect는 정규 DeletePermanent). C910 테스트 3건(창 셀프삭제/일반공격 비삭제/소환둔화).

## M-4/M-5 잔여 구현 (2026-07-02) — D1·K1~K5 완료, debt/축소 기록

설계·근거: [fidelity_m4m5_design.md](fidelity_m4m5_design.md). 완료: **D1** Decoy 술어(G9-055), **K1** VortexCanAttackPlayers un-flatten(GR-006·G9-039), **K2** Ascension top 삽입(G3.5-F68), **K3** Collision per-defender 면역 가드(G3.5-C910), **K4** TreatAsDigimon 중앙 chokepoint(G9-039), **K5** MindLinkClass + PlayMindLinkTamer 교정(G9-060). M-5 두 건은 AS-IS 검증으로 close(구현 불요).

**신규/잔여 debt:**
- **K2 — `CanAddSecurity` 게이트 미폴딩**: AS-IS AscensionProcess(·Save 등)는 `Owner.CanAddSecurity(효과)` 게이트를 거침. 그 제한 효과(`CannotAddSecurityClass`)가 **미포팅 스켈레톤**(grant 0)이라 현재 폴딩할 대상이 없음 — 해당 클래스 포팅 시 `TryAscensionAsync`/`TrySaveAsync`류에 게이트 추가 필요.
- **K3 — 시큐리티 카드 OnCounterTiming Collision 스캔 엣지**: AS-IS `Permanent.HasCollision`은 face-up **시큐리티 카드**의 효과도 스캔(Permanent.cs:3069). 포트 키워드 게이트는 필드 grant만 — 시큐리티-발 Collision grant 카드 존재 여부 census 후 필요 시 확장.
- **K5 — face-up/flipped 축소(검증된 1:1 축소)**: AS-IS MindLink 조건 `DigivolutionCards.Count(cs => cs.IsTamer && !cs.IsFlipped) == 0`에서 `!IsFlipped`는 축소 — 헤드리스에 under-card flip 모델 없음(전부 face-up = AS-IS 기본 상태). flip 메커니즘 도입 시 재방문.
- **K5 — leave-field 트리거 미발화**: `MindLinkClass.MindLink`의 테이머 필드 이탈은 직접 zone-move(레지스트리 정리는 수행)라 leave-field 트리거 창을 열지 않음. AS-IS `IPlacePermanentToDigivolutionCards`의 이탈 처리 대조 후 필요 시 sink 경유로 전환.
- **기존 debt 유지**: Decoy/Save의 first-candidate 결정론(AS-IS "select 1"; Decoy는 F68D per-card condition 창으로 부분 해소), Execute 턴종료 공격창.
- **AD1-A 축소 — Assembly 필드-대체 라틴트**: AS-IS `CanSubstituteForAssemblyCondition`(Permanent.cs:3843-3886)은 `ICanSelectAssemblyEffect` 보유 시 필드 퍼머넌트가 트래시 재료를 대체(퍼머넌트 통째로 밑에 들어감). 현행 Assembly 사용 15장 중 해당 효과 사용 카드 0 → 미모델(SelectAssemblyClass doc 명기). 해당 카드 포팅 시 확장.
- **AD1-A 정책 상속 — 재료 조합 단일화**: Assembly/DigiXros/DNA의 파라미터화 액션은 유효한 첫 재료 배정 1개만 열거(백트래킹) — AS-IS는 플레이어가 임의 조합 선택. 대체 조합이 게임적으로 다른 카드(재료의 진화원 수 등이 후속 효과에 영향) 등장 시 조합 열거 확장.
- **B3 마커 뉘앙스(문서-구현 불일치 기록)**: fix_design은 rule 삭제(DP≤0)에 `DeletedByRuleKey` 신설을 제안했으나 구현은 sink의 기존 `DeletedByEffectKey` 스탬프를 유지하고 `isDpZero`만 추가함. 현 소비자 전부 무영향 검증(Decode/Partition은 `!byBattle`[&&`!ownEffect` — rule source는 인스턴스 미해석이라 ownEffect=false]만 보고, Decoy는 deleter-enemy 게이트로 자연 배제). **미래에 "IsByEffect" 술어 소비자가 생기면** rule 삭제가 효과 삭제로 오독될 수 있음 — 그때 rule 마커 분리.

**부수 수정(일반 버그, 이번 세션 발견):**
- `ContinuousKeywordGate.HasKeyword(registry-only)`가 **player-scope grant의 source 카드를 홀더로 오인**(스코프·술어 우회) → player-scope 바인딩은 context 경로에서만 매칭하도록 수정.
- `PlayMindLinkTamerFromDigivolutionCards`가 Digimon-only 필터 재사용으로 **테이머를 영원히 못 꺼내던** 버그 → cardType/cardName 필터 추가.
- `DeletionReplacementTiming.PreOptions`(정적 superset 경로)의 Decoy 후보 계산에 effectRegistry 미전달 → 키워드-only Decoy가 sweep 경로에서 0 후보이던 갭 수정.

## 위반 조치 구현 (2026-07-02) — A1~A4·B1~B5·C군, 신규 debt

설계·검증표: [fidelity_violation_fix_design.md](fidelity_violation_fix_design.md). 전 항목 구현·green.
> **→ 선행조치 P1~P8 전체 + C7~C9 구현 완료(2026-07-02)**: [**fidelity_debt_prereq_design.md**](fidelity_debt_prereq_design.md) — 아래 debt **#1~#8 전부 해소**, 별건 C7(dual)·C8(link 분리 — 재검증 결과 이미 분리돼 있었음)·C9(수명주기 — 표적 감사 후 isLinkedEffect 라이브 게이트 배선, rootCardEffect는 검증된 축소) 까지 완료. 로컬 LLM 포팅 전 선행 처리 방침에 따라 스킬 패키지(카탈로그 재생성 스크립트·레시피)도 갱신. 신규 관찰: 죽은 카드 자신의 트리거는 "삭제 처리 중 동기 해소"(knockout/security-check 창) — 다른 삭제-타이밍 창을 추가할 때 같은 패턴을 쓸 것(drop 후 큐잉은 무효).

**신규/잔여 debt (P1~P8 반영 전 기준; 위 표기대로 6건 해소됨):**
- **삭제시점 키워드 스냅샷의 배틀-경로 갭**: sink `ApplyDelete`는 삭제 직전 POST 키워드(Fortitude/Ascension/Save/Decode/Partition/ArmorPurge)+Partition 조건을 메타로 스냅샷하지만(A4 부수 구현 — 없으면 binding drop으로 키워드-grant POST가 전부 inert), **배틀 삭제 경로(BattleResolver finalize)는 미적용** — 배틀 삭제로 죽는 키워드-grant Ascension/Save/Fortitude는 여전히 레지스트리 drop 시점에 따라 유실 가능. 배틀 finalize에 동일 스냅샷 폴딩 필요.
- **B5 다중-공격자 Attack 모드**: `SelectPermanentEffect.TryOpenAttack`은 첫 공격자 1개만 초이스 오픈(포팅된 Attack-모드 카드는 maxCount 1). AS-IS는 선택된 전 공격자 순차 — 다중 선택 카드 등장 시 공격자 큐 파킹 확장.
- **B5 조합 술어(canEndSelectCondition)**: 저장+`IsValidSelection` 노출까지 — ChoiceRequest 스키마가 집합 제약을 표현 못 해 초이스 공급자(RL/스크립트)가 소비해야 함. 중앙 검증-재요청 루프는 초이스 인프라 확장 시.
- ~~B4 다중 조건 reveal 패스~~ → **✅ 해소(P4, 2026-07-02)**: `RequestMultiChoice`+`RevealSelectPass`+`RevealFlowState`(mutualConditions 정확 규칙 포함, B7 테스트).
- ~~C5 Counter 2-pass~~ → **✅ 해소(P5, 2026-07-02)**: `IsCounterEffectKey` 마커 + 2-pass 파킹(W6 순서 테스트).
- ~~C3 중첩 치환~~ → **✅ 해소(P6, 2026-07-02)**: 희생이 sink Delete 경유 — 대상의 자체 치환 발동 + `sacrificeAwaiting` 파킹(F68 양방향 테스트).
- **B3 무DP permanent**: `TrashNoDPPermanentProcess`(printed DP 없는 permanent의 직접 트래시)는 포트 sweep이 미포괄(HasLethalDp가 defined-DP 요구) — 배틀에리어에 무DP 카드가 놓이는 메커니즘 등장 시 폴딩.
- **B2 시큐리티-디지몬 배틀 앞 OnSecurityCheck 순서**: battle→pierce 구간은 파킹으로 복원; SecurityResolver 내부(OnSecurityCheck 스킬 → 시큐리티 배틀) 순서는 후속(동일 파킹 패턴 적용 조사).
