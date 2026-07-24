# 이전-트랙 미반영 결함/미완 항목 전수 대조 (2026-07-24)

Base: HEAD `e6e3b5a0`. **조사 전용 — src/tests 무수정.** 대조 대상 원장 = `ENGINE_DEFECT_LEDGER_2026-07-24.md`
(DEF-A1~16·B1~18·C1~4·S1~21·R1~2). 판정 근거 = 현재 소스 grep/실독(census 문구 불신). 각 항목은 이전-트랙
census/원장에서 "미해소"로 표기된 것을 이번 조사에서 현재 소스로 재확인해, 아직 미해소이면서 위 DEF 목록에
없는 것만 "미반영"으로 수집했다.

---

## 1. 미반영 항목 — 신규 DEF 편입 대상

### 1.1 구조적 발명/사문 잔재 (C계열 신규 3건)

이 3건은 `c5_live_invention_census_2026-07-24.md`(System 1~6)가 상세 실측했으나, C0~C5 청산 커밋
(`db0b01f8`→`190424c0`)이 이 census의 **일부만** 집행했다. System 1(ContinuousEffectEvaluator/ModifierHelpers/
KeywordBaseBatch1·2/CardRequirementHelpers/Conditions/ContinuousAndRestrictionEffects)은 전량 삭제·재하우징
완료(현재 소스에 파일 부재/분할 확인) — **이미 해소**(§2 참조). 그러나 **System 5(NewModelContinuousScan)와
RestrictionHelpers/ReplacementHelpers의 사문 공개 API는 미집행** — 현재 소스에도 그대로 남아 있고, DEF
원장 C1~C4 어디에도 없다.

| ID(제안) | 심볼/파일 | 결함(AS-IS 대비) | 현재 소스 상태(검증) | 편입 계열 |
|---|---|---|---|---|
| **DEF-C5(신규)** | `Script/CardEffectCommons/NewModelContinuousScan.cs` (1692줄, public static 46메서드) | AS-IS는 연속 계산을 `Permanent`/`CardSource` getter에 인라인 스캔으로 분산(`Permanent.cs` `DP`/`HasBlocker`/`HasJamming`/`HasPierce`/`HasCollision` 등 getter가 **이미 존재**). 본 파일은 그 getter를 (context,cardId)→bool/int 어댑터로 **중복**하는 미러-경로 이탈 발명 파일(동일-파일명 규약 위반) — mirror-into-asis-file 규약 위반. | **존재 확인**(1692줄, 2026-07-24 HEAD). 라이브 소비 46참조/32파일(런타임 6+미러 코어 4+kind-class ~10+카드 3, 자기 제외) — `grep -rn "NewModelContinuousScan" src/HeadlessDCGO.Engine \| grep -v NewModelContinuousScan.cs \| wc -l`→49(테스트 포함). C5 census "System 5 REHOUSED(재배선→삭제)"가 상세 트랜치 설계(T5-DP/Cost·Keyword·Restriction·Deletion)까지 마쳤으나 **미집행**(파일 그대로). | **C**(발명, 구조-이동 잔존) — DEF-C1~C4 어디도 이 파일을 다루지 않음. C5 census의 (b)앵커별 4트랜치 설계를 그대로 상속 권고. |
| **DEF-C6(신규)** | `Script/CardEffectCommons/RestrictionHelpers.cs`의 공개 평가기(`Evaluate`/`ReadRestrictions`/`IsRestricted`/`CannotAttack`/`CannotBlock`/… 팩토리, `RestrictionHelperFactory`) | AS-IS는 `ICanNot*` 인터페이스 스캔으로 제약을 집행(`Permanent.CanMove`/`CanSelectBySkill` 등). 본 값-딕셔너리 평가기(`CannotRestriction`/`CannotRestrictionRequest/Result` 레코드 + `Evaluate`/`Read*`)는 AS-IS 무대응 발명. | **production consumer = 0 확인**(`grep -rn "RestrictionHelpers\.\(Evaluate\|ReadRestrictions\|IsRestricted\|CannotAttack(\)" src tests` → 유일 소비자 `tests/G3H-002.Cannot.restriction.helper.Tests` 뿐). 단, `CannotXKey` const 10종은 `ContinuousRestrictionGate`/`MatchStateMutationSink`/`NewModelContinuousScan`이 키-네임스페이스로 실사용(보존 대상, 삭제 스코프 아님). | **C**(발명, 사문 공개 API — DEF-C4 "사문 레거시 API 블록(호출 0)" 패턴과 동형이나 다른 파일). `c5_live_invention_census`가 "Dismantle: delete the dead public evaluator + factory + G3H-002 test" 명시했으나 미집행. |
| **DEF-C7(신규)** | `Script/CardEffectCommons/ReplacementHelpers.cs`의 공개 평가기(`Evaluate`/`ReadReplacements`/`PreventRemoval`/`PreventDeletion`/`ImmuneFromDpReduction`/`ImmuneFromCostReduction`, factory) | AS-IS는 `ICannotReduceCostEffect`/`ImmuneFromDpMinus` 인터페이스 스캔 + kind-class로 방지/면역을 처리. 본 값-딕셔너리 교체 모델은 AS-IS 무대응 발명. | **production consumer = 0 확인**(`grep -rn "ReplacementHelpers\.\(Evaluate\|ReadReplacements\|PreventRemoval\|PreventDeletion\|ImmuneFromDpReduction\|ImmuneFromCostReduction\)" src tests` → 유일 소비자 `tests/G3I-001.Replacement.prevention.helper.Tests` 뿐). `ImmuneFrom*Key`/`PreventRemovalKey` 등 const는 `CardEffectFactory.cs:348` 주석 인용 1건뿐 실사용 미확인(재검 필요, 삭제 스코프에서 keys는 보류 권고). | **C**(발명, 사문 공개 API). 동일 census가 "Dismantle: delete dead public evaluator + factory + G3I-001 test" 명시했으나 미집행. |

### 1.2 Design-item 이연 갭 (design_item_census §3 OPEN 42종 중 미등재분 — B/S계열)

`design_item_census_2026-07-23.md` §3은 96종 design-item 표기를 RESOLVED(41)/PERMANENT(13)/OPEN(42)로 실코드
대조해 분류했고, OPEN 42종 전부가 freeze_evidence/red_ledger 어디에도 미등재임을 확인했다(§8 원장 편입 목록).
본 조사는 이 42종을 **현재 소스(HEAD e6e3b5a0)로 재-grep**해 여전히 코드에 존재하는지 확인했다 — 결과:
**39종 현존(미해소 확정), 3종은 코드 자체가 사라짐(§2에서 "이미 해소"로 재분류)**. 아래는 현존 39종
전수(현재 위치 확인, 원장 미등재 재확인).

| ID | 심볼/파일(현재 소스 확인) | 무엇이 미구축(요약) | 도달성 | 편입 계열 제안 |
|---|---|---|---|---|
| P6A-HT-ENTERFIELD | `Headless/Bridge/ActivatedHashtableBridge.cs` | OnEnterFieldAnyone/WhenDigivolving이 CHECK 빌더만 사용; FULL play payload(evoRoots/evoRootTops/Root/oldLevels)를 PlayCardAction/DigivolveAction에서 미스레딩. 방언 재수렴 캠페인의 선행 블로커 | latent | S(substrate 페이로드 갭) |
| P6A-HT-USEOPTION | `ActivatedHashtableBridge.cs` / `Headless/Effects/SkillWindowSupply.cs` | OnUseOption {Card}만; AS-IS는 Root+Cost 동반 | latent | S |
| P6A-HT-SECURITY | `ActivatedHashtableBridge.cs`, `CardEffect/ST1/…/ST1_12.cs` | [Security] 활성 페이로드 face-up reveal 미착지 | latent | S |
| P6A-HT-DIGISOURCE | `ActivatedHashtableBridge.cs` | discarded SOURCE 리스트 미스레딩 | latent | S |
| P6A-HT-ENDBATTLE | `ActivatedHashtableBridge.cs` | OnEndBattle winner/loser 페이로드 미매핑(null) | latent | S |
| P6A-HT-CAUSE | `ActivatedHashtableBridge.cs` | AS-IS causing ICardEffect 객체 vs cause-id 스텁 | latent | S |
| P6A-STAMP-PERSISTENCE | `Headless/Effects/ActivatedEffectResolver.cs` | 마커만 보유, effect 객체 미보존 | latent | S |
| P6A-USED-JOURNAL | `ActivatedEffectResolver.cs` | used-effect journal 미구축 | latent | S |
| P6A-STACKED-DRAIN | `Script/AutoProcessing.cs` | stacked list를 window loop가 미-drain | latent | S |
| P6A-PLAYER-EFFECTLIST | `Script/CardSource.cs`(:2015 vs :2370)·`Player.cs:21` | 순수 갭은 사실상 flip됨; 잔여는 **주석 stale 모순**(한쪽은 "flip 완료", 다른 두 곳은 "미flip") — 정합 정리만 필요 | live(부분)/문서정합 | S(경미, 문서정리) |
| RD-④E-PSKEYWORD | `Script/CardEffectFactory.cs` | player-static keyword-grant 표면 미배선(census=0 production) | latent | B |
| RD-④E-TRIGGERGRANT | `CardEffectFactory.cs` | AddSkillClass keyword-grant 포팅 미결 | latent | B |
| MIG3-DEGEN-COUNTSELECT | `Script/CardController.cs:873`, `Script/SelectCountEffect.cs:19` | SelectCountEffect mirror/choice 부재 — **LOUD STUB**(live 도달 가능) | live(스텁 도달) | B |
| MIG3-LOCATIONTIME | `CardController.cs`(4사이트) | SetChangedLocationTime headless analog 부재(no-op) | live(no-op) | B |
| MIG3-SECURITYLOOKING / MIG6-SECURITYLOOKING | `Script/Player.cs`, `Script/SelectCardEffect.cs`, `Script/GameContext.cs` | SecurityLooking live reader 부재(delegated) | latent | S |
| MIG3-CANREDUCESECURITY | `CardController.cs`, `Player.cs` | Player.CanReduceSecurity stand-in | latent | S |
| MIG3-CANADDSECURITY | `CardController.cs`, `Headless/Effects/MatchStateMutationSink.cs` | Player.CanAddSecurity stub | latent | S |
| MIG3-TAPPEDANYONE-PAYLOAD | `CardController.cs`(2사이트) | tapped payload zone 미도출 | latent | S |
| MIG3-TRASHSEC-UNIFY | `MatchStateMutationSink.cs` | CanReduceSecurity 핸들러 미통합 | latent | S |
| RD9-87 | `Script/AttackProcess.cs`, `Headless/Effects/SkillWindowSupply.cs`, `Headless/Runtime/AttackDeclarationCommons.cs` | OnTappedAnyone/OnUntappedAnyone raw metadata write 미배선 | latent | S |
| RD9-90 | `AttackDeclarationCommons.cs`, `Headless/Runtime/AttackPermanentAction.cs` | [Main] skill declaration action 미포팅(ATTACK 대체발화) | latent | S |
| RD-W4-1 | `Script/SelectCardEffect.cs:344` | ChangeCostClass 등록 미배선 | latent | B |
| RD-W4-6 | `Script/SelectPermanentEffect.cs:609` | deferred choice, AS-IS caller 0 | latent | B |
| RD-W3-7 | `Headless/Runtime/AttackDeclarationCommons.cs`, `CardEffect/ST13/…`, `Script/SelectAttackEffect.cs`, `Script/CardEffectCommons/KeyWordEffects/Blitz.cs` | Blitz no-hook gate/offer cause-threading 잔여 | latent | S |
| RD-W3-6 | `Script/CardEffectCommons/DNADigivolveEffects.cs:23` | DNADigivolve behaviour nuance | latent | B |
| RD-W3-4 | `Script/CardEffectCommons.cs` | PlayCardsBridge 미지원 표면 | latent | B |
| RD-W3-2 | `Script/CardEffectCommons.cs` | RevealLibrary substrate gap | latent | S |
| RDW-05 | `Headless/Effects/SkillWindowSupply.cs`, `Script/AttackProcess.cs` | attackCauseEffectId만 스레딩, live ICardEffect 미보유 | latent | S |
| RD-P6C1-1 (≡MIG5-FRAME-MODEL) | `Script/Permanent.cs`, `Script/CardController.cs`, `Script/Player.cs`, `Script/CardSource.cs` 외 다수 카드 | PermanentFrame.IsBattleAreaFrame 모델 부재(field-frame 슬롯) | latent | B(인프라) |
| RD-P6C1-2 | `Permanent.cs`, `CardSource.cs`, `Player.cs`, `CardController.cs`, `TurnStateMachine.cs`, `CardObjectController.cs` 외 다수 | CanMove/AppFusion capacity check 생략(6+ 사이트) | latent | B |
| RD-P6C1-9 | `Script/CardSource.cs`, `Script/SelectDNACondition.cs`, `Script/CardController.cs:4513` | SelectDNACondition/CardController relocation → mirror CardSource 이전 대기 | latent | B |
| RD-P6B-2 | `Script/CardEffectCommons/NewModelContinuousScan.cs` | continuous-scan latent 경로 | latent | S(§1.1 DEF-C5와 동일 파일, 세부 서브아이템) |
| RD-P6B-5 | `NewModelContinuousScan.cs` | Decoy presence check gate-less | latent | S(상동) |
| RD-EXT2B-01-BATTLEFIELD | `Script/CardController.cs:4698` | "battle" HASHTABLE 키 live mirror reader 부재 | latent | S |
| MIG1-BEFOREONATTACK | `Script/AttackProcess.cs:200`, `Script/SelectAttackEffect.cs:26` | beforeOnAttack 콜백 1:1 존재하나 bridged caller 무설정 | latent | S |
| MIG1-KEYWORD-RELOCATE | `AttackProcess.cs:42` | AttackProcess 키워드 미러 relocation 미결 | latent | B |
| MIG1-EXECUTE-RELOCATE | `AttackProcess.cs:44` | DeleteSelfEffect relocation 미결 | latent | B |
| MIG2-ADDLINK-SELECT | `Headless/Runtime/LinkHelpers.cs`, `Script/Permanent.cs:4330` | LinkedMax>1 owner-selection 미배선(자동 oldest-first 대체) — **DEF-S10과 동일 갭의 design-item 짝**(DEF-S10은 이미 원장에 있음; 본 항목은 design-item ID 라벨만 원장에 미기재) | latent | S(DEF-S10 참조 추가 권고, 신규 DEF 불요) |
| P2-ISEXECUTING | `Script/TurnStateMachine.cs:41` | isExecuting 미미러 | latent | B |
| P2-STACKSKILLINFOS | `Script/GManager.cs:41` | AS-IS StackSkillInfos 미러 부분 | latent | B |
| F1-ADDHAND-FLUSHGRAIN | `Headless/Effects/MatchStateMutationSink.cs:457` | ADD-HAND 캐시 grain=sink FLUSH deferred | latent | S |
| RD7-71 | `Headless/Runtime/SecurityResolver.cs`(:172 부근 및 CardDP 관련) | security Digimon이 CardDP로 전투 — ordering divergence 주석(AS-IS 대비 계약 노트) | live | S |
| RD-BCE-01-sanctioned | `Headless/Effects/MatchStateMutationSink.cs` | RD-BCE-01의 sanctioned 변형(§2 PERMANENT와 짝, 관측 무변) | latent | S(경미) |

**카드-레벨 fidelity debt 5종**(RD-R6-05·RD-R6-06·C1w-24·C1w-25·C2-01)은 **의도적으로 이 표에서 제외**했다 —
현 원장 방침("카드층은 감사 대상 아님, 전량 무효·재포팅 대기, 개별 검증 불요")상 개별 카드 콘텐츠 갭은
재포팅으로 해소될 항목이라 판단. 단, 이들이 지목한 **엔진 프리미티브 결손**(이름족 술어 `HasGarurumonName`류,
reveal-3 코루틴 프리미티브, `BreakSecurityEffect` UI carrier, PRE leave-hook tier)이 재포팅 후에도 여전히
프리미티브 자체가 없다면 재발한다 — 재포팅 착수 전 별도 프리미티브 인벤토리 확인 권고(원장 편입 보류,
참고용으로만 기록).
`RD-BT13028-AceOverflow`(BT13_028 카드) 역시 같은 이유로 제외.

---

## 2. 이미 해소됨 (원장 불요 — 중복작업 방지)

아래는 이전-트랙 census가 "미해소/BLOCKED/latent"로 표기했으나, **현재 소스(HEAD e6e3b5a0) 확인 결과 이미
해소**된 항목. 재작업 금지.

### 2.1 `c5_live_invention_census_2026-07-24.md` System 1~4, 6 — 물리 삭제/재하우징 완료
- **System 1**(ContinuousEffectEvaluator + 빈-union 클러스터): `ContinuousEffectEvaluator.cs`·`ModifierHelpers.cs` **파일 자체 삭제 확인**. `ContinuousScopeEvaluation.cs`는 계획대로 "harmless empty stub"만 남기고 보존(`ApplicableEffects`가 `Array.Empty<EffectRequest>()` 반환, 주석에 "(C5-1) … DELETED with the empty-union evaluator" 명시) — **의도된 잔존, 갭 아님**.
- **System 3**(NumericModifier/ModifierHelpers): `ModifierHelpers.cs` 파일 삭제 확인, `ContinuousModifierGate.cs`는 cost 래퍼만 남아 `CardSource.GetPayingCostWithBaseCost`로 순수 delegate(주석 "(W3c-final) LEGACY substrate cost fold — RETIRED", producer 0 확인 명시) — **해소 완료**(최근 커밋 `10cfc98d` "cost 파이프라인 순수 AS-IS화"와 정합).
- **System 4**(KeywordBaseBatch1/2): `Script/CardEffectCommons/KeyWordEffects/KeywordBaseBatch1.cs`·`KeywordBaseBatch2.cs` **파일 삭제 확인**.
- **System 6a**(Conditions.cs `DigivolveCost` enum): 파일 삭제 확인.
- **System 6b**(CardRequirementHelpers.cs): 파일 삭제 확인.
- **System 6c**(ContinuousAndRestrictionEffects.cs): 파일 삭제 확인. 계획대로 분할 완료 — `CanNotMoveEffect`/`CanNotSelectBySkillEffect`는 `Script/CardEffects/RestrictionCarriers.cs`로 rehoused(헤더 주석 "(C3 REHOUSED) The two LIVE joint-carrier restriction effects were relocated here verbatim…" 확인), `BareCauseEffect`는 `Headless/Bridge/BareCauseEffect.cs`로 이관 확인.
- (System 5 `NewModelContinuousScan.cs`만 **미집행** — §1.1 DEF-C5로 편입.)

### 2.2 `structural_invention_census_2026-07-21.md` NEW-01 `OnPlayReactivation` — RETIRED
파일 자체가 사라짐(`Headless/Runtime/OnPlayReactivation.cs` 부재 확인). `Headless/Effects/ActivatedEffectResolver.cs:693`
주석 "(이연③-b RETIRED) `case ReuseWhenDigivolvingEffect` DELETED"와 `EX8_074.cs:31` "OnPlayReactivation driver
(which no-ops … once ReuseWhenDigivolvingEffect is gone)" 확인 — 발명 드라이버가 범용 broadcast 경로로 환원되며
완전 은퇴. census가 제안한 "소멸 경로"가 그대로 집행됨.

### 2.3 `Script/CardEffectCommons/EffectChoiceHelpers.cs` — 부분 해소(당초 발명 지적 부분은 제거됨)
verdicts_tobe_only.csv가 "INVENTED-LIVE"(공개 evaluator `EffectChoiceResolution`/`ApplyResult`/`ResolveAsync`
등 라이브 발명·프로덕션 0)로 지적했던 부분은 **이미 삭제**(현재 104줄, 헤더 주석 "(C5-0) The invented
choice-resolution engine … was DELETED here"). 잔존 4개 빌더(`Candidate`/`CreateCardRequest`/
`CreatePermanentRequest`/`CreateCountRequest`)는 census가 "라이브 빌더 유지" 대안으로 명시 승인한 부분 —
갭 아님.

### 2.4 `registry_probe_census_2026-07-20.md` 전체 클러스터 — 물리 삭제 완료
`EffectRegistry`(interface/InMemory)·`EffectBinding`·`IEffectBody`·`IActivatedCardEffect`·`IHeadlessCardEffect`·
`CardEffectRegistrar`·`ActivatedEffect(s)`·`LegacyActivatedBridge`·`InheritedGrantedSecurityHelpers`·
`TriggeredEffects` **전 파일 부재 확인**(`db0b01f8`+`190424c0`+이후 결함원장 확정 커밋 `d5699aa8`/`e6e3b5a0`).
freeze_evidence §1 "발명물 grep(비-주석) = 0"과 정합. `EffectDurationExpiry.cs`는 살아있으나 registry-sweep
기능은 전량 은퇴, 유일 잔존 멤버(`ExpireFixedCostCalc`)는 AS-IS 버킷-리셋 1:1 포트(주석 확인) — 갭 아님.

### 2.5 `primitive_residual_census_2026-07-22.md` SKELETON 13종 + UNPORTED 1종 — 전량 포팅 완료
`MinMax_DP_Cost_Level/**` 7파일(IsMaxCost/IsMinCost/IsMaxDP/IsMinDP/IsMinDigivolutionCards/IsMaxLevel/IsMinLevel),
`GiveEffect/**` 4종(ChangeLinkMax/StartOfMainAttack/ChangeDigivolutionCost/IgnoreDigivolutionRequirement),
`KeyWordEffects/Training.cs`, `TrashLinkedCards.cs` — **전부 `TODO: Skeleton only` 마커 0, 실 로직 확인**(2026-07-22
census 이후 포팅 완료). `GiveEffectToPermanent/CanNotBeDeletedByBattle.cs`(당시 UNPORTED)도 94줄 실 로직 확인 —
비대칭 갭 해소.
`SelectJogressEffect.cs`(당시 SKELETON)는 의도적 "bodiless"로 재확정(헤더 주석 "(SKEL-Exhaust) RECLASSIFIED …
No engine-primitive body to port at this layer — left intentionally bodiless") — PERMANENT 성격, 갭 아님.

### 2.6 `primitive_residual_census_2026-07-22.md` DIVERGENT 2종 — STOP 해소 완료
`CardEffectFactory/KeyWordEffects/BlastDigivolution.cs`·`BlastDNADigivolution.cs` 모두 `NotSupportedException`
**부재 확인**(현재 live STOP 좌석은 `Permanent.cs:4549`·`GManager.cs:198`·`TrashLinkedCards.cs:72`·
`CardController.cs:4339` 4곳뿐 — DEF 원장의 4-STOP-좌석과 정확히 일치). red_ledger "BlastDNA — PORTED (item 3a)"
+ freeze_evidence §7 "STOP 재계수: live 3+dead-가드 1"과 정합 — field-frame 슬롯 갭이 두 키워드 모두에서 해소됨.

### 2.7 `design_item_census_2026-07-23.md` §3.3 RD-④E 5종 중 3종 — 코드 소멸(부모 파일 삭제 동반)
`RD-④E-SELFRESTR`·`RD-④E-PSRESTR`·`RD-④E-PSMODIFIER`는 부모 파일 `ContinuousAndRestrictionEffects.cs`가
§2.1(System 6c)에서 삭제되며 함께 소멸(현재 소스 grep 0). 나머지 2종(`RD-④E-PSKEYWORD`·`RD-④E-TRIGGERGRANT`)은
`CardEffectFactory.cs`로 이관된 채 **잔존** — §1.2 표에 편입.

### 2.8 `red_ledger_2026-07-23.md` — 전량 CLEARED(이미 재확인됨), 신규 발견 없음
stale-pin 7 + documented-latent 26 + latent-STOP 3 전부 RESOLVED, 425/425 green, STOP 4좌석은 DEF 원장의
PERMANENT/R-계열과 이미 정합. 재조사에서 신규 미반영 항목 없음.

### 2.9 `freeze_evidence_2026-07-23.md` §4 "latent 6종" — design_item_census가 이미 반증
`RD-3A-02`·`MIG4-DETACH`·`RD-SKEL-01`·`RD-SW-E-01/02`·`R2-P2-2` 전부 RESOLVED/PERMANENT로 재분류 완료(design_item_census
§1/§2, 6번 특이발견). 이번 재조사에서도 동일 결론 — 신규 항목 없음.

---

## 3. 요약

| 구분 | 건수 |
|---|---|
| 신규 DEF-C 후보(구조적 발명/사문 API) | 3 (DEF-C5 NewModelContinuousScan · DEF-C6 RestrictionHelpers · DEF-C7 ReplacementHelpers) |
| 신규 편입 대상 design-item OPEN(B/S계열) | 39 (§1.2 표; 이 중 1건은 기존 DEF-S10과 병합 권고, 1건은 문서-정합만 필요) |
| 참고-보류(카드 재포팅 후 재확인) | 6 (RD-R6-05·RD-R6-06·C1w-24·C1w-25·C2-01·RD-BT13028-AceOverflow) |
| 이미 해소(재작업 금지) 클러스터 | 9 (§2.1~§2.9) |

**핵심 발견**: `NewModelContinuousScan.cs`(DEF-C5)가 가장 규모가 크고 확실한 미반영 — C5 census가 상세
설계(4트랜치)까지 마쳤음에도 물리 삭제만 유일하게 미집행. `RestrictionHelpers`/`ReplacementHelpers`의
사문 공개 API(DEF-C6/C7)는 DEF-C4와 동형 패턴이라 원장이 놓치기 쉬웠던 반복 사각지대. design-item OPEN
39종은 대부분 `Headless/Bridge/ActivatedHashtableBridge.cs`(P6A-HT-*, substrate 페이로드 미스레딩) 클러스터에
집중 — 이 파일 1곳이 8종의 latent 갭을 담고 있어 단일 트랜치로 상환 가능성 높음.
