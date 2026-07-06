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


def _paren_params(text: str, open_idx: int) -> str:
    """text[open_idx]가 '('일 때, 대응 ')'까지의 파라미터 목록을 collapsed로 반환."""
    depth = 0
    buf: list[str] = []
    for ch in text[open_idx:]:
        if ch == "(":
            depth += 1
            if depth == 1:
                continue
        elif ch == ")":
            depth -= 1
            if depth == 0:
                break
        buf.append(ch)
    return re.sub(r"\s+", " ", "".join(buf)).strip()


def _class_body(text: str, class_name: str) -> str:
    """class_name의 본문 소스 슬라이스(brace 추적)."""
    lines = text.splitlines()
    i, n = 0, len(lines)
    while i < n:
        m = CLASS.search(lines[i])
        if m and m.group(1) == class_name:
            depth, opened, j, body = 0, False, i, []
            while j < n:
                depth += lines[j].count("{") - lines[j].count("}")
                if "{" in lines[j]:
                    opened = True
                body.append(lines[j])
                if opened and depth == 0:
                    break
                j += 1
            return "\n".join(body)
        i += 1
    return ""


def commons_signatures(text: str) -> dict[str, str]:
    """CardEffectCommons public static 메서드 name -> params. gemma 진단이 커먼즈 메서드(CanTriggerOnPlay 등)의
    진짜 시그니처를 인용할 수 있게 한다(팩토리만으론 부족)."""
    body = _class_body(text, "CardEffectCommons")
    out: dict[str, str] = {}
    for m in re.finditer(r"public\s+static\s+[\w<>\[\],\.\?]+\s+(\w+)\s*\(", body):
        out.setdefault(m.group(1), _paren_params(body, m.end() - 1))
    return out


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


def enum_values() -> dict[str, list[str]]:
    """enum type name -> its member names, for the factory-arg enums the model tends to write bare
    (ChoiceZone.Trash written as 'Trash' etc.). Scanned from the engine so it stays in sync."""
    out: dict[str, list[str]] = {}
    roots = [
        ROOT / "src/HeadlessDCGO.Engine/Headless/Runtime",
        ROOT / "src/HeadlessDCGO.Engine/Headless/Choices",
        ROOT / "src/HeadlessDCGO.Engine/Headless/Effects",
        ROOT / "src/HeadlessDCGO.Engine/Headless/Services",
        ROOT / "src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons",
    ]
    seen: set[str] = set()
    for root in roots:
        if not root.exists():
            continue
        for path in root.rglob("*.cs"):
            text = path.read_text(encoding="utf-8", errors="ignore")
            for m in re.finditer(r"\benum\s+(\w+)\s*\{(.*?)\}", text, re.S):
                name = m.group(1)
                if name in seen:
                    continue
                members = re.findall(r"\b([A-Z]\w*)\b", m.group(2).split("//")[0] if "//" not in m.group(2) else m.group(2))
                # clean: split lines, take the leading identifier before '=' or ','
                vals = []
                for line in m.group(2).splitlines():
                    mm = re.match(r"\s*([A-Za-z_]\w*)\s*(?:=|,|$)", line.split("//")[0])
                    if mm:
                        vals.append(mm.group(1))
                if vals:
                    out[name] = vals
                    seen.add(name)
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
    # Class mis-reference (BT2 cold): factory wrongly nested under CardEffectCommons.
    "CardEffectFactory": "CardEffectFactory is a SEPARATE static class — call CardEffectFactory.<Method> directly, NOT CardEffectCommons.CardEffectFactory",
    "ActivateClass": "ActivateClass is an AS-IS class name with no headless equivalent. For an activated effect, return the matching factory (DrawCardsEffect / SelectAndDestroyEffect / ...) at its timing; the bridge resolves it (cheatsheet section 7)",
}


def main() -> None:
    text = FRAMEWORK.read_text(encoding="utf-8", errors="ignore")
    allow = {
        "CardEffectFactory": members_of(text, "CardEffectFactory"),
        "CardEffectCommons": members_of(text, "CardEffectCommons"),
        "CardSource": members_of(text, "CardSource"),
        "gates": gate_statics(),
        "enums": enum_values(),
        "factory_signatures": factory_signatures(text),
        "commons_signatures": commons_signatures(text),
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
