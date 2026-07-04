"""리플레이 — 정책 대 정책(또는 랜덤) 한 판을 두고 수순을 사람이 읽게 디코딩.

사용:
  python replay.py --p1 ../runs/l0-300k/policy.zip --p2 random --seed 7
  python replay.py --p1 <policy.zip> --p2 <policy.zip> --seed 7 --json   # 대시보드 API용
"""

from __future__ import annotations

import argparse
import json
import random

from dcgo_rl.bridge import BridgeClient

PHASE_PREFIX = "turn.phase."


def build_lanes(schema: dict) -> list[tuple[int, int, str]]:
    hand, field, choice = schema["maxHand"], schema["maxField"], schema["maxChoice"]
    lanes: list[tuple[int, int, str]] = []
    offset = 0

    def lane(name: str, size: int) -> None:
        nonlocal offset
        lanes.append((offset, offset + size, name))
        offset += size

    # FactoredActionSchema v1 레인 순서 그대로 (오프셋 계약).
    lane("NoOp", 1); lane("Pass", 1); lane("AdvancePhase", 1); lane("EndTurn", 1)
    lane("PlayCard", hand); lane("ActivateOption", hand); lane("Digivolve", hand * field)
    lane("DeclareAttack", field * (field + 1)); lane("ResolveChoice", choice + 1)
    lane("HatchDigitama", 1); lane("MoveBreeding", 1); lane("SpecialPlay", hand)
    return lanes


def decode_action(index: int, lanes: list[tuple[int, int, str]], schema: dict) -> str:
    field, choice = schema["maxField"], schema["maxChoice"]
    for start, end, name in lanes:
        if start <= index < end:
            local = index - start
            if name in ("PlayCard", "ActivateOption", "SpecialPlay"):
                return f"{name}(손패 {local}번)"
            if name == "Digivolve":
                return f"Digivolve(손패 {local // field}번 → 필드 {local % field}번)"
            if name == "DeclareAttack":
                attacker, target = local // (field + 1), local % (field + 1)
                where = "플레이어(다이렉트)" if target == field else f"상대 필드 {target}번"
                return f"DeclareAttack(필드 {attacker}번 → {where})"
            if name == "ResolveChoice":
                return "ResolveChoice(스킵)" if local == choice else f"ResolveChoice(후보 {local}번)"
            return name
    return f"?{index}"


class Player:
    def __init__(self, spec: str, deterministic: bool = True):
        self.spec = spec
        self._model = None
        if spec != "random":
            from sb3_contrib import MaskablePPO  # 지연 import — random 대전은 torch 불필요

            self._model = MaskablePPO.load(spec)
        self._deterministic = deterministic

    @property
    def label(self) -> str:
        return "랜덤" if self._model is None else "정책"

    def act(self, turn: dict, rng: random.Random) -> int:
        legal = [i for i, v in enumerate(turn["actionMask"]) if v == 1]
        if self._model is None:
            return rng.choice(legal)

        import numpy as np

        base = np.asarray(turn["observation"], dtype=np.float32)
        seat_onehot = np.zeros(2, dtype=np.float32)
        seat_onehot[turn["seat"] - 1] = 1.0
        observation = np.concatenate([base, seat_onehot])
        mask = np.asarray(turn["actionMask"], dtype=np.float64) == 1.0
        action, _ = self._model.predict(observation, action_masks=mask, deterministic=self._deterministic)
        return int(action)


def play_match(p1: Player, p2: Player, seed: int, decks: dict | None = None, max_steps: int = 2000) -> dict:
    client = BridgeClient(verify_vocab=False)
    try:
        schema = client.welcome["schema"]
        lanes = build_lanes(schema)
        features = client.describe()
        turn_index = features.index("turn.number")
        phase_indices = {
            i: name[len(PHASE_PREFIX):] for i, name in enumerate(features) if name.startswith(PHASE_PREFIX)
        }

        rng = random.Random(seed)
        msg = client.reset(seed, decks or {"1": "starter:ST1", "2": "starter:ST2"}, max_steps)
        steps: list[dict] = []
        while msg["type"] == "turn":
            seat = msg["seat"]
            obs = msg["observation"]
            player = p1 if seat == 1 else p2
            action = player.act(msg, rng)
            steps.append({
                "turn": int(obs[turn_index]),
                "phase": next((nm for i, nm in phase_indices.items() if obs[i] == 1.0), "?"),
                "seat": seat,
                "who": player.label,
                "actionIndex": action,
                "action": decode_action(action, lanes, schema),
                "legalCount": len([v for v in msg["actionMask"] if v == 1]),
            })
            msg = client.act(seat, action)

        return {"seed": seed, "p1": p1.spec, "p2": p2.spec, "steps": steps, "result": msg}
    finally:
        client.close()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--p1", default="random", help="policy.zip 경로 또는 'random'")
    parser.add_argument("--p2", default="random")
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--stochastic", action="store_true", help="정책을 비결정적으로 샘플")
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    record = play_match(
        Player(args.p1, deterministic=not args.stochastic),
        Player(args.p2, deterministic=not args.stochastic),
        args.seed,
    )

    if args.json:
        print(json.dumps(record, ensure_ascii=False))
        return

    result = record["result"]
    print(f"=== P1[{record['p1']}] vs P2[{record['p2']}] — seed {record['seed']} ===")
    last_turn = 0
    for step in record["steps"]:
        if step["turn"] != last_turn:
            print(f"\n--- 턴 {step['turn']} ---")
            last_turn = step["turn"]
        print(f"  T{step['turn']} {step['phase']:<12} P{step['seat']} {step['who']:<4}: "
              f"{step['action']}  (합법 {step['legalCount']}개)")
    print(f"\n=== 결과: 승자 P{result['winnerSeat']} | {result['reason']} | "
          f"{result['steps']}스텝 {result['turns']}턴 ===")


if __name__ == "__main__":
    main()
