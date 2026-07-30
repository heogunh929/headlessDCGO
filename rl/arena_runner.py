"""아레나 참조 러너 CLI (M4 최소본 — 설계 §1 SDK/러너의 전신).

API 키로 웹소켓 접속 → 래더 큐(또는 지정 도전) → your_turn마다 정책 함수로 착수.
기본 정책은 무작위 합법 수 — M4 게이트("러너 CLI 2개가 래더 매칭으로 한 판 완주") 검증용.

사용:  python arena_runner.py --server http://127.0.0.1:8791 --key <API키> [--create-room | --join <코드>] [--seed N]
       (옵션 없음 = 래더 큐)
"""

from __future__ import annotations

import argparse
import asyncio
import json
import random

import aiohttp


async def run(server: str, key: str, create_room: bool, join: str | None, seed: int, quiet: bool) -> dict | None:
    rng = random.Random(seed)
    url = server.rstrip("/").replace("http://", "ws://").replace("https://", "wss://") + f"/arena?key={key}"
    outcome: dict | None = None

    async with aiohttp.ClientSession() as session:
        async with session.ws_connect(url, heartbeat=30) as ws:
            async for raw in ws:
                if raw.type != aiohttp.WSMsgType.TEXT:
                    break
                msg = json.loads(raw.data)
                kind = msg.get("type")

                if kind == "hello":
                    if not quiet:
                        print(f"[{msg['handle']}] 접속 — 시즌 {msg['season']}")
                    if create_room:
                        await ws.send_json({"type": "create_room"})
                    elif join:
                        await ws.send_json({"type": "join_room", "code": join})
                    else:
                        await ws.send_json({"type": "enqueue"})

                elif kind == "room_created":
                    # flush — 파일 리다이렉트/파이프에서도 코드가 즉시 보이게(스크립트 회수용)
                    print(f"방 생성됨 — 코드: {msg['code']} (상대에게 전달, 참가 대기 중)", flush=True)

                elif kind == "queued":
                    if not quiet:
                        print(f"래더 큐 {msg['position']}번째")

                elif kind == "match_start":
                    if not quiet:
                        print(f"매치 {msg['matchId']} — 좌석 {msg['seat']}, 상대 {msg['opponent']['handle']}"
                              f"(레이팅 {msg['opponent']['rating']}), 내 덱 {msg['yourDeck'].get('name')}")

                elif kind == "your_turn":
                    actions = msg.get("legalActions") or []
                    pick = rng.choice(actions)
                    if not quiet:
                        print(f"  s{msg['stepIndex']} {msg.get('kind')}: {pick['desc']}")
                    await ws.send_json({"type": "action", "index": pick["index"]})

                elif kind == "match_end":
                    outcome = msg
                    print(f"종료: winner={msg.get('winnerSeat')} reason={msg.get('reason')} Δ레이팅={msg.get('ratingDelta')}")
                    break

                elif kind == "error":
                    print(f"오류: {msg.get('code')} {msg.get('message')}")
                    if not msg.get("retry"):
                        break

    return outcome


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", default="http://127.0.0.1:8791")
    parser.add_argument("--key", required=True)
    parser.add_argument("--create-room", action="store_true", help="방을 만들고 코드를 받아 대기")
    parser.add_argument("--join", default=None, help="방 코드로 참가")
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--quiet", action="store_true")
    args = parser.parse_args()
    asyncio.run(run(args.server, args.key, args.create_room, args.join, args.seed, args.quiet))


if __name__ == "__main__":
    main()
