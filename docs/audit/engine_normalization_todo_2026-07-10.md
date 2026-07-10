# 엔진 정상화 TODO (2026-07-10 기준)

Stage 5 창-루프 컷오버 **전체 완료**(PR#9, 391/391·RuleAudit 0) 시점의 잔여 정상화 항목.
출처: `rule_deficiency_remediation_design_2026-07-09.md`(이연 L1~L8 · P1 레지스터 · VR 재검수) + Stage 5 3b-iii 적대검수 신규 발견.
원칙: [[check-asis-before-implementing]] · [[result-equivalence-not-completion]] · [[adversarial-review-before-cutover]] · [[fidelity-over-coverage]].

---

## ✅ Stage 5로 해소된 항목 (재작업 불요)

| 항목 | 해소 내용 |
|------|-----------|
| **L4 / P1-2** RD-12 수집-소모 실피해 3종 | 소모를 수집→commit(실행 직전)으로 이동(`SchedulerCommit`) |
| **L5** RD-13 트리거 경로 optional | 창 인라인 yes/no(`ConfirmOptionalAsync`→WindowChoice)로 통합 |
| **P1-1** 같은-창 재평가 소실 | `GateLive`가 매 pass 스택 전체 재평가(re-entrant) |
| **VR-1**(부분) fizzle 소모 롤백 | 소모-시점 이동으로 fizzle 前 미소모(commit-recheck fizzle=미소모) |

---

## A. Stage 5 직후 마무리 (창-루프가 열어둔 잔여) — **최우선**

- [ ] **A-1 · P1-4** 컷인 창 same-effect dedup 미러 부재
  - AS-IS `HasExecutedSameEffect` skipCondition(AutoProcessing.cs:623-627; 컷인 창 CardController.cs:727·988·5189·5301·5709). 3b-iii 컷오버에 **미포함** — 같은 효과가 한 창에서 중복 컷인될 수 있음.
  - (부기) AS-IS `IsCutinEffectUsedMaxCount`(:1095-1098) 부호 역전 의심 — 포팅 시 명시 결정.
- [ ] **A-2 · L8 / RD-6** 턴 종료 시퀀스 정합
  - AS-IS는 [End of Turn] 창(:699)이 어택 루프(:705)보다 **先**인데 헤드리스는 역순(TryOpen 먼저). 창-루프 위에서 재정렬.
  - **라이브 버그**: BT1_021(EoTLose3Memory)이 새-턴 프레임에서 오해소 중(테스트 미고정). 드레인-前-플립으로 상환.
  - step4 지속-분기 = TODO-67.
- [ ] **A-3** (신규 latent) `HasEffectsAt` collect-time 비대칭
  - activated 마커는 collect서 1회 필터 → scheduler 半(per-pass 재평가, P1-1)과 비대칭. board-의존 `CardEffects(timing)` 카드에서만 발현(현재 정적 팩토리라 latent). 그런 카드 추가 시 per-pass 재스캔 필요.
- [ ] **A-4** (신규 문서화됨) scheduler-body-suspend 인터랙티브 리액터
  - 현재 `ResolveBodyLiveAsync`가 `NotSupportedException`으로 하드-강제(오늘 bound 리액터 전부 비-인터랙티브). 인터랙티브 bound 트리거 리액터 등장 시 → activated bridge 경유로 배선(SuspendedExternally).
  - F3 `RuleProcessAsync` mid-window 비-인터랙티브 불변식도 동일(현재 주석만, 강제 안 됨).

---

## B. RD-12/13 소모 정합 잔여 (선언형 / 재-스택 / 환불)

- [ ] **B-1 · P1-3** Consume 재실행 계약 위반(latent P0)
  - `ActivatedEffectResolver` Consume가 sink 밖 비-스테이징 변이 → capped **인터랙티브** body가 1회차 소모→suspend→재실행 시 CanActivate=false로 효과 증발+use 소진. **첫 "[Once Per Turn] choose…" 인터랙티브 카드 포팅 前 필수**(Consume을 body 완주 後로 이동 또는 staged화).
- [ ] **B-2 · P1-5** 선언형 메인 활성화 = 선언 시점 소모
  - AS-IS 3 소모지점 중 ②(TurnStateMachine.cs:1183-1186, optional·코스트보다 先) 미러. 선언형 메인-액션(UseCardEffect 상당) 포팅 時.
- [ ] **B-3 · P1-6** 재-스택 use 리셋 부재
  - AS-IS `CardSource.Init`(:345-350) 진화재료 스택 시 use 리셋. 헤드리스는 턴 경계만. 같은 턴 재-스택/재-플레이 카드 時.
- [ ] **B-4 · P1-7** RemoveUse 환불 프리미티브 부재
  - AS-IS 10+장(AD1_024:265·BT14_029:114)이 body 미실행 시 캡 환불. 해당 세트 포팅 前 선행 구축.
- [ ] **B-5 · P1-8** per-shape optional/cap 우회
  - IsOptional/MaxCountPerTurn이 uniform ActivatedEffect 전용 — resolver ~30 per-shape 케이스는 캡·yes/no 없음. uniform 프리미티브 이관([[asis-uniform-activateclass]])이 곧 상환.

---

## C. RD-4 삭제 / 진화원 트래시 잔여

- [ ] **C-1 · L6 잔여** Decode/Partition PRE 이동 (TODO-96 전체 정합). Save/Fortitude는 P0-3서 상환済.
- [ ] **C-2 · L7** ACE-소스 Overflow(TODO-98) · LinkedCards 트래시 — ACE-소스/Link 카드 포팅 時.
- [ ] **C-3 · P1-9** 보호필터 밀수(latent): `CanNotTrashFromDigivolutionCards`가 DiscardEvoRoots에 혼입 — 보호 키워드 producer 포팅 前 전용 무필터 경로 분리.
- [ ] **C-4 · P1-10** battle knock-out 창이 트래시 前 해소 — AS-IS는 소스+톱 트래시 後 해소. RD-4 전체 시퀀스 재설계 時.
- [ ] **C-5 · VR-6 (=RD-7 Part B)** 시큐리티 배틀 PRE would-be-deleted 창(Evade/Barrier/Fragment/Scapegoat) 미개방 — SecurityResolver는 POST Fortitude만. RD-7 시큐리티 배틀 공용화 時.

---

## D. 삭제 배치 정밀화 (under-fire 엣지)

- [ ] **D-1 · VR-8 / F1(b)** 같은 pass 독립 2 delete-process under-fire(AS-IS 2회, 여기 1회). emission 時 delete-process batch-id 스탬프. (공통 0-DP스윕/보드와이프=단일 process는 정확.)
- [ ] **D-2 · VR-9** `OnLeaveFieldAnyone` 배치 dedup 미러(AS-IS CardController:3746 동일 배치) — board-wide leave-field 리액터 포팅 時(현재 BroadcastTimings 부재로 가려짐).

---

## E. 카드 포팅 시점 트리거 (인프라 대기)

- [ ] **E-1 · L1** RD-1 효과-구동 free-digivolve 드로우 — reveal이 peek만(미이동 카드 드로우 발산, BT1_078). Executing-존/reveal-제거 모델(TODO-68/83) 랜딩 時.
- [ ] **E-2 · L2** RD-3 버스트 재-진화 엣지 — AS-IS `AddTrashTopCardAtTurnEnd` 정의 export 부재. AS-IS 정의 확보 / 재-진화-후-버스트 카드 포팅 時.
- [ ] **E-3 · L3** RD-2 ICanNotPlayCardEffect 연속 스캔 — 스켈레톤, producer 0. CanNotPlay/PutField producer 카드 포팅 前(TODO-49).

---

## F. 별도 대형 엔진 골 (Stage 5 밖)

- [ ] **F-1** Triggered→activated 브릿지 확산(~660 카드) — EVENT-BROADCAST 카테고리 driving-event 전달. 이름-커버리지 ≠ 완성([[triggered-activated-bridge]]).
- [ ] **F-2** 프리미티브 잔여: G11 Digisorption · OnAddDigivolutionCards 방출 · per-card 트래시보호([[bt2-bt3-primitive-dev]]).
- [x] ~~**F-3** continuous-DP P0 2건(0-clamp·isUpDown 순서)~~ — **상환済**(커밋 7cbf4fe5, DpCalculator 0-clamp 확인).

---

## 권고 착수 순서

1. **A군**(Stage 5 직후 마무리) — 창-루프가 방금 열어둔 것, **라이브 버그 A-2(BT1_021) 포함. 최우선.**
2. **B-1(P1-3)** — 첫 인터랙티브 capped 카드가 이걸 밟기 前 필수(latent P0).
3. 나머지 B~E는 각 명시된 카드/기능 착수 前 선행 구축([[strong-model-prebuild-latent-infra]], OPUS-only).
4. **F-1/F-2** 대형 골은 별도 트랙.
