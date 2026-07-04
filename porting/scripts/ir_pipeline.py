#!/usr/bin/env python3
"""IR 파이프라인 (pipeline-v3 stages 3·5·7·9, Phase A — 결정론, LLM 없음).

Source IR(porting/data/ir-src/<SET>.<COLOR>/<ID>.json, CardIr.Extract 산출) 를 받아:
  stage 3  로워링   — 심볼표(카탈로그)로 팩토리·술어를 헤드리스 어휘로 검증/치환
  stage 5  Canonical IR — 닫힌 어휘 IR (porting/data/ir/<SET>.<COLOR>/<ID>.json)
  stage 7  validator — 스키마·심볼·인자·커버리지
  stage 9  codegen   — Canonical IR → C# 미러 (기본은 staging diff, --write 시 교체)

Phase A 결정론 로워링 범위 (LLM 0):
  - 팩토리: 카탈로그 factory 표에 존재해야 함. 인자는 이름/위치로 시그니처에 매핑.
  - 값: const/int/string/null, ref card, ref <지역함수>.
  - 술어(지역 bool 함수): null 이거나, `CardEffectCommons.X(card)` (bool, card 인자만) 의
    &&/||/! 조합만 통과. 그 외(람다, 멤버 접근, id-형 술어, 미지 호출) 는 typed STOP.
STOP 은 카드/분기를 죽이지 않고 ledger 에 stage×code 로 기록 → 다음 작업 우선순위.

사용:
  python3 porting/scripts/ir_pipeline.py BT1 Blue            # SET+COLOR 전체
  python3 porting/scripts/ir_pipeline.py BT1 Blue BT1_031    # 특정 카드
  python3 porting/scripts/ir_pipeline.py --write BT1 Blue BT1_031   # 미러 교체까지
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
PORTING = Path(__file__).resolve().parents[1]
CATALOG = PORTING / "docs/PRIMITIVE-CATALOG.md"
IR_SRC = PORTING / "data/ir-src"
IR_CANON = PORTING / "data/ir"
GEN_DIR = PORTING / "data/gen"
MIRROR_ROOT = REPO / "src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect"

MIRROR_TEMPLATE = """\
using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.{set}.{color};

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// GENERATED FROM porting/data/ir/{set}.{color}/{card}.json — DO NOT EDIT (pipeline-v3 codegen).
public sealed class {card} : CEntity_Effect
{{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {{
        List<ICardEffect> cardEffects = new List<ICardEffect>();
{branches}
        return cardEffects;
    }}
}}
"""


# ---------- stage 3 support: symbol table from catalog ----------

def split_params(param_str: str) -> list[tuple[str, str]]:
    """'int changeValue, Func<Permanent, bool>? condition' -> [(type, name), ...]
    Bracket-depth-aware split (Func<Permanent, bool> contains a comma)."""
    parts, depth, cur = [], 0, ""
    for ch in param_str:
        if ch in "<([":
            depth += 1
        elif ch in ">)]":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append(cur)
            cur = ""
        else:
            cur += ch
    if cur.strip():
        parts.append(cur)
    out = []
    for p in parts:
        p = p.strip()
        if not p:
            continue
        # strip default value
        p = p.split("=", 1)[0].strip()
        # last token = name, rest = type
        m = re.match(r"^(.*\S)\s+(\w+)$", p)
        if m:
            out.append((m.group(1).strip(), m.group(2)))
    return out


def load_symbols() -> dict[str, dict]:
    """Parse both master tables of PRIMITIVE-CATALOG.md into {name: {kind, ret, params}}."""
    text = CATALOG.read_text(encoding="utf-8").splitlines()
    symbols: dict[str, dict] = {}
    section = None
    for line in text:
        if line.startswith("## "):
            if line.startswith("## 알파벳 마스터"):
                section = "factory"
            elif line.startswith("## CardEffectCommons 헬퍼 마스터"):
                section = "commons"
            else:
                section = None
            continue
        if section is None:
            continue
        m = re.match(r"\|\s*`(\w+)`\s*\|\s*`?([^|`]+?)`?\s*\|\s*`(.+?)`\s*\|", line)
        if not m:
            continue
        name, ret, sig = m.group(1), m.group(2).strip(), m.group(3)
        sm = re.match(r"[\w<>,?\[\]. ]+?\s+" + re.escape(name) + r"\s*\((.*)\)$", sig)
        params = split_params(sm.group(1)) if sm else []
        # first definition wins (factory table precedes commons); keep both keyed by kind
        symbols.setdefault(name, {"kind": section, "ret": ret, "params": params, "sig": sig})
    return symbols


# ---------- stage 3/5: lowering Source IR -> Canonical IR (or typed STOP) ----------

class Stop(Exception):
    def __init__(self, stage: str, code: str, detail: str):
        self.stage, self.code, self.detail = stage, code, detail
        super().__init__(f"{stage} / {code}: {detail}")


def lower_predicate(node: dict) -> dict:
    """Phase A: null | &&/||/! combination of CardEffectCommons.X(card) bool calls."""
    if node is None:
        return {"const": True}
    if "const" in node:
        return {"const": bool(node["const"])}
    if "not" in node:
        return {"not": lower_predicate(node["not"])}
    if node.get("binop") in ("&&", "||"):
        return {"binop": node["binop"],
                "lhs": lower_predicate(node["lhs"]),
                "rhs": lower_predicate(node["rhs"])}
    if "call" in node:
        name = node["call"]
        if not name.startswith("CardEffectCommons."):
            raise Stop("lowering:missing-rule", "STOP_MISSING_PRIMITIVE",
                       f"predicate call not a CardEffectCommons helper: {name}")
        short = name.split(".", 1)[1]
        sym = SYMBOLS.get(short)
        if sym is None or sym["kind"] != "commons":
            raise Stop("lowering:missing-op", "STOP_MISSING_PRIMITIVE",
                       f"unknown commons predicate: {short}")
        args = node.get("args", [])
        # Phase A: only card-only predicates pass through unchanged.
        if not all(a.get("ref") == "card" for a in args):
            raise Stop("lowering:missing-rule", "STOP_MULTI_STEP_OPTIONAL",
                       f"{short} takes non-card args (needs lowering rule): {args}")
        return {"call": name, "args": [{"ref": "card"} for _ in args]}
    # lambda / member / id-form predicate — beyond deterministic Phase A.
    if "lambda" in node:
        raise Stop("lowering:missing-rule", "STOP_MULTI_STEP_OPTIONAL",
                   "predicate lambda needs member→commons id-rewrite rule")
    if "member" in node:
        raise Stop("lowering:missing-rule", "STOP_MISSING_PRIMITIVE",
                   f"predicate member access needs rewrite rule: {node['member']}")
    raise Stop("lowering:tier-3", "STOP_RULE_AMBIGUOUS", f"unrecognized predicate node: {node}")


def lower_value(node: dict, local_fns: dict) -> dict:
    if "const" in node or "lit" in node or "null" in node:
        return node
    if "ref" in node:
        r = node["ref"]
        if r == "card":
            return node
        if r in local_fns:
            return {"localfn": r}
        raise Stop("lowering:tier-3", "STOP_RULE_AMBIGUOUS", f"unknown value ref: {r}")
    raise Stop("lowering:tier-3", "STOP_RULE_AMBIGUOUS", f"unrecognized value node: {node}")


def lower_card(src: dict) -> tuple[dict, list[dict]]:
    ledger: list[dict] = []
    canon = {"schema": "canonical-ir/1", "card": src["card"], "set": src["set"],
             "color": src["color"], "branches": []}
    for bi, br in enumerate(src["branches"]):
        timing = br["timing"]
        local_fns = {fn["name"]: fn for fn in br.get("localFns", [])}
        try:
            # 1. Effects first — a coroutine (ActivateClass) branch has opaque effects and its
            #    string-returning EffectDiscription() must NOT be mis-read as a predicate. Detect
            #    the coroutine shape here so it classifies cleanly (STOP_COMPLEX_TIMING) instead of
            #    dumping card-text into the ledger via failed predicate lowering.
            if any(eff.get("kind") != "factoryAdd" for eff in br.get("effects", [])):
                raise Stop("lowering:tier-3", "STOP_COMPLEX_TIMING",
                           "coroutine/ActivateClass or non-factory effect (declarative translation needed)")
            # 2. Local functions (predicates) — bool-returning only.
            lowered_fns = {}
            for fn in br.get("localFns", []):
                if fn.get("returns") not in (None, "bool"):
                    raise Stop("lowering:tier-3", "STOP_COMPLEX_TIMING",
                               f"non-bool local fn {fn['name']} ({fn.get('returns')}) — coroutine shape")
                if fn.get("params"):
                    raise Stop("lowering:missing-rule", "STOP_MULTI_STEP_OPTIONAL",
                               f"predicate {fn['name']} takes params (Permanent/id) — needs id-rewrite rule")
                lowered_fns[fn["name"]] = {"name": fn["name"], "body": lower_predicate(fn["body"])}
            # 3. Effects (now known all-factory).
            lowered_effects = []
            for eff in br.get("effects", []):
                fname = eff["factory"]
                sym = SYMBOLS.get(fname)
                if sym is None or sym["kind"] != "factory":
                    raise Stop("lowering:missing-op", "STOP_MISSING_PRIMITIVE",
                               f"unknown factory: {fname}")
                # map args by name (or position) to signature
                sig_params = sym["params"]
                out_args = []
                for i, a in enumerate(eff.get("args", [])):
                    pname = a.get("name") or (sig_params[i][1] if i < len(sig_params) else None)
                    if pname is None:
                        raise Stop("validator:symbol", "STOP_MISSING_PRIMITIVE",
                                   f"{fname}: arg {i} has no name and no signature slot")
                    out_args.append({"name": pname, "value": lower_value(a["value"], local_fns)})
                lowered_effects.append({"factory": fname, "args": out_args})
            canon["branches"].append({"timing": timing, "localFns": list(lowered_fns.values()),
                                      "effects": lowered_effects})
            ledger.append({"card": src["card"], "branch": bi, "timing": timing, "status": "lowered"})
        except Stop as s:
            canon["branches"].append({"timing": timing, "stop": {"stage": s.stage, "code": s.code,
                                                                 "detail": s.detail}})
            ledger.append({"card": src["card"], "branch": bi, "timing": timing,
                           "status": "stop", "stage": s.stage, "code": s.code, "detail": s.detail})
    return canon, ledger


# ---------- stage 7: validator ----------

def validate(canon: dict) -> list[str]:
    issues = []
    if canon.get("schema") != "canonical-ir/1":
        issues.append("bad schema")
    for br in canon["branches"]:
        if "stop" in br:
            continue  # typed STOP is a legitimate (non-lowered) branch state
        for eff in br["effects"]:
            sym = SYMBOLS.get(eff["factory"])
            if sym is None or sym["kind"] != "factory":
                issues.append(f"{br['timing']}: factory {eff['factory']} not in symbol table")
                continue
            need = {p[1] for p in sym["params"]}
            got = {a["name"] for a in eff["args"]}
            missing = need - got
            if missing:
                issues.append(f"{br['timing']}: {eff['factory']} missing args {missing}")
    return issues


def card_fully_lowered(canon: dict) -> bool:
    return canon["branches"] and all("stop" not in br for br in canon["branches"])


# ---------- stage 9: codegen ----------

def emit_value(v: dict) -> str:
    if "const" in v:
        return "true" if v["const"] else "false"
    if "lit" in v:
        return str(v["lit"]) if isinstance(v["lit"], int) else f"\"{v['lit']}\""
    if "null" in v:
        return "null"
    if "ref" in v:
        return v["ref"]
    if "localfn" in v:
        return v["localfn"]
    raise ValueError(f"cannot emit value {v}")


def emit_predicate(n: dict) -> str:
    if "const" in n:
        return "true" if n["const"] else "false"
    if "not" in n:
        return f"!({emit_predicate(n['not'])})"
    if n.get("binop") in ("&&", "||"):
        return f"({emit_predicate(n['lhs'])} {n['binop']} {emit_predicate(n['rhs'])})"
    if "call" in n:
        args = ", ".join(emit_value(a) for a in n.get("args", []))
        return f"{n['call']}({args})"
    raise ValueError(f"cannot emit predicate {n}")


def codegen(canon: dict) -> str:
    blocks = []
    for br in canon["branches"]:
        if "stop" in br:
            s = br["stop"]
            blocks.append(f"        if (timing == EffectTiming.{br['timing']})\n        {{\n"
                          f"            // STOP: {s['code']} ({s['stage']}) — {s['detail']} — 강모델\n        }}")
            continue
        lines = [f"        if (timing == EffectTiming.{br['timing']})", "        {"]
        for fn in br.get("localFns", []):
            lines.append(f"            bool {fn['name']}()")
            lines.append("            {")
            lines.append(f"                return {emit_predicate(fn['body'])};")
            lines.append("            }")
            lines.append("")
        for eff in br["effects"]:
            args = ",\n".join(f"                {a['name']}: {emit_value(a['value'])}" for a in eff["args"])
            lines.append(f"            cardEffects.Add(CardEffectFactory.{eff['factory']}(")
            lines.append(args + "));")
        lines.append("        }")
        blocks.append("\n".join(lines))
    return MIRROR_TEMPLATE.format(set=canon["set"], color=canon["color"], card=canon["card"],
                                  branches="\n\n".join(blocks))


# ---------- driver ----------

SYMBOLS: dict[str, dict] = {}


def main() -> int:
    global SYMBOLS
    args = sys.argv[1:]
    write = False
    if args and args[0] == "--write":
        write = True
        args = args[1:]
    if len(args) < 2:
        print(__doc__)
        return 1
    card_set, color = args[0], args[1]
    only = set(args[2:])

    SYMBOLS = load_symbols()
    src_dir = IR_SRC / f"{card_set}.{color}"
    if not src_dir.is_dir():
        print(f"Source IR 없음: {src_dir} — 먼저 CardIr.Extract 실행", file=sys.stderr)
        return 1

    all_ledger = []
    lowered_cards, stop_cards = [], []
    for f in sorted(src_dir.glob("*.json")):
        cid = f.stem
        if only and cid not in only:
            continue
        src = json.loads(f.read_text(encoding="utf-8"))
        canon, ledger = lower_card(src)
        all_ledger.extend(ledger)
        issues = validate(canon)
        if issues:
            print(f"{cid}: VALIDATOR FAIL — {issues}")
            stop_cards.append(cid)
            continue
        # persist canonical IR
        out = IR_CANON / f"{card_set}.{color}"
        out.mkdir(parents=True, exist_ok=True)
        (out / f"{cid}.json").write_text(json.dumps(canon, ensure_ascii=False, indent=2), encoding="utf-8")

        if card_fully_lowered(canon):
            cs = codegen(canon)
            GEN_DIR.mkdir(parents=True, exist_ok=True)
            gen_path = GEN_DIR / f"{cid}.cs"
            gen_path.write_text(cs, encoding="utf-8")
            lowered_cards.append(cid)
            if write:
                (MIRROR_ROOT / card_set / color / f"{cid}.cs").write_text(cs, encoding="utf-8")
                print(f"{cid}: lowered → mirror WRITTEN")
            else:
                print(f"{cid}: lowered → {gen_path.relative_to(REPO)} (staging; --write 로 교체)")
        else:
            stops = [b["stop"]["code"] for b in canon["branches"] if "stop" in b]
            stop_cards.append(cid)
            print(f"{cid}: STOP branches {stops}")

    # ledger summary
    if all_ledger:
        led_dir = PORTING / "data/ledger"
        led_dir.mkdir(parents=True, exist_ok=True)
        (led_dir / f"{card_set}.{color}.json").write_text(
            json.dumps(all_ledger, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\n== {card_set} {color}: {len(lowered_cards)} lowered, {len(stop_cards)} with STOP ==")
    print(f"   lowered: {lowered_cards}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
