#!/usr/bin/env python3
"""Generate a machine-readable symbol allowlist for the porting validator.

Extracts the PUBLIC member names of the symbol surfaces a ported card may call
(CardEffectFactory / CardEffectCommons / CardSource) plus the query gates, directly
from the engine source so the list stays in sync. Also carries a curated map of the
KNOWN hallucinations (BT2 re-measure, cheatsheet §9) -> the real symbol, so the
validator can emit corrective hints.

Usage: python3 tools/porting/pilot/gen_allowlist.py   # writes allowlist.json next to this file
"""
from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
FRAMEWORK = ROOT / "src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs"
GATES_DIR = ROOT / "src/HeadlessDCGO.Engine/Headless/Runtime"
OUT = Path(__file__).resolve().parent / "allowlist.json"

# member declaration at a class's direct scope: `public [mods] <type> <Name>` terminated by
# `(` (method) / `{` `=>` `=` `;` (property/field) OR END-OF-LINE (property whose `{` is on the next line).
MEMBER = re.compile(
    r"^\s*public\s+(?:(?:static|async|sealed|override|virtual|readonly|const|new|unsafe)\s+)*"
    r"[\w<>\[\],\.\?\s]+?\s(\w+)\s*(?:[\(\{=;]|$)"
)
CLASS = re.compile(r"\b(?:public|internal)\s+(?:static\s+|sealed\s+|partial\s+|abstract\s+)*class\s+(\w+)")


def members_of(text: str, class_name: str) -> list[str]:
    """Public member names declared at the DIRECT scope of `class_name` (brace-tracked)."""
    lines = text.splitlines()
    names: set[str] = set()
    i = 0
    n = len(lines)
    while i < n:
        m = CLASS.search(lines[i])
        if not m or m.group(1) != class_name:
            i += 1
            continue
        # find the opening brace of this class, then track depth.
        depth = 0
        opened = False
        j = i
        while j < n:
            depth += lines[j].count("{") - lines[j].count("}")
            if "{" in lines[j]:
                opened = True
            if opened and depth == 0:
                break  # class closed
            # capture members only at direct scope (depth == 1 relative to class body)
            if opened and depth == 1:
                mm = MEMBER.match(lines[j])
                if mm:
                    nm = mm.group(1)
                    if nm not in ("get", "set", "class", "return", "new"):
                        names.add(nm)
            j += 1
        return sorted(names)
    return []


def factory_signatures(text: str) -> dict[str, str]:
    """factory name -> its parameter list (collapsed), so the gemma diagnosis can cite the real signature."""
    out: dict[str, str] = {}
    for m in re.finditer(r"public\s+static\s+(?:ICardEffect|IActivatedCardEffect)\s+(\w+)\s*\(", text):
        name = m.group(1)
        # paren-match from the opening '(' to capture the full (possibly multi-line) param list.
        i = m.end() - 1
        depth = 0
        buf = []
        for ch in text[i:]:
            if ch == "(":
                depth += 1
                if depth == 1:
                    continue
            elif ch == ")":
                depth -= 1
                if depth == 0:
                    break
            buf.append(ch)
        params = re.sub(r"\s+", " ", "".join(buf)).strip()
        out.setdefault(name, params)
    return out


def gate_statics() -> dict[str, list[str]]:
    """public static method names of every *Gate.cs (query helpers like HasKeyword)."""
    out: dict[str, list[str]] = {}
    for path in sorted(GATES_DIR.glob("*Gate.cs")):
        text = path.read_text(encoding="utf-8", errors="ignore")
        cm = CLASS.search(text)
        if not cm:
            continue
        names = sorted(set(re.findall(r"public\s+static\s+[\w<>\[\],\.\?\s]+?\s(\w+)\s*\(", text)))
        if names:
            out[cm.group(1)] = names
    return out


# Curated §9 map: hallucinated name -> real form (BT2 re-measure). The validator suggests these.
KNOWN_HALLUCINATIONS = {
    "HasReboot": 'ContinuousKeywordGate.HasKeyword(card.Context, <id>, "Reboot")',
    "HasBlocker": 'ContinuousKeywordGate.HasKeyword(card.Context, <id>, "Blocker")',
    "GetTrashCount": '((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Trash).Count',
    "GetOpponentTrashCount": '((IZoneStateReader)card.Context.ZoneMover).GetCards(<opponent>, ChoiceZone.Trash).Count',
    "TopCardHasColor": 'card.HasCardColor("<Color>")',
    "IsOwnerOwnedDigimon": "x.Owner == card.Owner && x.IsDigimon",
    "PermanentId": "InstanceId",
    "IsMainPhase": "(no card-facing predicate — usually unnecessary; if truly needed this is a STOP)",
}


def main() -> None:
    text = FRAMEWORK.read_text(encoding="utf-8", errors="ignore")
    allow = {
        "CardEffectFactory": members_of(text, "CardEffectFactory"),
        "CardEffectCommons": members_of(text, "CardEffectCommons"),
        "CardSource": members_of(text, "CardSource"),
        "gates": gate_statics(),
        "factory_signatures": factory_signatures(text),
        "known_hallucinations": KNOWN_HALLUCINATIONS,
    }
    OUT.write_text(json.dumps(allow, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"allowlist -> {OUT}")
    print(f"  CardEffectFactory: {len(allow['CardEffectFactory'])}")
    print(f"  CardEffectCommons: {len(allow['CardEffectCommons'])}")
    print(f"  CardSource: {len(allow['CardSource'])}")
    print(f"  gates: {len(allow['gates'])} classes")


if __name__ == "__main__":
    main()
