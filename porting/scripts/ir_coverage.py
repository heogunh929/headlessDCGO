#!/usr/bin/env python3
"""저장소 전체 결정론 커버리지 진단 (pipeline-v3, 인메모리 — 파일 미생성).

porting/data/ir-src/*/*.json (CardIr.Extract 산출) 전체를 ir_pipeline 의 결정론 로워링에
통과시켜 통계만 집계한다. 미러/Canonical IR 을 쓰지 않으므로 저장소를 오염시키지 않는다.

리포트: 총 effect카드 / 결정론 자동포팅(전분기 lowered) 수·% / STOP 코드 히스토그램 /
강모델 큐(미해결 심볼 빈도순: missing-op·missing-rule).

사용: python3 porting/scripts/ir_coverage.py
"""
from __future__ import annotations

import importlib.util
import json
import re
from collections import Counter
from pathlib import Path

PORTING = Path(__file__).resolve().parents[1]
IR_SRC = PORTING / "data/ir-src"

# ir_pipeline 을 모듈로 로드해 로워링 로직 재사용
spec = importlib.util.spec_from_file_location("irp", PORTING / "scripts/ir_pipeline.py")
irp = importlib.util.module_from_spec(spec)
spec.loader.exec_module(irp)


def main() -> int:
    irp.SYMBOLS = irp.load_symbols()
    irp.derive_predicate_tables()
    irp.ATOMS = irp.load_atoms()
    irp.INTENTS, irp.PLUMBING = irp.load_intents()

    total = fully = 0
    code_hist = Counter()
    missing_op = Counter()
    missing_rule = Counter()
    per_set = Counter()
    per_set_lowered = Counter()

    sym_re = re.compile(r"(?:factory|op|helper|predicate|commons|intent mapping|call)[:\s]+.*?([A-Za-z_]\w+)\b")

    for f in sorted(IR_SRC.glob("*/*.json")):
        try:
            src = json.loads(f.read_text(encoding="utf-8"))
        except Exception:
            continue
        if not src.get("branches"):
            continue
        total += 1
        setname = src.get("set", "?")
        per_set[setname] += 1
        try:
            canon, _ = irp.lower_card(src)
        except Exception:
            code_hist["<pipeline-error>"] += 1
            continue
        stops = [b["stop"] for b in canon["branches"] if "stop" in b]
        if not stops:
            fully += 1
            per_set_lowered[setname] += 1
            continue
        for s in stops:
            code_hist[s["code"]] += 1
            # 미해결 심볼 추출 (detail 마지막 식별자)
            stage = s.get("stage", "")
            det = s.get("detail", "")
            m = re.findall(r"[A-Za-z_]\w{2,}", det)
            sym = m[-1] if m else "?"
            if stage.startswith("lowering:missing-op"):
                missing_op[sym] += 1
            elif stage.startswith("lowering:missing-rule"):
                missing_rule[sym] += 1

    print(f"=== 저장소 전체 결정론 커버리지 (effect카드 {total}장) ===")
    print(f"결정론 자동포팅(전분기 lowered): {fully}장  ({100*fully/total:.1f}%)" if total else "n/a")
    print(f"STOP 잔존: {total-fully}장\n")

    print("STOP 코드 히스토그램(분기 기준):")
    for k, v in code_hist.most_common():
        print(f"  {v:5d}  {k}")

    print("\n강모델 큐 — missing-op (프리미티브 부재 후보, 빈도순 상위 20):")
    for k, v in missing_op.most_common(20):
        print(f"  {v:4d}  {k}")

    print("\n강모델/검수 큐 — missing-rule (매핑·규칙 부재, 빈도순 상위 20):")
    for k, v in missing_rule.most_common(20):
        print(f"  {v:4d}  {k}")

    print("\n세트별 결정론 커버리지 (상위 15, lowered/total):")
    for s, n in per_set.most_common(15):
        print(f"  {s:8s} {per_set_lowered[s]:3d}/{n:3d}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
