"""평가 러너 (L0/B3): 학습 정책 vs 랜덤 합법 정책 승률 + Wilson 95% 신뢰구간."""

from __future__ import annotations

import math

from dcgo_rl.envs import DcgoSeatEnv


def wilson_ci(wins: int, n: int, z: float = 1.96) -> tuple[float, float]:
    """이항 승률의 Wilson score 95% 구간 (n=0이면 (0,1))."""
    if n == 0:
        return (0.0, 1.0)
    p = wins / n
    denom = 1 + z * z / n
    center = (p + z * z / (2 * n)) / denom
    margin = (z / denom) * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n))
    return (max(0.0, center - margin), min(1.0, center + margin))


def evaluate_winrate(
    model,
    n_matches: int,
    experiment_seed: int,
    deck_provider=None,
) -> dict:
    """n_matches 판 대전(좌석 교대) — {winrate, wins, losses, completed, truncated, ci95} 반환."""
    env = DcgoSeatEnv(experiment_seed=experiment_seed, deck_provider=deck_provider)
    wins = 0
    completed = 0
    truncated_n = 0
    try:
        for _ in range(n_matches):
            obs, _ = env.reset()
            while True:
                action, _ = model.predict(obs, action_masks=env.action_masks(), deterministic=True)
                obs, reward, terminated, truncated, _ = env.step(int(action))
                if terminated or truncated:
                    if terminated:
                        completed += 1
                        if reward > 0:
                            wins += 1
                    else:
                        truncated_n += 1
                    break
    finally:
        env.close()

    winrate = wins / completed if completed else 0.0
    return {
        "winrate": winrate,
        "wins": wins,
        "losses": completed - wins,
        "completed": completed,
        "truncated": truncated_n,
        "ci95": wilson_ci(wins, completed),
    }


if __name__ == "__main__":
    import argparse
    from pathlib import Path

    from sb3_contrib import MaskablePPO

    from dcgo_rl.cards import CardIndex
    from dcgo_rl.decks.providers import FixedPoolProvider
    from dcgo_rl.decks.recipe import load_recipe_file

    parser = argparse.ArgumentParser()
    parser.add_argument("model", type=str)
    parser.add_argument("--matches", type=int, default=120)
    parser.add_argument("--seed", type=int, default=819)
    parser.add_argument("--recipes", nargs="*", default=None,
                        help="덱 레시피 파일들 — 학습 때와 같은 풀로 평가해야 비교가 성립")
    args = parser.parse_args()

    provider = None
    if args.recipes:
        index = CardIndex.load()
        provider = FixedPoolProvider([load_recipe_file(Path(p).resolve(), index) for p in args.recipes])

    model = MaskablePPO.load(args.model)
    report = evaluate_winrate(model, args.matches, args.seed, deck_provider=provider)
    lo, hi = report["ci95"]
    print(f"winrate vs random: {report['winrate']:.1%} "
          f"({report['wins']}W/{report['losses']}L over {report['completed']} completed, "
          f"truncated {report['truncated']}) CI95=[{lo:.1%}, {hi:.1%}]")
