# AS-IS ↔ TO-BE 미러 5파일 로직 정합 대조 — 2026-07-24

opus 5기, 각 파일 AS-IS(DCGO/Assets/Scripts/Script) ↔ TO-BE(src/HeadlessDCGO.Engine/Assets/Scripts/Script) 전수 심볼 대조.
렌즈 없이(판정 카테고리·무죄 출구 미주입) "논리 동일한가/어디서 갈라지는가"만 질의. 상세=`$JOBTMP/congruence_*.md`.

**총평: 5/5 파일 모두 substrate 번역 외 실질 로직 차이 존재 — 순수 1:1 미러 아님.**

---

## 1. CardSource.cs (AS-IS 4357 → TO-BE 2833줄) — 가장 심각
- 대조 심볼 ~200. **삭제 89**(트레이트/이름 헬퍼 63 포함 — 일부는 예외목록 로직 DoruGreymon/Three Great Angels 제외까지 소실)·**이거 7**·**로직상이 22**.
- 값·판정 실제 갈림:
  - C1~C4 `EqualsCardName/ContainsCardName/EqualsTraits/ContainsTraits`: AS-IS 공백제거+소문자 정규화 → TO-BE OrdinalIgnoreCase. "Sky Dragon"·"Sea Animal" 매칭 실패.
  - C5 `HasCardColor`: DualCardColors union 드롭(=렌즈-없는 감사 A1). 듀얼컬러 색판정 탈락.
  - C6 `GetCostItself`: cost-change fold 누락(Definition.PlayCost만).
  - C7 `Level`: 무레벨 센티넬 1145140 → -1.
  - C8 `HasDP`: 판정원 변경(IsDigimon||DP>0 → dp메타 존재).
  - C9~C20: BaseCardNames 듀얼명 드롭·GetChangedCostItselef AI 조기반환 생략·GetPayingCostWithBaseCost Assembly 투영 상이·GetChangedLinkCost union 추가·EvoCosts 상위집합 매칭 등.

## 2. Permanent.cs (4187 → 4640줄)
- 심볼 ~114. ~90 verbatim·**부재 15(6건 재배치 흔적 없이 삭제)**·**로직차 6**·**헬퍼 위임 8(이 파일만으론 미검증)**.
- 값·판정 실제 갈림:
  - Level 무레벨 센티넬 1145140 드롭(TopCard.Level 그대로 seed).
  - CanMove 만원필드 가드 생략(RD-P6C1-2) — 만원이어도 이동 차단 안 됨.
  - CanMove breeding 분기가 구조적 죽은 코드(둘 다 멤버십으로 접힘).
  - HasDP 첫 줄 null 가드 생략.
- 삭제(엔진 전역 grep 0): DigivolutionOrLinkCards·Names_ForDNA·LibraryBounceEffect·HandBounceEffect·OldIsSuspended·IsAddedAsSourceByAppFusion.
- 위임(미검증): LinkedMax fold 순서·DiscardEvoRoots evoRoots/linkRoots 구분·AddLinkCard·AddDigivolutionCards* → LinkHelpers/DpBoostHelpers 등.

## 3. AutoProcessing.cs (1106 → 1618줄)
- AS-IS 44심볼 전수. substrate 번역 외 **차이 15**.
- 실행 경로·기제 갈림:
  - D9 DigimonLackDPProcess: 단일배치 DestroyPermanentsClass({"DPZero"}) → GameFlowProcessor.StateBasedDeletionSweepAsync 전량 위임. **TO-BE 주석이 미완 design item R2-P2-4 자인**.
  - D8 EndGameProcess: 승자 직접지정 → TerminalEvaluator.Evaluate 위임(승패판정 주체 이동).
  - D2~D5 IsNotDigimonInBreeding/IsNotHavingDP/IsDigimonLackDP/IsDigimonLackLinkCondition: AS-IS엔 없는 predicate 가드가 매칭 대상 축소.
  - D7 RuleProcess: 지연삭제 교체창 파킹·LinkMax 파킹·bool 반환(AS-IS void) 재구조화.

## 4. AttackProcess.cs (628 → 1025줄)
- 증가분 대부분 substrate 번역(창/UI/Photon strip·async park phase화). 실질 로직 차이:
  - C1 Attack 재진입 가드 협착(`IsAttacking` → `&& AttackerId!=attackerId`) — 동일공격자 재진입 동작 갈림.
  - C3 방어자 non-digimon 처리: fizzle(조용히 End) → BattleResolver 무조건 호출→검증실패 경로.
  - C4 전투진입 공격자 재검사 소실.
  - D3 카드-호출 EndAttack 창 발화 타이밍/동기성 변경.
  - D8 창 payload live cardEffect 손실(RDW-05).
  - B1/B3 Progress 등록·Execute self-delete 로직이 AttackProcess 스테이지 상주(자기 주석 MIG1-*-RELOCATE design item).
  - D1/D2 tap이 SuspendPermanentsClass.Tap(OnTappedAnyone/CanSuspend 발화) → raw metadata write(RD9-87).

## 5. CardEffectCommons.cs (1448 → 5352줄)
- partial class 한 조각(형제 디렉토리와 공유). TO-BE 메인이 형제 shard 로직 흡수 → 파일 단위 1:1 아님. AS-IS 41심볼 중 40 존재.
- 실질 차이:
  - D1 OptionSecurityEffect 게터 삭제(엔진 전역 0) — 카드가 로컬 재구현(BT18_098).
  - D2/D3 파라미터 드롭(AddThisCardToHand 타입 변경·AddActivateMainOptionSecurityEffect effectDiscription 드롭).
  - D7 DigivolveIntoHandOrTrashCard: AS-IS는 조건부 player cost-effect를 AddEffectToPlayer(UntilCalculateFixedCost)로 등록·해제 → TO-BE 인라인 계산(cost-pipeline 등록 라이프사이클 우회).
- 대다수는 AS-IS-시그니처 브릿지 오버로드로 호출형 보존(순손실 적음). 단 메인파일이 형제 shard를 흡수·오버로드 = same-path 파일미러 위반(AS-IS 기원은 있음).

---

## 종합
- **substrate 번역만 있는 파일 0.** 5개 모두 값/판정/실행경로가 실제로 갈리는 차이 보유.
- 반복 패턴: ①규칙 계산이 헬퍼/substrate로 위임돼 원파일에서 검증 불가(Permanent·AutoProcessing·AttackProcess) ②AS-IS엔 없는 predicate 가드/조건 협착 추가(CardSource C1~C8·AutoProcessing D2~D5·AttackProcess C1) ③심볼·로직 삭제(CardSource 89·Permanent 6 재배치흔적 0) ④design item 자표식으로 미완 상주(R2-P2-4·RDW-05·RD9-87·MIG1-*·RD-P6C1-2·RD-W3-*).
- 다수가 기존 렌즈-없는 감사(A1 DualCardColors·A13 Level 센티넬·D1-a cost필터·D4-f SecurityResolver 위임)와 교차 확인됨.
