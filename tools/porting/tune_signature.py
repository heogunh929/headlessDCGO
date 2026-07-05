"""P-DB2: 시그니처 입도 튜닝 — 확산율 vs 순도 트레이드오프 측정.

느슨한 시그니처일수록 확산율(레퍼런스 1장이 덮는 카드↑)은 오르지만, 서로 다른 헤드리스 헬퍼로
번역될 카드가 한 클러스터에 섞이면(순도↓) 유추가 깨진다. 이미 포팅된 카드를 정답셋으로:
'같은 시그니처 → 같은 포트 타깃'이 유지되는 한도에서 가장 느슨한 입도를 고른다.

사용:  python tools/porting/tune_signature.py
"""

from __future__ import annotations

import json
import os
import re
from collections import Counter, defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
IR_JSONL = REPO / "docs" / "porting" / "card_ir.jsonl"
PORTED_ROOT = REPO / "src" / "HeadlessDCGO.Engine" / "Assets" / "Scripts" / "CardEffect"


def ported_targets() -> dict[str, tuple[str, ...]]:
    """포팅된 카드 → 사용한 헤드리스 CardEffectFactory 헬퍼 집합(=포트 타깃)."""
    out: dict[str, tuple[str, ...]] = {}
    for path in PORTED_ROOT.rglob("*.cs"):
        txt = path.read_text(encoding="utf-8", errors="ignore")
        if len(txt.splitlines()) <= 12:
            continue
        targets = tuple(sorted(set(re.findall(r"CardEffectFactory\.([A-Za-z0-9_]+)", txt))))
        out[path.stem] = targets
    return out


# --- 후보 시그니처 정의 (AS-IS IR 피처에서 계산) ---------------------------------

def prim_names(rec):
    return frozenset(p["name"] for p in rec["primitives"])


def sig_strict(rec):  # S0 현행: 타이밍 + 프리미티브 멀티셋 + 키워드 멀티셋
    return (tuple(sorted(rec["timings"])),
            tuple(sorted((p["name"], p["count"]) for p in rec["primitives"])),
            tuple(sorted(rec["keywords"])))


def sig_nocounts(rec):  # S1: 카운트 제거(이름 집합)
    return (tuple(sorted(rec["timings"])), prim_names(rec), frozenset(rec["keywords"]))


def sig_semantic(rec):  # S2: 타이밍 + 프리미티브 + 키워드 + 커먼즈(효과 의도)
    return (tuple(sorted(rec["timings"])), prim_names(rec),
            frozenset(rec["keywords"]), frozenset(rec["commons"]))


def sig_notiming(rec):  # S3: 타이밍 제거(번역되는 경우 있음) — 프리미티브+키워드+커먼즈
    return (prim_names(rec), frozenset(rec["keywords"]), frozenset(rec["commons"]))


def sig_shape_only(rec):  # S4: 프리미티브 + 키워드 이름집합만(가장 느슨)
    return (prim_names(rec), frozenset(rec["keywords"]))


# (P-DB2) 효과 설명문 → 액션 태그. DCGO 템플릿 룰텍스트의 동사/명사를 정규화된 태그로.
_ACTION_PATTERNS = [
    ("delete", r"\bdelete\b"), ("draw", r"\bdraw\b"), ("dp_plus", r"gets? \+?\d"),
    ("dp_minus", r"\-\d+ DP|DP \-"), ("suspend", r"\bsuspend\b"), ("unsuspend", r"\bunsuspend\b"),
    ("bounce", r"return .* to .* hand|\bbounce\b"), ("trash", r"\btrash\b"), ("recovery", r"recovery|place .* security"),
    ("memory", r"memory"), ("deenergize", r"de-?digivolve|trash .* digivolution"),
    ("security", r"security"), ("blocker", r"\bblocker\b"), ("piercing", r"piercing|pierce"),
    ("draw_hand", r"add .* to .* hand|to your hand"), ("cannot", r"can'?t|cannot|unaffected"),
    ("play", r"\bplay\b"), ("digivolve", r"digivolve"), ("once_per_turn", r"once per turn"),
]
_action_re = [(tag, re.compile(pat, re.I)) for tag, pat in _ACTION_PATTERNS]


def action_tags(rec) -> frozenset:
    text = " ".join(rec.get("descriptions", []))
    return frozenset(tag for tag, rx in _action_re if rx.search(text))


def sig_desc(rec):  # S5: 키워드클래스 + 설명문 액션태그 (의미 형태)
    return (frozenset(rec["keywords"]), prim_names(rec), action_tags(rec))


def sig_desc_timing(rec):  # S6: S5 + 타이밍
    return (tuple(sorted(rec["timings"])), frozenset(rec["keywords"]), prim_names(rec), action_tags(rec))


CANDIDATES = {
    "S0 strict(현행)": sig_strict,
    "S1 nocounts": sig_nocounts,
    "S2 +commons": sig_semantic,
    "S3 -timing+commons": sig_notiming,
    "S4 shape-only": sig_shape_only,
    "S5 kw+desc-action": sig_desc,
    "S6 +timing": sig_desc_timing,
}


def main() -> None:
    records = [json.loads(l) for l in IR_JSONL.read_text(encoding="utf-8").splitlines() if l.strip()]
    ported = ported_targets()
    ported_recs = [r for r in records if r["card_id"] in ported]

    # pending = 아직 실구현 없는 효과 카드
    pending = [r for r in records if r["card_id"] not in ported and (r["primitives"] or r["keywords"])]

    print(f"{'시그니처':<20} {'pending클러스터':>13} {'확산율':>7} {'순도(포팅셋)':>12} {'ref커버pending':>14}")
    print("-" * 72)
    for name, fn in CANDIDATES.items():
        # 확산율: pending 카드가 몇 개 클러스터로 묶이나
        clusters = Counter(fn(r) for r in pending)
        fanout = len(pending) / max(1, len(clusters))

        # 순도: 포팅된 카드를 시그니처로 묶어, 각 그룹이 단일 포트 타깃인가
        by_sig = defaultdict(list)
        for r in ported_recs:
            by_sig[fn(r)].append(ported[r["card_id"]])
        pure = impure = 0
        for targets in by_sig.values():
            if len(set(targets)) == 1:
                pure += len(targets)
            else:
                impure += len(targets)
        purity = pure / max(1, pure + impure)

        # 레퍼런스가 덮는 pending: pending 시그니처 중 '포팅된 카드가 있는' 시그니처의 pending 수
        ported_sigs = set(by_sig)
        covered = sum(1 for r in pending if fn(r) in ported_sigs)

        print(f"{name:<20} {len(clusters):>13} {fanout:>7.1f} {purity:>11.0%} {covered:>14}")

    # S2 기준 커버 안 되는(레퍼런스 없는) 최대 클러스터 = 강모델 시딩 ROI 상위
    print("\n=== S2(+commons) 기준 레퍼런스 없는 최대 pending 클러스터 (시딩 ROI) ===")
    by_sig_s2 = defaultdict(list)
    for r in ported_recs:
        by_sig_s2[sig_semantic(r)] = ported[r["card_id"]]
    pend_clusters = defaultdict(list)
    for r in pending:
        pend_clusters[sig_semantic(r)].append(r["card_id"])
    unref = [(sig, cards) for sig, cards in pend_clusters.items() if sig not in by_sig_s2]
    for sig, cards in sorted(unref, key=lambda x: -len(x[1]))[:8]:
        prims = ",".join(sorted(sig[1])) or "(no-factory)"
        print(f"  {len(cards):>4}장  prims:[{prims[:50]}] 예:{cards[0]}")


if __name__ == "__main__":
    main()
