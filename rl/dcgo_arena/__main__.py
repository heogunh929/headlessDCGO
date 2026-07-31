"""dcgo-arena CLI — `python -m dcgo_arena play|daemon [--config 파일]` (온보딩 ①·④).

play   : 한 판(래더) 참가 후 종료. --create-room/--join 도 지원.
daemon : 상주 — 접속 유지, 서버 자동 매칭/래더로 판이 잡히는 대로 계속 둔다. 끊기면 재접속.
설정은 dcgo-arena.toml(config.py 참조). CLI 인자가 설정보다 우선.
"""

from __future__ import annotations

import argparse
import asyncio
import random

from dcgo_arena.client import ArenaClient, ArenaError
from dcgo_arena.config import build_agent, load_config


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(prog="dcgo-arena")
    parser.add_argument("mode", choices=["play", "daemon"], nargs="?", default="play")
    parser.add_argument("--config", default=None, help="dcgo-arena.toml 경로")
    parser.add_argument("--server", default=None)
    parser.add_argument("--key", default=None)
    parser.add_argument("--create-room", action="store_true")
    parser.add_argument("--join", default=None)
    parser.add_argument("--games", type=int, default=1, help="play 모드 래더 연전 판수")
    parser.add_argument("--seed", type=int, default=None)
    parser.add_argument("--quiet", action="store_true")
    return parser.parse_args()


async def run() -> None:
    args = parse_args()
    config = load_config(args.config)
    server = args.server or config.get("server")
    key = args.key or config.get("key")
    if not server or not key:
        raise SystemExit("server/key가 필요합니다 — dcgo-arena.toml 또는 --server/--key "
                         "(참가자 페이지의 퀵스타트에서 설정 파일을 복사하세요)")
    seed = args.seed if args.seed is not None else random.randrange(1, 2 ** 30)
    agent = build_agent(config, seed=seed, verbose=not args.quiet)
    client = ArenaClient(server, key)
    if config.get("_source") and not args.quiet:
        print(f"설정: {config['_source']}")

    def on_event(msg: dict) -> None:
        kind = msg.get("type")
        if args.quiet:
            if kind == "room_created":
                print(f"방 코드: {msg['code']}", flush=True)
            return
        if kind == "hello":
            print(f"[{msg['handle']}] 접속 — 시즌 {msg['season']}")
        elif kind == "room_created":
            print(f"방 생성됨 — 코드: {msg['code']} (상대에게 전달)", flush=True)
        elif kind == "queued":
            print(f"래더 큐 {msg['position']}번째")
        elif kind == "match_start":
            print(f"매치 {msg['matchId']} — 좌석 {msg['seat']}, 상대 {msg['opponent']['handle']}"
                  f"({msg['opponent']['rating']}){' [검증판]' if msg.get('verification') else ''}")
        elif kind == "your_turn":
            print(f"  s{msg['stepIndex']} {msg.get('kind')} — 합법 {len(msg.get('legalActions') or [])}수")

    if args.mode == "daemon":
        backoff = 3
        while True:
            try:
                result = await client.play(agent, mode="ladder", on_event=on_event)
                print(f"종료: winner={result.get('winnerSeat')} reason={result.get('reason')}"
                      f" Δ={result.get('ratingDelta')}", flush=True)
                backoff = 3
            except (ArenaError, OSError) as ex:
                print(f"연결 문제({ex}) — {backoff}초 후 재접속", flush=True)
                await asyncio.sleep(backoff)
                backoff = min(backoff * 2, 60)
        return

    mode = "create_room" if args.create_room else "join_room" if args.join else "ladder"
    for _ in range(args.games):
        result = await client.play(agent, mode=mode, room_code=args.join,
                                   on_event=on_event)
        print(f"종료: winner={result.get('winnerSeat')} reason={result.get('reason')}"
              f" Δ={result.get('ratingDelta')}")
        if mode != "ladder":
            break


def main() -> None:
    asyncio.run(run())


if __name__ == "__main__":
    main()
