# 엔진 동결 판단 증거 패키지 — 2026-07-23 (HEAD 75a32ad3)

**작성 목적**: 사용자 동결 선언 판단용. 코디네이터는 검증까지만 수행 — 동결 선언·풀 퍼징 실행은 사용자 결정.

## 1. 계기판 (실측)
- **발명물 grep(비-주석)**: EffectRegistry/ToBinding/EffectBinding-type/IActivatedCardEffect/LegacyBindingBridge = **0**
- **live NotSupportedException**: **4좌석 전수 원장-매핑**(REPAIR 배치 재검증, 2026-07-23 — final-polish 배치 후 BlastDNA STOP PORTED·잔여 3건 은퇴로 7→4 수렴; §2 UPDATE 라인과 동기화) — `CardController.cs:4242`(리뷰3 P2-② 코퍼스 중복-키 방어가드)·`GManager.cs:198`(RD-W4-3, 브릿지 W4 미지원 컴포넌트 타입)·`TrashLinkedCards.cs:72`(RD-SKEL-01, AS-IS 비대칭 루프 불성립)·`Permanent.cs:4549`(MIG4-DETACH-LIVE-TOP, 직접-라이브-탑 가드). 적대리뷰 근거 강도: 3건 solid(CardController/TrashLinkedCards/Permanent — 도달 불가 아키텍처 근거) + `GManager.cs:198`=contingent(새 컴포넌트 타입 요청 시 좌석化 가능한 "미포팅" 가드일 뿐, 심층 불가능 근거 아님).
- **커버리지 재감사(도구 수정판)**: ported 339·clean 338·greedy 0라운드·카드-backed 미커버 0 (AD1_025 표기=도구 하드코딩 팬텀 확증)

## 2. 게이트
- **전체 스위트**: (당시 스냅샷 395 green / 33 fail — stale-핀 7 + documented-latent 26). **UPDATE(final-polish 배치 2026-07-23, HEAD 9d32fa97): 425 green / 0 fail — 33 red 전량 해소**(stale-7 재하우징/은퇴·latent-26 L1~L4 인프라 착지 재구동); red_ledger_2026-07-23.md 재작성=CLEARED 참조.
- **확장 다이제스트**: 10시드 전부 R7-시점 기준값과 bit-identical (오늘 인프라 6골+카드 33장 공사 전체 궤적 무변) — 기준값: 1000=9F1DA795…, 1001=143A5B0C…, 1002=3D5F41C5…, 1003=AF6B8888…, 1004=AE4FFF68…, 1005=43A78823…, 1006=27A0E592…, 1007=19E27009…, 1008=59166510…, 1009=7DC59ACF…
- **behavior-mode 다이제스트(인코딩-독립 material)**: `RLB2-01.EngineProfile.Tests.dll behavior 5` 5시드 기준값 (encoding-independent — 상태-material만; final-polish 배치 2026-07-23 후 실측):
  - `seed=1000 steps=86 digest=EA4168196FDA6DCF4C2C9EEE5E66AFAC7CD431563C69857880893E3359979988`
  - `seed=1001 steps=51 digest=CBA289CA825A29E7147EE77487A976906C4DB71BEEA157DB188B8C1D4472AD28`
  - `seed=1002 steps=86 digest=BFC0E62EC3EC1EBF825B95DE4433664806F247FE27BB70B42734A592D237F337`
  - `seed=1003 steps=65 digest=629DA5CE549F48D6DDA5E854D19E9F3099061775842CB5F18BE6DC75EA9924BC`
  - `seed=1004 steps=58 digest=208E54EE43B09E0F771EC27A2EDDA9C713CEB4A0845AB0B5724F1FD9A5E2920E`
- **동결-게이트 적대리뷰(f1d1c835..75a32ad3) GO**: P0 0·엔진-결함 P1 0·**동결-차단 목록 공집합**

## 3. 오늘 아크 요약 (f619911b 이후)
인프라 6골 전량 소진(frame-WRITE·Digi-Burst·recovery·DNA-temp·cost-코너·G-Link 마감·효과-공격 pausable화)+write-표면 2종·카드 33장 포팅(witness 12+링크 7+롱테일 20+STOP-완성 2, 미러 카드 317→339)·실카드 기능 소생 다수(unsuspend/jogress collapse/보안 finisher/Blitz-hook/강제-배틀).

## 4. 열린 목록 (동결 판단 참고 — 차단 아님)
- **witness-adequacy P1 3건 — RESOLVED(final-polish 배치 2026-07-23)**: GAP2 타이밍-부정 4건 추가(board-absent·wrong-turn·no-move-window)+OnStartTurn 창-게이트 양성 / BT7_087 W3 read-측 단언 추가(AutoProcessing.RuleProcess 실구동, flag suppress→미청소·restore→청소) / 임계값 초과-부정 3스위트(T4 5-cost survivor·T6 6000-DP survivor·EX8_072 over-level survivor).
- **P2 정리 4건 — 3건 RESOLVED(final-polish 2026-07-23)**: BlastDNA STOP=PORTED(잔여 블로커 전 폐쇄, latent smoke) / jogress debt=RD-JOGRESS-P2로 통합·4사이트 태깅 / EX6_072 인자 스왑 수정(activateClass.EffectSourceCard). (analyze.py 하드코딩=본 배치 범위 외, 미해소.)
- **red 33** = 열린 엔지니어링 프론티어(전수 원장-귀속, red_ledger 참조)
- **latent 원장**(호출자-0): RD-3A-02·MIG4-DETACH·RD-SKEL-01(AS-IS-한계)·RD-SW-E-01/02(PRE 컷인)·R2-P2-2
- **미실행**: 풀 퍼징 10,044판(--workers 6) — 사용자 지시로 보류 (직전 부분-런 1,000판 수확 0)

## 5. 동결 계약 조항 초안 (발효=사용자 서명 시)
- **코어 동결**: Assets/Scripts 미러+Headless substrate — 수정은 수리-예외 경로만(신규 witness가 적발한 결함 or 원장 항목 해소, 적대리뷰+다이제스트 재검증 동반)
- **additive-only**: 신규 작업=카드 포팅+witness 스위트만; 신규 룰층 프리미티브/게이트 금지(계기판 grep=CI 가드화 가능)
- **기준값**: 본 문서 §2의 다이제스트 10시드 + red_ledger 33 + STOP 4좌석 목록(§1)

---

## §6. 수리 아크 최종 상태 (HEAD 42cf54ff)

- **red_ledger 33 전량 소진**: stale-7=행동 단언 13건 재귀속 후 은퇴 · latent-26=L1~L4 4파 전량 수리(실 엔진 수정 다수: 브로드캐스트 창 6종 정본 개설·보안 face-게이트·DpModifier fold·ambient 스코프 3좌석·DigiXros 발견·DigiEgg 표기 등)
- **latent-STOP 소진**: EX8_059(RD-3A-02 은퇴)·BT7_058(도달-불가 실증)·BlastDNA 포팅·사문 오버로드 삭제 → **live STOP = 영구-정당 4좌석**(방어 2[GManager=조건부]·AS-IS-불성립 1·direct-live-top 가드 1)
- **수리-아크 적대리뷰 GO**(75a32ad3..412bad09): 가짜-green 12스위트 표본 전수 CLEAN(다수는 오히려 강화)·P1 3건 즉시 상환 완료(42cf54ff: face-게이트 4장+witness·지뢰 컨버터 3케이스 제거·문서 동기화)
- **게이트**: 폴리시 시점 전체 스위트 **425/425 green** + 상환 후 인접 스위트 전부 green + 다이제스트 trio 불변. 상환-후 전체 재인증 런은 외부 정지로 미완(잔여 변경=카드 4장 게이트+컨버터 삭제·인접 green·다이제스트 불변 — 재인증 필요 시 1회 실행)
- **미결(사용자 결정)**: ①동결 선언 ②풀 퍼징 10,044판(--workers 6) ③상환-후 전체 스위트 재인증 여부
