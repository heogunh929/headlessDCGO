"""포팅 태스크 카드 생성기 (P-DB1, card_porting_database_design.md §0.2 북극성).

경량 모델에게 주는 '직관적 작업 단위'를 DB에서 조립한다:
  [대상 AS-IS] + [구조가 같은 이미 포팅된 레퍼런스: AS-IS→헤드리스 쌍] + [무엇이 다른가]
모델은 자유 생성이 아니라 레퍼런스에 대한 **유추 채우기**만 한다.

사용:
  python tools/porting/porting_task.py BT1_004            # 사람이 읽는 형태
  python tools/porting/porting_task.py BT1_004 --json     # 오케스트레이터 입력(세팅 트랙 인터페이스)
  python tools/porting/porting_task.py --set BT1 --list   # 세트의 ready 큐
"""

from __future__ import annotations

import argparse
import json
import sqlite3
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
DB_PATH = REPO / "docs" / "porting" / "card_ir.sqlite"
ASIS_ROOT = REPO / "DCGO" / "Assets" / "Scripts" / "CardEffect"
PORTED_ROOT = REPO / "src" / "HeadlessDCGO.Engine" / "Assets" / "Scripts" / "CardEffect"


def read_card(conn, card_id: str) -> dict:
    cols = [d[0] for d in conn.execute("SELECT * FROM card WHERE card_id=?", (card_id,)).description]
    row = conn.execute("SELECT * FROM card WHERE card_id=?", (card_id,)).fetchone()
    if row is None:
        raise SystemExit(f"card not found in DB: {card_id}")
    return dict(zip(cols, row))


def read_source(root: Path, source_path: str) -> str:
    path = root / source_path
    return path.read_text(encoding="utf-8", errors="ignore") if path.exists() else ""


def ported_path(card_id: str) -> Path | None:
    matches = list(PORTED_ROOT.rglob(f"{card_id}.cs"))
    return matches[0] if matches else None


def build_task(conn, card_id: str) -> dict:
    card = read_card(conn, card_id)
    task = {
        "card_id": card_id,
        "shape": card["shape"],
        "readiness": card["readiness"],
        "target_asis": read_source(ASIS_ROOT, card["source_path"]),
        "target_path": f"src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/{card['source_path']}",
        "primitives": [
            {"name": n, "count": cnt, "calls": json.loads(calls)}
            for n, cnt, calls in conn.execute(
                "SELECT name, count, calls FROM card_primitive WHERE card_id=?", (card_id,)
            )
        ],
        "reference": None,
        "instruction": None,
    }

    ref_id = card["reference_card"]
    if ref_id:
        ref = read_card(conn, ref_id)
        ref_ported = ported_path(ref_id)
        task["reference"] = {
            "card_id": ref_id,
            "asis": read_source(ASIS_ROOT, ref["source_path"]),
            "ported": ref_ported.read_text(encoding="utf-8", errors="ignore") if ref_ported else "",
        }
        task["instruction"] = (
            f"레퍼런스 {ref_id}는 대상과 같은 포팅 시그니처다. 레퍼런스의 AS-IS→헤드리스 변환을 그대로 "
            f"적용하되, 대상 AS-IS의 인자값(DP·매수·조건 술어 등)만 바꿔 넣어라. 구조·팩토리·타이밍은 동일하게."
        )
    elif card["readiness"] == "review":
        task["instruction"] = (
            "이 시그니처에 이미 포팅된 레퍼런스가 없다. 경량 모델 단독 부적합 — 강모델/사람이 이 클러스터의 "
            "레퍼런스 1장을 먼저 확정해야 한다(코루틴 의미 번역 판단 필요)."
        )
    elif card["readiness"] == "blocked":
        task["instruction"] = (
            f"누락 프리미티브로 대기: {card['blocking_prims']}. 프리미티브 착지 전까지 포팅 큐에서 격리."
        )
    return task


def print_human(task: dict) -> None:
    print(f"═══ 포팅 태스크: {task['card_id']} [{task['shape']} · {task['readiness']}] ═══\n")
    print(f"■ 지시:\n{task['instruction']}\n")
    print(f"■ 대상 AS-IS ({task['target_path']}):\n{strip(task['target_asis'])}\n")
    if task["reference"]:
        r = task["reference"]
        print(f"■ 레퍼런스 {r['card_id']} — AS-IS:\n{strip(r['asis'])}\n")
        print(f"■ 레퍼런스 {r['card_id']} — 헤드리스 포팅본 (이것을 본떠라):\n{strip(r['ported'])}")


def strip(source: str) -> str:
    lines = [ln for ln in source.splitlines() if ln.strip() and not ln.strip().startswith("using ")]
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("card_id", nargs="?")
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--set", dest="set_code")
    parser.add_argument("--list", action="store_true")
    args = parser.parse_args()

    conn = sqlite3.connect(str(DB_PATH))

    if args.list:
        where = "port_status='pending' AND readiness='ready'"
        params: tuple = ()
        if args.set_code:
            where += " AND set_code=?"
            params = (args.set_code,)
        rows = conn.execute(
            f"SELECT card_id, shape, reference_card FROM card WHERE {where} ORDER BY card_id", params
        ).fetchall()
        print(f"ready 큐 ({args.set_code or 'ALL'}): {len(rows)}장")
        for cid, shape, ref in rows[:60]:
            print(f"  {cid:<10} {shape:<8} ref={ref or '(codegen 가능)'}")
        return

    if not args.card_id:
        parser.error("card_id 또는 --list 필요")

    task = build_task(conn, args.card_id)
    if args.json:
        print(json.dumps(task, ensure_ascii=False, indent=2))
    else:
        print_human(task)


if __name__ == "__main__":
    main()
