"""da.play / da.daemon — TextArena식 한 호출 참가.

    import dcgo_arena as da
    da.play(server=..., key=...,
            agent=da.agents.OpenAICompat(...),   # 생략 = 무작위
            games=10)                            # daemon=True 로 상주

덱은 SDK가 관여하지 않는다(사용자 확정 2026-07-31): 생성·수정·사용 덱 지정 전부
웹 참가자 페이지 전용이며, 서버는 항상 그 계정의 활성 덱으로 참가시킨다.
활성 덱이 없으면 no_deck 에러 — 웹에서 덱을 등록·지정한 뒤 다시 실행한다.
"""

from __future__ import annotations

import asyncio
import random

from dcgo_arena.client import ArenaClient, ArenaError
from dcgo_arena.policies import RandomPolicy


def _default_on_event(msg: dict) -> None:
    kind = msg.get("type")
    if kind == "hello":
        print(f"[{msg['handle']}] 접속 — 시즌 {msg['season']}")
    elif kind == "room_created":
        print(f"방 코드: {msg['code']} (상대에게 전달)", flush=True)
    elif kind == "queued":
        print("큐 대기" + (f" {msg['position']}번째" if msg.get("position") else "")
              + (f" — {msg['notice']}" if msg.get("notice") else ""))
    elif kind == "match_start":
        print(f"매치 {msg['matchId']} — 좌석 {msg['seat']}, 상대 {msg['opponent']['handle']}"
              f"({msg['opponent']['rating']}){' [검증판]' if msg.get('verification') else ''}")


async def _run(server: str, key: str, agent, games: int,
               daemon: bool, room: str | None, join: str | None, on_event) -> list[dict]:
    client = ArenaClient(server, key)
    me = await client.me()
    if me.get("error"):
        raise ArenaError("API 키가 유효하지 않습니다 — 참가자 페이지에서 확인하세요")
    agent = agent or RandomPolicy(random.randrange(1, 2 ** 30))
    on_event = on_event or _default_on_event
    results = []
    mode = "create_room" if room == "create" else "join_room" if join else "ladder"

    if daemon:
        backoff = 3
        while True:
            try:
                result = await client.play(agent, mode="ladder", on_event=on_event)
                print(f"종료: winner={result.get('winnerSeat')} reason={result.get('reason')}"
                      f" Δ={result.get('ratingDelta')}", flush=True)
                results.append(result)
                backoff = 3
            except (ArenaError, OSError) as ex:
                print(f"연결 문제({ex}) — {backoff}초 후 재접속", flush=True)
                await asyncio.sleep(backoff)
                backoff = min(backoff * 2, 60)

    for _ in range(max(1, games)):
        result = await client.play(agent, mode=mode, room_code=join, on_event=on_event)
        print(f"종료: winner={result.get('winnerSeat')} reason={result.get('reason')}"
              f" Δ={result.get('ratingDelta')}")
        results.append(result)
        if mode != "ladder":
            break
    return results


def play(server: str, key: str, agent=None, games: int = 1,
         daemon: bool = False, room: str | None = None, join: str | None = None,
         on_event=None) -> list[dict]:
    """참가 한 호출. games=N 래더 연전, daemon=True 상주, room="create"/join="코드" 룸 매치.
    덱은 웹 참가자 페이지에서 지정한 활성 덱이 자동 적용된다."""
    return asyncio.run(_run(server, key, agent, games, daemon, room, join, on_event))


def daemon(server: str, key: str, agent=None, on_event=None) -> None:
    """상주 참가 — play(daemon=True)의 별칭."""
    play(server, key, agent=agent, daemon=True, on_event=on_event)
