import csv
from collections import defaultdict
from pathlib import Path

p = Path(__file__).resolve().parents[1] / "docs" / "asis_tobe_function_mapping_ko.csv"

for status in ("CONTRACT", "EXCLUDED"):
    rows = []
    with p.open(encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            if row.get("상태") == status:
                rows.append(row)

    print(f"=== {status} ({len(rows)}건) ===\n")

    if status == "CONTRACT":
        for r in rows:
            print(
                f"- {r['ASIS_클래스']}.{r['ASIS_함수']} "
                f"({r['ASIS_파일']}:{r['ASIS_행']})"
            )
            print(f"  -> {r['TOBE_클래스']}.{r['TOBE_함수']}")
            print(f"  {r['비고']}\n")
    else:
        by_file = defaultdict(list)
        for r in rows:
            by_file[r["ASIS_파일"]].append(r["ASIS_함수"])
        for file_name in sorted(by_file.keys()):
            funcs = by_file[file_name]
            print(f"[{file_name}] ({len(funcs)}개)")
            for fn in funcs[:8]:
                print(f"  - {fn}")
            if len(funcs) > 8:
                print(f"  ... 외 {len(funcs) - 8}개")
            print()
