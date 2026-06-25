import csv
from pathlib import Path


DETAIL_CSV = Path("docs/headless_complete_goal_breakdown_detailed_ko.csv")
INDEX_CSV = Path("docs/headless_goal_spec_index.csv")
OUT_CSV = Path("docs/headless_goal_prompts_compact_ko.csv")
OUT_MD = Path("docs/headless_goal_prompt_usage.md")


def read_csv(path):
    with path.open("r", encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def compact_prompt(row, spec_path):
    blocker = row["선행 Goal"]
    return "\n".join(
        [
            f"HeadlessDCGO.Engine Goal {row['Goal ID']}만 수행하라.",
            f"목표: {row['목표']}",
            f"상세 지시서: {spec_path}",
            f"선행 Goal: {blocker}",
            f"결과 문서: {row['결과 문서']}",
            "",
            "규칙:",
            "- 먼저 상세 지시서를 읽고 그 범위만 수행하라.",
            "- 원본 DCGO/Assets 파일은 수정하지 말라.",
            "- Goal 밖 작업과 다음 Phase 선행 작업을 하지 말라.",
            "- 단위테스트와 결과 문서 없이는 완료로 말하지 말라.",
            f"- 완료 기준: {row['완료 기준']}",
        ]
    )


def main():
    detail_rows = read_csv(DETAIL_CSV)
    index_rows = read_csv(INDEX_CSV)
    spec_by_id = {r["Goal ID"]: r["상세 지시서"] for r in index_rows}

    out_rows = []
    for row in detail_rows:
        spec_path = spec_by_id[row["Goal ID"]]
        prompt = compact_prompt(row, spec_path)
        out_rows.append(
            {
                "Goal ID": row["Goal ID"],
                "단계": row["단계"],
                "영역": row["영역"],
                "목표": row["목표"],
                "짧은 Goal 프롬프트": prompt,
                "상세 지시서": spec_path,
                "결과 문서": row["결과 문서"],
                "선행 Goal": row["선행 Goal"],
                "완료 기준": row["완료 기준"],
                "프롬프트 글자 수": str(len(prompt)),
            }
        )

    with OUT_CSV.open("w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=list(out_rows[0].keys()))
        writer.writeheader()
        writer.writerows(out_rows)

    sample = out_rows[0]["짧은 Goal 프롬프트"]
    usage = f"""# HeadlessDCGO.Engine 짧은 Goal 프롬프트 사용법

## 목적

`docs/goal-specs/*.md` 상세 지시서는 작업 기준을 보존하기 위한 긴 문서다.
실제로 Goal을 맡길 때는 긴 상세 지시서를 통째로 붙이지 말고, `docs/headless_goal_prompts_compact_ko.csv`의 짧은 Goal 프롬프트를 사용한다.

## 사용 규칙

- 실제 작업 지시는 짧은 Goal 프롬프트 하나만 보낸다.
- 작업자는 프롬프트에 적힌 상세 지시서를 먼저 읽는다.
- 상세 지시서는 기준 문서이고, Goal 프롬프트는 실행 지시다.
- Goal 하나가 끝나기 전 다음 Goal을 진행하지 않는다.
- 단위테스트와 결과 문서가 없으면 완료가 아니다.

## 출력 파일

- `docs/headless_goal_prompts_compact_ko.csv`
- `docs/goal-specs/*.md`

## 짧은 Goal 프롬프트 예시

```text
{sample}
```

## 권장 사용 방식

```text
docs/headless_goal_prompts_compact_ko.csv에서 <GOAL_ID> 행의 "짧은 Goal 프롬프트"만 사용한다.
상세 지시서는 프롬프트 안의 경로를 작업자가 직접 읽는다.
```
"""
    OUT_MD.write_text(usage, encoding="utf-8", newline="\n")
    print(f"wrote {OUT_CSV} rows={len(out_rows)}")
    print(f"wrote {OUT_MD}")


if __name__ == "__main__":
    main()
