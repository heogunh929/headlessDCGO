#!/usr/bin/env python3
"""IR 원장·레지스트리 (pipeline-v3 stages 6·11, Phase B — 결정론).

Canonical IR(porting/data/ir/<SET>.<COLOR>/<ID>.json, 진실원천)와 실제 미러 파일 상태를
읽어:
  stage 6  coverage ledger — 카드×분기 상태 + irHash + tier + STOP 집계(빈도 가중)
           → porting/data/ledger/<SET>.<COLOR>.json  및  porting/stop/<SET>.<COLOR>.md(렌더 뷰)
  stage 11 registry        — RL 카드풀 포함 결정 → porting/data/cardpool.json

포함 규칙(확정 r2): STOP 잔존 카드는 code 불문 제외. 전분기 lowered + 미러 live + 게이트
green 만 포함. code 의 가치는 부분 포함이 아니라 해소 우선순위(primitive 큐 빈도 정렬)다.

사용:
  python3 porting/scripts/ir_registry.py BT1 Blue        # SET+COLOR
  python3 porting/scripts/ir_registry.py BT1             # SET 전체(존재하는 color 전부)
  python3 porting/scripts/ir_registry.py --all           # data/ir 아래 전부
"""
from __future__ import annotations

import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
PORTING = Path(__file__).resolve().parents[1]
IR_CANON = PORTING / "data/ir"
LEDGER_DIR = PORTING / "data/ledger"
STOP_DIR = PORTING / "stop"
CARDPOOL = PORTING / "data/cardpool.json"
MIRROR_ROOT = REPO / "src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect"

IR_SCHEMA_VER = "canonical-ir/1"
TABLE_VER = "catalog@2026-07-04"   # 로워링 테이블(카탈로그) 버전 스탬프

# tier 를 STOP 코드에서 추정 (현재 로워링 상태 기준의 coarse tier).
TIER3_CODES = {"STOP_ENGINE_ARCHITECTURE", "STOP_COMPLEX_TIMING", "STOP_SPECIAL_PLAY"}


def semantic_hash(canon: dict) -> str:
    """정규화(키 정렬) Canonical IR 의 해시. 매크로 전개는 아직 없음(Phase B)."""
    payload = json.dumps(canon.get("branches", []), sort_keys=True, ensure_ascii=False)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()[:12]


def mirror_state(set_: str, color: str, card: str) -> str:
    """live | stop-pending | stub | missing | inert — 바인딩 게이트 개념의 파일-레벨 근사."""
    p = MIRROR_ROOT / set_ / color / f"{card}.cs"
    if not p.exists():
        return "missing"
    text = p.read_text(encoding="utf-8")
    if "Skeleton only" in text:
        return "stub"
    if "cardEffects.Add(" in text:
        return "live"
    if "// STOP" in text:
        return "stop-pending"
    return "inert"


def mirror_provenance(set_: str, color: str, card: str) -> str:
    p = MIRROR_ROOT / set_ / color / f"{card}.cs"
    if p.exists() and "// GENERATED FROM porting/data/ir" in p.read_text(encoding="utf-8"):
        return "ir-generated"
    return "handwritten"


def classify(canon: dict) -> tuple[int, bool, list[dict]]:
    """(tier, fully_lowered, branch_records) 반환."""
    branches = canon.get("branches", [])
    records, stops = [], []
    for bi, br in enumerate(branches):
        if "stop" in br:
            s = br["stop"]
            records.append({"branch": bi, "timing": br["timing"], "status": "stop",
                            "stage": s["stage"], "code": s["code"], "detail": s["detail"]})
            stops.append(s)
        else:
            records.append({"branch": bi, "timing": br["timing"], "status": "lowered"})
    fully = bool(branches) and not stops
    if fully:
        tier = 1
    elif any(s["code"] in TIER3_CODES or s["stage"].startswith("lowering:tier-3") for s in stops):
        tier = 3
    else:
        tier = 2
    return tier, fully, records


def build(set_: str, color: str) -> dict:
    ir_dir = IR_CANON / f"{set_}.{color}"
    if not ir_dir.is_dir():
        return {}
    ledger = []
    for f in sorted(ir_dir.glob("*.json")):
        canon = json.loads(f.read_text(encoding="utf-8"))
        card = canon["card"]
        tier, fully, records = classify(canon)
        state = mirror_state(set_, color, card)
        prov = mirror_provenance(set_, color, card)
        # 포함 결정
        stop_codes = [r["code"] for r in records if r["status"] == "stop"]
        if fully and state == "live":
            included, reason = True, ("ir-generated" if prov == "ir-generated" else "live (handwritten)")
        elif stop_codes:
            included, reason = False, stop_codes[0]
        else:
            included, reason = False, f"mirror {state}"
        ledger.append({
            "card": card, "set": set_, "color": color, "tier": tier,
            "irHash": semantic_hash(canon), "irSchemaVer": IR_SCHEMA_VER, "tableVer": TABLE_VER,
            "provenance": prov, "mirrorState": state, "fullyLowered": fully,
            "included": included, "reason": reason, "branches": records,
        })
    return {"set": set_, "color": color, "cards": ledger}


def render_stop_md(bundle: dict) -> str:
    set_, color = bundle["set"], bundle["color"]
    cards = bundle["cards"]
    by_code = defaultdict(list)
    missing_op_symbols = Counter()
    for c in cards:
        for br in c["branches"]:
            if br["status"] != "stop":
                continue
            by_code[br["code"]].append((c["card"], br["timing"], br["stage"], br["detail"]))
            if br["stage"].startswith("lowering:missing-op"):
                m = re.search(r"(?:factory|predicate|op)[:\s].*?(\w+)$", br["detail"])
                if m:
                    missing_op_symbols[m.group(1)] += 1
    out = [f"# STOP 원장 (렌더 뷰) — {set_} {color}", "",
           "> **자동생성** (porting/scripts/ir_registry.py). 수기 편집 금지 — Canonical IR 이 진실원천.",
           f"> tableVer={TABLE_VER}", ""]
    total_stop = sum(len(v) for v in by_code.values())
    if not total_stop:
        out.append("STOP 분기 없음 — 전 카드 로워링 완료.")
        return "\n".join(out) + "\n"
    if missing_op_symbols:
        out += ["## 프리미티브 큐 (missing-op, 빈도순) — 강모델 선행개발", ""]
        for sym, n in missing_op_symbols.most_common():
            out.append(f"- `{sym}` — {n}개 분기 차단")
        out.append("")
    out.append("## 코드별 STOP 분기")
    out.append("")
    for code in sorted(by_code, key=lambda k: -len(by_code[k])):
        rows = by_code[code]
        out.append(f"### {code} ({len(rows)})")
        out.append("")
        out.append("| 카드 | 타이밍 | stage | detail |")
        out.append("|---|---|---|---|")
        for card, timing, stage, detail in sorted(rows):
            out.append(f"| {card} | {timing} | {stage} | {detail.replace('|','\\|')} |")
        out.append("")
    return "\n".join(out) + "\n"


def main() -> int:
    args = sys.argv[1:]
    targets: list[tuple[str, str]] = []
    if args and args[0] == "--all":
        for d in sorted(IR_CANON.glob("*.*")):
            if d.is_dir():
                s, _, c = d.name.partition(".")
                targets.append((s, c))
    elif len(args) >= 2:
        targets = [(args[0], args[1])]
    elif len(args) == 1:
        for d in sorted(IR_CANON.glob(f"{args[0]}.*")):
            if d.is_dir():
                s, _, c = d.name.partition(".")
                targets.append((s, c))
    else:
        print(__doc__)
        return 1

    pool = {}
    if CARDPOOL.exists():
        pool = json.loads(CARDPOOL.read_text(encoding="utf-8"))
    pool.setdefault("cards", {})
    pool["schema"] = "cardpool/1"

    grand = Counter()
    for set_, color in targets:
        bundle = build(set_, color)
        if not bundle:
            print(f"{set_} {color}: Canonical IR 없음 — 건너뜀")
            continue
        LEDGER_DIR.mkdir(parents=True, exist_ok=True)
        (LEDGER_DIR / f"{set_}.{color}.json").write_text(
            json.dumps(bundle, ensure_ascii=False, indent=2), encoding="utf-8")
        STOP_DIR.mkdir(parents=True, exist_ok=True)
        (STOP_DIR / f"{set_}.{color}.md").write_text(render_stop_md(bundle), encoding="utf-8")

        inc = sum(1 for c in bundle["cards"] if c["included"])
        exc = len(bundle["cards"]) - inc
        grand["included"] += inc
        grand["excluded"] += exc
        for c in bundle["cards"]:
            pool["cards"][c["card"]] = {
                "set": set_, "color": color, "tier": c["tier"], "irHash": c["irHash"],
                "provenance": c["provenance"], "mirrorState": c["mirrorState"],
                "included": c["included"], "reason": c["reason"],
            }
        tiers = Counter(c["tier"] for c in bundle["cards"])
        print(f"{set_} {color}: {inc} included / {exc} excluded  "
              f"(tier1={tiers[1]} tier2={tiers[2]} tier3={tiers[3]})")

    CARDPOOL.write_text(json.dumps(pool, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\n== cardpool: {grand['included']} included / {grand['excluded']} excluded "
          f"(총 {len(pool['cards'])} 카드) → {CARDPOOL.relative_to(REPO)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
