#!/usr/bin/env python3
"""Fast pre-build validator for a ported card .cs.

Catches the dominant local-model failure class (BT2 re-measure) BEFORE the expensive
engine build: references to symbols that DO NOT EXIST on the known headless surfaces
(CardEffectFactory / CardEffectCommons / query gates / CardSource). Emits a precise,
corrective message (with the cheatsheet §9 mapping when the bad name is a known
hallucination) so the compile-fix retry has a far better signal than a raw compiler error.

Only flags HIGH-CONFIDENCE cases (member access on a statically-known class, or on a
variable whose CardSource type is visible in the same file) — never guesses, so a PASS
here does not replace the build; a FAIL is a definite error the build would also reject.

Usage:
  python3 validate_port.py path/to/Card.cs           # human output, exit 1 on findings
  python3 validate_port.py --json path/to/Card.cs     # JSON {ok, findings:[...]}
  cat Card.cs | python3 validate_port.py -             # read from stdin
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ALLOWLIST = Path(__file__).resolve().parent / "allowlist.json"

# `<Class>.<Member>` where <Class> is one we have an allowlist for.
STATIC_REF = re.compile(r"\b(CardEffectFactory|CardEffectCommons)\.(\w+)")
GATE_REF = re.compile(r"\b(\w+Gate)\.(\w+)")
# CardSource-typed local variables declared in the file (so `<var>.<Member>` is checkable).
CS_DECL = re.compile(r"\bCardSource\s+(\w+)\b")


def load_allowlist() -> dict:
    return json.loads(ALLOWLIST.read_text(encoding="utf-8"))


def validate(text: str, allow: dict) -> list[dict]:
    findings: list[dict] = []
    known = allow.get("known_hallucinations", {})

    def hint(name: str) -> str | None:
        return known.get(name)

    # CardSource variable names: the conventional `card` param + any declared CardSource var.
    cs_vars = {"card"} | set(CS_DECL.findall(text))
    cs_members = set(allow.get("CardSource", []))

    for cls, member in STATIC_REF.findall(text):
        if member not in set(allow.get(cls, [])):
            # 재발 패턴: CardSource 멤버(HasCardColor/CardNames/Level…)를 CardEffectCommons/Factory 하위로 오참조.
            if member in cs_members:
                sugg = f"이건 CardSource 멤버다 — {cls}.{member}가 아니라 card.{member} 로 호출"
            else:
                sugg = hint(member) or f"{cls}에 그런 멤버 없음 — PRIMITIVE-CATALOG.md / cheatsheet §9 확인"
            findings.append({
                "kind": "unknown-symbol", "symbol": f"{cls}.{member}",
                "message": f"'{cls}.{member}' 는 존재하지 않는다.",
                "suggestion": sugg,
            })

    gates = allow.get("gates", {})
    for gate, member in GATE_REF.findall(text):
        if gate in gates and member not in set(gates[gate]):
            findings.append({
                "kind": "unknown-symbol", "symbol": f"{gate}.{member}",
                "message": f"'{gate}.{member}' 는 존재하지 않는다.",
                "suggestion": hint(member) or f"{gate} 유효 멤버: {', '.join(gates[gate][:6])}",
            })

    # CardSource member access on a known CardSource variable.
    for var in cs_vars:
        for member in re.findall(rf"\b{re.escape(var)}\.(\w+)\s*[\(\.\)\s;,=]", text):
            if member and member not in cs_members and member[0].isupper():
                findings.append({
                    "kind": "unknown-member", "symbol": f"{var}.{member} (CardSource)",
                    "message": f"CardSource 에 '{member}' 멤버 없음.",
                    "suggestion": hint(member) or "CardSource 유효 멤버는 allowlist.json 참조",
                })

    # de-dup by symbol
    seen, uniq = set(), []
    for f in findings:
        if f["symbol"] not in seen:
            seen.add(f["symbol"])
            uniq.append(f)
    return uniq


def main() -> None:
    args = sys.argv[1:]
    as_json = "--json" in args
    args = [a for a in args if a != "--json"]
    if not args:
        print("usage: validate_port.py [--json] <file.cs|->", file=sys.stderr)
        sys.exit(2)
    src = sys.stdin.read() if args[0] == "-" else Path(args[0]).read_text(encoding="utf-8", errors="ignore")

    findings = validate(src, load_allowlist())
    if as_json:
        print(json.dumps({"ok": not findings, "findings": findings}, ensure_ascii=False, indent=2))
    else:
        if not findings:
            print("VALIDATE OK — 알려진 무효 심볼 없음")
        else:
            print(f"VALIDATE FAIL — {len(findings)}건")
            for f in findings:
                print(f"  ✗ {f['symbol']}\n      → {f['suggestion']}")
    sys.exit(1 if findings else 0)


if __name__ == "__main__":
    main()
