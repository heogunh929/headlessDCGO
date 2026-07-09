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

## 4. 신규 프리미티브 — 개발 완료 (Tranche 3~5, 2026-07-08)

9패밀리 개발 완료. 각 단언테스트(tests/BT23.PrimTranche3~5.Tests, 총 22건) + 회귀 green 후 커밋.

> ⚠ **충실도 감사(2026-07-08, 병렬 에이전트 5)**: 사용자 기준 **"구조가 다르면 FAIL"** 적용 시 9패밀리 전부 fidelity
> 부채 보유(결과-동일 modeling 포함). 실제 동작버그 2건(G12 CanNotTrash 필터·min-count) + 설계취약 1건(G10 mass 술어분리).
> 전부 미상환(기록만) → **[fidelity_debt.md](fidelity_debt.md) "BT2/BT3 프리미티브 (Tranche 3~5)" 섹션** 참조. 상환은 카드 배선 단계에서.

| 패밀리 | 카드 | 헤드리스 심볼 | 트랜치 |
|---|---|---|---|
| G7 | 112/BT1_084 | `SelectDigivolutionSourceToHandThenSelfFollowUpEffect`(follow-up `Action<sink>` 파라미터화) + `CardEffectCommons.UnsuspendSelf` | T3 `84af3236` |
| G1 | 063/070/073/BT1_078 | `RevealSelectThenPlaySelectedEffect` + `RevealPlayMode`(Digivolve/Play) + `Func<int>` revealCount | T3 |
| G5-static | 031/111 | `ChangeDigivolutionCostStaticEffect`(permanent/card/root 술어 오버로드) → `DigivolutionCostGateEffect`(dispatch-first 폴드, 손패 카드) | T3 |
| G8 | 019 | `SelectHandAttachToOwnStackThenMemoryEffect` + `DigivolutionStackHelpers.AddSourcesTopAsync` | T4 `1f375936` |
| G9 | 030 | `ActivatedPlayFromUnderEffect`(canTarget 술어 + isOptional; 2단계 select를 도달집합 동일 flatten으로 미러) | T4 |
| G10 | 107/112-WD | `SelectDeDigivolveThenConditionalDestroyEffect` + `MassDeDigivolveThenConditionalDestroyEffect`(flush 경계→post-state 술어→destroy) | T4 |
| G12 | 100-A | `ChooseCountThenTrashDigivolutionEffect`(`ChoiceType.Count` 0..N→매칭 전부 trash) | T4 |
| G13 | 102 | `OpponentBinaryChoiceEffect`(상대 소유 ModeChoice yes/no→분기, autoNoWhen) | T4 |
| G3 | 109/110, BT2_080/081 | `MatchStateMutationSink.SuppressOnPlayKey` → CardMoved 이벤트 one-shot 마커 → `AutoProcessingTriggerCollector`가 subject 자신의 OnPlay/OnEnterField 트리거 드롭. `PlayPermanentCards(activateETB:false)` throw 제거. | T5 |

## 4b. 별도 엔진 골로 분리 (2026-07-08, 사용자 결정) — 이번 프리미티브 패스 제외

fidelity-over-coverage 규약상 "근사치로 뭉개기" 금지 → 아래 둘은 엔진 확장이 필요해 별도 골로 분리.

- **G11 Digisorption (054/056)** — **딥 엔진 갭**. Digisorption은 *선택적 interactive* "당신의 Digimon 1마리를
  서스펜드해도 됨 → 디지볼브 코스트 -N"인데, `DigivolveAction.cs:179` 주석대로 **디지볼브 중 interactive(deferred-choice)
  BeforePayCost가 v1-미지원**(deferred가 뜨면 원가 지불). 또 기존 `SuspendCostReductionEffect`는 `PlayCostDeltaKey`(플레이
  코스트)만 감산 → **디지볼브-코스트 감산 변형 + deferred-choice를 디지볼브 코스트 경로까지 관통**시키는 엔진 작업 필요.
  056은 추가로 상대-Digimon-서스펜드(CanSuspendByDigisorption) + [Once Per Turn].
- ~~**이펙트-플레이 카드 효과 미등록** (G1·G3·G9 공통)~~ → **✅ 해소(2026-07-08, Fable 5)**: 등록을 sink 자체에
  내재화. `EngineContext.RegisterEnteredCardEffects`(enter-play chokepoint) 신설 + `MatchStateMutationSink` 생성자가
  `context`가 있으면 `onCardEnteredPlay`를 이 메서드로 **기본 폴백** → 스케줄러 sink뿐 아니라 `NewSink`(PlayPermanentCards,
  Fortitude/Decode/Partition)·리졸버 sink(play-from-under)·런타임 sink **전부** 카드-플레이 시 자동 등록. AS-IS 미러(모든 플레이가
  단일 `PlayCardClass.PlayCard()`→EffectList 라이브). **추가**: `CardEffectRegistrar.RegisterCard`를 **멱등화**(재등록 전
  `UnregisterCard`로 stale self-바인딩 제거) — 재진입(play-from-under·trash 재생·de-digivolve 복귀) 시 중복-바인딩 throw 방지.
  이는 액션 경로(PlayCardAction/DigivolveAction/SpecialPlayAction)의 잠재적 재진입 중복도 함께 해소. 검증: `tests/G8-003`
  (NewSink 등록+멱등 2건), 회귀 345 green, RuleAudit 0. **주의**: "특정 경로로 카드 내는 카드가 아직 없다"는 이유로 일부 sink를 배선
  제외하지 않음 — 포팅 진행 시 당연히 추가될 것이므로 context-보유 sink 전체를 균일 배선(사용자 지시 2026-07-08).
  잔여: G3 억제의 구조적 debt(source+timing vs per-effect `IsOnPlay`)는 별건([fidelity_debt.md] G3 항목).

## 4c. 검증 대기 (G1~13 범위 밖)

- G17 ignore-option-security(097) — `CanNotAffectedStaticEffect` 대조 후 genuine 여부 확정 (이번 범위 밖).

## 5. FALSE STOP (프리미티브 불필요 — 재포팅만) — 설계 §2

BT3_003 / 014 / 015 / 071 / 099 / 101 / 105 / 106 / 040(color half). 각 대체 심볼은 설계 문서 §2 표 참조.
BT2에서도 동종 오판 존재(예: BT2_008/001 트래시-매수 쿼리 = `MatchConditionOwnersCardCountInTrash` 실재).

## 6. 다음 단계 (재개 지점)

1. ~~G7·G1·G5-static / G8·G9·G10·G12·G13 / G3~~ — **완료**(§4, Tranche 3~5).
2. **별도 엔진 골**(§4b): ~~(a) 이펙트-플레이 등록 인프라 배선~~ **✅ 완료(2026-07-08)**, (b) G11 Digisorption(deferred-digivolve-cost),
   (c) `OnAddDigivolutionCards` 방출 배선(G8), (d) per-card 트래시 보호 부여 클래스.
3. FALSE STOP 9장 + §4 커버 카드 재포팅 → 통합 회귀. (등록 인프라(2a) **완료됨** → G1/G3/G9 낸-카드 효과가 이제 실동작 가능.)

**진행 규약**: 프리미티브는 AS-IS 1:1 + uniform ActivatedEffect 규약, throw/근사 금지, 트랜치마다
컴파일+단언테스트+회귀 green 후 커밋. 참조: [[bt1-porting-complete-stop-infra]] [[asis-uniform-activateclass]] [[fidelity-over-coverage]].
