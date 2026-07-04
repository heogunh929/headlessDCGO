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
using HeadlessDCGO.Engine.Headless.Effects;

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
        optional = "=" in p or p.split()[0] == "params"   # has default value → optional
        p = p.split("=", 1)[0].strip()
        # last token = name, rest = type
        m = re.match(r"^(.*\S)\s+(\w+)$", p)
        if m:
            out.append((m.group(1).strip(), m.group(2), optional))
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
    # comparison (Phase C): <member/atom> <op> <literal>
    if node.get("binop") in (">=", "<=", ">", "<", "==", "!="):
        return {"cmp": node["binop"],
                "lhs": lower_predicate(node["lhs"]),
                "rhs": lower_operand(node["rhs"])}
    if "lit" in node:
        return {"lit": node["lit"]}
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
        # id-lambda predicates: HasMatchConditionOpponentsPermanent(card, id => <pred over id>)
        # The 2nd arg is Func<HeadlessEntityId,bool>; lower the lambda, rewriting `id.X` member
        # accesses to CardEffectCommons.X(card, id) for exact-match permanent-subject helpers.
        if short in ID_LAMBDA_CALLS and len(args) == 2 and args[0].get("ref") == "card" \
                and "lambda" in args[1]:
            lam = args[1]["lambda"]
            param = lam["params"][0]
            body = lower_perm_lambda(lam["body"], param)
            return {"call": name, "args": [{"ref": "card"},
                                           {"lambda": {"param": "id", "body": body, "orig": param}}]}
        # card-only predicates pass through unchanged.
        if not all(a.get("ref") == "card" for a in args):
            raise Stop("lowering:missing-rule", "STOP_MULTI_STEP_OPTIONAL",
                       f"{short} takes non-card args (needs lowering rule): {args}")
        return {"call": name, "args": [{"ref": "card"} for _ in args]}
    # base atom rewrite (Phase C): self-scoped member access → CardEffectCommons.<emit>(card).
    #   member  "card.Owner.MemoryForPlayer"                 -> trailing name = atom
    #   memberOf .HasPierce on call card.PermanentOfThisCard() -> node["name"] = atom
    if "member" in node:
        return lower_self_atom(node["member"].split(".")[-1], node["member"].split(".")[0] == "card",
                               node["member"])
    if "memberOf" in node:
        return lower_self_atom(node["name"], roots_at_card(node["memberOf"]), node)
    if "lambda" in node:
        raise Stop("lowering:missing-rule", "STOP_MULTI_STEP_OPTIONAL",
                   "predicate lambda needs member→commons id-rewrite rule (Permanent/id subject)")
    raise Stop("lowering:tier-3", "STOP_RULE_AMBIGUOUS", f"unrecognized predicate node: {node}")


def lower_operand(node: dict) -> dict:
    """RHS of a comparison — literal or a lowered atom."""
    if "lit" in node:
        return {"lit": node["lit"]}
    if "const" in node or "null" in node:
        return node
    return lower_predicate(node)


def roots_at_card(node: dict) -> bool:
    if "call" in node:
        return node["call"].split(".")[0] == "card"
    if "member" in node:
        return node["member"].split(".")[0] == "card"
    if "ref" in node:
        return node["ref"] == "card"
    if "memberOf" in node:
        return roots_at_card(node["memberOf"])
    return False


PERMANENT_MEMBERS = {
    "BaseDP", "DP", "DigivolutionCards", "HasBlocker", "HasJamming", "HasKeyword",
    "HasNoDigivolutionCards", "HasPierce", "HasReboot", "HasRush", "InstanceId",
    "IsDigimon", "IsEmpty", "IsSuspended", "IsTamer", "IsToken", "OwnerId",
    "Stack", "TopCard", "TopInstanceId",
}
_COLOR_RE = re.compile(r"^(.*)\.CardColors\.Contains$")


def lower_perm_predicate(node: dict, param: str) -> dict:
    """Func<Permanent,bool> 술어(permanentCondition/DefenderCondition) 로워링.
    Permanent 멤버는 passthrough(allowlist), CardColors.Contains(CardColor.X)→HasCardColor("X"),
    commons 호출은 존재 검증 후 passthrough. 검증 안 되는 구성은 STOP(뭉갬 금지)."""
    if node is None:
        return {"const": True}
    if "const" in node or "lit" in node:
        return node
    if "not" in node:
        return {"not": lower_perm_predicate(node["not"], param)}
    if node.get("binop") in ("&&", "||"):
        return {"binop": node["binop"], "lhs": lower_perm_predicate(node["lhs"], param),
                "rhs": lower_perm_predicate(node["rhs"], param)}
    if node.get("binop") in (">=", "<=", ">", "<", "==", "!="):
        return {"cmp": node["binop"], "lhs": lower_perm_predicate(node["lhs"], param),
                "rhs": lower_perm_predicate(node["rhs"], param)}
    if "member" in node:
        segs = node["member"].split(".")
        if segs[0] == param and (len(segs) == 1 or segs[1] in PERMANENT_MEMBERS):
            return {"raw": node["member"]}
        raise Stop("lowering:missing-rule", "STOP_RULE_AMBIGUOUS",
                   f"permanent member not in allowlist: {node['member']}")
    if "call" in node:
        name = node["call"]
        m = _COLOR_RE.match(name)
        if m:  # <recv>.CardColors.Contains(CardColor.X) -> <recv>.HasCardColor("X")
            recv = m.group(1)
            arg = node.get("args", [{}])[0]
            col = arg.get("member", "").split(".")[-1] if "member" in arg else None
            if recv.split(".")[0] == param and col:
                return {"raw": f'{recv}.HasCardColor("{col}")'}
            raise Stop("lowering:missing-rule", "STOP_RULE_AMBIGUOUS", f"unhandled color contains: {node}")
        if name.startswith("CardEffectCommons."):
            short = name.split(".", 1)[1]
            sym = SYMBOLS.get(short)
            if sym is None or sym["kind"] != "commons":
                raise Stop("lowering:missing-op", "STOP_MISSING_PRIMITIVE", f"unknown commons: {short}")
            a = ", ".join(x.get("ref", "?") for x in node.get("args", []))
            if "?" in a.split(", "):
                raise Stop("lowering:missing-rule", "STOP_RULE_AMBIGUOUS", f"commons arg not a simple ref: {node}")
            return {"raw": f"CardEffectCommons.{short}({a})"}
    raise Stop("lowering:missing-rule", "STOP_RULE_AMBIGUOUS", f"unhandled permanent-predicate node: {node}")


def lower_perm_lambda(node: dict, param: str) -> dict:
    """id-람다 본문 로워링: `param.X` 멤버 → CardEffectCommons.X(card, id) (X 가 정확히
    permanent-subject commons 헬퍼일 때만; 아니면 STOP — 뭉갬 금지)."""
    if node is None:
        return {"const": True}
    if "const" in node:
        return node
    if "not" in node:
        return {"not": lower_perm_lambda(node["not"], param)}
    if node.get("binop") in ("&&", "||"):
        return {"binop": node["binop"],
                "lhs": lower_perm_lambda(node["lhs"], param),
                "rhs": lower_perm_lambda(node["rhs"], param)}
    if "member" in node and node["member"].split(".")[0] == param:
        x = node["member"].split(".")[-1]
        if x in PERM_ATOMS:
            return {"call": f"CardEffectCommons.{x}", "args": [{"ref": "card"}, {"ref": "id"}]}
        raise Stop("lowering:missing-rule", "STOP_RULE_AMBIGUOUS",
                   f"lambda member '{x}' has no exact CardEffectCommons(card,id) helper (→ LLM 후보)")
    if "call" in node and node["call"].startswith("CardEffectCommons."):
        # already a commons call inside the lambda (e.g. HasNoDigivolutionCards(card, id))
        return node
    raise Stop("lowering:missing-rule", "STOP_RULE_AMBIGUOUS", f"unrecognized lambda node: {node}")


def lower_self_atom(name: str, is_self: bool, raw) -> dict:
    atom = ATOMS.get(name)
    if atom is None:
        raise Stop("lowering:missing-rule", "STOP_MISSING_PRIMITIVE",
                   f"member access has no base atom rule: {raw}")
    if atom["subject"] != "self" or not is_self:
        raise Stop("lowering:missing-rule", "STOP_MULTI_STEP_OPTIONAL",
                   f"atom {name} needs non-self subject rewrite (Permanent/id): {raw}")
    emit = atom["emit"]
    sym = SYMBOLS.get(emit)
    if sym is None or sym["kind"] != "commons":
        raise Stop("lowering:missing-op", "STOP_MISSING_PRIMITIVE",
                   f"base atom {name} emit target not in commons symbols: {emit}")
    return {"call": f"CardEffectCommons.{emit}", "args": [{"ref": "card"}]}


def short_call(name: str) -> str:
    return name.split(".")[-1]


def strip_plumbing(node: dict):
    """activateCondition 에서 배관 술어(CanActivateOnX/CanAddMemory 등)를 제거하고
    의미 술어 나머지를 반환(없으면 None)."""
    if node is None:
        return None
    if "call" in node and short_call(node["call"]) in PLUMBING:
        return None
    if node.get("binop") == "&&":
        lhs = strip_plumbing(node["lhs"])
        rhs = strip_plumbing(node["rhs"])
        if lhs is None:
            return rhs
        if rhs is None:
            return lhs
        return {"binop": "&&", "lhs": lhs, "rhs": rhs}
    if "const" in node:
        return None if node["const"] else node
    return node  # semantic remainder — lowered downstream


def resolve_description(desc, local_fns: dict) -> str:
    if desc is None:
        return ""
    if "lit" in desc:
        return desc["lit"]
    if "call" in desc:
        fn = local_fns.get(short_call(desc["call"]))
        if fn and "lit" in fn.get("body", {}):
            return fn["body"]["lit"]
    return ""


def lower_intent_value(node: dict) -> dict:
    """코루틴 intent 인자 값 — 리터럴 또는 열거형 멤버(EffectDuration.X)."""
    if "lit" in node or "const" in node:
        return node
    if "member" in node and node["member"].startswith("EffectDuration."):
        return {"enum": node["member"]}
    raise Stop("lowering:missing-rule", "STOP_COMPLEX_TIMING",
               f"intent arg not a literal/known enum: {node}")


def lower_activate(act: dict, timing: str, local_fns: dict):
    """ActivateClass 활성효과 → 단일 TriggerEffect 팩토리 (canonical factoryAdd) + 파생 Condition."""
    coro = local_fns.get(act.get("coroutine", ""))
    if not coro or "yields" not in coro.get("body", {}):
        raise Stop("lowering:tier-3", "STOP_COMPLEX_TIMING", "coroutine body not captured")
    yields = coro["body"]["yields"]
    if len(yields) != 1:
        raise Stop("lowering:missing-rule", "STOP_MULTI_STEP_OPTIONAL",
                   f"coroutine has {len(yields)} effects (multi-step) — 강모델")
    intent = yields[0]
    # Activation-effect coroutine (IActivatedCardEffect: Draw/…). Emitted plainly at the timing —
    # the activation flow (option/security/on-play) fires + gates it, so no condition/description slot.
    # Only WIRED timings actually fire (else STOP activation-pending). CanActivate must be pure plumbing
    # (the activation factory has no condition slot; a real semantic condition can't be expressed).
    akey = intent.get("ctorCall")
    if akey is None and "call" in intent and intent["call"].startswith("CardEffectCommons."):
        akey = intent["call"].split(".")[-1]
    if akey in ACTIVATION_INTENTS:
        aspec = ACTIVATION_INTENTS[akey]
        if timing not in ACTIVATION_WIRED:
            raise Stop("lowering:tier-3", "STOP_COMPLEX_TIMING",
                       f"activation intent {akey} at unwired timing {timing} (won't fire) — 강모델")
        afactory = aspec["factory"]
        asym = SYMBOLS.get(afactory)
        if asym is None or asym["kind"] != "factory":
            raise Stop("lowering:missing-op", "STOP_MISSING_PRIMITIVE", f"activation factory absent: {afactory}")
        # activation factories have no condition slot — a semantic (non-plumbing) activate-condition
        # cannot be expressed, so STOP rather than silently drop it.
        acf = local_fns.get(act.get("activateCondition", ""))
        if acf is not None and strip_plumbing(acf.get("body")) is not None:
            raise Stop("lowering:missing-rule", "STOP_MULTI_STEP_OPTIONAL",
                       f"{akey} has a semantic activate-condition (activation factory has no condition slot) — 강모델")
        card_param = next((p[1] for p in asym["params"] if p[0] == "CardSource"), "card")
        out = [{"name": card_param, "value": {"ref": "card"}}]
        if aspec["kind"] == "ctor-count":
            ctor_args = intent.get("ctorArgs", [])
            cnt = ctor_args[aspec["count_arg"]] if aspec["count_arg"] < len(ctor_args) else {}
            if "lit" not in cnt:
                raise Stop("lowering:missing-rule", "STOP_COMPLEX_TIMING", f"activation count not a literal: {cnt}")
            int_param = next((p[1] for p in asym["params"] if p[0] == "int"), "count")
            out.append({"name": int_param, "value": {"lit": cnt["lit"]}})
        return {"factory": afactory, "args": out}, None
    if "call" not in intent:
        raise Stop("lowering:tier-3", "STOP_COMPLEX_TIMING", f"non-call coroutine intent: {intent}")
    spec = INTENTS.get(intent["call"])
    if spec is None:
        raise Stop("lowering:missing-rule", "STOP_COMPLEX_TIMING",
                   f"no coroutine intent mapping: {intent['call']}")
    factory = spec["factory"]
    sym = SYMBOLS.get(factory)
    if sym is None or sym["kind"] != "factory":
        raise Stop("lowering:missing-op", "STOP_MISSING_PRIMITIVE",
                   f"intent factory not in symbols: {factory}")
    iargs = intent.get("args", [])
    # self-target guard: some intents (DP/SAttack buffs) only map to a *self* trigger factory
    # when their target arg is this card's own permanent.
    st = spec.get("self_target_arg")
    if st is not None:
        tgt = iargs[st] if st < len(iargs) else {}
        if not (tgt.get("call", "").endswith("PermanentOfThisCard")):
            raise Stop("lowering:missing-rule", "STOP_COMPLEX_TIMING",
                       f"{intent['call']} target not self (needs targeted factory): {tgt}")
    # map factory params <- intent arg indices
    mapped = {}
    for pname, idx in spec.get("arg_map", {}).items():
        if idx >= len(iargs):
            raise Stop("lowering:missing-rule", "STOP_COMPLEX_TIMING",
                       f"{intent['call']} missing arg {idx} for {pname}")
        mapped[pname] = lower_intent_value(iargs[idx])
    # condition: strip plumbing; semantic remainder (if any) → Condition local fn
    cond_localfn = None
    # condition: BOTH CanUseCondition and CanActivateCondition carry semantic restrictions
    # beyond the trigger plumbing (e.g. IsOwnerTurn = "[Your Turn]", DefendingPermanent != null =
    # "attacking a Digimon"). Strip plumbing from each, AND-merge the semantic remainders, and
    # lower them into `condition`. Dropping a non-plumbing remainder would be a fidelity bug —
    # if it cannot be lowered, STOP the card (never silently drop).
    cond_value = {"null": True}
    remainders = []
    for fn_key in ("activateCondition", "useCondition"):
        f = local_fns.get(act.get(fn_key, ""))
        if f is not None:
            r = strip_plumbing(f.get("body"))
            if r is not None:
                remainders.append(r)
    if remainders:
        merged = remainders[0]
        for r in remainders[1:]:
            merged = {"binop": "&&", "lhs": merged, "rhs": r}
        cond_localfn = {"name": "Condition", "body": lower_predicate(merged)}
        cond_value = {"localfn": "Condition"}
    slots = {
        "timing": {"timing": timing},
        "isInheritedEffect": act.get("inherited", {"const": False}),
        "card": {"ref": "card"},
        "condition": cond_value,
        "description": {"lit": resolve_description(act.get("description"), local_fns)},
        **mapped,
    }
    out_args = [{"name": p[1], "value": slots[p[1]]} for p in sym["params"] if p[1] in slots]
    return {"factory": factory, "args": out_args}, cond_localfn


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
            effs = br.get("effects", [])
            # 0. Activate (coroutine) effect — fold ActivateClass builder into a TriggerEffect.
            #    Handle the clean single-activate branch deterministically via the intent table.
            if any(e.get("kind") == "activate" for e in effs):
                if len(effs) != 1 or effs[0].get("kind") != "activate":
                    raise Stop("lowering:tier-3", "STOP_COMPLEX_TIMING",
                               "activate mixed with other effects (multi-step) — 강모델")
                fa, cond_fn = lower_activate(effs[0], timing, local_fns)
                canon["branches"].append({"timing": timing,
                                          "localFns": [cond_fn] if cond_fn else [],
                                          "effects": [fa]})
                ledger.append({"card": src["card"], "branch": bi, "timing": timing, "status": "lowered"})
                continue
            # 1. Effects first — non-factory (opaque) effect → coroutine/complex, STOP cleanly
            #    (its string-returning EffectDiscription must not be mis-read as a predicate).
            if any(eff.get("kind") != "factoryAdd" for eff in effs):
                raise Stop("lowering:tier-3", "STOP_COMPLEX_TIMING",
                           "coroutine/ActivateClass or non-factory effect (declarative translation needed)")
            # subject type of each param-taking predicate, from the factory slot that references it
            fn_ptype = {}  # localfn name -> "Permanent" | "HeadlessEntityId"
            for eff in effs:
                fsym = SYMBOLS.get(eff.get("factory", ""))
                if not fsym:
                    continue
                slot = {p[1]: p[0] for p in fsym["params"]}
                for a in eff.get("args", []):
                    ref = a["value"].get("ref") if isinstance(a.get("value"), dict) else None
                    pname = a.get("name")
                    t = slot.get(pname, "")
                    if ref and "Func<Permanent" in t.replace(" ", ""):
                        fn_ptype[ref] = "Permanent"
            # 2. Local functions (predicates).
            lowered_fns = {}
            for fn in br.get("localFns", []):
                if fn.get("returns") not in (None, "bool"):
                    raise Stop("lowering:tier-3", "STOP_COMPLEX_TIMING",
                               f"non-bool local fn {fn['name']} ({fn.get('returns')}) — coroutine shape")
                if fn.get("params"):
                    if fn_ptype.get(fn["name"]) == "Permanent":
                        p = fn["params"][0]
                        lowered_fns[fn["name"]] = {"name": fn["name"], "ptype": "Permanent", "param": p,
                                                   "body": lower_perm_predicate(fn["body"], p)}
                        continue
                    raise Stop("lowering:missing-rule", "STOP_MULTI_STEP_OPTIONAL",
                               f"predicate {fn['name']} param subject unresolved (not Func<Permanent>)")
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
            need = {p[1] for p in sym["params"] if not p[2]}  # non-optional (no default) only
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
    if "timing" in v:
        return f"EffectTiming.{v['timing']}"
    if "enum" in v:
        return v["enum"]
    raise ValueError(f"cannot emit value {v}")


def emit_predicate(n: dict) -> str:
    if "raw" in n:
        return n["raw"]
    if "const" in n:
        return "true" if n["const"] else "false"
    if "lit" in n:
        return str(n["lit"]) if isinstance(n["lit"], int) else f"\"{n['lit']}\""
    if "null" in n:
        return "null"
    if "not" in n:
        return f"!({emit_predicate(n['not'])})"
    if n.get("binop") in ("&&", "||"):
        return f"({emit_predicate(n['lhs'])} {n['binop']} {emit_predicate(n['rhs'])})"
    if "cmp" in n:
        return f"({emit_predicate(n['lhs'])} {n['cmp']} {emit_predicate(n['rhs'])})"
    if "lambda" in n:
        lam = n["lambda"]
        return f"{lam['param']} => {emit_predicate(lam['body'])}"
    if "call" in n:
        args = ", ".join(emit_predicate(a) if "lambda" in a else emit_value(a) for a in n.get("args", []))
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
            sig = f"{fn['ptype']} {fn['param']}" if fn.get("ptype") else ""
            lines.append(f"            bool {fn['name']}({sig})")
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
ATOMS: dict[str, dict] = {}
INTENTS: dict[str, dict] = {}
PLUMBING: set[str] = set()
ACTIVATION_INTENTS: dict[str, dict] = {}
ACTIVATION_WIRED: set[str] = set()
PERM_ATOMS: set[str] = set()        # commons bool(CardSource, HeadlessEntityId) — permanent-subject
ID_LAMBDA_CALLS: set[str] = set()   # commons taking a Func<HeadlessEntityId,bool> lambda


def derive_predicate_tables() -> None:
    """심볼표에서 permanent-subject 술어(PERM_ATOMS)와 id-람다 호출(ID_LAMBDA_CALLS)을 도출."""
    global PERM_ATOMS, ID_LAMBDA_CALLS
    for name, sym in SYMBOLS.items():
        if sym["kind"] != "commons":
            continue
        ps = sym["params"]
        if sym["ret"] == "bool" and len(ps) == 2 \
                and ps[0][0] == "CardSource" and ps[1][0] == "HeadlessEntityId":
            PERM_ATOMS.add(name)
        if any("Func<HeadlessEntityId" in p[0].replace(" ", "") for p in ps):
            ID_LAMBDA_CALLS.add(name)


def load_atoms() -> dict[str, dict]:
    path = PORTING / "data/atoms.json"
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8")).get("base", {})


def load_intents() -> tuple[dict, set]:
    path = PORTING / "data/intents.json"
    if not path.exists():
        return {}, set()
    d = json.loads(path.read_text(encoding="utf-8"))
    return d.get("intents", {}), set(d.get("plumbing_predicates", []))


def load_activation() -> tuple[dict, set]:
    path = PORTING / "data/intents.json"
    if not path.exists():
        return {}, set()
    d = json.loads(path.read_text(encoding="utf-8"))
    return d.get("activation_intents", {}), set(d.get("activation_wired_timings", []))


def main() -> int:
    global SYMBOLS, ATOMS, INTENTS, PLUMBING, ACTIVATION_INTENTS, ACTIVATION_WIRED
    ATOMS = load_atoms()
    INTENTS, PLUMBING = load_intents()
    ACTIVATION_INTENTS, ACTIVATION_WIRED = load_activation()
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
    derive_predicate_tables()
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
