"""P-DB2+: 포팅 작업량 분포 심층 분석 — 시딩 수의 실제 형태를 여러 각도로.

시딩 수는 '입도'의 함수다. 이 스크립트는 입도 스윕(정확→액션패밀리→shape)으로 팬아웃 곡선을 그리고,
발매순 첫 타깃(BT1)의 머리/꼬리, 꼬리의 복잡도(고유효과가 어렵나 쉽나), 액션패밀리 커버리지를 측정한다.
"""

from __future__ import annotations

import json
import sqlite3
from collections import Counter, defaultdict
from pathlib import Path

DB = Path(__file__).resolve().parents[2] / "docs" / "porting" / "card_ir.sqlite"


def load():
    c = sqlite3.connect(str(DB))
    cols = "card_id,set_code,shape,signature_hash,action_tags,keywords,timings,readiness,port_status,card_type"
    rows = [dict(zip(cols.split(","), r))
            for r in c.execute(f"SELECT {cols} FROM card")]
    c.close()
    for r in rows:
        r["action_tags"] = tuple(json.loads(r["action_tags"]))
        r["keywords"] = tuple(json.loads(r["keywords"]))
        r["timings"] = tuple(json.loads(r["timings"]))
        r["prim_count"] = None
    return rows


def head_tail(sig_of, cards):
    sizes = Counter(sig_of(r) for r in cards)
    n = len(cards)
    singles = sum(1 for v in sizes.values() if v == 1)
    head = n - singles
    return len(sizes), singles, head


def main():
    rows = load()
    # 분석 대상 = 아직 포팅 안 된 효과 카드 (vanilla는 has_effect 별개라 여기선 전부 효과보유)
    pending = [r for r in rows if r["port_status"] == "pending"]

    # 입도별 시그니처 함수
    grans = {
        "정확(S5 hash)": lambda r: r["signature_hash"],
        "액션+키워드": lambda r: (r["action_tags"], r["keywords"]),
        "액션태그만": lambda r: r["action_tags"],
        "키워드만": lambda r: r["keywords"],
    }

    print(f"=== A. 팬아웃 곡선 (pending {len(pending)}장, 입도별 시딩 수) ===")
    print(f"{'입도':<16}{'시딩(클러스터)':>13}{'고유효과(크기1)':>15}{'배치가능(크기2+)':>15}{'팬아웃':>8}")
    for name, fn in grans.items():
        clusters, singles, head = head_tail(fn, pending)
        print(f"{name:<16}{clusters:>13}{singles:>15}{head:>15}{len(pending)/clusters:>8.1f}")

    # B. 발매순 첫 타깃들의 머리/꼬리
    print(f"\n=== B. 세트별 분포 (정확 시그니처 기준, 발매순 초기 세트) ===")
    print(f"{'세트':<8}{'pending':>8}{'시딩':>7}{'고유효과':>9}{'배치가능':>9}{'꼬리%':>7}")
    for st in ["ST1", "ST2", "ST3", "BT1", "BT2", "BT3", "BT4", "BT5"]:
        sub = [r for r in pending if r["set_code"] == st]
        if not sub:
            continue
        clusters, singles, head = head_tail(lambda r: r["signature_hash"], sub)
        print(f"{st:<8}{len(sub):>8}{clusters:>7}{singles:>9}{head:>9}{singles/len(sub):>6.0%}")

    # C. 꼬리(고유효과)의 복잡도 — 고유하다고 어려운 건 아니다
    print(f"\n=== C. 고유효과(크기1) 복잡도 분해 ===")
    sizes = Counter(r["signature_hash"] for r in pending)
    singletons = [r for r in pending if sizes[r["signature_hash"]] == 1]
    by_shape = Counter(r["shape"] for r in singletons)
    print(f"고유효과 {len(singletons)}장의 형태: {dict(by_shape)}")
    factory_single = by_shape.get("factory", 0)
    print(f"  → 이 중 factory형 {factory_single}장 = 고유해도 인자채우기(자동생성 후보, 강모델 불요)")

    # D. 액션패밀리 커버리지 — 경량모델이 패밀리 내 일반화한다고 가정할 때의 하한
    print(f"\n=== D. 액션패밀리 관점 (경량모델 일반화 가정 시 시딩 하한) ===")
    fam = defaultdict(list)
    for r in pending:
        fam[r["action_tags"]].append(r)
    ported_fams = set()
    for r in rows:
        if r["port_status"] == "ported":
            ported_fams.add(r["action_tags"])
    fams_with_ref = sum(1 for k in fam if k in ported_fams)
    cards_in_covered_fam = sum(len(v) for k, v in fam.items() if k in ported_fams)
    print(f"액션패밀리 {len(fam)}종 중 이미 레퍼런스 보유 {fams_with_ref}종")
    print(f"  → 레퍼런스 있는 패밀리가 덮는 pending: {cards_in_covered_fam}장")
    print(f"  → 시딩 필요 패밀리(레퍼런스 없음): {len(fam)-fams_with_ref}종")
    # 무(無)액션태그 = 설명문에서 액션 못 뽑은 카드(정적효과 등) 별도
    no_tag = len(fam.get((), []))
    print(f"  주의: 액션태그 없는(정적효과/설명문 미매칭) pending {no_tag}장 — 이 그룹은 별도 세분 필요")


if __name__ == "__main__":
    main()
