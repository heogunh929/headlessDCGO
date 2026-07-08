# BT2/BT3 누락 프리미티브 개발 — 진행 상태 기록

기록일: 2026-07-08. 작성: Fable 5(강모델 프리미티브 패스). 세션 연속 기준.

## 0. 이 문서의 목적

BT2·BT3 포팅에서 나온 STOP(프리미티브 미존재로 미등록) 카드들의 **누락 프리미티브 개발** 진행 상태를
기록한다. 설계 근거는 `docs/audit/bt3_stop_gap_design.md`(Opus 검증본). 이 문서는 그 위에서 **무엇이
개발/커밋됐고, 무엇이 실은 배선으로 해소되며, 무엇이 남았는지**를 추적한다.

## 1. 관련 산출물 위치

- **프리미티브 개발 브랜치**: `worktree-bt1-stop-remainder` (base `porting/sonnet-bt1-3` = d7350dd0)
  - `ea339be5` — BT1 STOP-remainder 프리미티브 5종(078 reveal-digivolve / 084 own-stack-select / 056
    multi-zone-play / 087 security-select-recovery-shuffle / **109 디지볼브 코스트 양측술어 파이프라인**)
  - `5020d35b` — **Tranche 1**: G2 OnUseOption 디스패치 / G4 draw-then-discard / G6 SA any-scope / G14 조건부 zone-select 후속
  - `ad0d57f7` — **Tranche 2**: G16 트래시→시큐리티 zone-card 배치
- **BT3 포팅(7 PR, 통합 green)**: #2 Red / #3 Yellow / #4 White / #5 Black / #6 Green / #7 Purple / #8 Blue.
  통합 빌드(d7350dd0+7색) 컴파일 0 오류 + 회귀 339/339. base 발산으로 BT3_074(Black) 시그니처 수정(#5에 반영).
- **BT2 sonnet 포팅(46장, 미커밋)**: 이 워크트리 `src/.../CardEffect/BT2/`에 잔존(--keep). PASS 46 / cold-skip 31 / timeout 14.
- **설계 문서**: `docs/audit/bt3_stop_gap_design.md`(17패밀리 + FALSE STOP 9장 + 우선순위).

## 2. 완료된 프리미티브 (커밋·테스트됨, 340 회귀 green)

| ID | 프리미티브 | 성격 | 커버 카드 | 테스트 |
|---|---|---|---|---|
| G2 | `EventBroadcastActivatedTimings`에 `OnUseOption` 추가(GameFlowProcessor) | 1줄 디스패치 | BT3_091/096/088-b2 + BT2 | tests/BT23.PrimTranche1 (2건) |
| G4 | `ActivatedDrawThenDiscardEffect` + `DrawThenDiscardEffect` 팩토리 + resolver case | 헬퍼 래핑(DrawAndDiscardCards) | BT3_006/088-b1 | tests/BT23.PrimTranche1 (2건, 원자성) |
| G6 | `ChangeSAttackStaticEffect`에 `scopeAnyPlayer` 옵션 | 오버로드 | BT3_040 SA half | 컴파일+회귀 |
| G14 | `ActivatedSelectFromZoneEffect`에 `onSelectedAny` 훅 + 팩토리 파라미터 | 조건부 후속 | BT3_034 | 컴파일+회귀 |
| G16 | `SelectAndPutSecurityFromZoneEffect`(zone-card→security top face-down) | 얇은 팩토리 | BT3_041 | 컴파일+회귀 |

테스트 프로젝트: `tests/BT23.PrimTranche1.Tests` (G2·G4 동작 단언 4/4), 픽스처 `TfxOnUseOptionMemory`.

## 3. 정정 — 설계 "genuine gap" 중 실은 배선/기존자산인 것

Opus 검증이 **내 BT1 프리미티브(ea339be5)가 없는 d7350dd0 base**에서 돌아 과다계상됐다. 아래는 신규
프리미티브 불필요:

- **G15 (pay+play+self-delete, 086/087)** → `DestroyPermanentsEffect(card, new[]{card.InstanceId})`가 이미
  즉시 self-delete. 3-효과 시퀀스(`GainMemoryActivatedEffect(-3)` → `SelectAndPlayFromZoneEffect` →
  `DestroyPermanentsEffect(self)`)로 **순수 배선**. (behavior: 시퀀스 원자성만 확인 필요)
- **G5 (FROM-퍼머넌트 코스트게이트, 031/103/111)** → ea339be5의 `RegisterDigivolutionCostDeltaForPlayer` +
  `ResolveDigivolutionCost(targetPermanentId)` + `ScopeDigivolveTargetPredicateKey`가 **양측 술어를 이미 지원**.
  activated/one-shot(103)은 그대로 재사용. 연속-static(031/111 "[All Turns]")만 소량 추가 필요.
- **G7 (자기 진화원 stack select→후속, 112+BT1_084)** → ea339be5의
  `SelectDigivolutionSourceToHandThenUnsuspendSelfEffect`를 **후속 액션 파라미터화**(현재 Unsuspend 하드코딩)만.

## 4. 남은 신규 프리미티브 (미개발)

| 우선 | 패밀리 | 카드 | 성격 | 비고 |
|---|---|---|---|---|
| 다음 | G7 일반화 | 112 (+BT1_084) | 기존 효과에 follow-up Action 파라미터 | 저위험 |
| 다음 | G1 reveal→play | 063/070/073 (+BT1_078) | `RevealSelectThenFreeDigivolveSelfEffect`에 PlayMode(PlayAsNewPermanent) 추가 | 내 자산 일반화 |
| 다음 | G5 static 변형 | 031/111 | 파이프라인 재사용, 연속 self-modifier | 저위험 |
| 중 | G3 activateETB 억제 | 109/110, BT2_080/081 | PlayCard mutation에 SuppressOnPlay 태그 → enter-play 트리거 스킵 | **민감**(On Play 억제 경로) |
| 중 | G8 attach-to-stack | 019 | 신규 sink mutation(핸드 카드→퍼머넌트 진화원 stack) + 팩토리 | |
| 중 | G9 nested dependent play | 030 | 2단계 select(퍼머넌트→그 진화원)→play | G1과 "zone 아닌 카드 play" 공유 |
| 중 | G10 de-digivolve→조건부 | 107/112-WD | de-digivolve 후 flush boundary → post-state 술어 → destroy | |
| 중 | G12 count-select+apply-all | 100 | 신규 `SelectCountEffect(min,max)` choice + 팬아웃 body | |
| 중 | G13 opponent binary→분기 | 102 | 상대에게 yes/no ChoiceType.Confirm → 분기 | |
| 딥 | G11 Digisorption | 054/056 | `WhenDigisorption` 타이밍+cut-in broadcast + suspend-cost-reduce + CanSuspendByDigisorption | **별도 엔진 골** |
| 검증 | G17 ignore-option-security | 097 | `CanNotAffectedStaticEffect` 대조 후 genuine 여부 확정 | |

## 5. FALSE STOP (프리미티브 불필요 — 재포팅만) — 설계 §2

BT3_003 / 014 / 015 / 071 / 099 / 101 / 105 / 106 / 040(color half). 각 대체 심볼은 설계 문서 §2 표 참조.
BT2에서도 동종 오판 존재(예: BT2_008/001 트래시-매수 쿼리 = `MatchConditionOwnersCardCountInTrash` 실재).

## 6. 다음 단계 (재개 지점)

1. **G7·G1·G5-static** — 내 BT1 자산 일반화(저위험), 각 단언테스트.
2. **G8/G9/G10/G12/G13** — 신규 프리미티브, AS-IS 1:1, 각 테스트.
3. **G3** — On Play 억제(민감). enter-play 트리거 발화 경로 정밀 조사 후.
4. **G11 Digisorption** — 별도 골로 분리.
5. FALSE STOP 9장 + 위 커버 카드 재포팅 → 통합 회귀.

**진행 규약**: 프리미티브는 AS-IS 1:1 + uniform ActivatedEffect 규약, throw/근사 금지, 트랜치마다
컴파일+단언테스트+340 회귀 green 후 커밋. 참조: [[bt1-porting-complete-stop-infra]] [[asis-uniform-activateclass]] [[fidelity-over-coverage]].
