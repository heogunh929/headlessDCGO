"""L1 — 리그 학습 (roadmap L1): 스냅샷 리그 + Elo + 80/20 샘플러 + 매치업 매트릭스.

루프: 학습기가 리그 풀(과거 자신 스냅샷)과 대전하며 학습하고, freeze 주기마다 현재
정책을 얼려 풀에 편입한다(C-3·C-6). 상대가 없는 초반은 랜덤 상대로 부트스트랩.

게이트(roadmap L1 종료조건):
  1) 스냅샷 승급 사이클 지속 동작 — 학습기 레이팅 추세 상승
  2) 매트릭스 표본 축적 후 약점 샘플링 자동 전환(mode=weakness 관측)
  3) 임의 스냅샷 재현 대전(시드) 일치 — 결정론 체크

사용: python train_league.py [--steps 60000] [--freeze-every 10000] [--out ../runs/league-l1]
"""

from __future__ import annotations

import argparse
import json
import random
import time
from pathlib import Path

from sb3_contrib import MaskablePPO
from sb3_contrib.common.wrappers import ActionMasker
from stable_baselines3.common.callbacks import BaseCallback
from stable_baselines3.common.vec_env import DummyVecEnv

from dcgo_rl.bridge import BridgeClient
from dcgo_rl.envs import DcgoSeatEnv
from dcgo_rl.league.matchup import MatchupMatrix
from dcgo_rl.league.opponents import LeagueOpponentPool, PolicyOpponent
from dcgo_rl.league.rating import EloBook
from dcgo_rl.league.sampler import OpponentSampler
from dcgo_rl.league.snapshots import SnapshotStore
from dcgo_rl.policy.extractor import CardEmbeddingExtractor, card_id_indices

LEARNER = "learner"


class LeagueCallback(BaseCallback):
    """에피소드 결과 → Elo/매트릭스 갱신 + freeze 주기마다 스냅샷 편입."""

    def __init__(
        self,
        pool: LeagueOpponentPool,
        ratings: EloBook,
        matrix: MatchupMatrix,
        store: SnapshotStore,
        freeze_every: int,
        snapshot_meta_base: dict,
        log_path: Path,
    ):
        super().__init__()
        self.pool = pool
        self.ratings = ratings
        self.matrix = matrix
        self.store = store
        self.freeze_every = freeze_every
        self.meta_base = snapshot_meta_base
        self.log_path = log_path
        self._next_freeze = freeze_every
        self._frozen = 0
        self.mode_counts: dict[str, int] = {}

    def _on_step(self) -> bool:
        for info in self.locals.get("infos", []):
            result = info.get("result")
            if not result:
                continue

            assignment = self.pool.take_assignment(result["matchId"])
            opponent_id, mode = assignment if assignment else ("random", "unknown")
            self.mode_counts[mode] = self.mode_counts.get(mode, 0) + 1

            seat = info["seat"]
            reward = float(result["rewards"][str(seat)])
            score = 1.0 if reward > 0 else (0.0 if reward < 0 else 0.5)

            if opponent_id != "random":  # 랜덤 부트스트랩 상대는 레이팅 주체가 아님
                self.ratings.update(LEARNER, opponent_id, score)
                self.matrix.record(LEARNER, opponent_id, score)

            with open(self.log_path, "a", encoding="utf-8") as f:
                f.write(json.dumps({
                    "step": self.num_timesteps,
                    "opponent": opponent_id,
                    "mode": mode,
                    "score": score,
                    "learner_rating": round(self.ratings.rating(LEARNER), 1),
                    "reason": result["reason"],
                    "turns": result["turns"],
                }) + "\n")

        if self.num_timesteps >= self._next_freeze:
            self._freeze()
            self._next_freeze += self.freeze_every
        return True

    def _freeze(self) -> None:
        self._frozen += 1
        snapshot_id = f"{self.meta_base['lineage']}-s{self._frozen:03d}"
        rating = self.ratings.rating(LEARNER)
        meta = dict(self.meta_base)
        meta.update({
            "snapshot_id": snapshot_id,
            "global_step": self.num_timesteps,
            "rating": round(rating, 1),
            "generation": self._frozen,
        })
        self.store.save(self.model, meta)
        self.ratings.set_rating(snapshot_id, rating)
        self.pool.add_snapshot(snapshot_id)
        print(f"[freeze] {snapshot_id} @ step {self.num_timesteps} rating {rating:.0f} "
              f"(pool={len(self.pool.pool_ids)})")


def determinism_check(learner_path: Path, store: SnapshotStore, snapshot_id: str, seed: int) -> bool:
    """게이트 3: 같은 시드 + 결정적 정책 양쪽 = 두 번 돌려 같은 결과(승자·스텝)."""
    learner = PolicyOpponent(MaskablePPO.load(str(learner_path)), deterministic=True)
    opponent = PolicyOpponent(MaskablePPO.load(str(store.policy_path(snapshot_id))), deterministic=True)

    def play_once() -> tuple:
        client = BridgeClient(verify_vocab=False)
        try:
            msg = client.reset(seed, {"1": "starter:ST1", "2": "starter:ST2"}, 2000)
            while msg["type"] == "turn":
                actor = learner if msg["seat"] == 1 else opponent
                msg = client.act(msg["seat"], actor.act(msg))
            return msg["winnerSeat"], msg["steps"], msg["reason"]
        finally:
            client.close()

    first, second = play_once(), play_once()
    print(f"[determinism] run1={first} run2={second}")
    return first == second


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--steps", type=int, default=60_000)
    parser.add_argument("--n-envs", type=int, default=4)
    parser.add_argument("--freeze-every", type=int, default=10_000)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--init", type=str, default="../runs/l0-300k/policy.zip",
                        help="시작 정책(L0 산출물에서 이어서). 'fresh'면 밑바닥부터")
    parser.add_argument("--weakness-min-games", type=int, default=30)
    parser.add_argument("--rating-window", type=float, default=200.0)
    parser.add_argument("--out", type=str, default="../runs/league-l1")
    parser.add_argument("--log-level", choices=["OFF", "RESULT", "REPLAY", "ANALYSIS", "TRACE"],
                        default="OFF", help="매치 이벤트 로그 레벨(RlBridgeHost). OFF=끔")
    args = parser.parse_args()

    out_dir = Path(args.out).resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    lineage = "st-league"

    probe = BridgeClient()
    feature_names = probe.describe()
    card_indices = card_id_indices(feature_names)
    vocab_size = probe.vocab_size
    obs_schema_hash = probe.welcome["obsSchemaHash"]
    vocab_version = probe.welcome["vocabVersion"]
    probe.close()

    store = SnapshotStore(out_dir / "snapshots")
    ratings = EloBook()
    matrix = MatchupMatrix(out_dir / "matchup.sqlite")
    sampler = OpponentSampler(
        weakness_min_games=args.weakness_min_games, rating_window=args.rating_window
    )
    pool = LeagueOpponentPool(
        store, sampler, ratings, matrix, learner_id=LEARNER, sample_rng=random.Random(args.seed)
    )

    def make_env(rank: int):
        def _thunk():
            env = DcgoSeatEnv(
                experiment_seed=args.seed * 1000 + rank,
                opponent=pool,
                result_log=str(out_dir / f"results-env{rank}.jsonl"),
                log_level=args.log_level,
                event_log=(str(out_dir / f"event-env{rank}.jsonl") if args.log_level != "OFF" else None),
            )
            return ActionMasker(env, lambda e: e.action_masks())
        return _thunk

    vec_env = DummyVecEnv([make_env(rank) for rank in range(args.n_envs)])

    if args.init != "fresh":
        model = MaskablePPO.load(str(Path(args.init).resolve()), env=vec_env)
        print(f"init from {args.init}")
    else:
        model = MaskablePPO(
            "MlpPolicy", vec_env, seed=args.seed, n_steps=256, batch_size=256,
            policy_kwargs={
                "features_extractor_class": CardEmbeddingExtractor,
                "features_extractor_kwargs": {"card_indices": card_indices, "vocab_size": vocab_size},
            },
        )

    meta_base = {
        "lineage": lineage,
        "parent": args.init if args.init != "fresh" else None,
        "obs_schema_hash": obs_schema_hash,
        "vocab_version": vocab_version,
        "arch": "mlp+card-embed",
        "deck_context": ["starter:ST1", "starter:ST2"],
        "card_pool": "ST1+ST2",
    }
    callback = LeagueCallback(
        pool, ratings, matrix, store, args.freeze_every, meta_base, out_dir / "league_log.jsonl"
    )

    started = time.time()
    model.learn(total_timesteps=args.steps, callback=callback)
    elapsed = time.time() - started
    vec_env.close()

    final_path = out_dir / "learner_final.zip"
    model.save(final_path)
    ratings.save(out_dir / "ratings.json")

    # --- 게이트 리포트 -----------------------------------------------------------
    print(f"\ntraining: {args.steps} steps in {elapsed:.0f}s -> {args.steps / elapsed:.1f} steps/sec")
    print(f"snapshots: {[m['snapshot_id'] for m in store.list_metas()]}")
    print(f"ratings: learner={ratings.rating(LEARNER):.0f} | " +
          " ".join(f"{p}={r:.0f}" for p, r in sorted(ratings.snapshot().items()) if p != LEARNER))
    print(f"sampler modes: {callback.mode_counts}")
    print("matchup (learner vs snapshot):")
    for snap in pool.pool_ids:
        print(f"  vs {snap}: winrate={matrix.winrate(LEARNER, snap)} games={matrix.games(LEARNER, snap)}")

    gate1 = ratings.rating(LEARNER) > EloBook().initial
    gate2 = callback.mode_counts.get("weakness", 0) > 0
    gate3 = determinism_check(final_path, store, pool.pool_ids[0], seed=args.seed + 555) if pool.pool_ids else False
    print(f"\nGATE1 rating-trend-up: {gate1} | GATE2 weakness-sampling-engaged: {gate2} | "
          f"GATE3 seeded-replay-identical: {gate3}")
    matrix.close()


if __name__ == "__main__":
    main()
