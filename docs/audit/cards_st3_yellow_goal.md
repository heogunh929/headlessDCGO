# Goal `CARDS-ST3-Yellow` (ST3/Yellow, 12장)

- 작성일: 2026-06-30
- 기준: [card_group_standard.md](card_group_standard.md) — 그룹(`<세트>/<색상>`) 1개 = goal 1개 = 테스트 프로젝트 1개.
- 테스트 프로젝트: `tests/CardEffect.ST3.Yellow.Tests/`
- 참조 구현: ST1/Red(검증 완료), ST7/Red. 레시피: [card_porting_recipe.md](card_porting_recipe.md).
- 현 상태: 미러 `.cs`는 **스켈레톤(TODO)**, 테스트 프로젝트 **미생성**.
- 테마: 격파 트리거 / 시큐리티 매수 조건 / DP 증감 / 리커버리.
- 종료조건: `CardEffect.ST3.Yellow.Tests` green + 전체 `bash scripts/run-tests.sh` green. 커밋은 사용자 지시 시.

## 공유 선결(엔진, 한 번만): effectClass 별칭 dispatch
ST3_07은 **자체 효과 파일이 없고** cards.json `effectClass: "ST1_06"`로 ST1_06을 재사용한다. 현 dispatch는 **cardNumber로만** 효과 클래스를 찾아([CardPortingFramework.cs:932](../../src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs), [ActivatedEffectResolver.cs:38](../../src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/ActivatedEffectResolver.cs)) 별칭 카드의 효과가 **등록되지 않는다**. 로더는 `metadata["effectClass"]`를 이미 싣는다([CardBaseEntityLoader.cs:80](../../src/HeadlessDCGO.Engine/Headless/DataLoading/CardBaseEntityLoader.cs)).
- **수정:** `CardEffectDispatch.TryCreateForCard(def)` 추가 — effectClass 우선, 없으면 cardNumber 폴백. 위 2개 호출부 교체.
- **규모:** ST1_06 별칭 42장 + 이종 일러스트 재판(`*_P2/_P3`) 수천 장 동시 해금. ST2_07·ST3_07 공통 선결.
- **무회귀:** effectClass 없는 카드는 종전대로 cardNumber 해석. 미포팅 별칭은 no-op.
- ST2 goal과 공유 — 둘 중 먼저 착수하는 쪽에서 1회 수행(전용 테스트 `tests/G9-001.EffectClassAlias.Tests/`). ST2를 먼저 했다면 이 goal에선 통과 확인만.

## 카드 목록 (12장)
| 카드 | 타이밍 | 효과 | 메커니즘 / 재사용 |
|---|---|---|---|
| ST3_01 | OnDestroyedAnyone(1회/턴) | [내턴][턴1회] 상대 0DP 격파 시 이 Digimon 이번턴 DP+1000 | 격파 트리거+자신 타임드버프, 1회/턴 — 재사용(게이트 확인) |
| ST3_04 | OnDestroyedAnyone(1회/턴) | [내턴][턴1회] 상대 0DP 격파 시 메모리+1 | 트리거+메모리, 1회/턴 — 재사용 |
| ST3_05 | OnAllyAttack | [어택시] 시큐리티 4장 이상이면 메모리+1 | **시큐리티 매수 조건**(≥4) — 헬퍼 확인 |
| **ST3_07** (Unimon) | None + OnAllyAttack | `<Blocker>` + [어택시] 메모리-2 | **별칭 → `ST1_06`**(이미 포팅). 신규 파일 없음. **선결: effectClass dispatch**. sub-test만 추가 |
| ST3_08 | OnAllyAttack(활성) | [어택시] 상대 Digimon 1체 이번턴 DP-1000 | 선택+타깃 DP 디버프(타임드) — 재사용 |
| ST3_09 | OnEnterFieldAnyone(진화시) | [진화시] 시큐리티 3장 이하면 <Recovery+1(덱)> | `IRecovery` + 시큐리티 매수 조건(≤3) — 재사용(확인) |
| ST3_11 | OnAllyAttack(활성) | [어택시] 상대 Digimon 1체 이번턴 DP-4000 | ST3_08과 동형(값만) — 재사용 |
| ST3_12 | None(지속) + SecuritySkill | 시큐리티 Digimon DP 보정(지속); [시큐리티] 테이머 등장 | `ChangeSecurityDigimonCardDPStaticEffect`+테이머 — 재사용(확인) |
| ST3_13 | OptionSkill + SecuritySkill | [메인] 자신 Digimon 1체 이번턴 DP+3000; [시큐리티] 자신 전 Digimon·시큐리티Digimon DP+5000 후 이 카드 손으로 | 타깃/플레이어스코프 버프 + 자기 바운스 — 재사용 |
| ST3_14 | OptionSkill + SecuritySkill | [메인] 상대 Digimon 1체 이번턴 DP-2000; [시큐리티] 이 카드 손으로 | 타깃 디버프 + 자기 손추가 — 재사용 |
| ST3_15 | OptionSkill + SecuritySkill | [메인] 상대 Digimon 1체 <시큐리티어택-3> 다음 상대턴 끝까지; [시큐리티] 상대 전 Digimon <시큐리티어택-1> 이번턴 | **시큐리티어택 디버프(타임드)** — 키워드/SAttack 게이트 확인 |
| ST3_16 | OptionSkill + SecuritySkill | [메인] 상대 Digimon 1체 이번턴 DP-10000; [시큐리티] (동형) | 타깃 디버프 — 재사용 |

권장 작업 순서: **07(별칭, 선결 후 sub-test만)** → 04 → 01 → 16 → 08 → 11 → 14 → 13 → 05 → 09 → 12 → 15.

## 효과 없는 나머지 카드(완전성)
- **바닐라**(효과·effectClass 없음): ST3_03/06/10 — 포팅 대상 아님(스탯만). 작업·테스트 불필요.
- **DigiEgg/재판 별칭**: ST3_02, `*_P2/_P3` 등 — 자체 효과 없이 베이스 클래스 별칭. **공유 선결 dispatch 1회로 자동 해결**. 그룹 종료 시 회귀로만 확인.

## sub-test 방침
카드별 1+개: 트리거(실 `MatchStateMutationSink` 해소), 타깃 디버프(`Apply`+`EffectDurationExpiry` 만료), 시큐리티 매수 조건(보드 셋업으로 경계값 4/3 검증), 리커버리(덱→시큐리티 이동 검증), 시큐리티=`SecuritySkill` 경로. ST3_07=별칭이 ST1_06 효과로 등록·동작함을 검증.
