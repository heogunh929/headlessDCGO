# Design-item 전수 수집·3분류 census (2026-07-23)

> 조사 전용 산출물. src/tests 무수정. HEAD = main(미커밋 canonical 보강 1건 보존).
> 목적: 소스 내 `design item` 주석-형태 이연을 전수 수집·심사해, "이연-분류 규약을 우회한"
> 미등재 OPEN을 원장 편입 대상으로 확정한다. **판정 근거 = 실코드/실측 grep; 주석 문구는 불신·교차검증.**

## 0. 수집 방법 (전칭 주장 grep 병기)

```
grep -rn --binary-files=text "design item" src/HeadlessDCGO.Engine --include=*.cs      # 165 라인
grep -rhoE '\b(RD…|RDW…|P6A…|MIG…|F1…|R2-P2-…|C[0-9]w?-…|B2-…|P2-[A-Z]+)\b' … | sort -u  # 238 family-ID 유니버스
grep -rln --binary-files=text "design item" tests/ docs/audit/                          # 교차-참조 원장·witness
```

- `design item` 주석 = **165 라인** (src/HeadlessDCGO.Engine 전역).
- 그 위에 표기된 **고유 design-item ID = 96종** (오타·접두 변형 정규화 후; 아래 §1~§3 전수).
- `design item` 문구 **없이** RD-/P6A-/MIG-/F1- 계열 ID만 인용하는 주석까지 합친 family-ID 유니버스 = **238종**
  (§5 추가-어휘 스윕에서 신규분 심사).
- 정규화 병합: `RD-J-01`↔`RD-J03`/`RD-J-03`(동일), `RD-W4-2`≡`RD-W3-1`(주석 명시 동일체),
  `MIG3-SECURITYLOOKING`↔`MIG6-SECURITYLOOKING`(같은 갭의 두 파일 표기), `MIG5-PLAYER-EFFECTLIST`→`P6A-PLAYER-EFFECTLIST`(supersede).

## 집계 (design-item 표기 96종)

| 분류 | 수 | 정의 |
|---|---|---|
| **RESOLVED** | **41** | 갭이 수리/구축·은퇴됨. 주석만 잔존 → 스테일-주석 정리 후보(§4) |
| **PERMANENT** | **13** | AS-IS 한계/방어 STOP/도달불가로 영구-정당. STOP 4좌석과 정합 |
| **OPEN** | **42** | 룰층 작업 실제 잔존. §3 전수 |
| 합계 | 96 | |

- **원장 미등재 OPEN = 38** (기존 freeze_evidence §4 / red_ledger가 명시 등재한 OPEN은 4종뿐 — §3.9 참조).
- 기존 원장이 "차단 아님"으로 넘긴 대량 OPEN이 이 census의 핵심 산출물.

---

## 1. RESOLVED (41) — 스테일-주석 정리 후보

각 행: ID — 근거(현재 구현 위치/원장). "실코드 확인"은 grep 또는 red_ledger 착지 라인 대조.

| ID | 근거 (RESOLVED) |
|---|---|
| RD-C1-CARDEFFECT-IDTHREAD | cause SOURCE id 재구성으로 CLOSED (`SkillWindowSupply.cs:619`, `MatchStateMutationSink.cs:1440` "RESOLVED"). §4 주석 혼재 주의(:154/:167 "stays a GAP" 스테일). |
| RD-P6C3-D1 / RD-P6C3-D2 | CLOSED — BT9_109 [When Attacking] live (수리-9 REHAB, `BT9_109.cs:37/232/275`). |
| W3c-CANNOTPLAY-PLAYERBUCKET | RESOLVED — registry-read 삭제 (`CanNotPlayOptionScan.cs:68`). |
| RD-3A-01 | now resolved (`DeletionReplacementGate.cs:128`). |
| RD-3A-02 | RETIRED — 발명 temp 삭제 (red_ledger §Latent-STOP, `CardEffectCommons.cs:2760`). |
| RD-S3-BT17_095 | RESOLVED — temp-material DNA family witnessed (`BT17_095.cs:14`, tests/DNATEMP-Witness). |
| C2-02 / MIG5-CANLINK-PAYCOST | resolved (`CardSource.cs:1864/2109`). |
| MIG3-CUTIN-WOULDDISCARD (= RD-SW-E-01) | RESOLVED — LIVE cut-in (`CardController.cs:1177`). |
| MIG3-CUTIN-WHENUNTAP (= RD-SW-E-02) | RESOLVED — LIVE (`CardController.cs:2002`). |
| RD-SW-E-01 / RD-SW-E-02 | LANDED (PRE 컷인; freeze §4 latent 원장 등재). |
| MIG3-UNTAPPEDANYONE-PAYLOAD (emit) | WIRED (`CardController.cs:1953`). |
| MIG2-TRIGGER-SURFACE | RESOLVED in stages (`AutoProcessing.cs:45`); red_ledger MIG2-RuleProcess. |
| F1-ATC-EMIT-CENTRALIZE | resolved (`AttackProcess.cs:774`). |
| F1-ENDATTACK-HOOK | RETIRED (`AttackProcess.cs:637`). |
| F1-ADD-COUNTER (P2-1) | 배치 id 착지 (`MatchStateMutationSink.cs:148`, `EngineContext.cs:175`); red_ledger F1-Tier1-OnAdd. |
| MIG5-CANADDMEMORY | retired — live AddMemory (`Player.cs:762` "retires design item MIG5-CANADDMEMORY"). |
| MIG5-CANREDUCECOST | retired — live ICannotReduceCostEffect scan (`Player.cs:519`). |
| RD-P6C2-1 (Ascension) | ported verbatim (`Ascension.cs:6`). |
| RD-P6C2-3 | closed — Fragment/Decoy portable verbatim (`Fragment.cs:4`, `Decoy.cs:4`). |
| RD-P6C2-4 (MaterialSave) | 정본 (`MaterialSave.cs:5`). |
| R2-P2-2 (= RD-R2P2-WhenRemoveFieldPre) | RESOLVED for live path, witnessed (`TriggerTimingMap.cs:91`); freeze §4 등재. |
| RD-3B-INTERACTIVE | LANDED — promote-to-defer substrate (`DeletionReplacementGate.cs:23`; :1399 스테일). |
| RD-R3-01 (양 의미 모두) | ①삭제-교체 창 cut-in: "REPLACES the RD-R3-01 STOP stub" (`MultipleSkills.cs:11`) ②printed EvolutionCondition 토큰: LIVE (`DigivolutionCostHelpers.cs`, `DigivolveAction.cs:609`). **이중-ID** — §6 특이발견. |
| D2w-25 | REVIVED — mass deck-bottom-bounce ported (`AD1_025.cs:29`). |
| RD-GC2-01 | RegisterBaseBatch1/2 DELETED, zero production (`KeywordBaseBatch1.cs:279`). |
| B2-05 | MainSkillActivateAction live (red_ledger B2-MainSkillDeclare; `MainSkillActivateAction.cs:30`). |
| RD-R3W1b-01 | 창 SkillInfo-currency cutover batch W1 착지 (`SkillWindowContinuation.cs:1`). |
| RD-R2-04 | deletion-replacement 창 정본 (red_ledger R2-DeletionPipeline). |
| MIG4-DISCARDEVOROOTS-PUTTOTRASH | LIVE (`Permanent.cs:3920`; red_ledger item 3a). |
| RD-P6C1-8 | resolved — AddHandCard single-card overload live (red_ledger item 3a). |
| RDW-01 | CLOSED (bounce snapshot; `SkillWindowSupply.cs:127`, repair_ledger_arc 등재). |
| RD-IDFLIP-01 | transitional id-form 표면 물리 은퇴 (`SelectPermanentEffect.cs:298`; freeze §10). |
| RD-JOGRESS-P2 | 통합·4사이트 태깅 (freeze §4 P2; r4 doc). |
| F1-ENDATTACK-LIVENESS | gate-side re-check (C2, `AttackProcess.cs:648`). |
| F1-M1-INHERITSCAN | red_ledger F1-M1-InheritScan GREEN. |
| MIG2-RuleProcess / MIG5-CardSource / SEC-FaceUpSecuritySource | red_ledger #29/#30/#32 착지. |
| RD-R3-02 (r4 의미: PermanentBookkeepingStore) | 상환 a69965d6 (r4_tsm_s1 §64). **CutInProcess.cs 의미와 이중-ID** — §6. |
| RD-RETIRE-* / RD-J-01 | 발명 가드 10좌석 은퇴 (freeze §7; structural_invention_census). |
| RD-P6C2-6/7/10/11 등 P6C2 잔여 | rebuild_p6_cluster2_notes 착지 원장(대량 verbatim-port 완료). |

> 정리 후보: 위 41종의 주석은 "GAP/deferred/STOP" 문구가 잔존하나 실코드는 live. 특히
> `SkillWindowSupply.cs:154/167`(RD-C1 "stays a GAP"), `MatchStateMutationSink.cs:1399`(RD-3B "is design item"),
> `CardSource.cs:2370`·`Player.cs:21`(P6A-PLAYER-EFFECTLIST "until … flips" — §6 stale).

---

## 2. PERMANENT (13) — AS-IS 한계·방어 STOP·도달불가

STOP 4좌석과 정합 확인: live `NotSupportedException` = **정확히 4개**
(`grep -rn "throw new NotSupportedException" src/HeadlessDCGO.Engine` → GManager:198·CardController:4283·Permanent:4549·TrashLinkedCards:72).

| ID | 종류 | 영구-정당 근거 |
|---|---|---|
| **RD-SKEL-01** | STOP좌석 | `TrashLinkedCards.cs:72` — AS-IS 비대칭 루프(DigivolutionCards.Count 예산 ↔ LinkedCards 풀, used-host 미추적) 충실 번역 불가. freeze §1. |
| **RD-W4-3** | STOP좌석(조건부) | `GManager.cs:198` — 브릿지 W4 미지원 컴포넌트 타입 `GetComponent<T>` 방어. freeze §1(contingent). |
| **MIG4-DETACH-LIVE-TOP** | STOP좌석 | `Permanent.cs:4549` — live field-top 직접 re-parent AS-IS 무호출. freeze §1. |
| (무-ID 방어) | STOP좌석 | `CardController.cs:4283` — DISPATCH-REMAP double-key 가드(동일효과 2키 이중등재 STOP). ID 없음 — §6. |
| RD-W5-3 | 방어 STOP | `UserSelectionManager.cs:127/130` — SetInt/SetBool 무-값이면 AS-IS는 무한 폴 → loud STOP. NotSupported 아닌 별 예외지만 영구 방어. |
| RD-R3-02 (CutInProcess) | dead 코드 | `CutInProcess.cs:8` — CutInProcessCoroutine은 AS-IS에서 DEAD. |
| RD-W4-2 (≡ RD-W3-1) | 도달불가 | `SelectPermanentEffect.cs:697` — prefix-monotone AS-IS 조건에 unreachable gate. |
| RD-EXT3-01 / RD-EXT3-02 | AS-IS-inert | `SelectDigiXrosClass.cs:459`·`SelectAssemblyClass.cs:229` — AS-IS 원문이 이미 `//break;` 주석처리(발화 안 함). 충실 미러 = 유지. |
| CARDSOURCE-EQUALITY | substrate-by-design | `Permanent.cs:89`·`CardSource.cs:50` — AS-IS 객체-동일성 의존을 substrate id로 번역(발명 아님). |
| RD-RC-03 | AS-IS live path | `ContinuousKeywordGate.cs:97`·`CardLeavePlayCleanup.cs:125` — "the LIVE path is the AS-IS interface scan"; 갭 아님(정본 경로 명명). |
| RD-BCE-01 | by-design | `ContinuousAndRestrictionEffects.cs:143` — source-less cause 축약(AS-IS 등가). |
| RD-R6-04 | frame-model inert | `BT2_080.cs:18` — FRAME-MODEL, AS-IS EMPTY-frame cap, 관측 무변(inert). |

> RD-W4-3·RD-SKEL-01·MIG4-DETACH-LIVE-TOP는 red_ledger "Remaining STOP seats (4)"에 등재됨.
> CardController:4283 좌석은 design-item ID 미부여(§6 특이발견).

---

## 3. OPEN (42) — 미상환 이연 (룰층 작업 잔존)

컬럼: ID | 실체(무엇이 미구축) | 도달성(live로 밟히나 / latent=호출자0) | 규모 | 원장등재?

### 3.1 P6A 하드코딩-브릿지 페이로드 (두-방언 이음새 뿌리) — 8종

| ID | 실체 | 도달성 | 규모 | 등재 |
|---|---|---|---|---|
| **P6A-HT-ENTERFIELD** | OnEnterFieldAnyone/WhenDigivolving이 CHECK 빌더만 사용; FULL play payload(`OnEnterFieldHashtable`: evoRoots/evoRootTops/Root/oldLevels, `HashtableSetting.cs:146`)를 PlayCardAction/DigivolveAction에서 스레딩해야 함. `ActivatedHashtableBridge.cs:103`. **두-방언 재수렴(§3.8)의 선행 블로커.** | **latent**(CHECK 페이로드로 현행 발화; FULL 페이로드 무-스레딩) | **L** | ✗ |
| P6A-HT-USEOPTION | OnUseOption {Card}만; AS-IS는 Root+Cost 동반(`CardController.cs:1754`). `ActivatedHashtableBridge.cs:110`. | latent(현행 부분 페이로드로 발화) | S | ✗ |
| P6A-HT-SECURITY | [Security] 활성 페이로드 face-up reveal 미착지(isFaceDown=true 고정). `:135`. ST1_12도 "security-skill flow not yet built"(`ST1_12.cs:9`). | latent | M | ✗ |
| P6A-HT-DIGISOURCE | OnDigivolutionCardDiscarded/ReturnToDeckBottom의 discarded SOURCE 리스트 미-스레딩(minimally mapped). `:241`. | latent | S | ✗ |
| P6A-HT-ENDBATTLE | OnEndBattle battle-result 페이로드(winner/loser) 미매핑 → null 반환. `:258`. | latent(null-payload) | S | ✗ |
| P6A-HT-CAUSE | AS-IS causing ICardEffect 객체 vs 미러 cause-id 스텁. `:24`. | latent(스텁으로 두 데이터점 보존) | S | ✗ |
| P6A-STAMP-PERSISTENCE | 마커만 보유, effect 객체 미보존 (`ActivatedEffectResolver.cs:183`). | latent | M | ✗ |
| P6A-USED-JOURNAL | used-effect journal 미구축 (`ActivatedEffectResolver.cs:506`). | latent | S | ✗ |
| P6A-STACKED-DRAIN | stacked list를 window loop가 미-drain (`AutoProcessing.cs:793`). | latent(현행 유일 seed만) | S | ✗ |

### 3.2 P6A-PLAYER-EFFECTLIST (부분 상환·stale 혼재)

| ID | 실체 | 도달성 | 규모 | 등재 |
|---|---|---|---|---|
| P6A-PLAYER-EFFECTLIST | GiveEffectToPlayer → Player.EffectList flip. `CardSource.cs:2015-2020`는 "since R3-C2/R6-P flip … enumerates"(=상환) 이나 `CardSource.cs:2370`·`Player.cs:21`은 "until … flips"(미상환)로 **stale 모순**. 순수 갭은 사실상 flip됨 — 잔여는 주석 정합. | live(부분) | XS(주석 정합) | ✗ **§6 stale** |

### 3.3 이연④-e KEEP+MARK — production-0 latent surface (5종)

`ContinuousAndRestrictionEffects.cs` — 실카드 production census=0(팩토리 무구성). AS-IS 표면 보존, live 미배선.

| ID | 실체 | 도달성 | 규모 | 등재 |
|---|---|---|---|---|
| RD-④E-SELFRESTR | CanNot* self-static self-restriction 표면 미배선 | latent(census 0) | S | ✗ |
| RD-④E-PSRESTR | player-static restriction 표면 미배선 | latent(census 0) | S | ✗ |
| RD-④E-PSKEYWORD | player-static keyword-grant 미배선(=CardEffectFactory.cs:439 RD-④E-TRIGGERGRANT 짝) | latent(census 0) | M | ✗ |
| RD-④E-TRIGGERGRANT | AddSkillClass keyword-grant 포팅 미결(later corpus 결정) | latent(census 0) | M | ✗ |
| RD-④E-PSMODIFIER | ChangeDP player-static modifier 미배선 | latent(census 0) | S | ✗ |

### 3.4 MIG3/5/6 stub·stand-in (미배선 리더/에미션)

| ID | 실체 | 도달성 | 규모 | 등재 |
|---|---|---|---|---|
| MIG3-DEGEN-COUNTSELECT | SelectCountEffect mirror/choice 부재 — LOUD STUB (`CardController.cs:873`, `SelectCountEffect.cs:19`) | live(스텁 도달 가능) | M | ✗ |
| MIG3-LOCATIONTIME | SetChangedLocationTime headless analog 부재(4사이트 no-op) (`CardController.cs:828/918/1066/2793`) | live(no-op) | S | ✗ |
| MIG3-SECURITYLOOKING / MIG6-SECURITYLOOKING | SecurityLooking live reader 부재(delegated) (`Player.cs:472`, `GameContext.cs:40`) — **두 파일 동일 갭** | latent | S | ✗ |
| MIG3-CANREDUCESECURITY | Player.CanReduceSecurity stand-in (`CardController.cs:327`, `Player.cs:514`) | latent | S | ✗ |
| MIG3-CANADDSECURITY | Player.CanAddSecurity stub (`CardController.cs:327`, `MatchStateMutationSink.cs:47`) | latent | S | ✗ |
| MIG3-TAPPEDANYONE-PAYLOAD | tapped payload zone-미도출(emission 반 of RD9-87) (`CardController.cs:1809/1892`) | latent | S | ✗ |
| MIG3-TRASHSEC-UNIFY | CanReduceSecurity 핸들러 통합(slice 3c) (`MatchStateMutationSink.cs:1063`) | latent | S | ✗ |

### 3.5 RD9 — 공격/서스펜드 raw metadata 라우팅

| ID | 실체 | 도달성 | 규모 | 등재 |
|---|---|---|---|---|
| RD9-87 | OnTappedAnyone/OnUntappedAnyone raw metadata write 미배선(SuspendPermanentsClass.Tap) — 행동변경 deferred (`AttackProcess.cs:926`, `SkillWindowSupply.cs:398`, `AttackDeclarationCommons.cs:23`) | latent | M | ✗ |
| RD9-90 | [Main] skill declaration action 미포팅 → ATTACK에서 대체 발화 (`AttackDeclarationCommons.cs:25`, `AttackPermanentAction.cs:147`) | latent | M | ✗ |

### 3.6 브릿지 W3/W4 잔여 latent

| ID | 실체 | 도달성 | 규모 | 등재 |
|---|---|---|---|---|
| RD-W4-1 | ChangeCostClass 등록 미배선 (`SelectCardEffect.cs:344`) | latent | S | ✗ |
| RD-W4-6 | deferred choice; zero AS-IS caller (`SelectPermanentEffect.cs:609`) | latent | S | ✗ |
| RD-W3-7 | Blitz no-hook gate/offer cause-threading residual (`Blitz.cs:74/89`) | latent | S | ✗ |
| RD-W3-6 | DNADigivolve behaviour nuance (`DNADigivolveEffects.cs:23`) | latent | S | ✗ |
| RD-W3-4 | PlayCardsBridge 미지원 표면(비-silent) (`PlayCardsBridge.cs:242`) | latent | S | ✗ |
| RD-W3-2 | RevealLibrary substrate gap (`PlayCardsBridge.cs:207`) | latent | S | ✗ |
| RDW-05 | attackCauseEffectId만 스레딩, live ICardEffect 미보유 GAP (`SkillWindowSupply.cs:44`) | latent | S | ✗ |

### 3.7 프레임-모델·capacity·relocation (P6C1) latent

| ID | 실체 | 도달성 | 규모 | 등재 |
|---|---|---|---|---|
| RD-P6C1-1 (= MIG5-FRAME-MODEL) | PermanentFrame.IsBattleAreaFrame 모델 부재 (`Permanent.cs:3025`) | latent | M | ✗ |
| RD-P6C1-2 | CanMove/AppFusion capacity check 생략(6+ 사이트) (`Permanent.cs:3028`, `CardSource.cs:871/2199`, `Player.cs:252`, `CardController.cs:4524`) | latent | M | ✗ |
| RD-P6C1-9 | SelectDNACondition/CardController relocation → mirror CardSource 이전 대기 (`CardController.cs:4513`, `SelectDNACondition.cs:14`) | latent | S | ✗ |
| RD-P6B-2 | continuous-scan latent 경로 (`NewModelContinuousScan.cs:1827`) | latent | S | ✗ |
| RD-P6B-5 | Decoy presence check gate-less (`NewModelContinuousScan.cs:1196`) | latent | S | ✗ |
| RD-EXT2B-01-BATTLEFIELD | "battle" HASHTABLE 키 live mirror reader 부재 (`CardController.cs:4698`) | latent | S | ✗ |

### 3.8 카드-레벨 fidelity debt (omitted 브랜치) — 무-throw 부분포팅

| ID | 실체 | 도달성 | 규모 | 등재 |
|---|---|---|---|---|
| RD-R6-05 | BT15_083 [On Play] reveal-3 브랜치 OMITTED — HasGarurumonName 이름족 술어·reveal 코루틴 프리미티브 부재 (`BT15_083.cs:15`) | latent(그 창 미발화) | M | ✗ |
| RD-R6-06 | BT24_018 [When Digivolving] security-break + [All Turns] WhenRemoveField prevention OMITTED — BreakSecurityEffect UI carrier·PRE leave-hook tier 부재 (`BT24_018.cs:15`) | latent | M | ✗ |
| C1w-24 | BT19_024 interactive hand/source 3효과 미포팅 (`BT19_024.cs:17`) | latent | M | ✗ |
| C1w-25 | BT16_025 interactive suspend 2효과 미포팅 (`BT16_025.cs:23`) | latent | M | ✗ |
| C2-01 | IsLinkedEffect activated-effect lifecycle latent (`ActivatedEffectResolver.cs:57`, BT22_035/BT21_059) | latent | M | ✗ |
| RD-CATK-EATTACK-MULTI | eAttack multi(Blitz용 unreachable) (`AttackPermanentAction.cs:230`) | latent | S | ✗ |
| RD-BT13028-AceOverflow | BT13_028 ACE overflow 잔여 | latent | S | ✗ |

### 3.9 MIG1/2 relocation·latent hook

| ID | 실체 | 도달성 | 규모 | 등재 |
|---|---|---|---|---|
| MIG1-BEFOREONATTACK | beforeOnAttack 콜백 1:1 존재하나 bridged caller 무설정 (`AttackProcess.cs:200`, `SelectAttackEffect.cs:26`) | latent | S | ✗ |
| MIG1-KEYWORD-RELOCATE | AttackProcess 키워드 미러 relocation 미결 (`AttackProcess.cs:42`) | latent | S | ✗ |
| MIG1-EXECUTE-RELOCATE | DeleteSelfEffect relocation 미결 (`AttackProcess.cs:44`) | latent | S | ✗ |
| MIG2-ADDLINK-SELECT | LinkedMax>1 owner-selection 미배선 (`Permanent.cs:4330`) | latent | S | ✗ |

### 3.10 효과-모델 리빌드 descriptive latent

| ID | 실체 | 도달성 | 규모 | 등재 |
|---|---|---|---|---|
| P2-ISEXECUTING | TurnStateMachine.isExecuting 미미러 (`TurnStateMachine.cs:41`) | latent | S | ✗ |
| P2-STACKSKILLINFOS | AS-IS StackSkillInfos 미러 부분 (`GManager.cs:41`) | latent | S | ✗ |
| F1-ADDHAND-FLUSHGRAIN | ADD-HAND 캐시 grain=sink FLUSH deferred(적대리뷰 P2-2) (`MatchStateMutationSink.cs:457`) | latent | S | ✗ |
| RD7-71 | security Digimon이 CardDP로 전투 — ordering divergence 주석 (`SecurityResolver.cs:172/910`) | live | S | ✗ |
| RD-BCE-01-sanctioned | (RD-BCE-01의 sanctioned 변형; §2 PERMANENT와 짝 — 관측 무변) | latent | XS | ✗ |

### 원장 등재 상태 요약 (§3 전수 42)

- freeze_evidence §4 latent 원장(호출자-0)이 명시 등재: **RD-3A-02·MIG4-DETACH·RD-SKEL-01·RD-SW-E-01/02·R2-P2-2**
  — 이들은 이미 RESOLVED/PERMANENT로 재분류됨(§1/§2). 즉 **freeze §4가 등재한 "latent"는 실제 OPEN이 아님.**
- red_ledger가 등재한 것은 STOP 4좌석(RD-W4-3·RD-SKEL-01·MIG4-DETACH-LIVE-TOP + 무-ID)뿐 → PERMANENT.
- **결과: §3 OPEN 42종 중 원장 등재 = 0. 미등재 = 42.**
  단 정규화 중복(MIG3/6-SECURITYLOOKING 1쌍, RD-P6C1-1≡MIG5-FRAME-MODEL 1쌍, P6A-PLAYER-EFFECTLIST=사실상-상환 1건,
  RD-BCE-01-sanctioned=§2 짝 1건)을 순수 신규 룰층 갭에서 제외하면 **원장 편입 필요 실질 OPEN ≈ 38.**

---

## 4. 스테일-주석 정리 후보 (RESOLVED인데 GAP/STOP 문구 잔존)

전수는 §1. 우선 정리 대상(모순-강도 순):
1. `SkillWindowSupply.cs:154,167` — RD-C1-CARDEFFECT-IDTHREAD "stays a GAP" (실제 CLOSED at :619).
2. `MatchStateMutationSink.cs:1399` — RD-3B-INTERACTIVE "is design item" (실제 LANDED).
3. `CardSource.cs:2370` + `Player.cs:21` — P6A-PLAYER-EFFECTLIST "until GiveEffectToPlayer flips" (실제 flip됨; §6).
4. freeze_evidence §4 line 28 "latent 원장"에 나열된 6종 중 5종(RD-3A-02·MIG4-DETACH·RD-SW-E-01/02·R2-P2-2)은
   RESOLVED/PERMANENT — "latent"로 남길 이유 없음(문서 정합 권고).

---

## 5. §추가-어휘 스윕 (design-item 표기 밖의 공지된 이연)

코디네이터 실측 계수 대조 + 실코드 triage(RESOLVED-역사 vs OPEN).

### 5.1 Script/ 엔진층 스캐폴드 헤더 (실측 48 라인) — 전수 판별

`grep -rni "TODO" src/…/Assets/Scripts/Script --include=*.cs` = **48 라인**:
- **47** = 동일 스캐폴드 헤더 `// TODO: Skeleton only. Port or implement deterministic .NET logic later.` (각 파일 line 7).
- **1** = `ICardEffect.cs:499` `//TODO: Look into this for the on deletion General issue` (AS-IS 계승 인라인 노트, 사소).

**판별 결과: 47 스캐폴드 파일 전량 = 코드-0 플레이스홀더(모두 정확히 7라인, 주석뿐 — namespace/class 없음).
`NotImplemented`·`throw` 0건. 살아있는 컴파일 스켈레톤은 하나도 없음.** 기능 triage:

| 유형 | 파일 | 상태 |
|---|---|---|
| 기능 LIVE(헤드리스 미러명으로 존재) → **스테일 플레이스홀더** | CEntity_Effect(→`CardEffectInterfaces.cs`), StarterDeck(→`Headless/DataLoading/StarterDecks.cs`), GameRandom(→`Headless/Services/GameRandomSource.cs`), Combinations(→`Choices/ChoiceCompletability.cs`), 카드로더 8종(LoadCSV/LoadJSON/DeckData/CardInfo/GSSReader/OfficialCardListUtility/DeckCodeUtility/ConvertBinaryNumber → `Headless/DataLoading/`), ContinuousController(→`NewModelContinuousScan`/`ContinuousKeywordGate`), ShuffleDeckCode/StreamingAssetsUtility/DeckBuildingRule, PlayerSelection/*(→`IChoiceProvider`), JogressEffectObject/DigiXrosEffectObject(→Runtime), DataTools/*, CheatAction, StartTurnTamerMemory, JsonSerializedClass, Networking/*(in-process 불요) | 정리 후보 |
| 순수 Unity UI(헤드리스 substrate-stripped, by-design) | DeckInfoPanel, NextPhaseButton, DeckListPanel, CardDistributionTab, FieldPermanentCard, EditDeck, HandCard, CardPrefab_CreateDeck, CreateNewDeckButton, ShowPhaseObject, ShowPhaseNotificationObject, PermanentDetail, CheckCardPanel, DetailCard_DeckEditor, SpellRestoration, Effects, Effect Examples/Link_Examples | 정리 후보(불요) |

**→ Script/ 48건 중 rule-layer OPEN = 0. 전량 스테일/불요 플레이스홀더 = 정리 후보(+ MEMORY의 "엔진 소스 리터럴 TODO 금지" 규약 위반이므로 청소 권장).**

### 5.2 CardEffect/ 셸 헤더 (실측 3,574 라인)

`grep -rni "TODO" …/CardEffect` = **3,574** (동일 스캐폴드 헤더). 개별 심사 제외.
→ **1줄 항목: "미포팅 카드 셸 큐(대량-포팅 대상)" ≈ 3,574 셸.** (coordinator 계수 ~3,594와 정합; 저장소 시점차.)
rl-env/coverage 트랙의 ⑦ 대량 포팅 스코프. 룰층 갭 아님(카드 콘텐츠).

### 5.3 어휘 가족 triage (같은 3분류; 실측 계수 대조)

| 가족 | 실측 계수 | RESOLVED-역사/무관 | OPEN(실코드 대조) |
|---|---|---|---|
| `deferred` | 351 | 대다수(REMOVED/retired stopgap 서술·해소 원장) | §3에 이미 포섭된 latent(RD9-87·P6A-*·RD-W* 등). 신규 0. |
| `이연` | 83 | 대다수(이연④ 분류 서술·완료 기록) | RD-④E 5종(§3.3)·이연④-g 서술. 신규 0. |
| `not yet` | 22 | 서술적/흐름 | 신규 소량: `HeadlessChoiceState.cs:32`(choice 상태 serialization 미노출 — RL 관측성, XS)·`SelectCardEffect.cs:15`(F-3.7 미매핑, XS)·`MainSkillActivateAction.cs:33`(per-index resolution 미노출, S). 나머지는 §3 기수집(P6A-HT-*·MIG3-DEGEN·ST1_12 security). |
| `HACK` | 4 | 4/4 무관(BT9_081 "hack DROPPED"·"Hackmon"=카드명) | 0 |
| `stopgap` | 3 | 3/3 REMOVED/retired(AttackPermanentAction proxy 제거·MainSkillActivateAction=real home) | 0 |
| `for now` | 4 | 3(AS-IS UI 노트·MIG1 tracked) | 소량: `CardEffectCommons.cs:2260`(trash-root caller "STOP for now" — 조건부 STOP, XS)·`GiveEffectToPermanentOrPlayer.cs:4`(batch-C relocation "for now", XS) |
| `transitional` | 4 | 4/4 RETIRED(R7 종점·id-flip 은퇴) | 0 |
| `임시` | 4 | 4/4 무관(BT17_026 게임텍스트 "임시 treated as X"=카드 기제) | 0 |

**→ 어휘 가족 OPEN 순증 = 소량 XS/S 5건**(choice-serialization 노출·SelectCardEffect F-3.7·MainSkillActivate per-index·trash-root STOP·GiveEffect relocation) — 전부 latent/사소, §3 편입 권고이나 규모 XS~S.

### 5.4 design-item 문구 없는 RD-/P6A-/MIG- ID (612 라인) — 신규 ID 추출

family-ID 유니버스 238종 중 §1~§3 design-item 집합(96) 밖 = ~142종. 활성-갭 어휘 동반(latent/GAP/STOP/deferred/no live)·해소어(RESOLVED/CLOSED/RETIRED/LIVE) 제외 필터 → **89 라인**이 OPEN-후보. 실코드 대조 결과 대부분은:
- **rebuild 단계 원장 ID**(RD-P6Bx·RD-P6Cx·RD-EXTx·RD-Rx·RD-Wx·RDW-0x): 각 `docs/audit/rebuild_p6_cluster*`·`rebuild_bridge_w*`·`window_supply_correspondence` **작업 원장에 등재된** 착지 항목(대부분 RESOLVED). freeze/red 원장에는 없으나 전용 원장 보유 → "미등재"로 계상 안 함.
- 순수 신규 OPEN(전용 원장에도 미등재, §3 미포함): 실측상 **없음** — 89 후보 전량이 §3 기수집 ID이거나 전용 rebuild 원장 등재분.

예외 표기 확인: `RDW-NN`·`RD-R3-NN`·`RD-2`/`RD-4`/`RD-3`(단독 숫자) = 플레이스홀더/절삭 인용(실 ID 아님). `RD-J03`=`RD-J-03` 오타변형(정규화 병합). 신규 순증 ID = **0**.

---

## 6. 특이 발견 (오분류·이중-ID·주석-외 이연)

1. **이중-ID `RD-R3-01`**: 두 무관 기능에 동일 ID — ①삭제-교체 창 cut-in drain(MatchStateMutationSink/CardController/MultipleSkills) ②printed EvolutionCondition 토큰 시스템(DigivolutionCostHelpers/DigivolveAction/CardSource). 둘 다 RESOLVED이나 ID 충돌은 원장 추적을 오도. **분리-재명명 권고.**
2. **이중-ID `RD-R3-02`**: ①CutInProcess DEAD-STOP(PERMANENT) ②PermanentBookkeepingStore 수명(r4 P1-2, RESOLVED). 서로 다른 갭.
3. **STOP 좌석 `CardController.cs:4283` = design-item ID 미부여**: DISPATCH-REMAP double-key 방어인데 나머지 3좌석과 달리 RD-ID가 없음. retirement-guard 규약상 ID 부착 권고(예: RD-DISPATCH-DBLKEY).
4. **stale-모순 `P6A-PLAYER-EFFECTLIST`**: 한 파일(`CardSource.cs:2015`)은 flip 완료 서술, 다른 두 파일(`CardSource.cs:2370`·`Player.cs:21`)은 미상환 서술 — 동일 ID 상반 주석.
5. **freeze §4 "latent 6종" 오등재**: 나열된 RD-3A-02·MIG4-DETACH·RD-SW-E-01/02·R2-P2-2가 실제로는 RESOLVED/PERMANENT. 즉 freeze §4는 진짜 OPEN(§3의 42종)을 **하나도** 담지 못했다 — 임무 전제("극히 일부만 열거") 실증.
6. **접두 정규화**: `RD-P6C1-1`≡`MIG5-FRAME-MODEL`, `RD-W4-2`≡`RD-W3-1`, `MIG3-SECURITYLOOKING`≡`MIG6-SECURITYLOOKING`, `MIG5-PLAYER-EFFECTLIST`→`P6A-PLAYER-EFFECTLIST`(supersede) — 문서마다 다른 표기.
7. **주석-외 이연 표기**: Script/ 48 스캐폴드 헤더·CardEffect/ 3,574 셸 헤더는 `design item` 아닌 `TODO: Skeleton only`로 이연을 기록 → design-item 규약을 우회한 최대 구멍(다만 룰층 OPEN은 0, 콘텐츠 큐).

---

## 7. 방언 재수렴 캠페인 (리터럴-키 20파일 re-key) — 심사

- **실체**: [When Digivolving]을 AS-IS는 On Play와 동일 `OnEnterFieldAnyone` 창 공유 + `CanTriggerWhenDigivolving`로 구분.
  미러 실행기는 진화 시 **두 창(OnEnterFieldAnyone + WhenDigivolving)을 같은 hashtable로 모두 개방**
  (`CardController.cs:4243-4297` DISPATCH-REMAP BRIDGE). 코퍼스 실측: **전용-키 파일과 AS-IS-리터럴 병존**
  (canonical §290: 전용-키 54 / AS-IS-리터럴 20). 동일 효과 2키 이중등재 시 실행기 STOP(§2 CardController:4283 좌석).
- **campaign**: AS-IS-리터럴 20파일을 단일 전용-키로 재수렴 = **P6A-HT-ENTERFIELD 완성 후** 예정된 이연
  (`card_porting_canonical_2026-07-23.md:298-299`에 명시). 신규 카드는 이미 전용-키 규칙 강제(선택 사안 아님).
- **도달성/규모**: latent(현행 DISPATCH-REMAP 브릿지로 양 방언 모두 발화 — 회귀 없음). 재수렴 자체는 **P6A-HT-ENTERFIELD(L) 선행 블로커에 종속** + re-key 20파일(**M**, 기계적). 브릿지 은퇴가 종점.
- **원장 등재?**: ✗ — canonical 주석·code 주석에만 존재, freeze/red 원장 미등재. **편입 필요.**
  (참고: WhenDigivolving 전용-키 파일 실측 `grep -rln "미러 방언\|mirror dialect\|dedicated WhenDigivolving" …/CardEffect` = 42; canonical의 54는 다른 카운트 기준 — 정확 계수는 P6A-HT-ENTERFIELD 착지 시 재실측 권고.)

---

## 8. 원장 편입 필요 목록 (핵심 산출물)

freeze_evidence / red_ledger에 **미등재인 OPEN** (전용 rebuild 원장 보유분 제외, 순수 룰층 갭):

**L/M 규모(우선):**
1. `P6A-HT-ENTERFIELD` (L) — 두-방언 이음새 뿌리, FULL play payload 스레딩.
2. **방언 재수렴 캠페인**(M, ①에 종속) — 리터럴-키 ~20파일 re-key + 브릿지 은퇴.
3. `P6A-HT-SECURITY`(M)·`P6A-STAMP-PERSISTENCE`(M) — 브릿지 페이로드/스탬프.
4. `RD9-87`(M)·`RD9-90`(M) — tapped/untapped raw metadata·[Main] 선언 액션.
5. `RD-P6C1-1`≡MIG5-FRAME-MODEL(M)·`RD-P6C1-2`(M) — 프레임 모델·capacity check.
6. `MIG3-DEGEN-COUNTSELECT`(M, LOUD STUB) — SelectCountEffect mirror.
7. 카드 fidelity debt `RD-R6-05·RD-R6-06·C1w-24·C1w-25·C2-01`(각 M) — omitted 브랜치, 프리미티브 선행.
8. `RD-④E-{SELFRESTR,PSRESTR,PSKEYWORD,TRIGGERGRANT,PSMODIFIER}`(S~M) — production-0 latent 표면.

**S/XS 규모:** P6A-HT-{USEOPTION,DIGISOURCE,ENDBATTLE,CAUSE}·P6A-{STACKED-DRAIN,USED-JOURNAL}·MIG3-{LOCATIONTIME,SECURITYLOOKING,CANREDUCESECURITY,CANADDSECURITY,TAPPEDANYONE-PAYLOAD,TRASHSEC-UNIFY}·RD-W{4-1,4-6,3-7,3-6,3-4,3-2}·RDW-05·RD-P6{C1-9,B-2,B-5}·RD-EXT2B-01-BATTLEFIELD·MIG1-{BEFOREONATTACK,KEYWORD-RELOCATE,EXECUTE-RELOCATE}·MIG2-ADDLINK-SELECT·RD-CATK-EATTACK-MULTI·RD-BT13028-AceOverflow·P2-{ISEXECUTING,STACKSKILLINFOS}·F1-ADDHAND-FLUSHGRAIN·RD7-71 + 어휘스윕 XS 5건(§5.3).

**주의**: 이들은 전량 **latent(호출자-0 또는 브릿지-우회)** — live 대량-포팅(⑦) 착수 전에는 회귀 없음.
그러나 완성-정의 규약(`completion-is-structural-not-scoped`)상 **동결/마감 전 편입 대상**이지 이연-종결 대상 아님.
