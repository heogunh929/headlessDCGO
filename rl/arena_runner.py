"""아레나 참조 러너 CLI — dcgo-arena SDK 기반 (M5 공개 품질).

설정만으로 래더/룸 참가. 정책 2종:
  --policy random           무작위 합법 수(연결 검증·바닥선)
  --policy llm              OpenAI 호환 endpoint (--llm-base/--llm-key/--llm-model 또는
                            환경변수 DCGO_LLM_BASE/DCGO_LLM_KEY/DCGO_LLM_MODEL)

사용 예:
  python arena_runner.py --server http://서버:8791 --key <API키>                # 래더
  python arena_runner.py ... --create-room                                     # 방 생성
  python arena_runner.py ... --join ABC123                                     # 방 참가
  python arena_runner.py ... --policy llm --llm-base http://localhost:11434/v1 --llm-model llama3
"""

from __future__ import annotations

import argparse
import asyncio
import os

from dcgo_arena import ArenaClient, LLMPolicy, RandomPolicy


def build_policy(args) -> object:
    if args.policy == "llm":
        base = args.llm_base or os.environ.get("DCGO_LLM_BASE", "")
        key = args.llm_key or os.environ.get("DCGO_LLM_KEY", "none")
        model = args.llm_model or os.environ.get("DCGO_LLM_MODEL", "")
        if not base or not model:
            raise SystemExit("--policy llm에는 --llm-base/--llm-model(또는 환경변수) 필요")
        return LLMPolicy(base, key, model, temperature=args.llm_temp, seed=args.seed, verbose=not args.quiet)
    return RandomPolicy(args.seed)


async def run(args) -> None:
    client = ArenaClient(args.server, args.key)
    mode = "create_room" if args.create_room else "join_room" if args.join else "ladder"

    def on_event(msg: dict) -> None:
        kind = msg.get("type")
        if kind == "hello" and not args.quiet:
            print(f"[{msg['handle']}] 접속 — 시즌 {msg['season']}")
        elif kind == "room_created":
            print(f"방 생성됨 — 코드: {msg['code']} (상대에게 전달, 참가 대기 중)", flush=True)
        elif kind == "queued" and not args.quiet:
            print(f"래더 큐 {msg['position']}번째")
        elif kind == "match_start" and not args.quiet:
            print(f"매치 {msg['matchId']} — 좌석 {msg['seat']}, 상대 {msg['opponent']['handle']}"
                  f"(레이팅 {msg['opponent']['rating']})")
        elif kind == "your_turn" and not args.quiet:
            print(f"  s{msg['stepIndex']} {msg.get('kind')} — 합법 {len(msg.get('legalActions') or [])}수")

    for game in range(args.games):
        result = await client.play(build_policy(args), mode=mode, room_code=args.join,
                                   deck_id=args.deck_id, on_event=on_event)
        print(f"종료: winner={result.get('winnerSeat')} reason={result.get('reason')}"
              f" Δ레이팅={result.get('ratingDelta')}")
        if mode != "ladder":
            break   # 룸은 1판


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", default="http://127.0.0.1:8791")
    parser.add_argument("--key", required=True)
    parser.add_argument("--create-room", action="store_true")
    parser.add_argument("--join", default=None, help="방 코드로 참가")
    parser.add_argument("--deck-id", type=int, default=None, help="이 판에 쓸 덱 ID(기본=활성 덱)")
    parser.add_argument("--games", type=int, default=1, help="래더 연전 판수")
    parser.add_argument("--policy", choices=["random", "llm"], default="random")
    parser.add_argument("--llm-base", default=None, help="OpenAI 호환 base URL (…/v1)")
    parser.add_argument("--llm-key", default=None)
    parser.add_argument("--llm-model", default=None)
    parser.add_argument("--llm-temp", type=float, default=0.3)
    parser.add_argument("--seed", type=int, default=0)
    parser.add_argument("--quiet", action="store_true")
    args = parser.parse_args()
    asyncio.run(run(args))


if __name__ == "__main__":
    main()
