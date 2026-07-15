# C-Del(삭제-치환) wave3 사전조사 보고서 (2026-07-15)

Base: `454eb84a`(a1-step1-integration). 조사 전용 배치(무수정, working tree clean). AS-IS grep `--binary-files=text`. 스위트 실측: `PRIM-P0.WouldBeDeletedWindow`=PASS.

## §A. AS-IS 진실표 정밀화

**AS-IS 삭제 시퀀스**(`DCGO/…/CardController.cs:3667-3873`, `DestroyPermanentsClass.Destroy`) — 클러스터 전체의 발화 골격:
1. `willBeRemoveField=true` 전 대상(:3684)
2. **PRE 컷인 창 ①** `autoProcessing_CutIn.StackSkillInfos(WhenPermanentWouldRemoveFieldCheckHashtable, WhenPermanentWouldBeDeleted)`(:3690-3696)
3. **PRE 컷인 창 ②** 동일 hashtable로 `WhenRemoveField`(:3699-3705)
4. `HasAwaitingActivateEffects` 시 `autoProcessing_CutIn.TriggeredSkillProcess` — 치환 body 실행, `willBeRemoveField=false`로 삭제 취소(:3707-3724)
5. survivor 고정: `destroyTargetPermanents_Fixed`(:3729-3732)
6. **POST 창** `OnDeletionHashtable→OnDestroyedAnyone` **제거 전 수집**(collect-before-removal, :3736-3743) → `OnLeaveFieldAnyone`(:3749-3756)
7. 물리 트래시: `DiscardEvoRoots`→`RemoveField`→`AddTrashCard`(:3846-3852)

치환 기제 = **배치 단위**(리스트 전체가 한 Destroy 시퀀스), PRE 컷인 스택(①② back-to-back·동일 hashtable·같은 TriggeredSkillProcess 해소) 중 `willBeRemoveField=false` 개서. POST=삭제 확정 후 재생/구제.

| 키워드 | 인쇄 factory | 게이트/CanActivate | Process | 창 | GainK | AS-IS 소비 |
|---|---|---|---|---|---|---|
| Evade | Factory/Evade.cs:10/32 | CanTriggerEvade(Commons/Evade.cs:9); cost=미탭 suspend | suspend self→취소(:39) | **PRE** WouldBeDeleted | **有** GainEvade(:53) | 18 |
| Decode | Factory/Decode.cs:8/28 | CanTriggerWhenRemoveField && !IsByBattle(:51-55) | 진화원 1장 무료 play(취소 아님) | **PRE** WhenRemoveField | **有** GainDecode(Commons/Decode.cs:79) — **미러 부재** | 15 |
| Decoy | Factory/Decoy.cs:10/32 | 타 permanent RemoveField && IsByEffect(적)(:53-72) | 타 Decoy 삭제→보호대상 취소 | **PRE** WouldBeDeleted | 無 | 12 |
| Fragment | Factory/Fragment.cs:7/29 | CanTriggerWhenRemoveField(:52); N진화원≥trashValue | N 진화원 트래시→취소 | **PRE** WhenRemoveField | 無 | 7 |
| Partition | Factory/Partition.cs:41/60 | CanTriggerPartition(Commons/Partition.cs:10): !ByBattle && !ByEffect(owner) | 색군별 2장 무료 play(취소 아님) | **PRE** 컷인 | 無 | 13 |
| Fortitude | Factory/Fortitude.cs:10/33 | CanTriggerOnDeletion(Commons/Fortitude.cs:9); IsExistOnTrash+진화원 스냅샷(:16, 트래시-게이트) | 트래시서 자기 무료 재play | **POST** OnDestroyedAnyone | **有** GainFortitude(:67) — **quirk: EvadeEffect 생성**해 OnDestroyedAnyone 버킷 저장(:89-91) | 27 |
| Scapegoat | Factory/Scapegoat.cs:11/33 + 정적 ScapegoatClass(IScapegoatEffect) | self RemoveField && !ByEffect(owner)(:64-83) | 타 아군 delete→성공 시 self 취소 | **PRE** WouldBeDeleted | 無 | 17 |
| Save | Factory/Save.cs:10 | CanTriggerOnDeletion(:32-35); IsTopCardInTrashOnDeletion(트래시-게이트) | 트래시서 아군 Tamer 밑으로 | **POST** OnDestroyedAnyone | 無 | 52 |
| MaterialSave | Factory/MaterialSave.cs:10 | IsExistOnBattleArea && CanTriggerWhenRemoveField(:43-54); IsContainDigiXrosCondition | DigiXros 조건 카드 Tamer 밑으로 | **PRE**(등록=WouldBeDeleted, EX4_020:159) | 無 | 13 |

카드 등록 timing은 인쇄 keyword가 `CardEffects`의 `if (timing==X)` 키로 결정(예: Evade=BT13_023 WhenPermanentWouldBeDeleted).

## §B. 미러 현황 맵

**발명 게이트 인벤토리** — firing-half가 **공유 2파일에 집중**(키워드별 파일 분리 불가 → wave3 내부 병렬 금지):
- `Headless/Runtime/DeletionReplacementGate.cs`(835줄): 12종 metadata 키(:23-57) + TryEvade(:66)/TryBarrierAsync(:93)/FindDecoyRedirect(:140)/TryFortitudeReplayAsync(:195)/ApplyFragmentAsync(:300)/TryAscensionAsync(:365)/FindScapegoatSacrifice(:411)/TryDecodePlaySourceAsync(:629)/TryPartitionPlaySourceAsync(:648)/TrySaveAsync(:733)
- `Headless/Runtime/DeletionReplacementTiming.cs`(1121줄): PRE/POST options(:62/152/269)·RequestChoice(:370)/ResolveChoice(:612)·Apply* body. `ChoiceType.DeletionReplacement`로 surfacing.
- 비-발화 기능 혼입: 원인 술어·후보 조건(FindDecodeSourceCandidates, Partition 색/레벨)·sink 스냅샷 키·재귀 sacrifice(ApplySacrificeAsync/SettleAwaitingSacrifices) — 은퇴 시 AS-IS 창 경로가 대체 공급해야 함.
- 발화 인식 = `HasReplacementKeyword`(Gate.cs:497): metadata flag OR ContinuousKeywordGate(registry/ambient) OR DecoyAcceptsSubject(:473) — 인쇄 ActivateClass를 **창 해소 없이 presence 스캔만**.

**PRE 컷인 창 liveness(실측)**:
- 미러에 AS-IS `Destroy()` 충실 1:1 포트 존재(`Assets/…/CardController.cs:3431-3476`): PRE ①②(:3454-3469)+POST(:3489-3509) 전부 개방. 단 직접 호출자 2장뿐(BT9_081·BT1_084).
- **보편 효과-삭제 = invented sink** `MatchStateMutationSink.ExecuteStagedDeleteAsync`(:1085): OnDestroyedAnyone/OnLeaveFieldAnyone은 live 개방(:1164-1169), **PRE 컷인 창은 미개방** — `DeletionReplacementTiming.HasPreOption`으로 defer(pendingDeletion, :1109-1119/:1179-1211). 전 미러 `StackSkillInfos(WhenPermanentWouldBeDeleted)` 호출=faithful Destroy() 1곳뿐(grep).
- 창 수집 substrate 준비됨: `GetSkillInfos`(:932-1050)=AS-IS 1:1 EffectList 직독(**ToBinding 불필요**), W3 버킷 live.

**C-Act "ToBinding 없음" 갭 재검 = 부분 무효화**: 창 수집은 EffectList 직독이므로 faithful Destroy 경유면 인쇄 ActivateClass 정상 수집. 실질 갭 3건: (a) **sink가 PRE 컷인 창 미개방**(핵심 blocker), (b) MaterialSave `IsContainDigiXrosCondition` 미포팅(RD-P6C2-4, factory throw), (c) **GainDecode 미포팅**(AS-IS 존재·미러 부재). AS-IS Gain 존재=Evade/Decode/Fortitude 3종, 미러=GainEvade·GainFortitude만.

**이중발화 표본 검증**: **PRE 9종=이중발화 없음**(창이 안 열리므로 게이트만 발화) → 위험=**발화 공백**. **POST 3종(Fortitude/Save/Ascension)=이중발화 위험군** — 인쇄 등록 timing=OnDestroyedAnyone(실측)이고 sink가 그 창을 이미 live로 열므로, 인쇄 ActivateClass를 실포팅하는 순간 Raid/Alliance/Vortex 동형 이중발화(현재 미러 소비 0=latent).

**presence 감사**: 미러에 AS-IS 충실 presence getter(Permanent.HasEvade:1257·HasBarrier:1315, EffectList 스캔)와 발명 ContinuousKeywordGate **두 기제 병존**. firing-half 은퇴는 presence 불건드림 → 소비자 안전.

## §C. 미조사 3종 판정 — 전부 클러스터 편입

- **Barrier**: PRE, 배틀-전용 생존 치환(Evade 동형, security top 트래시=cost). Factory/Barrier.cs:53-55(IsByBattle). 미러=Gate.TryBarrierAsync.
- **ArmorPurge**: PRE, 삭제 취소(top만 트래시·under-source 승격, Fragment 인접). Factory/ArmorPurge.cs:21. 미러=DeDigivolveHelpers.ArmorPurgeTopAsync+PRE option.
- **Ascension**: POST 재생(Save/Fortitude 동형, 트래시→security), `isOptional=false` quirk(Commons/Ascension.cs:12). 미러=Gate.TryAscensionAsync. **POST 이중발화 위험군 포함.**

→ C-Del 실규모 = **12종**(PRE 9: Evade·Barrier·ArmorPurge·Decoy·Scapegoat·Decode·Partition·Fragment·MaterialSave / POST 3: Fortitude·Save·Ascension).

## §D. 교차 원장 동승 판정

| 항목 | 판정 | 근거 |
|---|---|---|
| RD-C2-DEFERRED-DELETE-BATCH(GameFlowProcessor.cs:324) | **3b에 태움(필수)** | deferred-finalize per-card 경로 자체가 은퇴 대상(PRE transport가 대체) — 분리=이중작업 |
| RD-C2-SECCHECK-INTERACTIVE-ORDERING(SecurityResolver.cs:173) | **분리** | 시큐리티 도메인(C-Btl/security 소관), C-Del 파일과 서로소 |
| A2-P1-1(MetadataActionProcessor.cs:976 주석) | **주석 정정만 동승 가능, 실작업 분리** | C-EoT 도메인 — wave3에 태우면 스코프 오염 |
| sink leave-경로(bounce/link/security-put pre-move 창) | **분리(별도 골)** | 삭제 아닌 leave 경로, 소비 키워드 상이 — 3b substrate 완성 후 재사용 가능 |

## §E. 리스크·검증 표면

- **행동위험 비대칭**: PRE=발화-공백(substrate 미비 시 카드 즉사) ↔ POST=이중발화. witness도 이질: PRE="생존/개서 실제 발생", POST="정확히 1회".
- 난도 최상: Fortitude(GainFortitude EvadeEffect quirk verbatim 필수), Scapegoat/Decoy(cross-card sacrifice=타 permanent PRE 창 재귀), Partition(2-source 색군 반복 pick).
- C군 교훈: uncapped 픽스처·수집-시점 토폴로지 AS-IS 재도출(collect-before-removal=SkillInfo heap 참조로 트래시 후 해소)·배치 원자성(리스트 전체 한 Destroy=1회 stack).
- **최대 회귀-민감 seam**: `willBeRemoveField`(faithful Destroy) ↔ `pendingDeletion`+`ClearDeletion`(invented sink) 이중 상태모델 화해(3b).
- 회귀 게이트 스위트: PRIM-P0.WouldBeDeletedWindow·C1-DecodePartitionPre·G3.5-C46/C57/F68/F68D/C4D/C13/C14/C5/D2·RD5-ScapegoatGuards·G9-059/058/055/069/074·R2-DeletionPipeline·RD4. 미러 ported 소비: Evade/Decode/Fragment/Scapegoat 각1·Partition 2·나머지 0 → witness=Tfx+BT13_023/BT19_024/BT16_025.

## §F. 배치 분할 (확정)

공유 3파일(Gate/Timing/Sink)+GameFlowProcessor에 firing-half 집중 → **wave3 내부 순차, 병렬 금지**.

1. **3a — POST 클러스터(Fortitude·Save·Ascension)**: substrate 신설 불필요(OnDestroyedAnyone live). 인쇄 ActivateClass 실포팅+게이트 POST-half 은퇴 **동시**(이중발화 차단). GainFortitude quirk verbatim.
2. **3b — PRE 컷인 창 transport substrate**: sink 보편 삭제 경로에 PRE 컷인 창 개방(faithful Destroy 라우팅 or 컷인 삽입+상태모델 화해). RD-C2-DEFERRED-DELETE-BATCH 해소 동승. 부수 선행: GainDecode 포팅·RD-P6C2-4(IsContainDigiXrosCondition). **최대·최고위험 — substrate 완결 후에만 3c.**
3. **3c — PRE 9종 발화-half 은퇴**: 3b 위에서 인쇄/부여 rewire+Gate/Timing PRE 은퇴+cross-card sacrifice 창-경유 재하우징. 내부 키워드-순차 커밋.

G-clean(funnel wrapper 삭제)=wave4 유지.
