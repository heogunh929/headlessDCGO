"""카드 포팅 IR JSONL → SQLite DB + 파생 뷰 (P-DB1, stdlib 전용).

card_porting_database_design.md §1-3 구현. 추출기(tools/CardIrExtractor)가 낸 card_ir.jsonl을
로드하고, 헤드리스 프리미티브 갭·cards.json 카드타입·포팅 상태·시그니처 해시를 붙여 card_ir.sqlite를
만든다. 조회/집계는 이 DB에 SQL로.

사용:  python tools/porting/build_card_db.py
"""

from __future__ import annotations

import hashlib
import json
import re
import sqlite3
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
IR_JSONL = REPO / "docs" / "porting" / "card_ir.jsonl"
DB_PATH = REPO / "docs" / "porting" / "card_ir.sqlite"
CARDS_JSON = REPO / "src" / "HeadlessDCGO.Engine" / "Assets" / "CardBaseEntity" / "cards.json"
HEADLESS_SRC = REPO / "src" / "HeadlessDCGO.Engine" / "Assets" / "Scripts" / "Script"
PORTED_ROOT = REPO / "src" / "HeadlessDCGO.Engine" / "Assets" / "Scripts" / "CardEffect"

# AS-IS 정규화 규칙과 동일 (cards.json 조인용). CardVocabulary.CanonicalCardNumber 미러.
_VARIANT = re.compile(r"_P\d+$")


def canonical(card_number: str) -> str:
    return _VARIANT.sub("", card_number.strip().upper().replace("-", "_"))


def headless_primitives() -> set[str]:
    """헤드리스가 정의한 팩토리 프리미티브 이름 집합 (readiness 판정용)."""
    names: set[str] = set()
    pat = re.compile(r"public static [A-Za-z0-9_<>,?\[\] ]+ ([A-Za-z0-9_]+)\(")
    for path in HEADLESS_SRC.rglob("*.cs"):
        for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
            m = pat.search(line)
            if m:
                names.add(m.group(1))
    return names


def card_types() -> dict[str, str]:
    types: dict[str, str] = {}
    for rec in json.loads(CARDS_JSON.read_text(encoding="utf-8")):
        types[canonical(rec["cardNumber"])] = rec.get("cardType", "Unknown")
    return types


def port_shape(rec: dict) -> str:
    """포팅 형태 — 경량 모델의 작업 종류를 가르는 1급 신호.
    factory  = CardEffectFactory one-liner (기계적 인자채우기)
    inline   = new *Class + 코루틴 (의미 번역 → 헤드리스 헬퍼 매핑, 레퍼런스 유추)
    mixed    = 둘 다
    vanilla  = 효과 없음
    """
    has_prim = bool(rec["primitives"])
    has_kw = bool(rec["keywords"])
    if not has_prim and not has_kw:
        return "vanilla"
    if has_prim and has_kw:
        return "mixed"
    return "factory" if has_prim else "inline"


# (P-DB2) 설명문 액션 태그 — DCGO 템플릿 룰텍스트에서 액션 동사를 정규화. 코루틴 액션이 정적
# 추출로 안 잡히는 경우의 판별 신호(tune_signature.py에서 순도 89%로 실측된 S5 시그니처의 핵심).
_ACTION_PATTERNS = [
    ("delete", r"\bdelete\b"), ("draw", r"\bdraw\b"), ("dp_plus", r"gets? \+?\d"),
    ("dp_minus", r"\-\d+ DP|DP \-"), ("suspend", r"\bsuspend\b"), ("unsuspend", r"\bunsuspend\b"),
    ("bounce", r"return .* to .* hand|\bbounce\b"), ("trash", r"\btrash\b"),
    ("recovery", r"recovery|place .* security"), ("memory", r"memory"),
    ("deenergize", r"de-?digivolve|trash .* digivolution"), ("security", r"security"),
    ("blocker", r"\bblocker\b"), ("piercing", r"piercing|pierce"),
    ("to_hand", r"add .* to .* hand|to your hand"), ("cannot", r"can'?t|cannot|unaffected"),
    ("play", r"\bplay\b"), ("digivolve", r"digivolve"), ("once_per_turn", r"once per turn"),
]
_action_re = [(tag, re.compile(pat, re.I)) for tag, pat in _ACTION_PATTERNS]


def action_tags(rec: dict) -> list[str]:
    text = " ".join(rec.get("descriptions", []))
    return sorted(tag for tag, rx in _action_re if rx.search(text))


def signature_hash(rec: dict) -> str:
    """레퍼런스 페어링 키 = 인라인클래스 + 프리미티브이름 + 설명문 액션태그 (S5, tune_signature.py 실측).
    순도 우선(89%): 같은 해시 = 같은 포트 타깃일 확률 높음 → 신뢰할 유추 원본. strict보다 커버리지는
    낮지만(틀린 레퍼런스가 경량 모델을 오도하는 것보다 신뢰도가 중요)."""
    prim_names = sorted({p["name"] for p in rec["primitives"]})
    payload = json.dumps([sorted(rec["keywords"]), prim_names, action_tags(rec)], sort_keys=True)
    return hashlib.sha1(payload.encode()).hexdigest()[:16]


def already_ported(card_id: str) -> bool:
    """헤드리스에 실구현(>12줄 = 스켈레톤 초과)이 있으면 ported로 표시."""
    for path in PORTED_ROOT.rglob(f"{card_id}.cs"):
        if len(path.read_text(encoding="utf-8", errors="ignore").splitlines()) > 12:
            return True
    return False


def main() -> None:
    if not IR_JSONL.exists():
        raise SystemExit(f"IR not found: {IR_JSONL}\n먼저: dotnet run --project tools/CardIrExtractor")

    hl_prims = headless_primitives()
    types = card_types()
    records = [json.loads(line) for line in IR_JSONL.read_text(encoding="utf-8").splitlines() if line.strip()]

    # 커버리지 자기검증: IR 프리미티브 전량 대비 헤드리스 갭.
    all_prims = {p["name"] for r in records for p in r["primitives"]}
    gap_prims = sorted(all_prims - hl_prims)

    # 1차 패스: 상태·형태·시그니처. 2차 패스에서 레퍼런스 페어링(포팅된 카드 인덱스 필요).
    enriched = []
    for r in records:
        cid = r["card_id"]
        missing = sorted({p["name"] for p in r["primitives"]} & set(gap_prims))
        status = "ported" if already_ported(cid) else "pending"
        enriched.append({
            **r,
            "shape": port_shape(r),
            "sig": signature_hash(r),
            "blocking": missing,
            "status": status,
        })

    # 레퍼런스 인덱스: 같은 시그니처의 이미 포팅된 카드 = 유추 원본(경량 모델의 정답 예시).
    ported_by_sig: dict[str, list[str]] = {}
    for e in enriched:
        if e["status"] == "ported":
            ported_by_sig.setdefault(e["sig"], []).append(e["card_id"])

    DB_PATH.unlink(missing_ok=True)
    conn = sqlite3.connect(str(DB_PATH))
    conn.execute("""
        CREATE TABLE card (
            card_id TEXT PRIMARY KEY, set_code TEXT, color TEXT, card_type TEXT,
            has_effect INTEGER, shape TEXT, timings TEXT, commons TEXT, keywords TEXT,
            actions TEXT, action_tags TEXT, descriptions TEXT,
            signature_hash TEXT, readiness TEXT, blocking_prims TEXT,
            port_status TEXT, reference_card TEXT, source_path TEXT
        )""")
    conn.execute("""
        CREATE TABLE card_primitive (
            card_id TEXT, name TEXT, count INTEGER, calls TEXT,
            FOREIGN KEY (card_id) REFERENCES card(card_id)
        )""")

    for e in enriched:
        # readiness: blocked(갭 프리미티브) > factory형은 ready > inline/mixed는 레퍼런스 있으면 ready,
        # 없으면 review(번역 판단 필요 — 경량 모델 단독 부적합, 강모델/사람 레퍼런스 선행).
        refs = [c for c in ported_by_sig.get(e["sig"], []) if c != e["card_id"]]
        reference = refs[0] if refs else None
        if e["blocking"]:
            readiness = "blocked"
        elif not e["has_effect"]:
            readiness = "vanilla"
        elif e["shape"] == "factory":
            readiness = "ready"
        else:
            readiness = "ready" if reference else "review"

        conn.execute(
            "INSERT INTO card VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
            (e["card_id"], e["set_code"], e["color"], types.get(e["card_id"], "Unknown"),
             int(e["has_effect"]), e["shape"], json.dumps(e["timings"]), json.dumps(e["commons"]),
             json.dumps(e["keywords"]), json.dumps(e.get("actions", [])),
             json.dumps(action_tags(e)), json.dumps(e.get("descriptions", [])),
             e["sig"], readiness, json.dumps(e["blocking"]),
             e["status"], reference, e["source_path"]),
        )
        for p in e["primitives"]:
            conn.execute(
                "INSERT INTO card_primitive VALUES (?,?,?,?)",
                (e["card_id"], p["name"], p["count"], json.dumps(p["calls"])),
            )

    conn.execute("CREATE INDEX idx_sig ON card(signature_hash)")
    conn.execute("CREATE INDEX idx_set ON card(set_code)")
    conn.execute("CREATE INDEX idx_status ON card(port_status)")
    conn.commit()

    def count(where: str) -> int:
        return conn.execute("SELECT COUNT(*) FROM card WHERE " + where).fetchone()[0]

    total_ported = count("port_status='ported'")
    refs_paired = count("port_status='pending' AND reference_card IS NOT NULL")
    print(f"DB: {DB_PATH}")
    print(f"카드 {len(records)} / 이미포팅 {total_ported}")
    print("포팅 형태:")
    for shape in ("factory", "inline", "mixed", "vanilla"):
        total_shape = count(f"shape='{shape}'")
        pending_shape = count(f"shape='{shape}' AND port_status='pending'")
        print(f"  {shape:<8} {total_shape}장  (pending {pending_shape})")
    print("준비도(pending 중):")
    for rd in ("ready", "review", "blocked"):
        n_rd = count(f"readiness='{rd}' AND port_status='pending'")
        print(f"  {rd:<8} {n_rd}장")
    print(f"레퍼런스 페어링된 pending 카드: {refs_paired}장")
    print(f"갭 프리미티브 {len(gap_prims)}종: {gap_prims}")
    conn.close()


if __name__ == "__main__":
    main()
