#!/usr/bin/env python3
"""Regenerate the alphabetical master table of docs/porting/PRIMITIVE-CATALOG.md.

Extracts every `public static ICardEffect|IActivatedCardEffect <Name>(...)` factory from
CardPortingFramework.cs and rewrites the `## 알파벳 마스터` section in place. The curated
category quick-reference above it is left untouched (update by hand when semantics change).

Also extracts EVERY public static method of the `CardEffectCommons` class (any return
type) into a `## CardEffectCommons 헬퍼 마스터` section below the factory table. Cards
call this helper layer directly (predicates like HasMatchConditionPermanent, imperative
helpers like ChangeDigimonDP) — leaving it out of the catalog caused the porter to STOP
on helpers that already exist (BT1 Red wave 1).

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

# Any public static method (any return type) — used for the CardEffectCommons helper layer.
COMMONS_METHOD_RE = re.compile(
    r"public static ([\w<>,?\[\]\. ]+?)\s+(\w+)\s*\((.*?)\)\s*(?==>|\{|\bwhere\b)",
    re.DOTALL,
)

COMMONS_HEADING = "## CardEffectCommons 헬퍼 마스터 (이름 → 시그니처)"


def normalize(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip()


def commons_class_body(source: str) -> str:
    """Return the brace-matched body of `public static class CardEffectCommons`."""
    marker = "public static class CardEffectCommons"
    start = source.find(marker)
    if start < 0:
        return ""
    brace = source.find("{", start)
    depth = 0
    for i in range(brace, len(source)):
        if source[i] == "{":
            depth += 1
        elif source[i] == "}":
            depth -= 1
            if depth == 0:
                return source[brace + 1 : i]
    return source[brace + 1 :]


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

    # CardEffectCommons helper layer (predicates + imperative helpers cards call directly).
    framework = SOURCES[0].read_text(encoding="utf-8")
    body = commons_class_body(framework)
    commons_rows = []
    seen = set()
    for match in COMMONS_METHOD_RE.finditer(body):
        ret, name, params = normalize(match.group(1)), match.group(2), normalize(match.group(3))
        key = (name, params)
        if key in seen:
            continue
        seen.add(key)
        signature = f"{ret} {name}({params})".replace("|", "\\|")
        commons_rows.append((name, ret, signature))
    commons_rows.sort(key=lambda r: (r[0].lower(), r[2]))

    commons_table = [
        "> 카드 코드가 직접 부르는 `CardEffectCommons.<이름>(...)` 헬퍼 전수(자동생성). "
        "술어(`HasMatchCondition*`, `CanTrigger*`, `Is*`)는 condition/코루틴 안에서 그대로 호출 가능. "
        "명령형 헬퍼(`ChangeDigimonDP`, `AddThisCardToHand` 등)는 코루틴 번역 시 레시피 의도→팩토리 표를 먼저 본다.",
        "",
        "| 헬퍼 | 반환 | 시그니처 |",
        "|---|---|---|",
    ]
    for name, ret, signature in commons_rows:
        commons_table.append(f"| `{name}` | `{ret}` | `{signature}` |")

    catalog = CATALOG.read_text(encoding="utf-8")
    head, _, _ = catalog.partition(MASTER_HEADING)
    if not head:
        print("master heading not found in catalog", file=sys.stderr)
        return 1

    # Refresh the factory count in the intro line.
    head = re.sub(r"공개 팩토리 \*\*\d+종\*\*", f"공개 팩토리 **{len(rows)}종**", head)

    CATALOG.write_text(
        head
        + MASTER_HEADING
        + "\n\n"
        + "\n".join(table)
        + "\n\n"
        + COMMONS_HEADING
        + "\n\n"
        + "\n".join(commons_table)
        + "\n",
        encoding="utf-8",
    )
    print(f"regenerated master table: {len(rows)} factories, {len(commons_rows)} commons helpers")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
