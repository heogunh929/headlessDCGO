"""seat 파일 브리지 — 에이전트 앱(Claude Code/Codex 등)의 세션 AI가 직접 수를 두는 통로.

사용자 확정(2026-08-01): 참가 UX = 에이전트 앱에서 스킬(/디지몬한판)을 호출하면 그 세션의
AI가 한 판을 직접 진행한다. ws는 착수 제한(기본 60초)이 있어 연결을 물고 있을 상주가 필요
한데, 세션 AI는 소켓을 직접 못 잡으므로 이 브리지가 파일로 중계한다:

    브리지(백그라운드): ws 접속·매칭 → 결정마다 <dir>/turn.json 기록 → <dir>/answer.json 대기
    에이전트(세션 AI):  turn.json의 stateText·legalActions를 읽고 {"index": N}을 answer.json에

  - {"auto": "random"} 을 쓰면 남은 판을 무작위 합법 수로 자동 완주(긴 판 이관·이탈용)
  - 55초 무응답·불법 인덱스 반복 시 무작위 폴백 — 몰수패 방지가 우선
  - 판이 끝나면 <dir>/result.json 기록 후 종료
"""

from __future__ import annotations

import asyncio
import json
import random
import time
from pathlib import Path

from dcgo_arena.client import ArenaClient
from dcgo_arena.state_text import state_to_text


def _write_json(path: Path, payload: dict) -> None:
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(payload, ensure_ascii=False, indent=1), encoding="utf-8")
    tmp.replace(path)   # 원자 교체 — 에이전트가 반쯤 쓰인 파일을 읽지 않게


async def run_seat(server: str, key: str, directory: str = ".dcgo-seat",
                   mode: str = "ladder", join: str | None = None, seed: int = 0,
                   lang: str | None = None) -> dict:
    d = Path(directory)
    d.mkdir(parents=True, exist_ok=True)
    for name in ("turn.json", "answer.json", "result.json", "status.json"):
        (d / name).unlink(missing_ok=True)

    rng = random.Random(seed or random.randrange(1, 2 ** 30))
    auto = {"on": False}
    counter = {"n": 0}
    names: dict = {}   # 카드번호→한글명 — 접속 후 채움(실패 시 빈 채로 영문 진행)

    async def policy(turn: dict) -> int:
        legal = [a["index"] for a in turn["legalActions"]]
        if auto["on"]:
            return rng.choice(legal)
        counter["n"] += 1
        (d / "answer.json").unlink(missing_ok=True)
        payload = {"turnNo": counter["n"], "kind": turn.get("kind"),
                   "stateText": state_to_text(turn, names),
                   "legalActions": turn.get("legalActions")}
        _write_json(d / "turn.json", payload)

        deadline = time.monotonic() + 55   # 서버 착수 제한(60초) 안쪽
        while time.monotonic() < deadline:
            if (d / "answer.json").exists():
                try:
                    answer = json.loads((d / "answer.json").read_text(encoding="utf-8"))
                except (OSError, json.JSONDecodeError):
                    await asyncio.sleep(0.2)
                    continue
                (d / "answer.json").unlink(missing_ok=True)
                if answer.get("auto") == "random":
                    auto["on"] = True
                    return rng.choice(legal)
                index = int(answer.get("index", -1))
                if index in legal:
                    return index
                payload["error"] = f"불법 인덱스 {index} — legalActions에서 다시 고르세요"
                _write_json(d / "turn.json", payload)
            await asyncio.sleep(0.3)
        return rng.choice(legal)   # 무응답 폴백

    def on_event(msg: dict) -> None:
        if msg.get("type") == "match_start" and mode == "practice" and not msg.get("practice"):
            # 연습 요청이 랭킹전으로 성립 = 클라이언트/서버 버전 불일치(실측 2026-08-01:
            # 과도기 SDK의 --practice 무시 폴백) — 조용히 랭킹전을 두는 것보다 즉시 중단이 낫다.
            raise RuntimeError("연습판(--practice) 요청이 랭킹전으로 성립 — SDK를 서버에서 새로 받으세요")
        if msg.get("type") in ("hello", "queued", "match_start", "room_created"):
            _write_json(d / "status.json", msg)
            print(json.dumps(msg, ensure_ascii=False), flush=True)

    client = ArenaClient(server, key)
    # 카드명 언어(사용자 확정 2026-08-01): --lang > 서버 계정 설정(me.lang) > ko.
    # ko = 한글명(없으면 영문 폴백), en = 영문명. 실패해도 판은 카드번호로 진행.
    try:
        if lang is None:
            lang = str((await client.me()).get("lang") or "ko")
        card_data = await client.cards()
        for cid, c in (card_data.get("cards") or {}).items():
            name = c.get("name") if lang == "en" else (c.get("nameKo") or c.get("name"))
            if name:
                names[cid] = name
    except Exception:
        pass
    result = await client.play(policy, mode=mode, room_code=join, on_event=on_event)
    (d / "turn.json").unlink(missing_ok=True)
    _write_json(d / "result.json", result)
    print("결과: " + json.dumps(result, ensure_ascii=False), flush=True)
    return result
