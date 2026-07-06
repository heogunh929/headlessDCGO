#!/usr/bin/env python3
"""Structured-output porting: the model emits a JSON SPEC, the deterministic renderer makes the C#.

Instead of asking the local model for free-form headless C# (where it hallucinates signatures /
namespaces / the card.X convention), we ask it for a small SPEC (which timing, which factory, which
arg values, which predicate expressions). render_spec.py then renders exact C#. The model only does
the semantic mapping (its strength); the mechanical C# is deterministic (its weakness removed).

Usage:
  LOCAL_LLM_BASE_URL=... CODER_MODEL=... python3 tools/porting/pilot/port_via_spec.py BT2_030 [BT2_028 ...]
"""
from __future__ import annotations

import json
import re
import sqlite3
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from model_router import LocalModelRouter  # noqa: E402
from port_with_local import compile_gate, _TIMINGS, DB  # noqa: E402
from render_spec import render  # noqa: E402
from validate_port import validate, load_allowlist  # noqa: E402

ROOT = Path(__file__).resolve().parents[3]
DCGO = ROOT / "DCGO" / "Assets" / "Scripts" / "CardEffect"
ALLOW = load_allowlist()
ACTION_MAP = json.loads((Path(__file__).resolve().parent / "action_map.json").read_text(encoding="utf-8"))

SPEC_SYSTEM = """You convert a Digimon card's AS-IS (Unity C#) into a SPEC (JSON) that a deterministic renderer turns into headless C#.
Output ONLY the JSON spec inside a ```json code block. No C#, no prose, no comments.

Schema:
{
  "namespace": "<given>",
  "className": "<given>",
  "effects": [ { "timing": "<EffectTiming>", "factory": "<factory>", "args": { "<paramName>": <value> } } ]
}

Rules:
- One "effects" entry per (timing, factory). If the AS-IS has N effects across timings, produce N entries. Match the AS-IS effects exactly.
- "timing": exactly one valid EffectTiming from the list below.
- "factory": exactly one factory from the Available factories list below. Pick the one whose meaning AND signature match the AS-IS effect.
- "args": give each factory parameter BY NAME. OMIT the CardSource 'card' parameter (auto-filled).
  * int / bool / string params: a literal value.
  * predicate params: a C# boolean EXPRESSION string (do NOT write the lambda arrow — the renderer adds it).
      The lambda variable depends on the param's Func TYPE in the signature — write the expression using EXACTLY that variable:
        - Func<bool>                  -> NO target variable. Use only `card` / global state. e.g. CardEffectCommons.IsOwnerTurn(card)
        - Func<HeadlessEntityId,bool> -> target var is `id`.  Query it: CardEffectCommons.<Predicate>(card, id). e.g. CardEffectCommons.LevelOf(card, id) <= 4
        - Func<Permanent,bool>        -> target var is `p`.   Query it: CardEffectCommons.<Predicate>(p, card). (permanent FIRST)
        - Func<CardSource,bool>       -> target var is `cs`.  Query it: CardEffectCommons.<Predicate>(cs, card) or cs.<Property>
      `card` always = THIS effect's source card (self). NEVER use a target variable that doesn't match the param's Func type.
      THIS card's own property (its color/level/owner) -> card.<Property>. Do NOT use card.TopCard / card.Level to mean the target.
- Do NOT invent factories or predicates outside the lists below.
- Preserve the AS-IS condition faithfully — never blur a compound predicate into a single coarse check.
"""


def factory_context(action_tags: list[str]) -> str:
    sigs = ALLOW["factory_signatures"]
    facs: set[str] = set()
    for t in action_tags:
        e = ACTION_MAP.get(t, {})
        if e.get("factory"):
            facs.add(e["factory"])
        facs.update(e.get("also", []))
    facs.update(f for f in ALLOW["CardEffectFactory"]
                if f.startswith("SelectAnd") or f in ("DrawCardsEffect",) or f.startswith("CanNot"))
    return "\n".join(f"- {f}({sigs[f]})" for f in sorted(facs) if f in sigs)


def commons_context() -> str:
    comm = [c for c in ALLOW["CardEffectCommons"]
            if c.startswith(("Is", "Has", "Level", "Match", "CanTrigger")) or "Count" in c]
    return ", ".join(sorted(comm))


def build_user(card_id: str, ns: str, asis: str, tags: list[str], last_err: str) -> str:
    parts = [
        f"Valid EffectTiming: {', '.join(_TIMINGS)}",
        f"\nAvailable factories (name + signature):\n{factory_context(tags)}",
        f"\nAvailable CardEffectCommons predicates (for predicate slots):\n{commons_context()}",
        f"\nnamespace = {ns}\nclassName = {card_id}",
        f"\n## Target AS-IS\n{asis}",
    ]
    if last_err:
        parts.append(f"\n## Previous attempt failed — fix and re-output the JSON spec\n{last_err[-1200:]}")
    parts.append("\nOutput the JSON spec:")
    return "\n".join(parts)


def extract_json(text: str) -> dict:
    m = re.search(r"```(?:json)?\s*(\{.*\})\s*```", text, re.S)
    blob = m.group(1) if m else text
    s, e = blob.find("{"), blob.rfind("}")
    return json.loads(blob[s:e + 1])


def port_card(card_id: str, retries: int = 3) -> dict:
    conn = sqlite3.connect(str(DB))
    row = conn.execute("SELECT source_path, action_tags FROM card WHERE card_id=?", (card_id,)).fetchone()
    rel, tags = row[0], json.loads(row[1] or "[]")
    asis = (DCGO / rel).read_text(encoding="utf-8", errors="ignore")
    ns = "HeadlessDCGO.Engine.Assets.Scripts.CardEffect." + str(Path(rel).parent).replace("/", ".")

    router = LocalModelRouter()
    last_err = ""
    for attempt in range(1, retries + 2):
        raw = router.call("coder", SPEC_SYSTEM, build_user(card_id, ns, asis, tags, last_err), extract_code=False)
        try:
            spec = extract_json(raw)
        except Exception as ex:  # noqa: BLE001
            last_err = f"JSON parse error: {ex}. Output ONLY a valid JSON spec in a ```json block."
            continue
        try:
            cs = render(spec, ALLOW)
        except Exception as ex:  # noqa: BLE001
            last_err = f"Render error: {ex}. Use only listed factories and correct param names."
            continue
        findings = validate(cs, ALLOW)
        if findings:
            last_err = "Invalid symbols:\n" + "\n".join(f"- {f['symbol']}: {f['suggestion']}" for f in findings)
            continue
        ok, detail = compile_gate(cs, card_id, rel, keep_on_pass=False)
        if ok:
            return {"card_id": card_id, "ok": True, "attempts": attempt, "spec": spec}
        last_err = detail
    return {"card_id": card_id, "ok": False, "attempts": retries + 1, "last_err": last_err[-500:]}


def main() -> None:
    for cid in sys.argv[1:] or ["BT2_030"]:
        rec = port_card(cid)
        print(f"=== {cid}: {'PASS' if rec['ok'] else 'FAIL'} | attempts={rec['attempts']} ===", flush=True)
        if rec["ok"]:
            print("  spec effects:", [(e["timing"], e["factory"]) for e in rec["spec"]["effects"]], flush=True)
        else:
            print("  last_err:", rec["last_err"], flush=True)


if __name__ == "__main__":
    main()
