import csv
from pathlib import Path

SOURCE = Path("docs/headless_complete_goal_breakdown_ko.csv")
OUT_DIR = Path("docs/goal-specs")
INDEX = Path("docs/headless_goal_spec_index.csv")

AREA_REFERENCES = {
    "런타임": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "docs/headless_complete_architecture_design.md",
        "docs/headless_complete_porting_sequence.md",
    ],
    "상태/존": [
        "src/HeadlessDCGO.Engine/Headless/Services",
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "DCGO/Assets/Scripts/Script/Player.cs",
        "DCGO/Assets/Scripts/Script/CardController.cs",
        "DCGO/Assets/Scripts/Script/CardObjectController.cs",
    ],
    "Unity 전역 접근 대체": [
        "src/HeadlessDCGO.Engine/Headless/Bridge",
        "DCGO/Assets/Scripts/Script/GManager.cs",
        "DCGO/Assets/Scripts/Script/ContinuousController.cs",
    ],
    "코루틴 대체": [
        "src/HeadlessDCGO.Engine/Headless/Coroutines",
        "DCGO/Assets/Scripts/Script/AutoProcessing.cs",
        "DCGO/Assets/Scripts/Script/AttackProcess.cs",
    ],
    "선택 처리 대체": [
        "src/HeadlessDCGO.Engine/Headless/Choices",
        "DCGO/Assets/Scripts/Script/SelectCardEffect.cs",
        "DCGO/Assets/Scripts/Script/SelectPermanentEffect.cs",
        "DCGO/Assets/Scripts/Script/SelectCountEffect.cs",
        "DCGO/Assets/Scripts/Script/SelectHandEffect.cs",
        "DCGO/Assets/Scripts/Script/PlayerSelection",
    ],
    "효과 처리": [
        "src/HeadlessDCGO.Engine/Headless/Effects",
        "DCGO/Assets/Scripts/Script/AutoProcessing.cs",
        "DCGO/Assets/Scripts/Script/Effects.cs",
        "DCGO/Assets/Scripts/Script/MultipleSkills.cs",
    ],
    "세션/네트워크 대체": [
        "src/HeadlessDCGO.Engine/Headless/Runtime",
        "docs/dotnet_non_unity_dependency_replacement_plan.md",
    ],
    "데이터 로딩": [
        "src/HeadlessDCGO.Engine/Headless/DataLoading",
        "src/HeadlessDCGO.Engine/Headless/Services/ICardRepository.cs",
        "DCGO/Assets/CardBaseEntity",
        "DCGO/Assets/Scripts/Script/DeckData.cs",
        "DCGO/Assets/Scripts/Script/DeckCodeUtility.cs",
    ],
    "진단/결정성": [
        "src/HeadlessDCGO.Engine/Headless/Diagnostics",
        "src/HeadlessDCGO.Engine/Headless/Services/IRandomSource.cs",
        "src/HeadlessDCGO.Engine/Headless/Services/ILogSink.cs",
        "DCGO/Assets/Scripts/Script/GameRandom.cs",
    ],
    "턴/페이즈 흐름": [
        "DCGO/Assets/Scripts/Script/TurnStateMachine.cs",
        "src/HeadlessDCGO.Engine/Headless/Runtime",
    ],
    "게임 컨텍스트": [
        "DCGO/Assets/Scripts/Script/GameContext.cs",
        "src/HeadlessDCGO.Engine/Headless/Runtime",
    ],
    "플레이어 상태": [
        "DCGO/Assets/Scripts/Script/Player.cs",
        "src/HeadlessDCGO.Engine/Headless/Services",
    ],
    "카드 컨트롤러": [
        "DCGO/Assets/Scripts/Script/CardController.cs",
        "DCGO/Assets/Scripts/Script/CardObjectController.cs",
        "src/HeadlessDCGO.Engine/Headless/Services/IZoneMover.cs",
    ],
    "메인 페이즈 액션": [
        "DCGO/Assets/Scripts/Script/MainPhaseAction",
        "src/HeadlessDCGO.Engine/Headless/Runtime",
    ],
    "자동 효과 처리": [
        "DCGO/Assets/Scripts/Script/AutoProcessing.cs",
        "src/HeadlessDCGO.Engine/Headless/Effects",
    ],
    "공격/배틀 처리": [
        "DCGO/Assets/Scripts/Script/AttackProcess.cs",
        "DCGO/Assets/Scripts/Script/SelectAttackEffect.cs",
        "src/HeadlessDCGO.Engine/Headless/Runtime",
    ],
    "효과 계약": [
        "DCGO/Assets/Scripts/Script/ICardEffect.cs",
        "DCGO/Assets/Scripts/Script/CardEffectInterfaces.cs",
        "DCGO/Assets/Scripts/Script/SkillInfo.cs",
    ],
    "효과 컨텍스트": [
        "DCGO/Assets/Scripts/Script/CardEffectCommons/GetFromHashtable.cs",
        "DCGO/Assets/Scripts/Script/CardEffectCommons/HashtableSetting.cs",
        "src/HeadlessDCGO.Engine/Headless/Effects/EffectContext.cs",
    ],
    "조건 처리": [
        "DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects",
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
    ],
    "요구사항 처리": [
        "DCGO/Assets/Scripts/Script/CardEffectCommons/MinMax_DP_Cost_Level",
        "DCGO/Assets/Scripts/Script/CardEffectFactory",
    ],
    "비용 처리": [
        "DCGO/Assets/Scripts/Script/CardEffectCommons/ShowReducedCost.cs",
        "DCGO/Assets/Scripts/Script/CardEffectFactory",
    ],
    "대상/존 조회": [
        "DCGO/Assets/Scripts/Script/CardEffectCommons",
        "src/HeadlessDCGO.Engine/Headless/Services/IZoneStateReader.cs",
    ],
    "키워드 효과": [
        "DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects",
        "DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects",
    ],
    "카드별 효과": [
        "DCGO/Assets/Scripts/Script/CardEffects",
        "DCGO/Assets/Scripts/Script/CardEffectFactory",
        "DCGO/Assets/CardBaseEntity",
    ],
    "AS-IS 비교": [
        "docs/headless_unity_dependent_functions.csv",
        "docs/headless_source_origin_mapping.csv",
    ],
}

COMMON_REFERENCES = [
    "docs/headless_complete_goal_breakdown_ko.csv",
    "docs/headless_goal_execution_prompt.md",
    "docs/headless_complete_unit_test_plan.md",
]


def safe_name(goal_id: str, goal: str) -> str:
    keep = []
    for ch in goal.lower():
        if ch.isalnum():
            keep.append(ch)
        elif ch in (" ", "-", "_"):
            keep.append("_")
    slug = "".join(keep).strip("_")
    while "__" in slug:
        slug = slug.replace("__", "_")
    if not slug:
        slug = "goal"
    return f"{goal_id}_{slug}.md"


def bullet(items):
    return "\n".join(f"- `{item}`" for item in items)


def references_for(area: str, phase: str):
    refs = list(COMMON_REFERENCES)
    refs.extend(AREA_REFERENCES.get(area, []))
    if "Phase 4" in phase and "DCGO/Assets/CardBaseEntity" not in refs:
        refs.append("DCGO/Assets/CardBaseEntity")
    seen = []
    for ref in refs:
        if ref not in seen:
            seen.append(ref)
    return seen


def phase_guard(phase: str) -> str:
    if "Phase 1" in phase:
        return "- Phase 1 범위 안에서만 작업한다.\n- `Assets/...` 실제 룰/카드 효과 포팅은 하지 않는다."
    if "Phase 0" in phase:
        return "- 설계/문서 검증만 수행한다.\n- C# 구현은 하지 않는다."
    return "- 해당 Phase의 선행 Goal이 완료된 경우에만 작업한다.\n- 원본 `DCGO/Assets/...` 파일은 수정하지 않는다."


def spec_text(row: dict) -> str:
    goal_id = row["Goal ID"]
    phase = row["단계"]
    area = row["영역"]
    goal = row["목표"]
    result_doc = row["결과 문서"]
    refs = references_for(area, phase)
    blocked = row["선행 Goal"]
    return f"""# {goal_id} {goal} 상세 지시서

## 1. Goal 식별 정보

- Goal ID: `{goal_id}`
- Phase: `{phase}`
- 영역: `{area}`
- 우선순위: `{row['우선순위']}`
- 선행 Goal: `{blocked}`

## 2. 목표 설명

{row['목표']}를 완료한다.

이 Goal의 작업 범위는 다음으로 제한한다.

> {row['작업 범위']}

## 3. 작업 범위

- Goal CSV의 `작업 범위`와 `산출물`에 적힌 항목만 수행한다.
- 필요한 경우 AS-IS 원본 파일을 읽어 동작 의미를 확인한다.
- 확인한 원본 동작은 Headless 기준 API에 맞춰 정리한다.
- 구현 산출물이 있는 경우 반드시 단위테스트를 함께 작성한다.

## 4. 범위 밖 작업

- 이 Goal과 무관한 Phase/Goal을 함께 수행하지 않는다.
- 원본 `DCGO/Assets/...` 파일을 수정하지 않는다.
{phase_guard(phase)}
- 단위테스트 없이 완료 선언하지 않는다.
- 결과 문서 없이 완료 선언하지 않는다.

## 5. 참조 파일

{bullet(refs)}

## 6. 산출물

CSV 기준 산출물:

> {row['산출물']}

작업자는 실제 수정/생성 파일을 결과 문서에 기록해야 한다.

## 7. 단위테스트 요구사항

CSV 기준 단위테스트 범위:

> {row['단위테스트 범위']}

테스트 작성 원칙:

- 이 Goal에서 바꾼 동작만 검증한다.
- 선행 Goal의 내부 동작을 다시 구현하거나 재정의하지 않는다.
- 실패 테스트가 있으면 같은 Goal 범위 안에서 수정한다.
- 테스트 명령과 결과 수치를 결과 문서에 기록한다.

## 8. 결과 문서

Goal 완료 시 아래 경로에 결과 문서를 작성한다.

- `{result_doc}`

결과 문서에는 반드시 다음 항목을 포함한다.

- 실행 일시
- 수정/생성 파일
- 테스트 명령
- 전체/통과/실패/스킵 수
- 실패 상세
- 미해결 리스크
- 완료 판정

## 9. 완료 체크리스트

- [ ] 선행 Goal이 충족되었거나 선행 Goal이 없음을 확인했다.
- [ ] Goal 범위 밖 작업을 하지 않았다.
- [ ] 원본 `DCGO/Assets/...` 파일을 수정하지 않았다.
- [ ] 필요한 참조 파일을 읽었다.
- [ ] 산출물을 작성했다.
- [ ] 단위테스트를 작성했다.
- [ ] 단위테스트를 실행했다.
- [ ] 실패 테스트가 없다.
- [ ] 결과 문서를 작성했다.
- [ ] 완료 기준 `{row['완료 기준']}`을 충족했다.

## 10. 실행 프롬프트

```text
HeadlessDCGO.Engine Goal {goal_id}를 수행하라.

반드시 먼저 이 상세 지시서를 읽어라:
docs/goal-specs/{safe_name(goal_id, goal)}

이번 작업은 {goal_id} 하나만 완료하는 것이 목표다.
선행 Goal은 `{blocked}`이다.

작업 범위:
{row['작업 범위']}

산출물:
{row['산출물']}

단위테스트 범위:
{row['단위테스트 범위']}

결과 문서:
{result_doc}

완료 기준:
{row['완료 기준']}

원본 DCGO/Assets 파일은 수정하지 말라.
Goal 범위 밖 작업을 하지 말라.
단위테스트와 결과 문서 없이는 완료로 말하지 말라.
```
"""


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    with SOURCE.open("r", encoding="utf-8-sig", newline="") as source:
        rows = list(csv.DictReader(source))

    index_rows = []
    for row in rows:
        file_name = safe_name(row["Goal ID"], row["목표"])
        path = OUT_DIR / file_name
        path.write_text(spec_text(row), encoding="utf-8")
        index_rows.append({
            "Goal ID": row["Goal ID"],
            "단계": row["단계"],
            "영역": row["영역"],
            "목표": row["목표"],
            "상세 지시서": str(path).replace("\\", "/"),
            "결과 문서": row["결과 문서"],
            "선행 Goal": row["선행 Goal"],
        })

    with INDEX.open("w", encoding="utf-8-sig", newline="") as target:
        writer = csv.DictWriter(target, fieldnames=["Goal ID", "단계", "영역", "목표", "상세 지시서", "결과 문서", "선행 Goal"])
        writer.writeheader()
        writer.writerows(index_rows)

    print(f"wrote {len(index_rows)} goal specs to {OUT_DIR}")
    print(f"wrote index {INDEX}")


if __name__ == "__main__":
    main()
