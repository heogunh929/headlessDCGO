# Goal `CARDS-ST2-Blue` (ST2/Blue, 12장)

- 작성일: 2026-06-30
- 기준: [card_group_standard.md](card_group_standard.md) — 그룹(`<세트>/<색상>`) 1개 = goal 1개 = 테스트 프로젝트 1개.
- 테스트 프로젝트: `tests/CardEffect.ST2.Blue.Tests/`
- 참조 구현: ST1/Red(검증 완료), ST7/Red. 레시피: [card_porting_recipe.md](card_porting_recipe.md).
- 현 상태: 미러 `.cs`는 **스켈레톤(TODO)**, 테스트 프로젝트 **미생성**.
- 테마: 전위(digivolution)카드 트래시 / 무전위 상대 견제 / 언서스펜드.
- 종료조건: `CardEffect.ST2.Blue.Tests` green + 전체 `bash scripts/run-tests.sh` green. 커밋은 사용자 지시 시.

## 공유 선결(엔진, 한 번만): effectClass 별칭 dispatch
ST2_07은 **자체 효과 파일이 없고** cards.json `effectClass: "ST1_06"`로 ST1_06을 재사용한다. 현 dispatch는 **cardNumber로만** 효과 클래스를 찾아([CardPortingFramework.cs:932](../../src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs), [ActivatedEffectResolver.cs:38](../../src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/ActivatedEffectResolver.cs)) 별칭 카드의 효과가 **등록되지 않는다**. 로더는 `metadata["effectClass"]`를 이미 싣는다([CardBaseEntityLoader.cs:80](../../src/HeadlessDCGO.Engine/Headless/DataLoading/CardBaseEntityLoader.cs)).
- **수정:** `CardEffectDispatch.TryCreateForCard(def)` 추가 — effectClass 우선, 없으면 cardNumber 폴백. 위 2개 호출부 교체.
- **규모:** ST1_06 별칭 42장 + 이종 일러스트 재판(`*_P2/_P3`) 수천 장 동시 해금. ST2_07·ST3_07 공통 선결.
- **무회귀:** effectClass 없는 카드는 종전대로 cardNumber 해석. 미포팅 별칭은 no-op.
- ST3 goal과 공유 — 둘 중 먼저 착수하는 쪽에서 1회 수행(전용 테스트 `tests/G9-001.EffectClassAlias.Tests/`).

## 카드 목록 (12장)
| 카드 | 타이밍 | 효과 | 메커니즘 / 재사용 |
|---|---|---|---|
| ST2_01 | None(계승·지속) | 자신 턴 & 상대 배틀 Digimon이 무전위면 자신 DP+1000 | `ChangeSelfDPStaticEffect`(조건·계승) — 재사용 |
| ST2_03 | OnAllyAttack(계승·활성) | [어택시] 상대 lvl≤5 Digimon 1체의 맨밑 전위카드 1장 트래시 | 선택+`TrashDigivolutionCardsFromTopOrBottom` — 재사용(확인) |
| ST2_06 | OnAllyAttack(계승·활성) | [어택시] 상대 Digimon 1체의 맨밑 전위카드 1장 트래시 | 위와 동일(조건만 완화) |
| **ST2_07** (Grizzlymon) | None + OnAllyAttack | `<Blocker>` + [어택시] 메모리-2 | **별칭 → `ST1_06`**(이미 포팅). 신규 파일 없음. **선결: effectClass dispatch**. sub-test만 추가 |
| ST2_08 | None(계승·지속) | 상대가 무전위 Digimon 보유 시 자신 시큐리티어택+1 | `ChangeSelfSAttackStaticEffect` — 재사용 |
| ST2_09 | OnEnterFieldAnyone(진화시) | [진화시] 상대 Digimon 1체의 맨밑 전위카드 2장 트래시 | 선택+트래시(count=2) — 재사용 |
| ST2_11 | OnAllyAttack(1회/턴) | [어택시][턴1회] 이 Digimon 언서스펜드 | `IUnsuspendPermanents` + **1회/턴 게이트**(SetHashString/order=1) |
| ST2_12 | OnStartTurn + SecuritySkill | [내턴시작] 상대 무전위 Digimon 있으면 메모리+1; [시큐리티] 테이머 등장 | 메모리+`PlaySelfTamerSecurityEffect` — 재사용 |
| ST2_13 | OptionSkill + SecuritySkill | [메인] 메모리+1; [시큐리티] 메모리+2 | 옵션/시큐리티 메모리 — 재사용 |
| ST2_14 | OptionSkill + SecuritySkill | [메인] 상대 무전위 Digimon 1체 다음 상대턴 끝까지 어택/블록 불가; [시큐리티] 다음 내턴 끝까지 동일 | `CanNotAttack`/`CanNotBlock`+`EffectDuration` — 재사용 |
| ST2_15 | OptionSkill + SecuritySkill | [메인] 자신 Digimon 밑 전위카드 1장을 코스트 없이 다른 Digimon으로 플레이 | **고위험·신규** — 밑카드를 별도 Digimon으로 플레이(SpecialPlay/PlayFromUnder 경로) |
| ST2_16 | OptionSkill + SecuritySkill | [메인] 상대 Digimon 1체 손으로 바운스(전위카드 전부 트래시) | `ReturnToHand`/바운스 — 재사용(확인) |

권장 작업 순서(쉬움→어려움): **07(별칭, 선결 후 sub-test만)** → 01 → 08 → 06 → 03 → 09 → 13 → 12 → 11 → 16 → 14 → **15(마지막, 필요 시 엔진 선포팅)**.

## 효과 없는 나머지 카드(완전성)
- **바닐라**(효과·effectClass 없음): ST2_04/05/10 — 포팅 대상 아님(스탯만). 작업·테스트 불필요.
- **DigiEgg/재판 별칭**: ST2_02→ST2_01, `*_P2/_P3` 등 — 자체 효과 없이 베이스 클래스 별칭. **공유 선결 dispatch 1회로 자동 해결**. 그룹 종료 시 회귀로만 확인.

## sub-test 방침
카드별 1+개: 지속=게이트(`ContinuousDpGate`/`GetKeywordEffects`), 활성/선택=`SelectPermanentEffect`+`ScriptedChoiceProvider`+`Apply`, 만료=`EffectDurationExpiry`, 트리거-메모리=실 `MatchStateMutationSink`, 시큐리티=`SecuritySkill` 경로. ST2_07=별칭이 ST1_06 효과(Blocker 게이트 + 어택 메모리-2)로 등록·동작함을 검증.
