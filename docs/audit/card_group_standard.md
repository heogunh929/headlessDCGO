# 카드 그룹 기준 (goal · 테스트 단위)

- 작성일: 2026-06-29
- 목적: per-card 포팅의 **작업/테스트 단위를 그룹으로 고정**한다. goal 생성·테스트 프로젝트·진척 추적이 모두 이 그룹 기준을 따른다.

## 1. 그룹 단위 = `<세트>/<색상>`
원본/미러의 디렉터리 구조와 **1:1**: `Assets/Scripts/CardEffect/<Set>/<Color>/`.
- 예: `CardEffect/ST1/Red`, `CardEffect/BT1/Blue`.
- 현황: **세트 63 · 그룹 325 · 카드 3918** (그룹당 평균 ~12장). 전체 목록은 [card_groups_inventory.csv](../card_groups_inventory.csv).
- 색상 7종: Green/Yellow/Purple/Blue/Black/Red/White. 세트 접두: ST(스타터)·BT·EX·AD·RB·P·LM.

## 2. 그룹 1개 = goal 1개 = 테스트 프로젝트 1개
| 항목 | 규칙 | 예 |
|---|---|---|
| Goal id | `CARDS-<Set>-<Color>` | `CARDS-ST1-Red` |
| 테스트 프로젝트 | `tests/CardEffect.<Set>.<Color>.Tests/` | `tests/CardEffect.ST1.Red.Tests/` |
| 산출물 | 그룹 내 **모든 카드**(미러 .cs) 포팅 + 그 테스트 프로젝트에 **카드별 sub-test** | ST1/Red 12장 → 1 프로젝트에 12+ sub-test |
| 종료조건 | 그룹 테스트 프로젝트 green + 전체 스위트 `bash scripts/run-tests.sh` green |  |

## 3. 왜 그룹당 "1 프로젝트, 다중 sub-test"인가 (속도)
테스트 비용의 병목은 **단언 수가 아니라 프로젝트 수**(프로젝트마다 빌드+프로세스 기동 ~3.5s; sub-test는 한 프로세스 내 <1s 다수).
- 카드당 프로젝트(3918개) → 스위트 수십 분~시간. **금지.**
- 그룹당 프로젝트(≤325개) → 한 프로젝트가 카드 ~12장을 <1초에 검증 → 전체도 수 분.
- 병렬 러너(`JOBS`, 기본 CPU수)와 결합: 현재 204 프로젝트 ≈ 2분. 325까지 가도 수 분대 유지.

규칙: **카드 테스트는 그 그룹의 단일 `CardEffect.<Set>.<Color>.Tests` 프로젝트에 sub-test로 추가**한다. 새 프로젝트를 카드마다 만들지 않는다.

## 4. 테스트 프로젝트 형태 (그룹 1개)
`Program.cs` 1개에 `(name, body)` 배열 러너 + 카드별 sub-test. (기존 헤드리스 테스트 관례와 동일.)
- 연속/키워드: 등록 후 게이트(`ContinuousDpGate`/`ContinuousModifierGate`/`GetKeywordEffects`)로 검증.
- 트리거-메모리: 본체를 실 `MatchStateMutationSink`로 해소.
- 활성/선택/지속: `SelectPermanentEffect`+`ScriptedChoiceProvider`+`Apply`/`ApplyBuff` 명령형 검증, `EffectDurationExpiry`로 만료.
- 레시피·헬퍼는 [card_porting_recipe.md](card_porting_recipe.md).

## 5. 권장 진행 순서 (goal 생성용)
난이도/대표성 기준 우선순위(인벤토리에서 이 순서로 goal 발주 권장):
1. **ST 세트**(스타터, 단순 효과 비중↑) — ST1~ST23. ST1/Red은 검증 완료(참조 구현).
2. **BT 세트**(주력) — BT1→ 상위.
3. **EX / AD / 기타**.
색상 내 순서는 무관. 한 세트를 색상별로 나눠 병렬 발주 가능.

## 6. 기존 임시 프로젝트 정리 (후속)
이번에 만든 검증용 임시 프로젝트는 이 기준 이전 산출물이라 **해당 그룹 goal 실행 시 통합**한다:
- `P1-ST710.Port.Tests` → `CardEffect.ST7.Red.Tests` (ST7/Red goal 시)
- `P1-ST1.RedWave1/RedTriggers/RedActivated/RedTimedBuff.Tests` → `CardEffect.ST1.Red.Tests` (ST1/Red goal 시)
통합 전까지는 그대로 두어도 회귀 게이트로 동작한다(중복 검증일 뿐 무해).
