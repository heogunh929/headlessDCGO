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
- ~~**미실행**: 풀 퍼징 10,044판~~ → **§8로 종결**: 사용자 결정(2026-07-23)으로 풀 퍼징 폐기, 500판 strict 퍼징 PASS로 갈음

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
- **미결(사용자 결정)**: ①동결 선언 ②풀 퍼징(→§8에서 종결: 500판 PASS 갈음) ③상환-후 전체 스위트 재인증 여부

## §7. 완전-상환 아크 종결 (HEAD c0a76bbe)

- **상환-사냥 1차**: 실행-가능 8건 → 전량 상환 (LevelOf 결함·스테일-STOP 4장·RD-J-01 발명 가드 10좌석·PRE 컷인 2창 신설+R2-P2-2 기-live 실증·Level_Assembly fold+EX9_074·BT19_089+stand-in flag)
- **재사냥 검증**: 8/8 REPAID-VERIFIED(단언 약화 0·1:1 스팟-diff) + 신규 3건(스테일 헤더·A8G1 전제-스테일 판명·LevelOf 소비자 핀 3장) 즉시 소진
- **발견 곡선**: 33 → 8 → 3(XS/S·엔진결함 0) → 0 수렴. 재발 방지 규약: 수리 완료-정의=원장 전 참조 동기화·후속-노트 즉시 큐잉
- **STOP 재계수**: live 3(GManager:198·CardController:4283·Permanent:4549) + dead-가드 1(TrashLinkedCards:72) — 전부 영구-정당
- **게이트**: 배치별 witness+인접 green·다이제스트 full+behavior 전 배치 bit-identical. 전체-스위트 최종 인증 런은 중단 2회로 미완주(마지막 완주=폴리시 시점 425/425, 이후 변경 전부 개별 게이트 green) — 재인증 1회는 사용자 옵션
- **미결(사용자 결정 3)**: ①전체 스위트 재인증 런 ②풀 퍼징 10,044판(--workers 6) ③동결 선언 — ②는 §8에서 종결

## §8. 퍼징 게이트 종결 (2026-07-23, 사용자 결정)

- **CPU 스윗스팟 실측** (100판씩 동일 조건): w4=17.2 / **w6=24.0** / w8=24.8 steps/s → 물리 6코어 초과 이득 +3%뿐, **6워커 확정** (`runs/sweetspot-w4|w6|w8/`)
- **500판 strict 퍼징 PASS** (`runs/fuzz-500/`): 9덱×81 순서쌍·workers 6·maxSteps 2000 — **결함 0**(throw/stop/deadlock/cap/hang/driver 전 0)·**재검 50판 다이제스트 드리프트 0**·23,718스텝·24.9 steps/s·좌석 편향 없음(P1 패 259/P2 패 241)
- **사용자 결정**: 풀 퍼징 10,044판은 실행하지 않음 — 본 500판 런을 퍼징 게이트 PASS로 확정
- 참고: 07-18 B4(64.8 steps/s) 대비 스텝당 비용 ~2.6배 — fidelity 공사(live 스캔·창 개설)의 구조적 비용, 동결 후 substrate 최적화 트랙 후보

## §9. 소프트 동결 발효 (2026-07-23, 사용자 선언)

**"소프트 동결로 엔진부는 1차 마무리"** — §5 초안을 소프트 등급으로 발효:

- **대상**: Assets/Scripts 미러 + Headless substrate (엔진 코어 전체)
- **소프트 규약**: 코어 수정=수리-예외 경로만(신규 witness가 적발한 결함, 원장 항목 해소 — 적대리뷰+다이제스트 재검증 동반). additive-only 작업(카드 포팅·witness 스위트·어댑터/RL 측)은 자유. 하드-락 아님: 정당 사유+게이트 통과 시 코어 수리 허용
- **기준값(베이스라인)**: §2 확장 다이제스트 10시드 + behavior 5시드 · live STOP 4좌석(§7 재계수: live 3+dead 1) · 발명물 grep 0 · 전체 스위트 425/425(폴리시 시점) · 500판 strict 퍼징 PASS(§8)
- **미채택**: 상환-후 전체 스위트 재인증 런(사용자 결정으로 미실행 — 이후 필요 시 1회 실행 가능, 잔여 변경분은 개별 게이트 green)
- **다음 단계**: ⑦ 대량 포팅(Haiku 파일럿 재실측 선행 — ⑥ 정본 패스·4b OLD 삭제는 07-18/19 기완료) 및 substrate 성능 최적화 후보(§8 성능 노트)는 동결과 독립 진행
