#!/usr/bin/env python3
"""LLM 조각 제안 하네스 (pipeline-v3 stage 4, Phase C).

결정론 로워링(stage 3)이 실패한 조각만 로컬 LLM 에 넘겨 **Canonical IR 후보**를 받는다.
LLM 출력은 스키마 구속 JSON + evidence 인용 필수이며, validator 가 (스키마·심볼 존재·
인용 실재) 를 검사한다. **후보는 실행 불가** — porting/data/suggestions/ 큐에만 쓰이고,
Canonical IR 진입·미러 반영은 강모델/사람 승인 후 atoms.json(base/macro) 승격으로만.

계약(설계 §4):
  - 제안 대상 = ledger 의 stop(stage lowering:missing-rule|missing-op) 조각.
  - LLM 은 후보 op/atom + evidence(카탈로그 행 인용) + confidence 만 낸다.
  - evidence 가 실재하지 않으면 validator 자동 반려(= suggestion-rejected).

사용:
  python3 porting/scripts/ir_suggest.py BT1 Blue BT1_004                 # 조각 프롬프트만 출력(dry)
  python3 porting/scripts/ir_suggest.py --model qwen3-coder:30b BT1 Blue BT1_004   # 실제 제안
환경: OLLAMA_URL (기본 http://192.168.0.42:11434)
"""
from __future__ import annotations

import json
import os
import re
import sys
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
PORTING = Path(__file__).resolve().parents[1]
CATALOG = PORTING / "docs/PRIMITIVE-CATALOG.md"
IR_SRC = PORTING / "data/ir-src"
SUGG_DIR = PORTING / "data/suggestions"
OLLAMA_URL = os.environ.get("OLLAMA_URL", "http://192.168.0.42:11434")

CANDIDATE_SCHEMA = {
    "type": "object",
    "required": ["emit", "args", "evidence", "confidence"],
    "properties": {
        "emit": {"type": "string", "description": "CardEffectCommons.<helper> 정확한 이름"},
        "args": {"type": "array", "items": {"type": "string"}},
        "evidence": {"type": "array", "items": {"type": "string"},
                     "description": "카탈로그에 실재하는 헬퍼 이름(들). 없으면 반려."},
        "confidence": {"type": "string", "enum": ["high", "low"]},
        "note": {"type": "string"},
    },
}


def load_commons_helpers() -> dict[str, str]:
    """commons 헬퍼 이름 → 시그니처 행 (evidence 검증·후보 제시용)."""
    out, section = {}, None
    for line in CATALOG.read_text(encoding="utf-8").splitlines():
        if line.startswith("## "):
            section = "commons" if line.startswith("## CardEffectCommons 헬퍼 마스터") else None
            continue
        if section != "commons":
            continue
        m = re.match(r"\|\s*`(\w+)`\s*\|\s*`?([^|`]+?)`?\s*\|\s*`(.+?)`\s*\|", line)
        if m:
            out[m.group(1)] = m.group(3)
    return out


def find_stop_fragments(src: dict) -> list[dict]:
    """Source IR 에서 결정론 로워링이 못 다룰 조각(멤버/람다/비-card 호출)을 수집."""
    frags = []

    def walk(node, path):
        if not isinstance(node, dict):
            return
        if "member" in node or "memberOf" in node or "lambda" in node:
            frags.append({"path": path, "node": node})
            return
        for k, v in node.items():
            if isinstance(v, dict):
                walk(v, f"{path}/{k}")
            elif isinstance(v, list):
                for i, it in enumerate(v):
                    walk(it, f"{path}/{k}[{i}]")

    for bi, br in enumerate(src["branches"]):
        for fn in br.get("localFns", []):
            walk(fn.get("body"), f"branch{bi}/{fn['name']}")
    return frags


def keyword_candidates(frag: dict, helpers: dict[str, str], k: int = 12) -> list[str]:
    """조각 텍스트에서 뽑은 토큰으로 후보 헬퍼를 이름 매칭(제안 좁히기)."""
    text = json.dumps(frag["node"], ensure_ascii=False)
    toks = {t.lower() for t in re.findall(r"[A-Za-z]{4,}", text)}
    scored = []
    for name, sig in helpers.items():
        score = sum(1 for t in toks if t in name.lower())
        if score:
            scored.append((score, name, sig))
    scored.sort(reverse=True)
    return [f"| `{n}` | `{s}` |" for _, n, s in scored[:k]]


def build_prompt(card: str, frag: dict, cands: list[str]) -> str:
    return f"""You lower ONE DCGO predicate fragment to a headless CardEffectCommons call.
Card {card}. Fragment (Source IR JSON, DCGO vocabulary):

{json.dumps(frag['node'], ensure_ascii=False, indent=2)}

You MUST pick `emit` from these catalog helpers ONLY (do not invent):

| helper | signature |
|---|---|
{chr(10).join(cands) if cands else '| (none matched — return confidence low) |'}

Output STRICT JSON (no prose) matching:
{{"emit":"<helper name from the table>","args":["card", ...],
  "evidence":["<helper name you relied on, must be in the table>"],
  "confidence":"high|low","note":"<one line>"}}
If no helper fits, return confidence "low" with your best guess and evidence []."""


def call_ollama(model: str, prompt: str, timeout: int = 120) -> dict:
    body = json.dumps({
        "model": model, "stream": False, "format": "json",
        "options": {"temperature": 0.1, "num_ctx": 8192},
        "messages": [{"role": "user", "content": prompt}],
    }).encode()
    req = urllib.request.Request(f"{OLLAMA_URL}/api/chat", data=body,
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        resp = json.loads(r.read())
    return json.loads(resp["message"]["content"])


def validate_candidate(cand: dict, helpers: dict[str, str]) -> list[str]:
    issues = []
    for req in ("emit", "evidence", "confidence"):
        if req not in cand:
            issues.append(f"missing field: {req}")
    emit = cand.get("emit", "")
    if emit and emit not in helpers:
        issues.append(f"emit '{emit}' not in commons symbol table (invented)")
    for ev in cand.get("evidence", []):
        if ev not in helpers:
            issues.append(f"evidence '{ev}' does not resolve to a catalog helper (fabricated)")
    return issues


def main() -> int:
    args = sys.argv[1:]
    model = None
    if args and args[0] == "--model":
        model = args[1]
        args = args[2:]
    if len(args) < 3:
        print(__doc__)
        return 1
    set_, color, card = args[0], args[1], args[2]

    src_path = IR_SRC / f"{set_}.{color}" / f"{card}.json"
    if not src_path.exists():
        print(f"Source IR 없음: {src_path}", file=sys.stderr)
        return 1
    src = json.loads(src_path.read_text(encoding="utf-8"))
    helpers = load_commons_helpers()
    frags = find_stop_fragments(src)
    if not frags:
        print(f"{card}: 미해결 조각 없음(결정론 로워링으로 충분).")
        return 0

    SUGG_DIR.mkdir(parents=True, exist_ok=True)
    for i, frag in enumerate(frags):
        cands = keyword_candidates(frag, helpers)
        prompt = build_prompt(card, frag, cands)
        print(f"\n=== fragment {i} @ {frag['path']} ===")
        if model is None:
            print(prompt)
            print("\n(dry — --model <name> 으로 실제 제안 생성)")
            continue
        try:
            cand = call_ollama(model, prompt)
        except Exception as e:
            print(f"  LLM 호출 실패: {e}")
            continue
        issues = validate_candidate(cand, helpers)
        verdict = "REJECTED" if issues else "queued"
        record = {"card": card, "set": set_, "color": color, "path": frag["path"],
                  "fragment": frag["node"], "candidate": cand,
                  "validator": {"verdict": verdict, "issues": issues}, "model": model}
        out = SUGG_DIR / f"{card}.frag{i}.json"
        out.write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"  candidate: emit={cand.get('emit')} conf={cand.get('confidence')} → {verdict}")
        if issues:
            for x in issues:
                print(f"    ! {x}")
        print(f"  → {out.relative_to(REPO)} (advisory; 승격은 강모델/사람 승인)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
