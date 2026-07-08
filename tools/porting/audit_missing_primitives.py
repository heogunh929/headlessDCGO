"""파일단위 프리미티브-누락 감사 (단일 로컬 모델, stdlib 전용).

포팅/변환을 하지 않는다. AS-IS 카드 .cs를 하나씩 읽어, 헤드리스 프리미티브 목록(allowlist)과 대조해
"이 카드가 필요로 하는데 헤드리스에 없는 프리미티브/능력"만 보고하게 한다. 결과는 JSONL로 누적(resumable).

환경변수:
  LOCAL_LLM_BASE_URL   기본 http://192.168.0.42:11434/v1
  LOCAL_LLM_API_KEY    기본 ollama
  AUDIT_MODEL          사용할 모델 id (필수, 예: gemma3:27b)
  AUDIT_ROOT           AS-IS 카드 루트 (기본 DCGO/Assets/Scripts/CardEffect)
  AUDIT_OUT            출력 JSONL (기본 docs/porting/primitive_audit.jsonl)
  AUDIT_LIMIT          처리 최대 장수(디버그, 0=전체)

사용:  AUDIT_MODEL=gemma3:27b python tools/porting/audit_missing_primitives.py
"""

from __future__ import annotations

import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
BASE = os.environ.get("LOCAL_LLM_BASE_URL", "http://192.168.0.42:11434/v1").rstrip("/") + "/chat/completions"
API_KEY = os.environ.get("LOCAL_LLM_API_KEY", "ollama")
MODEL = os.environ.get("AUDIT_MODEL", "")
ROOT = REPO / os.environ.get("AUDIT_ROOT", "DCGO/Assets/Scripts/CardEffect")
OUT = REPO / os.environ.get("AUDIT_OUT", "docs/porting/primitive_audit.jsonl")
LIMIT = int(os.environ.get("AUDIT_LIMIT", "0"))
ALLOWLIST = REPO / "tools" / "porting" / "pilot" / "allowlist.json"

SYSTEM = (
    "You audit a Digimon TCG card's ORIGINAL (Unity C#) effect against the headless engine's AVAILABLE "
    "primitives. You do NOT port or rewrite anything. Your ONLY job: decide whether the headless engine is "
    "MISSING any primitive / factory / capability this card's effect needs.\n"
    "A primitive is MISSING if the card's effect requires a capability with NO matching entry in the provided "
    "headless primitive list AND no reasonable composition of listed primitives covers it (e.g. a needed "
    "COMBINATION like 'pay a self-suspend cost THEN reveal-and-route the top deck card' where each half exists "
    "but the composed body does not, or a needed activation ACTION/timing the engine has no path for).\n"
    "Output EXACTLY this format, nothing else:\n"
    "MISSING: <comma-separated short capability names, or NONE>\n"
    "WHY: <one concise line per missing item; omit if NONE>"
)


def load_reference() -> str:
    data = json.loads(ALLOWLIST.read_text(encoding="utf-8"))
    lines = []
    for cls, names in data.items():
        lines.append(f"{cls}: " + ", ".join(names))
    return "\n".join(lines)


def post(payload: dict, timeout: float = 300.0) -> str:
    data = json.dumps(payload).encode("utf-8")
    last = None
    for attempt in range(4):
        req = urllib.request.Request(
            BASE, data=data,
            headers={"Content-Type": "application/json", "Authorization": f"Bearer {API_KEY}"},
            method="POST",
        )
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                raw = json.loads(resp.read().decode("utf-8"))
                return (raw.get("choices") or [{}])[0].get("message", {}).get("content", "") or ""
        except (urllib.error.URLError, ConnectionError, TimeoutError) as ex:
            last = ex
            time.sleep(2 * (attempt + 1))
    raise last if last is not None else RuntimeError("post failed")


def done_ids() -> set[str]:
    ids: set[str] = set()
    if OUT.exists():
        for line in OUT.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line:
                continue
            try:
                ids.add(json.loads(line)["card_id"])
            except Exception:  # noqa: BLE001
                pass
    return ids


def main() -> int:
    if not MODEL:
        print("AUDIT_MODEL 미설정", file=sys.stderr)
        return 2
    if not ROOT.exists():
        print(f"카드 루트 없음: {ROOT}", file=sys.stderr)
        return 2

    reference = load_reference()
    files = sorted(ROOT.rglob("*.cs"), key=lambda p: p.name)
    already = done_ids()
    OUT.parent.mkdir(parents=True, exist_ok=True)

    total = len(files)
    processed = 0
    with OUT.open("a", encoding="utf-8") as fh:
        for i, f in enumerate(files, 1):
            card_id = f.stem
            if card_id in already:
                continue
            src = f.read_text(encoding="utf-8", errors="ignore")
            user = (
                f"## Headless AVAILABLE primitives (class: symbols)\n{reference}\n\n"
                f"## Original card {card_id}\n{src}\n\n"
                "Report ONLY missing headless primitives/capabilities for THIS card, in the required format."
            )
            t = time.time()
            try:
                text = post({
                    "model": MODEL,
                    "temperature": 0,
                    "stream": False,
                    "messages": [{"role": "system", "content": SYSTEM}, {"role": "user", "content": user}],
                })
                err = ""
            except Exception as ex:  # noqa: BLE001
                text, err = "", str(ex)[:300]
            rec = {"card_id": card_id, "model": MODEL, "text": text.strip(), "sec": round(time.time() - t, 1)}
            if err:
                rec["error"] = err
            fh.write(json.dumps(rec, ensure_ascii=False) + "\n")
            fh.flush()
            processed += 1
            miss = "ERR" if err else ("NONE" if "MISSING: NONE" in text.upper() else "MISS")
            print(f"[{i}/{total}] {card_id}: {miss} ({rec['sec']}s)", flush=True)
            if LIMIT and processed >= LIMIT:
                break

    print(f"완료: {processed}장 감사 (누적 출력 {OUT})", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
