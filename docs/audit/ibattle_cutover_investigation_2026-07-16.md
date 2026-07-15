# RD-CBTL-01 절단면 사전조사 — IBattle/battle-해시테이블 표현형 이관 (2026-07-16)

Base: `ea083ab9`(wave3 병합 후 main). 조사 전용 배치(무수정). AS-IS grep `--binary-files=text`.

## 핵심 판정
설계 STOP 노트(RD-CBTL-01)의 "IBattle 표현형 이관 = 배틀 루틴 본체 재하우징" 등치는 **분리 가능** — 배틀 본체는 이미 BattleResolver(Headless substrate)로 재하우징 완료(비-스코프 유지)이고, 필요한 것은 **transport 경계의 결과-형 IBattle 페이로드 재구성**뿐. "페이로드 합성 금지"=날조 금지이지 실 결과의 AS-IS 표현형 재구성 금지가 아님(미러 `PierceCheckHashtableOfPermanent`가 동일 구성 기법을 이미 blessed). **골 실질 = BattleResolver substrate ~150-250줄 + 2 seam(배틀 삭제창·시큐리티체크창), 카드 churn 0.**

## §A. AS-IS IBattle 표면
- `IBattle` = class(CardController.cs:4427-4773). ctor(Attacking/Defending Permanent·DefendingCard·IsWithoutAttack)·`hashtable`(public)·CompareStats(:4460, Iceclad→진화원 수, else DP, Clamp(-1,1))·`Battle()`(:4474).
- Battle() 수명: Permanent.battle back-ref(:4505/4510)→OnStartBattle 창(:4557)→승패 산정(:4609-4671, 패자 add 前 `CanBeDestroyedByBattle` 게이트)→**키 6종 탑재(:4694-4700)**→`DestroyPermanentsClass(LoserPermanents, hashtable).Destroy()`(:4705, battle 재-thread=:3672→:3740 → Retaliation 발화)→DestroyedPermanents fix(:4709)→OnEndBattle(:4718)→**OnDetermineDoSecurityCheck GetSkillInfos(:4731-4737 → Pierce 발화)**→AutoProcessCheck(:4746)→UntilEndBattleEffects 리셋(:4750)→battle=null(:4763).
- 키 의미론: `WinnerPermanents`/`LoserPermanents`=스냅샷(name/color/level 동결), `*_real`=live(identity·`IsDestroyedByBattle=true`), `LoserCard`·`WasTie`·`battle`(self). Retaliation/Pierce 게이트=`_real` 판독.
- GetBattleFromHashtable 소비자(비-def 6파일): CardController:3672(Destroy 재-thread)·Retaliation.cs:26,91·WhenDeleteOpponentDigimonByBattle.cs:20(Pierce 하부)·WhenWinBattle.cs·WhenDeleteOpponentDigimon.cs:20·OnDeletion.cs(IsByBattle). 키워드 게이트는 부분집합 — 나머지=전투-파생 효과.

## §B. 미러 현황
- 미러 IBattle=데이터-홀더 shell(:3334-3382, Battle() 부재 — 본체는 BattleResolver 906줄).
- BattleResolver 결정#4 지점: OnDeletion 스택 :285-292(boolean-마커 오버로드, battle=null)·수동 Retaliation 블록=:168-184(문서 ":150-166"은 stale)·수동 HasPierce=:332-334(":277-278" stale, `TriggersPiercingSecurityCheck`로 AttackPipeline threading)·W6 tail :342-355(OnEndBattle을 flat 메타 emit=신-모델 이벤트 표현형). **배틀 경로에 OnDetermineDoSecurityCheck 창 자체가 미개방.**
- `OnDeletionHashtable` 2 오버로드: AS-IS 시그니처(:123, battle!=null이면 "battle" 키 탑재)와 boolean-마커(:184). BattleResolver는 후자 호출.
- **미러 키워드 체인은 전량 1:1·IBattle-aware**(CanActivateRetaliation/RetaliationProcess/CanTriggerPierce/CanActivatePierce/PierceProcess verbatim, Gain* live) — 유일 갭=창이 battle=null로 개방되어 전 체인이 no-op.
- 합성 IBattle: `PierceCheckHashtableOfPermanent`(HashtableSetting.cs:19)=presence 질의용 완전 구성(Permanent.cs:943 소비, P1w/BT1_091). 실발화 경로 아님이나 구성 기법 blessed.
- 신-모델 평행 표현형: `CanTriggerWhenDeleteOpponentDigimonByBattle(ctx,…)`(CardEffectCommons.cs:899)=OnEndBattle emit "winnerIds" 판독으로 발화 중; AS-IS Hashtable 오버로드는 휴면. **2 평행 표현형 병존**(구조부채, 별도 골).

## §C. 절단면 비교 — ① 채택
- **① 페이로드만 이관(채택)**: FinalizeAsync가 이미 보유한 승패 데이터로 실 IBattle 구성→`.hashtable`에 6종 키 탑재→AS-IS 시그니처 오버로드로 삭제창 개방+OnDetermineDoSecurityCheck 창 신설(:4731 미러)+수동 Retaliation/HasPierce 원자 은퇴. 합성 아님(실 결과 재-표현). ~150-250줄+2 seam.
- **② 배틀 본체 재하우징(기각)**: ~800줄+, F68/C-Del transport/D-1/R4 전면 충돌, 비-스코프. 후속 원장.
- **③ 혼합(①+OnEndBattle 통일, 후속 골)**: WhenWinBattle류를 동일 해시테이블로 라우팅, 이벤트경로 은퇴 — 2 평행 표현형 통일. ① 착지 후 별도.
- Pierce 통일: PierceCheckHashtableOfPermanent(가상 presence)와 실 창 입력의 **IBattle 구성 helper 공용화**.

## §D. 해소 시 연쇄
수동블록 2종 은퇴(창 XOR 게이트)·F68R 승격(수동 same-round drag→post-battle 창-발화, 행동 변경=witness 필수)·IsByBattle의 GetBattleFromHashtable 직독 가능·G-clean 추가(HasRetaliationKey/HasPiercingKey/RetaliationFiredKey·continuous 마커)·**재하우징 계기판 18/18 완주**.

## §E. 골 스코프
단일 opus 배치(설계 패스→원자 flip 1컷): ①helper 추출(합성+실 공용, **loser _real 스냅샷 판정** — AS-IS _real은 배틀 시점 동결이나 미러 Permanent는 live-view, Retaliation은 트래시 後 발화라 post-trash 판독 문제=최고 리스크, 설계 패스에서 AS-IS 도출) →②FinalizeAsync IBattle 급전 →③OnDetermineDoSecurityCheck 창 신설 →④수동블록 원자 은퇴(TriggersPiercingSecurityCheck→창 PierceProcess의 DoSecurityCheck 대체 검증) →⑤SecurityResolver 시큐리티-배틀-loss Retaliation 감사.
witness: 인쇄 Retaliation=BT2_074/BT2_080·인쇄 Pierce=BT1_022/ST7_10/EX8_051·부여 Pierce=BT1_091·부여 Retaliation=Tfx(live 0)·F68R·G3.5-D1·단일-발화 probe.
리스크: loser _real 트래시-후 생존(최고)·F68R 순서변경·Pierce 타이밍(창 드레인 vs AttackPipeline follow-up)·시큐리티-배틀-loss transport.
