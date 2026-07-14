# C-worksheet — 창 SkillInfo 컷오버 타이밍별 준비도·seam·파리티·제거 목록 (2026-07-15)

설계=`window_skillinfo_cutover_design_2026-07-14.md`, 공급층 대응표=`window_supply_correspondence_2026-07-15.md`. C-map 조사(읽기전용) 결과. C1/C2 배치의 실행 사양.

## 총평 — 컷오버 표면 3가족
1. **삭제 2타이밍**: 미러 `DestroyPermanentsClass.Destroy()`가 **이미 `StackSkillInfos` 호출 중**(CardController.cs:3272/3284) — main 인스턴스 미드레인이라 dead. live 발화=삭제 CardMoved 브릿지. 플립=드레인 개시+브릿지 삭제-수집 제거.
2. **R ~13타이밍**(공격 5·discard/security/tap/draw/digiburst/top-trash/link-discard/deck-bottom/faceup-sec·trash→hand): 미러 루틴이 AS-IS 위치에 포팅돼 있고 payload 로컬 in scope. 현행 캐리어=`TriggerEventEmitter.Emit`/zone-파생 — **교체 필수(병존=double-fire)**.
3. **M ~13타이밍**(OnPlay/WhenDigivolving/OnMove/OnUseOption/bounce·deck-bounce·put-security/OnAddDigivolutionCards/WhenLinked/턴경계 3종/library-return): AS-IS 루틴이 Headless 소유(§0 비스코프 클러스터: battle/security/bounce/play/turn 재하우징). **플립 시 A4 transport 유지 + SkillWindowSupply 변환**(payload는 emit 보강으로 byte-동형화) — 위치 발산은 기존 상태 유지·R2/R4 debt로 명기.

P 4건: OnEndAttack(CardEffect 미보존 — plain=null이라 실질 R)·OnCounterTiming(컷인 seam, RDW-06)·OnAddHand(zone-파생과 dedup)·OnDigivolutionCardDiscarded(Headless 헬퍼로 위임된 emit).

## 타이밍별 표 (요지)
- **AttackProcess.cs(미러)**: OnAllyAttack R(:273-278)·OnEndAttack P(:671-676, hook 주의)·OnCounterTiming P(:340-368, 컷인 2-pass)·OnBlockAnyone R(:810-816, BlockTiming.cs:180 emit 교체)·OnAttackTargetChanged R(:837-842, RaidAttackSwitch.cs:146+BlockTiming.cs:186 emit 교체).
- **CardController.cs(미러)**: OnDestroyedAnyone/OnLeaveFieldAnyone(Destroy) R-이미호출(:3272/:3284)·OnDiscardHand R(:96)·OnDiscardSecurity R(:726-736)·OnDiscardLibrary R(:1839-1853)·OnLoseSecurity R(:1443-1453)·OnAddSecurity R(:1491-1493)·OnFaceUpSecurityIncreased R(:1503/:1561)·OnDraw R(:206)·OnUseDigiburst R(:537)·OnTappedAnyone R(:1663-1675)·OnUnTappedAnyone R(:1772-1782)·WhenTopCardTrashed R(:856/:989/:2009)·OnDigivolutionCardReturnToDeckBottom R(:1372)·OnLinkCardDiscarded R(:1253)·OnDigivolutionCardDiscarded P(:1095-1103→DigivolutionStackHelpers:267/301).
- **CardObjectController.cs(미러)**: OnReturnCardsToHandFromTrash R-verbatim(:235)·OnAddHand P(:213).
- **M**: bounce/deck-bounce/link/put-security/breeding-move 계열(sink/ZoneMover 소유)·OnPermamemtReturnedToHand·OnPlay/OnEnterFieldAnyone(PlayCardAction.cs:225+OnPlayReactivation.cs:53)·WhenDigivolving(DigivolveAction)·OnMove(TriggerTimingMap:106)·OnReturnCardsToLibraryFromTrash(:137)·OnAddDigivolutionCards(DigivolutionStackHelpers:560)·WhenLinked(LinkHelpers:167)·OnStartTurn/OnStartMainPhase/OnEndTurn(MetadataActionProcessor:1022/:872/:910)·OnUseOption(RD-P6C1-4 STOP, OptionActivateAction:94).

## seam 7 플립 사양
1. GameFlowProcessor.AutoProcessAsync(:628-657): CollectUnifiedSeed+WindowResolver.DriveAsync → 미러 `autoProcessing.AutoProcessCheck()`(RulesTiming 스택+TriggeredSkillProcess).
2. MetadataActionProcessor WindowChoice(:501-521): WindowResolution 답변+re-drive → SkillWindowContinuation 답변 기록+MultipleSkills resume(A2 키).
3. 링크-트림 F3(:527-567): 동일 continuation re-drive.
4. deferred-activation(:736-745): 동일.
5. BattleResolver.ResolveKnockOutWindowAsync(:432-449): RunSyncWindowAsync → `GetSkillInfos(ht,OnKnockOut)`+PutStackedSkill+`AutoProcessCheck`(AS-IS CardController ~:4600 형).
6. ResolveStartBattleWindowAsync(:454-470)+**:53 registry 게이트 제거**: → `StackSkillInfos(OnStartBattle)`+AutoProcessCheck(AS-IS :4557/:4600).
7. SecurityResolver(:363-380)→RunSecurityCheckWindowAsync(WRW:67-90): → `GetSkillInfos(ht,OnSecurityCheck)`+**IReduceSecurity ref-merge**(AS-IS :3982-3985→:5448). ※5-7의 드라이버(BattleResolver/SecurityResolver 루틴)는 비스코프 — 창 기제만 교체.

## 모집단 파리티 (판정: GetSkillInfos ⊇ bridge, 유리)
- 상속효과: **파리티 ✓**(미러 EffectList_ForCard가 AS-IS :1497-1546과 byte-동일 — inherited :2047·flipped 게이트 :2029·IsDigimon :2035; bridge의 ScanZones+inherited-scan 분해와 일치).
- trash/hand/필드-top: 파리티 ✓.
- **플립이 추가하는 4모집단**(현재 포팅 ≈0, witness 항목): ①브리딩(GetFieldPermanents=battle+breeding vs bridge=BattleArea만) ②앞면 시큐리티(bridge 미스캔) ③링크소스 IsLinkedEffect(C2-01) ④player-scope activated(F3 동승으로 파리티 성립).
- **드랍 0** — 역방향(위험 방향) 불일치 없음.

## who-fires 변경 플래그
- **F1-ENDATTACK-HOOK**: EndAttackTriggerHook이 off-queue로 스케줄러-half 직행(CollectUnifiedSeed는 OnEndAttack skip WRW:302-315) — C2에서 hook 은퇴 필수(잔존 시 double-fire latent).
- 삭제 topology: bridge subject-단독 → AS-IS any-match 2회 StackSkillInfos(미러 :3272/:3284 재현) — firedDeletion/firedLeaveBatch collapse 대체. witness 필요.
- phase-게이트: 신모델 CanTrigger 1차 게이트=DoneStartGame — witness 하네스 phase 전진 필수(§5.5 F4 발견).

## 구경로 제거 목록 (C2)
GameFlowProcessor:628-657 교체·CollectUnifiedSeed(WRW:271-359)·CollectActivatedBridgeTriggers+ScanZones+마커(WRW:663-1111)·Gate/Commit/ResolveBody/Deps(WRW:121-661)·WindowResolver 격리(FilterToMinimumBatch→SkillWindowSupply.SequenceByMinimumBatch)·AgentWindowChoicePort/WindowResolutionController→SkillWindow\*(W1)·AutoProcessingTriggerCollector 창-시드(소비자 WRW:80/127/280·BattleResolver:447/469·EndAttackTriggerHook·SecurityDelayedTriggerHook)·EffectRegistry.GetEffectsForTiming 창-읽기+BattleResolver:53 게이트.
**잔존(R3-W3)**: GetContinuousEffects 소비자(InMemoryEffectQueryService:25·EffectRegistry:81·PlayerScopeContinuousHelpers:78·MatchStateMutationSink:1250/1281).

## C1/C2 분할
- **C1(행동-중립 준비)**: ①R 위치에 main-인스턴스 StackSkillInfos 인서트(캐리어 유지 — main 미드레인이라 inert; 컷인-인스턴스 타이밍은 제외=C2) ②M 타이밍의 emit payload 보강+SkillWindowSupply 변환 확장(byte-동형, 휴면) ③검증=기존 green 유지.
- **C2(단일 플립)**: seam 7 전환+캐리어 제거+구경로 제거+F3(4-arg AddEffectToPlayer 버킷 전환, BT1_021/090·EoTLose3Memory·TriggeredMemory/GainMemory fold)+EndAttackTriggerHook 은퇴+Tfx 8 은퇴+authorized-red 하네스 재조준.
