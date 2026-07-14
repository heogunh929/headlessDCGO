# AS-IS 키워드 발화 진실표 (2026-07-15, F2r 재감사)

배경: 창엔진 SkillInfo 이관(design=`window_skillinfo_cutover_design_2026-07-14.md`) 중 F2 배치가 TO-BE 배선만 보고 "키워드 창-비경유"로 오판(사용자 지적으로 철회) → AS-IS 기준 전수 재감사 결과. 코디네이터가 GainRetaliation/GainVortex 표본을 AS-IS 원문으로 직접 검증함.

## 핵심 메커니즘 (AS-IS)
- 창 수집 `AutoProcessing.GetSkillInfos`(:770-885) = 5존 스캔(player효과/필드 permanent/트래시/핸드/앞면시큐리티), `ActivateICardEffect`+`CanTrigger`만 SkillInfo화.
- 부여 저장 `AddEffectToPermanent`(GiveEffectToPermanentOrPlayer.cs:11-51) = Permanent duration 버킷 5종(UntilOwnerTurnEnd/UntilOpponentTurnEnd/UntilEachTurnEnd/UntilEndAttack/UntilNextUntap)에 `Func<EffectTiming,ICardEffect>` 저장; `Permanent.EffectList(timing)`이 `EffectList_Added`(Permanent.cs:1380-1492)로 병합. `AddEffectToPlayer`(:57-89)는 Player 버킷.
- **부여 효과는 permanent-in-play 스캔으로만 가시**(CardSource.EffectList는 인쇄 효과만 반환) — 트래시/핸드 스캔은 인쇄 효과만 줍는다.
- **Retaliation "트래시에서 발화" 미스터리 해명**: DestroyPermanentsClass.Destroy가 `StackSkillInfos(OnDestroyedAnyone)`을 **제거 전**(CardController.cs:3736) 수집 → SkillInfo가 heap 참조 보존 → 해소 시점(카드는 이미 트래시)에 `CanActivateRetaliation`의 IsExistOnTrash 게이트 통과. = **collect-before-removal + SkillInfo 지속 + CanActivate 트래시-게이트** 패턴(Save/Fortitude/Decoy/Fragment 동일). 컷오버 설계의 수집-시점 충실이 중요한 이유.

## 판정 요약
- **창-발화 → 재하우징 필요 18종**: Retaliation(OnDestroyedAnyone)·Vortex/Overclock/Execute(OnEndTurn)·Save/MaterialSave(OnDestroyedAnyone/WhenRemoveField)·Fortitude·Scapegoat(WhenPermanentWouldBeDeleted)·Alliance/Raid(OnAllyAttack)·Blitz(OnEnterFieldAnyone/OnPlay·WhenDigivolving)·Training(activated)·Pierce(OnDetermineDoSecurityCheck)·Evade·Decode·Decoy·Fragment·Partition(WhenPermanentWouldBeDeleted/WhenRemoveField) — AS-IS는 전부 ActivateClass가 GetSkillInfos/컷인 창으로 발화, 포트는 전용 게이트로 우회:
  - 삭제-치환 클러스터(DeletionReplacementGate/Timing): Evade·Decode·Decoy·Fragment·Partition·Fortitude·Scapegoat·Save
  - EoT 공격 클러스터(EndOfTurnEffectAttack/EffectDrivenAttack): Vortex·Overclock·Execute(RD-R2-01 결합)
  - 전투/시큐리티 플래그(BattleResolver): Retaliation·Pierce
  - 공격선언(RaidAttackSwitch/AllianceAttackBoost/AttackPermanentAction): Raid·Alliance·Blitz
  - activated 헬퍼(DigivolutionStackHelpers): Training·MaterialSave
- **AS-IS 자체가 전용 읽기 = 포트 충실 7종**: Rush·Iceclad·Blocker·Jamming·Collision·Reboot(EffectList(None)/OnCounterTiming is-인터페이스 스캔)·MindLink(카드 스크립트 인라인). **Progress 혼합**(연속 면역 half 충실, ProgressProcess half=RD-R2-01 STOP).
- 미조사: ArmorPurge·Ascension·Barrier(삭제-치환/연속 동일 패턴 추정).

세부 file:line 표는 F2r 에이전트 보고 원문(세션 기록) 및 본 문서 이력 참조. 표본 검증: GainRetaliation=Commons/Retaliation.cs:158-160, GainVortex=Commons/Vortex.cs:96-108.

## 컷오버 봉쇄 지점(저장층)
1. **P6A-PERMANENT-EFFECTLIST-ADDED**: 미러 `Permanent.EffectList_Added`(Permanent.cs:1818-1822)=빈 리스트 — permanent 부여 버킷 부재.
2. **RD-P6C3-C1**: 미러 `AddEffectToPermanent`(CardEffectCommons.cs:2853-2886)=registry lowering, 신모델 ActivateClass엔 NotSupportedException.
3. R6P-EOT-PLAYER-EFFECTLIST: Player 버킷은 존재하나 창이 미열거(C 배치 F3 동승으로 해소 예정).

## 스코프 판정
- **이 골(창엔진 이관)에 편입**: 저장층 1·2 해소 = **W3 배치**(Permanent duration 버킷 5종 + EffectList_Added 1:1 + AddEffectToPermanent AS-IS 버킷 저장 전환) — 창의 수집 실행가능성 자체이므로 창 골 스코프.
- **후속 region-골(R2 승계)**: 키워드 18종의 Gain*/인쇄 팩토리를 창-발화 ActivateClass로 재하우징 + 전용 게이트 해체(클러스터 단위 5골 추정: 삭제치환/EoT공격/전투플래그/공격선언/activated헬퍼). 컷오버 자체는 이들을 깨지 않음(게이트 경유 발화 유지) — 구조 발산으로 명기.
