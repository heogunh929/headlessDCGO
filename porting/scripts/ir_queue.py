#!/usr/bin/env python3
"""강모델 후속개발 큐 생성 (pipeline-v3, 인메모리 집계 — 저장소 오염 없음).

저장소 전체 Source IR 을 ir_pipeline 결정론 로워링에 통과시켜, STOP 을 **차단원인별**로
집계하고 각 원인에 (차단 카드 수 · 대응 헤드리스 심볼 · 발화등급 · 필요 작업유형)을 붙여
우선순위 큐를 만든다. 강모델은 이 큐를 빈도순으로 소비한다.

작업유형(work_type):
  intent-map(fireable) — 대응 팩토리가 ICardEffect(발화). 매핑 행만 추가하면 됨(porting-side).
  trigger-wrap|activation — 대응이 IActivatedCardEffect(미발화). 트리거 래퍼 or 활성경로 필요(엔진).
  new-primitive         — 대응 심볼 없음. 프리미티브 신설 필요(엔진).
  composition           — 다중 yield(효과 합성) 필요.
  predicate/misc        — 술어/표현 규칙.

출력: porting/data/queue.json, porting/data/queue.md
"""
from __future__ import annotations

import importlib.util
import json
import re
from collections import Counter, defaultdict
from pathlib import Path

PORTING = Path(__file__).resolve().parents[1]
IR_SRC = PORTING / "data/ir-src"

spec = importlib.util.spec_from_file_location("irp", PORTING / "scripts/ir_pipeline.py")
irp = importlib.util.module_from_spec(spec)
spec.loader.exec_module(irp)


def action_tokens(intent_call: str) -> list[str]:
    """intent 호출 문자열에서 액션 식별자 추출 (new DrawClass(..).Draw -> [DrawClass, Draw])."""
    return re.findall(r"[A-Za-z_]\w{3,}", intent_call)


def find_factory(tokens: list[str], symbols: dict, activated: set) -> tuple[str, str]:
    """토큰과 이름이 겹치는 카탈로그 팩토리를 찾아 (이름, 발화등급) 반환. 없으면 ('', '')."""
    toks = [t for t in tokens if t not in ("new", "card", "Owner", "instance", "List", "Permanent")]
    best = ""
    for name, sym in symbols.items():
        if sym["kind"] != "factory":
            continue
        if any(t in name or name in t for t in toks):
            # 액션 이름이 팩토리명에 포함되면 후보
            if not best or len(name) < len(best):
                best = name
    if not best:
        return "", ""
    return best, ("activated" if best in activated else "fireable")


def main() -> int:
    irp.SYMBOLS = irp.load_symbols()
    irp.derive_predicate_tables()
    irp.ATOMS = irp.load_atoms()
    irp.INTENTS, irp.PLUMBING = irp.load_intents()
    mapped_intents = set(irp.INTENTS.keys())

    cat = (PORTING / "docs/PRIMITIVE-CATALOG.md").read_text(encoding="utf-8")
    activated = set(re.findall(r"\|\s*`(\w+)`\s*\|\s*IActivatedCardEffect\s*\|", cat))

    intent_cards = defaultdict(set)     # intent -> {cards}
    intent_multi = defaultdict(set)     # multi-yield intents
    code_cards = defaultdict(set)       # STOP code -> {cards}

    for f in sorted(IR_SRC.glob("*/*.json")):
        try:
            src = json.loads(f.read_text(encoding="utf-8"))
        except Exception:
            continue
        if not src.get("branches"):
            continue
        card = src["card"]
        # coroutine intents (from Source IR yields) — the dominant blocker
        for br in src["branches"]:
            for fn in br.get("localFns", []):
                b = fn.get("body", {})
                if isinstance(b, dict) and "yields" in b:
                    ys = b["yields"]
                    if len(ys) == 1 and isinstance(ys[0], dict) and "call" in ys[0]:
                        intent_cards[ys[0]["call"]].add(card)
                    elif len(ys) > 1:
                        intent_multi["<multi-yield>"].add(card)
        # STOP code buckets
        try:
            canon, _ = irp.lower_card(src)
        except Exception:
            continue
        for b in canon["branches"]:
            if "stop" in b:
                code_cards[b["stop"]["code"]].add(card)

    # build queue rows for coroutine intents
    rows = []
    for intent, cards in intent_cards.items():
        if intent in mapped_intents:
            continue
        toks = action_tokens(intent)
        fac, firing = find_factory(toks, irp.SYMBOLS, activated)
        # 상호작용(선택 기반) 의도는 본질적으로 activation path 필요 — 이름과 무관하게 우선 분류
        if re.search(r"select\w*Effect\.Activate|Select|Reveal|Choose", intent):
            work = "activation(interactive)"
        elif not fac:
            work = "needs-analysis"     # 팩토리 이름매칭 실패 — commons/특수, 강모델 판정 필요
        elif firing == "activated":
            work = "trigger-wrap|activation"
        else:
            work = "maybe-fireable(heuristic)"
        rows.append({
            "blocker": intent.strip().split("\n")[0][:80],
            "cards_blocked": len(cards),
            "headless_factory": fac or None,
            "firing": firing or None,
            "work_type": work,
            "sample": sorted(cards)[:6],
        })
    rows.sort(key=lambda r: -r["cards_blocked"])

    multi_n = len(intent_multi["<multi-yield>"])
    queue = {
        "schema": "strong-model-queue/1",
        "summary": {
            "coroutine_intent_blockers": len(rows),
            "multi_yield_cards": multi_n,
            "stop_code_cards": {k: len(v) for k, v in sorted(code_cards.items(), key=lambda x: -len(x[1]))},
        },
        "intents": rows,
    }
    (PORTING / "data/queue.json").write_text(json.dumps(queue, ensure_ascii=False, indent=2), encoding="utf-8")

    # render markdown
    md = ["# 강모델 후속개발 큐 (자동생성)", "",
          "> `porting/scripts/ir_queue.py` 산출. 저장소 전체 Source IR 기준. 빈도(차단 카드 수)순.", "",
          f"- 코루틴 의도 차단원인: **{len(rows)}종**",
          f"- 다중-yield(합성 필요) 카드: **{multi_n}장**",
          f"- STOP 코드별 카드 수: {queue['summary']['stop_code_cards']}", "",
          "## 코루틴 의도 차단원인 (빈도순)", "",
          "| 차단원인(intent) | 차단카드 | 헤드리스 팩토리 | 발화등급 | 작업유형 |",
          "|---|---|---|---|---|"]
    for r in rows:
        md.append(f"| `{r['blocker']}` | {r['cards_blocked']} | {r['headless_factory'] or '—'} "
                  f"| {r['firing'] or '—'} | {r['work_type']} |")
    (PORTING / "data/queue.md").write_text("\n".join(md) + "\n", encoding="utf-8")

    print(f"큐 생성: porting/data/queue.json (+.md)")
    print(f"  코루틴 의도 차단원인 {len(rows)}종, 다중-yield {multi_n}장")
    print("  작업유형 분포:", dict(Counter(r["work_type"] for r in rows)))
    print("\n  상위 12 차단원인:")
    for r in rows[:12]:
        print(f"    {r['cards_blocked']:4d}  [{r['work_type']:22s}] {r['blocker']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
