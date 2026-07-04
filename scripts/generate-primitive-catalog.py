#!/usr/bin/env python3
"""Regenerate the alphabetical master table of docs/porting/PRIMITIVE-CATALOG.md.

Extracts every `public static ICardEffect|IActivatedCardEffect <Name>(...)` factory from
CardPortingFramework.cs and rewrites the `## 알파벳 마스터` section in place. The curated
category quick-reference above it is left untouched (update by hand when semantics change).

Usage: python3 scripts/generate-primitive-catalog.py
"""

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCES = [
    ROOT / "src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs",
    *sorted((ROOT / "src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory").rglob("*.cs")),
]
CATALOG = ROOT / "docs/porting/PRIMITIVE-CATALOG.md"
MASTER_HEADING = "## 알파벳 마스터 (이름 → 시그니처)"

SIGNATURE_RE = re.compile(
    r"public static (ICardEffect|IActivatedCardEffect)\s+(\w+)\s*\((.*?)\)\s*(?==>|\{)",
    re.DOTALL,
)


def normalize(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip()


def main() -> int:
    rows = []
    for path in SOURCES:
        source = path.read_text(encoding="utf-8")
        for match in SIGNATURE_RE.finditer(source):
            ret, name, params = match.group(1), match.group(2), normalize(match.group(3))
            signature = f"{ret} {name}({params})".replace("|", "\\|")
            rows.append((name, ret, signature))

    rows.sort(key=lambda r: r[0].lower())
    if not rows:
        print("no factories found — aborting", file=sys.stderr)
        return 1

    table = [
        "| 팩토리 | 반환 | 시그니처 |",
        "|---|---|---|",
    ]
    for name, ret, signature in rows:
        table.append(f"| `{name}` | {ret} | `{signature}` |")

    catalog = CATALOG.read_text(encoding="utf-8")
    head, _, _ = catalog.partition(MASTER_HEADING)
    if not head:
        print("master heading not found in catalog", file=sys.stderr)
        return 1

    # Refresh the factory count in the intro line.
    head = re.sub(r"공개 팩토리 \*\*\d+종\*\*", f"공개 팩토리 **{len(rows)}종**", head)

    CATALOG.write_text(head + MASTER_HEADING + "\n\n" + "\n".join(table) + "\n", encoding="utf-8")
    print(f"regenerated master table: {len(rows)} factories")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
