# 엔진/프리미티브 완성 TODO (2026-07-14 정리)

빅뱅 리빌드 P7/P8 완주까지 남은 작업. 실제 개발은 지시 후 착수. 근거=각 항목의 RD design item(docs/audit/rebuild_*.md).

현재: 엔진 green, 스위트 ~369/427 PASS. **union은 과도기 비계 — 종점=스캔전용+레지스트리 삭제**(memory registry-deletion-endpoint).

---

## A. 컷오버 완주 (union → 스캔전용, 레지스트리 삭제) — P8 [최우선 축]

- [ ] **A1. 구모델 카드 재포팅** (`new ActivatedEffect`+`IEffectBody` → 신 `ActivateClass`+AS-IS 코루틴; 레시피=rebuild_p8_report_recipe.md)
  - 실카드 51: BT1 32(**12 재포팅 완료·미커밋**, BT1_039·109 STOP, 18 남음) · BT2 5 · EX8 3 · 기타 11(ST4/5·EX1·BT8/9/13/19/22/24·AD1)
  - Tfx 테스트픽스처 27 (AS-IS 없음 → 신모델 테스트구성 재작성 or 대체)
- [ ] **A1.5. 신모델 스캔 스코프 완성** (엔진, 카드 재포팅과 별개)
  - 증상: ST1/2/3 카드는 **이미 신모델인데도** 테스트 FAIL — "inherited SA +1 got 1", "inherited DP +1000 got 2000".
  - 원인: `NewModelContinuousScan.FoldSAttack`/`FoldDp` 등이 **inherited(진화원 top-card 아래) + player-scope** 연속효과를 순회 안 함. AS-IS `Permanent.DP`/`Strike_AllowMinus`는 진화원·플레이어 스코프 포함.
  - 처리: AS-IS 스코프대로 fold가 inherited/player 소스까지 순회하도록 완성. → ST1/2/3 다수 + C1~5/E3 witness 회복.
- [ ] **A2. consumer 스캔-전용 flip** — union의 legacy-OR 가지 제거(ContinuousDpGate·KeywordGate·Modifier·Restriction·Immunity·DeletionReplacement·DigivolveAction·LinkHelpers·SecurityResolver·MatchStateMutationSink). 신모델 스캔만 남김.
- [ ] **A3. 레지스트리 인프라 삭제** — EffectRegistry·EffectBinding·ToBinding·LegacyBindingBridge·ActivatedEffect·ActivatedEffectResolver·IEffectBody 프리미티브군(~30+ 파일). AS-IS와 완전 1:1.
- [ ] **A4. 스캔전용 427 green 검증** + RuleAudit 0

**의존**: A1(전량) → A2 → A3. A1이 안 끝나면 A3 삭제 시 구모델 카드 붕괴.

---

## B. 프리미티브/인프라 대형 갭 (STOP 서브시스템) — RD-P6C

**처리원칙**: 각 갭을 (재하우징=Headless에 검증로직 있음, AS-IS 구조로 감쌈 / 부분 / 신규)로 분류. 조사로 확인함(아래 "있는것"=실제 Headless 자산).

- [ ] **B1. 비용/요구 엔진** (RD-P6C1-2, C2-10/11) — **재하우징(저위험)**
  - 있는것: `Headless/Bridge/PayCostRoot.cs`·`Effects/PlayCostHelpers.cs`·`Effects/DigivolutionCostHelpers.cs` (지불 파이프라인 작동 중)
  - 갭: AS-IS `CardSource.EvoCosts`/`CostList` 구조 부재 (값-union이 `AddedDigivolutionCosts`로 착수함)
  - 처리: 기존 cost 헬퍼를 AS-IS `EvoCosts` 멤버 형태로 미러 CardSource에 감쌈. Arts/Blast digivolve는 프레임(B3) 의존분만 대기.
- [ ] **B2. Select* 컴포넌트군** — **재하우징(저위험)**
  - `SelectAttackEffect` (RD-P6C2-5/8/9): 있는것=`Headless/Runtime/OverclockEffect.cs`+`AttackProcess`(goal-1). 처리=공격선택을 AS-IS `SelectAttackEffect` thin wrapper로. → Overclock/Vortex/Execute Process가 호출.
  - `SelectHandEffect` (RD-P6C1-7 / **RD-P8-01 BT1_039**): 현재 7줄 스켈레톤. 있는것=`SelectCardEffect`(Root.Hand) 인프라. 처리=그 위에 AS-IS 별개 클래스로 실구현(치환 아님 — BT9_109 선례가 치환 거부).
  - `SelectDigiXros.Select` interactive(RD-P6C1-5)·`selectBurst/AppFusion`(RD-P6C1-6): 특수플레이 auto-match는 있음, **interactive select만 신규**.
- [ ] **B3. 프레임 모델** (RD-P6C1-1/8, C2-1) — **분리(중위험)**
  - AS-IS frame=카드 위치/슬롯 상태. Headless는 zone-store+metadata로 대체(substrate 이탈, 등가 없음).
  - 처리: **게임로직 프레임 read만**(`CanPlayCardTargetFrame`·`PermanentFrame` 진화 타겟팅) zone/metadata로 재하우징. **UI 프레임(위치/렌더)은 substrate-strip 유지**. 케이스 분석 필요. `CardObjectController` zone-move는 기존 zone 헬퍼로 재하우징.
- [ ] **B4. 프로세스/배치 헬퍼** (RD-P6C2-2/6) — **재하우징(저위험)**
  - 있는것: 삭제 로직 전부 Headless에(`MatchStateMutationSink`·`CardLeavePlayCleanup`·`DeletionSourceTrash`, 배치삭제=D-1/D-2 goal, Partition=C-1 goal).
  - 처리: 기존 헬퍼를 AS-IS `DestroyPermanentsClass`/`PartitionClass` 구조로 감쌈.
- [ ] **B5. 링크 시스템** (RD-P6C2-7) — **부분(중위험, 실질 신규 최다)**
  - 있는것: 링크 zone·membership(`ContinuousFieldMembership`)·트리거(F-1 WhenLinked goal)·`FoldLinkCost`(값-union 추가).
  - **신규 갭**: 링크 PLAY 오케스트레이션(`ILinkCard` 배치)·pay-cost 통합·WhenWouldLink 창(`autoProcessing_CutIn`)·`IPlacePermanentToLinkCards`.
  - 처리: 존재분 재하우징 + play/pay-cost 신규 구축(Opus).
- [ ] **B6. 신모델 grant store** (RD-P6C3-C1 / **RD-P8-02 BT1_109**) — **재설계(A3와 결합)**
  - 있는것: `AddEffectToPlayer`(BT1_021/104/090·EX1_072 등 사용 중)이나 **구모델**(`ToBinding` 리플렉션).
  - 처리: 레지스트리 삭제(A3)와 함께 **부여효과도 별도 저장소 없이 live EffectList 스캔**(AS-IS 방식)으로 재설계 + `getCardEffect` 지연-오버로드. A3와 동시 진행.
- [ ] **B7. 개별 술어** (RD-P6C2-3/4) — **대부분 완료/재하우징(저위험)**
  - `CanBeDestroyedBySkill`: consumer-union 라운드가 이미 `NewModelContinuousScan`에 추가. `IsContainDigiXros`: `MaterialSave`에 부분. 잔여 소량 재하우징.

**요약**: B1·B2·B4·B7=재하우징(저위험, Sonnet 배선+Opus 검증) / B3·B5=부분·신규(Opus 개발) / B6=A3와 동시 재설계. no-simplification, 갭이면 STOP(발명 금지).

---

## C. union 잔재 (A2/A3 완료 시 대부분 소멸) — RD-P6B

- [ ] RD-P6B-6 DigiBurst continuous-grant body 오배선(`ActivatedEffectResolver`) — A3에서 소멸
- [ ] RD-P6B-7 cause-conditional 잔여(`CanNotBeDestroyedBySkill`/`ImmuneFromDPMinus`-cause)
- [ ] RD-P6B-8 blanket "cannot be destroyed" 정밀화
- [ ] RD-P6B-9 InvertSAttack vs LEGACY SA delta — A3(legacy 제거)에서 소멸
- [ ] RD-P6B-14 DeletionReplacementGate 잔여(G9-055 defer 1건)
- (RD-P6B-10/11/12/15/16/17/18 = RESOLVED)

**핵심**: 이 문제 클래스는 대부분 "legacy consumer가 신모델을 못 봄" → **레지스트리 삭제(A3) 시 근원 소멸**. 개별 패치 대상 아님.

---

## D. 재포팅 중 STOP(카드) — RD-P8

- [ ] RD-P8-01 BT1_039 → B2(SelectHandEffect) 대기
- [ ] RD-P8-02 BT1_109 → B6(신모델 grant store) 대기

---

## E. 잔여 테스트 FAIL (~58) 분류 (위 항목 해소 시 대부분 통과)

- **ST1/2/3·C1~5·E3 = 카드는 이미 신모델** → 실패는 **A1.5(엔진 inherited/player 스캔 스코프)** 문제, 재포팅 아님. (ST2만 1장 혼용 잔존=A1)
- activated/trigger-flow(choice-pending·cross-card) → 일부 B2/엔진
- 특수플레이(DigiXros/DNA) → B1/B2
- 기존 red(리빌드 무관): G1I-004·G3G-001/002(UnityEngine scope-guard)·G13-003(scripted-choice)

---

## 권장 순서 (2026-07-14 사용자 정정 — 엔진 먼저, 카드는 마지막에 1장씩)

**원칙**: 불완전한 엔진 위에 카드를 bulk 포팅하면 검증 불가 → 무의미. 기반(엔진)을 먼저 정확히 세운다.

1. **union 전부 제거** (A2+A3 먼저) — legacy-OR 가지·EffectRegistry·EffectBinding·ToBinding·LegacyBridge·ActivatedEffect·IEffectBody 삭제 → 스캔-전용 단일 모델. **빅뱅 red 감수**(구모델 카드 다수 red 됨 — 정상).
2. **엔진 룰 완성** (A1.5 + C 소멸 확인) — 스캔-전용 위에서 AS-IS 전 스코프(self/**inherited(진화원)**/player)·전 연속카테고리·룰 처리 정확하게. RuleAudit 관점 룰 우선.
3. **프리미티브 + 카드 1장씩** (B + A1을 짝지어) — 각 카드가 요구하는 프리미티브(B갭)를 Opus가 AS-IS 1:1로 만들고, 그 카드를 포팅하고, **정확한 엔진에 대고 witness 테스트**. 1장 완료→다음. Sonnet 배선·Opus 프리미티브·no-simplification.
4. **A4** 427 green + RuleAudit 0 → 리빌드 완주
5. (이후) 카드 포팅 본작업 ~3,918장

**무의미(하지 말 것)**: 엔진 룰 완성 전 카드 bulk 재포팅(구 A1-first 계획). → **미커밋 BT1 12장 재포팅도 이 범주 = 되돌림 대상.**
