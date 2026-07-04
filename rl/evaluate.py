"""평가 러너 (L0 최소형): 학습 정책 vs 랜덤 합법 정책 승률."""

from __future__ import annotations

from dcgo_rl.envs import DcgoSeatEnv


def evaluate_winrate(model, n_matches: int, experiment_seed: int) -> float:
    env = DcgoSeatEnv(experiment_seed=experiment_seed)
    wins = 0
    completed = 0
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
                    break
    finally:
        env.close()

    return wins / completed if completed else 0.0


if __name__ == "__main__":
    import argparse

    from sb3_contrib import MaskablePPO

    parser = argparse.ArgumentParser()
    parser.add_argument("model", type=str)
    parser.add_argument("--matches", type=int, default=40)
    parser.add_argument("--seed", type=int, default=819)
    args = parser.parse_args()

    model = MaskablePPO.load(args.model)
    print(f"winrate vs random: {evaluate_winrate(model, args.matches, args.seed):.1%}")
