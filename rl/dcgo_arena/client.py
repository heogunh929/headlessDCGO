"""ArenaClient — 아레나 ws 프로토콜(설계 §3.2)의 파이썬 클라이언트.

서버→ hello | queued | room_created | match_start | your_turn | match_end | error
클라→ enqueue | create_room | join_room | action | resign

정책은 콜러블 하나: policy(turn: dict) -> int (착수 인덱스). turn에는 kind/state(관측 필터
적용된 보드)/legalActions([{index, desc}])가 실린다. 불법 수는 서버가 retry로 되돌려주며
클라이언트는 정책을 다시 호출한다(무한 방지 카운터 포함).
"""

from __future__ import annotations

import json
from typing import Awaitable, Callable

import aiohttp

Policy = Callable[[dict], "int | Awaitable[int]"]


class ArenaError(RuntimeError):
    pass


class ArenaClient:
    def __init__(self, server: str, key: str):
        self.server = server.rstrip("/")
        self.key = key
        self.handle: str | None = None

    def _ws_url(self) -> str:
        return self.server.replace("http://", "ws://").replace("https://", "wss://") + f"/arena?key={self.key}"

    async def play(self, policy: Policy, mode: str = "ladder", room_code: str | None = None,
                   on_event: Callable[[dict], None] | None = None) -> dict:
        """한 판 참가(완주까지). mode = ladder | create_room | join_room. 결과 match_end를 반환."""
        import inspect

        async def decide(turn: dict) -> int:
            picked = policy(turn)
            return await picked if inspect.isawaitable(picked) else picked

        async with aiohttp.ClientSession() as session:
            async with session.ws_connect(self._ws_url(), heartbeat=30) as ws:
                async for raw in ws:
                    if raw.type != aiohttp.WSMsgType.TEXT:
                        break
                    msg = json.loads(raw.data)
                    if on_event:
                        on_event(msg)
                    kind = msg.get("type")

                    if kind == "hello":
                        self.handle = msg.get("handle")
                        if mode == "create_room":
                            await ws.send_json({"type": "create_room"})
                        elif mode == "join_room":
                            if not room_code:
                                raise ArenaError("join_room에는 room_code 필요")
                            await ws.send_json({"type": "join_room", "code": room_code})
                        else:
                            await ws.send_json({"type": "enqueue"})

                    elif kind == "your_turn":
                        retries = 0
                        index = await decide(msg)
                        await ws.send_json({"type": "action", "index": int(index)})
                        # 불법 수 재시도는 서버 error(retry)로 돌아온다 — 아래 error 분기에서 처리하기
                        # 위해 마지막 turn을 보관.
                        self._last_turn, self._retries = msg, retries

                    elif kind == "error":
                        if msg.get("retry") and getattr(self, "_last_turn", None) is not None:
                            self._retries = getattr(self, "_retries", 0) + 1
                            if self._retries > 5:
                                await ws.send_json({"type": "resign"})
                                continue
                            index = await decide(self._last_turn)
                            await ws.send_json({"type": "action", "index": int(index)})
                        elif msg.get("code") in ("auth", "no_deck", "no_room", "busy", "host_unavailable", "engine"):
                            raise ArenaError(f"{msg.get('code')}: {msg.get('message')}")

                    elif kind == "match_end":
                        return msg

        raise ArenaError("연결이 판 종료 전에 닫혔습니다")

    # ---- 덱/이력 REST (키 인증 표면) ----

    async def _api(self, method: str, path: str, body: dict | None = None) -> dict | list:
        async with aiohttp.ClientSession() as session:
            async with session.request(method, f"{self.server}{path}",
                                       headers={"X-Arena-Key": self.key, "Content-Type": "application/json"},
                                       data=json.dumps(body) if body is not None else None) as response:
                return await response.json()

    async def me(self) -> dict:
        return await self._api("GET", "/api/arena/me")

    async def decks(self) -> list:
        """등록 덱 조회(읽기 전용) — 생성·수정·활성 지정은 웹 참가자 페이지 전용(2026-07-31 확정)."""
        return await self._api("GET", "/api/arena/decks")

    async def history(self) -> list:
        return await self._api("GET", "/api/arena/history")
