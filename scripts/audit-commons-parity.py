#!/usr/bin/env python3
"""Commons/factory parity audit: which AS-IS symbols do CARDS call that the port lacks by name?

Scans DCGO/Assets/Scripts/CardEffect/ for `CardEffectCommons.X(` and `CardEffectFactory.X(` call
sites, diffs against the port's public surface, and reports the missing names with card-file
frequencies. Names in KNOWN_TRANSLATED are coroutine/process symbols covered by the recipe's
intent->factory table (not name-mirrored by design); names in KNOWN_STOP are explicit STOP surface.
The W6 wave is DONE when everything else reaches zero.

Usage: python3 scripts/audit-commons-parity.py [--all]   (--all includes translated/stop rows)
"""

import re
import subprocess
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CARDS = ROOT / "DCGO/Assets/Scripts/CardEffect"
PORT_FILES = [
    ROOT / "src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs",
    *sorted((ROOT / "src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory").rglob("*.cs")),
    ROOT / "src/HeadlessDCGO.Engine/Assets/Scripts/Script/PermanentEffectFactory.cs",
]

# Coroutine/process symbols the recipe maps to differently-named factories (intent translation).
KNOWN_TRANSLATED = {
    "ShowReducedCost", "CardEffectHashtable", "RevealDeckTopCardsAndProcessForAll",
    "SimplifiedRevealDeckTopCardsAndSelect", "RevealDeckTopCardsAndSelect",
    "ActivateClassesForSharedEffects", "customPermanentMessageArray_ChangeDP",
}

# Explicit STOP surface (strong-model only until a wave lands them).
KNOWN_STOP = {
    "AddSkillClass", "AddEffectToPlayer", "PlayOptionCards", "AddMaxTrashCountDigiXrosClass",
}


def call_counts(prefix: str) -> Counter:
    result = subprocess.run(
        ["grep", "-rhoE", rf"{prefix}\.[A-Za-z_]+\(", str(CARDS)],
        capture_output=True, text=True, check=False)
    return Counter(m.split(".")[1].rstrip("(") for m in result.stdout.split())


def port_surface() -> set:
    names = set()
    sig = re.compile(r"public (?:static )?[\w<>,\s\[\]\?\(\)]*?\b(\w+)(?:<[^>]+>)?\s*\(")
    for path in PORT_FILES:
        if path.exists():
            names.update(sig.findall(path.read_text(encoding="utf-8")))
    return names


def main() -> int:
    show_all = "--all" in sys.argv
    port = port_surface()
    total_missing = 0
    for prefix in ("CardEffectCommons", "CardEffectFactory"):
        calls = call_counts(prefix)
        missing = [(c, n) for n, c in calls.items() if n not in port]
        missing.sort(reverse=True)
        actionable = [(c, n) for c, n in missing if n not in KNOWN_TRANSLATED and n not in KNOWN_STOP]
        total_missing += len(actionable)
        print(f"\n== {prefix}: 카드 호출 {len(calls)}종 / 포트 미존재 {len(missing)}종 / 조치 대상 {len(actionable)}종 ==")
        rows = missing if show_all else actionable
        for count, name in rows:
            tag = " [translated]" if name in KNOWN_TRANSLATED else (" [STOP]" if name in KNOWN_STOP else "")
            print(f"{count:5d}  {name}{tag}")

    print(f"\nTOTAL actionable: {total_missing}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
