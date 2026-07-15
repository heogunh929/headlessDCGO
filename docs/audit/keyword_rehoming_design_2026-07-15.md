# 키워드 창-재하우징 설계 (R2 승계 5클러스터, 2026-07-15)

전제=창엔진 SkillInfo 이관 완료(15커밋, `window_skillinfo_cutover_design_2026-07-14.md`). 진실표=`keyword_firing_truth_table_2026-07-15.md`(AS-IS 발화 기제·file:line 전수). Fable5=설계·검수, 구현=서브에이전트 배치.

## 0. 골 정의
**대상**: 창-발화 18키워드의 발화를 발명 게이트 5클러스터에서 **AS-IS 창 경로**(인쇄=카드 CardEffects의 factory ActivateClass / 부여=Commons GainK→AddEffectToPermanent 버킷→GetSkillInfos 수집)로 재하우징하고 게이트의 발화-half를 해체.
**비-스코프**: 충실 7종(Rush/Iceclad/Blocker/Jamming/Collision/Reboot/MindLink)+Progress 연속-half — AS-IS 자체가 전용 읽기. battle/security 루틴 본체 재하우징.
**계기판**: 발명 게이트 발화-half 참조 0(클러스터별)·`GainKeywordToPermanent` 깔때기의 18키워드 wrapper 참조 0·KeywordBaseBatch1/2 창-관련 kind 참조 0·registry continuous 마커 등록(18키워드 grant분) 0.

## 1. 클러스터 공통 레시피 (배치당 키워드 1~8종)
키워드 K마다 원자적으로:
1. **인쇄 경로**: 미러 `CardEffectFactory/KeyWordEffects/K.cs`를 AS-IS 동명 파일 1:1로 확인/완성(ActivateClass — CanActivateK/KProcess는 Commons partial), 소비 카드의 CardEffects 반환 확인(대표 1~2장 AS-IS 대조).
2. **부여 경로**: 미러 `CardEffectCommons/KeyWordEffects/K.cs`의 `GainK`를 AS-IS 1:1(CanNotBeAffected 게이트→`CardEffectFactory.KEffect` ActivateClass→`AddEffectToPermanent(duration, timing)` — W3 버킷 live). 카드 호출부를 발명 wrapper(`CardEffectCommons.GainK(…CardSource)` 3-arg)에서 AS-IS 시그니처로 rewire.
3. **게이트 발화-half 은퇴**: 해당 게이트에서 K의 발화/선택 로직 제거(게이트의 타 기능은 잔존). 단일-발화 검증(창 XOR 게이트).
4. **witness**: 인쇄 1장+부여 1장 최소, 발화가 창 경유(StackedSkillInfos→MultipleSkills)임을 단언. DoneStartGame/ambient 하네스 규약 준수.
5. **깔때기 wrapper는 삭제하지 않음**(dead로 방치) — 공유 파일 CardEffectCommons.cs 충돌 방지, 최종 G-clean 배치가 일괄 삭제.

## 2. 클러스터 정의·순서
| # | 클러스터 | 키워드 | 은퇴 대상 게이트 | 비고 |
|---|---|---|---|---|
| C-EoT | EoT공격 | Vortex·Overclock·Execute | EndOfTurnEffectAttack·EffectDrivenAttack·OverclockEffect | OnEndTurn 창 live. Execute=RD-R2-01 재판정(W3 UntilEndAttackEffects 버킷으로 해소 가능성 — 검증 후 판정). Vortex SelectAttackEffect 경로는 R5-B 기재배선 |
| C-Act | activated 헬퍼 | Training·MaterialSave | DigivolutionStackHelpers의 해당 분기 | 인쇄-전용(Gain 없음=깔때기 무관). MaterialSave=WhenRemoveField 컷인 의존 시 정직 STOP |
| C-Atk | 공격선언 | Raid·Alliance·Blitz | RaidAttackSwitch·AllianceAttackBoost·AttackPermanentAction의 발화-half | OnAllyAttack 창 live(C2r). Blitz=OnPlay/WhenDigivolving 공급층(C1d RDW-04). MIG1-KEYWORD-RELOCATE·카운터 인접(RDW-06은 별도) |
| C-Btl | 전투/시큐리티 | Retaliation·Pierce | BattleResolver의 HasRetaliation/HasPierce 발화 읽기 | Retaliation=live 삭제창(OnDestroyedAnyone) 경유. Pierce=OnDetermineDoSecurityCheck 창을 배틀 판정점에 transport 개방(결정#4 패턴) |
| C-Del | 삭제-치환 | Evade·Decode·Decoy·Fragment·Partition·Fortitude·Scapegoat·Save | DeletionReplacementGate/Timing의 해당 8종 half | 최대·최고위험(PRE 컷인 창=WhenPermanentWouldBeDeleted/WhenRemoveField 스택-사이드 배선 동반, C~D군 witness 표면) — 마지막, 자체 사전조사 필수 |
| G-clean | 깔때기 청소 | — | GainKeywordToPermanent+wrapper 18종+KeywordBaseBatch 창-kind | 전 클러스터 후, 참조 0 grep 일괄 |

**병렬 규율**: 깔때기 분리로 클러스터는 파일-서로소 — 단 동일 카드 파일을 두 클러스터가 만질 가능성(다중 키워드 카드)은 각 배치가 편집 카드 목록을 보고, 코디네이터가 충돌 시 순차화. wave1=C-EoT∥C-Act∥하네스-triage(테스트 전용), wave2=C-Atk∥C-Btl, wave3=C-Del, wave4=G-clean+적대리뷰.

## 3. 병행 배치: 하네스 triage (이월 원장)
unwired-timing red 4종(F1-Tier2-WhenLinked/OnAddDigivolutionCards/OnAttackTargetChanged/OnBlockAnyone)·NewTimingsFire 잔여·RD11·G11-004 — 원인 분류(하네스 phase/ambient 클래스=수정, 엔진 갭=보고만). 단언 약화 금지·기대값 AS-IS 유도·엔진 파일 수정 금지.

## 4. 리스크
- grant→버킷 전환과 게이트 은퇴의 **키워드-단위 원자성**(어긋나면 발화 공백/이중발화) — 레시피 3의 단일-발화 검증 필수.
- 부여 효과의 발화 시점 충실: collect-before-removal(Retaliation류)·EoT 버킷 리셋 순서(발화 후 리셋 — AS-IS TurnStateMachine 순서 대조).
- ContinuousKeywordGate.HasKeyword union을 읽는 **비-발화**(presence) 소비자가 게이트 은퇴로 깨지지 않는지 클러스터별 감사.

## 5. 배치 판정 기록
- **W-EoTFIX = RD-CEoT-01 오진 판정(2026-07-15, 엔진 무수정)**: EoT 드레인은 전 스코프 해소(witness 4/4: permanent-sync·permanent-interactive suspend/resume·player·supply 전경로). C-EoT 관측의 실체=**RD-CEoT-WIRING**(live 키워드가 창에 미배선 — 카드 CardEffects가 ActivateClass 미반환·Gain 미사용; 발화는 게이트+continuous 마커뿐). → C-EoT-2 재시도 가능: 레시피대로 배선하면 창이 해소함이 실증됨. 부수: CEntity_EffectController:88 null-TopCard NRE(RD6/GR-001/GR-006 full-match 계열)=미러/하네스 셋업 아티팩트, AS-IS 무가드 — 가드 발명 금지, 원인 셋업 추적 필요(W-GAPS 편입).
- **triage 엔진-갭 5건(W-GAPS 배치 대상)**: WhenLinked/OnAddDigi CardEffect=null vs 게이트(재배치=Permanent.AddLinkCard/AddDigivolutionCards 정위치)·OnEndBattle 창 미개방·bounce RDW-01·OnDigivolutionCardDiscarded·DcgoMatch.GetLegalActions ambient 자기-스코프.
- **C-EoT = 정직 STOP, 무수정(2026-07-15) — 엔진 갭 실증 적발**: **RD-CEoT-01** = EoT 창이 permanent-스코프 activated ActivateClass를 수집(GetSkillInfos OK)하되 **해소하지 않음**(gate-off 계측: VortexProcess 진입 0회·pending choice 없음; mandatory로도 동일; player-스코프는 RD6로 정상 해소 실증 — 갭은 permanent-스코프 특이). 관련 이연 항목=MetadataActionProcessor.cs:998-1031 A2-P1-1. 게이트(EndOfTurnEffectAttack/EffectDrivenAttack)는 유일 발화라 은퇴 불가 → **W-EoTFIX 엔진 배치 선행** 후 C-EoT 재시도. 부수 판정: Vortex/Overclock의 factory·Process는 이미 AS-IS 1:1, AS-IS GainX 시그니처만 부재·grant 호출 카드 0장(rewire 대상 없음). **Execute RD-R2-01 재판정**: W3 버킷으로 원-blocker 해소됐으나 신규 blocker = PermanentEffectFactory.DeleteSelfEffect/AddDetailClass가 발명 binding-rule 모델(AS-IS ActivateClass 오버로드 부재) → STOP 유지, PermanentEffectFactory AS-IS화가 선행조건. Latent 보고: 비-Main phase 합법행동 생성 시 CEntity_EffectController:88 NRE(ambient 부재) — RD6 NRE와 동일 계열.
- **C-Act = 정직 이중-STOP, 무수정(2026-07-15)**: ①**Training** — 호출 표면(OnDeclaration→resolver→ActivateClass)은 존재하나 AS-IS 본문의 face-down place-under(`AddDigivolutionCardsBottom(isFacedown:true)`)가 미러에서 throw(**MIG4-ADDDIGI-FACEDOWN**, Permanent.cs:3633) — 충실 재하우징이 본문 중간 throw가 됨. → **P-FD 프리미티브 배치**(Permanent.cs face-down 소스 기입, AS-IS 1:1) 선행 후 wave2에 재편입. ②**MaterialSave** — `WhenPermanentWouldBeDeleted` 스택형 창 부재(PRE-삭제는 발명 DeletionReplacementGate+registry-binding 경로가 담당, 신모델 ActivateClass는 ToBinding 없음) + RD-P6C2-4(IsContainDigiXrosCondition 미포팅) → **C-Del로 이관**(설계 §2 예측과 일치). 두 키워드 모두 live 소비 카드 0·발명 wrapper도 dead 확인 — 현재 발화 경로 자체가 죽어 있어 행동 공백 없음.
