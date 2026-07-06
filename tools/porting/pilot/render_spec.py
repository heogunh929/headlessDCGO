#!/usr/bin/env python3
"""Deterministic renderer: a card SPEC (hybrid schema) -> exact headless C#.

The LLM emits only a spec (which timing, which factory, which arg values, which predicate
expressions). This renderer fills the EXACT factory signature (arg order/types), the usings,
namespace, class skeleton, and the `card.` convention — deterministically, from allowlist.json
(generated from the real engine source). This eliminates the mechanical error classes the model
kept producing (wrong signature, missing arg, missing using, CardEffectCommons-vs-card, code
fences) because the model never writes those parts.

Spec shape:
  {
    "namespace": "HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue",
    "className": "BT2_030",
    "usings": ["HeadlessDCGO.Engine.Headless.Runtime"],   # optional extras beyond the defaults
    "effects": [
      { "timing": "OnEnterFieldAnyone", "factory": "SelectAndBounceEffect",
        "args": { "canTarget": "<predicate expr>", "maxCount": 2, "canEndNotMax": true, "description": "..." } },
      ...
    ]
  }

Rigid slots (timing/factory/scalars) are validated + placed exactly. Expressive slots (predicates
for Func<...> params) are the model's C# expression, wrapped as a lambda of the correct arity.
`CardSource card` params are auto-filled with the method's `card`.

Usage: python3 render_spec.py spec.json   # prints the .cs to stdout
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ALLOWLIST = Path(__file__).resolve().parent / "allowlist.json"
DEFAULT_USINGS = [
    "HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons",
    "HeadlessDCGO.Engine.Headless.Services",
]


def _split_top_level(params: str) -> list[str]:
    """Split a param list on top-level commas (ignoring commas inside <> or ())."""
    out, depth, cur = [], 0, []
    for ch in params:
        if ch in "<(":
            depth += 1
        elif ch in ">)":
            depth -= 1
        if ch == "," and depth == 0:
            out.append("".join(cur).strip())
            cur = []
        else:
            cur.append(ch)
    if "".join(cur).strip():
        out.append("".join(cur).strip())
    return out


def _parse_params(sig: str) -> list[tuple[str, str]]:
    """(type, name) for each param; name is the last identifier, type is the rest."""
    result = []
    for p in _split_top_level(sig):
        if not p:
            continue
        m = re.match(r"^(.*?)(\b\w+)\s*(=.*)?$", p.strip())
        if m:
            result.append((m.group(1).strip(), m.group(2)))
    return result


# lambda variable name per single-arg predicate type (so the model's expression can reference it).
FUNC_VARS = {"HeadlessEntityId": "id", "Permanent": "p", "CardSource": "cs"}
_TARGET_VARS = ("id", "p", "cs")


def _render_func(base: str, pname: str, expr: str) -> str:
    """Wrap a predicate expression as a lambda of the correct arity/var, and validate arity.
    Func<bool> -> () => (...) and MUST NOT reference a target var; Func<X,bool> -> <var> => (...)."""
    inner = base[len("Func<"):].rsplit(">", 1)[0]
    argtypes = [t.strip() for t in _split_top_level(inner)]
    if len(argtypes) <= 1:  # Func<bool> / Func<int> — no target arg
        for v in _TARGET_VARS:
            if re.search(rf"\b{v}\b", expr):
                raise ValueError(
                    f"param '{pname}' is {base} (no target argument) — its expression must NOT reference "
                    f"'{v}'. Use only 'card' / global state (e.g. CardEffectCommons.IsOwnerTurn(card)).")
        return f"() => ({expr})"
    vtype = argtypes[0]
    var = FUNC_VARS.get(vtype, "x")
    # a single-arg predicate must reference its own var, not a different target var.
    for v in _TARGET_VARS:
        if v != var and re.search(rf"\b{v}\b", expr):
            raise ValueError(
                f"param '{pname}' is {base}; its lambda variable is '{var}' (a {vtype}), not '{v}'. "
                f"Rewrite the expression using '{var}'.")
    return f"{var} => ({expr})"


def _render_arg(ptype: str, pname: str, args: dict) -> str | None:
    """Render one argument to C# source. Returns None to omit an optional arg not provided."""
    base = ptype.rstrip("?").strip()
    if base == "CardSource":
        return "card"  # method param, always available
    provided = pname in args
    if not provided:
        return None if ptype.endswith("?") else "null"
    val = args[pname]
    if base.startswith("Func<"):
        return _render_func(base, pname, str(val))
    if base == "int":
        # a plain literal, or a C# int EXPRESSION (e.g. Math.Min(2, Commons.Count(...))) — pass verbatim.
        if isinstance(val, bool):
            return str(int(val))
        if isinstance(val, (int, float)):
            return str(int(val))
        s = str(val).strip()
        return s if not re.fullmatch(r"-?\d+", s) else str(int(s))
    if base == "bool":
        if isinstance(val, bool):
            return "true" if val else "false"
        return str(val)  # a bool expression string
    if base == "string":
        return json.dumps(str(val))  # C# string literal
    # enums / other: pass verbatim
    return str(val)


def render(spec: dict, allow: dict) -> str:
    sigs = allow.get("factory_signatures", {})
    factories = set(allow.get("CardEffectFactory", []))

    usings = list(dict.fromkeys(DEFAULT_USINGS + list(spec.get("usings", []))))
    lines = [f"namespace {spec['namespace']};", ""]
    lines += [f"using {u};" for u in usings]
    lines += ["", f"public sealed class {spec['className']} : CEntity_Effect", "{",
              "    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)",
              "    {", "        var effects = new List<ICardEffect>();"]

    # group effects by timing, preserving order
    by_timing: dict[str, list[dict]] = {}
    for eff in spec["effects"]:
        by_timing.setdefault(eff["timing"], []).append(eff)

    for timing, effs in by_timing.items():
        lines.append(f"        if (timing == EffectTiming.{timing})")
        lines.append("        {")
        for eff in effs:
            fac = eff["factory"]
            if fac not in factories:
                raise ValueError(f"unknown factory: {fac} (not in allowlist)")
            params = _parse_params(sigs.get(fac, ""))
            rendered = [_render_arg(t, n, eff.get("args", {})) for t, n in params]
            call_args = ", ".join(a for a in rendered if a is not None)
            lines.append(f"            effects.Add(CardEffectFactory.{fac}({call_args}));")
        lines.append("        }")

    lines += ["        return effects;", "    }", "}", ""]
    return "\n".join(lines)


def main() -> None:
    if len(sys.argv) < 2:
        print("usage: render_spec.py <spec.json>", file=sys.stderr)
        sys.exit(2)
    spec = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    allow = json.loads(ALLOWLIST.read_text(encoding="utf-8"))
    print(render(spec, allow))


if __name__ == "__main__":
    main()
