"""리그 상대 정책 어댑터 — env의 opponent 콜백에 스냅샷 정책을 꽂는다 (C-3 스냅샷 리그).

`LeagueOpponentPool`은 매치 단위로 상대를 샘플링한다: turn 메시지의 matchId가 바뀌면
새 매치로 보고 샘플러에게 상대를 물어 그 매치 동안 고정한다. 학습 루프(콜백)는
`take_assignment(matchId)`로 그 매치의 상대를 회수해 Elo/매트릭스에 기록한다.
DummyVecEnv(인프로세스) 전제 — Subproc으로 가면 이 공유 상태는 워커로 넘어가므로 L1 범위 밖.
"""

from __future__ import annotations

import random

import numpy as np

from dcgo_rl.league.matchup import MatchupMatrix
from dcgo_rl.league.rating import EloBook
from dcgo_rl.league.sampler import OpponentSampler
from dcgo_rl.league.snapshots import SnapshotStore


def random_action(turn: dict, rng: random.Random) -> int:
    legal = [i for i, v in enumerate(turn["actionMask"]) if v == 1]
    return rng.choice(legal)


class PolicyOpponent:
    """스냅샷 MaskablePPO를 seat 프로토콜 turn 메시지로 구동."""

    def __init__(self, model, deterministic: bool = False):
        self._model = model
        self._deterministic = deterministic

    def act(self, turn: dict) -> int:
        base = np.asarray(turn["observation"], dtype=np.float32)
        seat_onehot = np.zeros(2, dtype=np.float32)
        seat_onehot[turn["seat"] - 1] = 1.0
        observation = np.concatenate([base, seat_onehot])
        mask = np.asarray(turn["actionMask"], dtype=np.float64) == 1.0
        action, _ = self._model.predict(observation, action_masks=mask, deterministic=self._deterministic)
        return int(action)


class LeagueOpponentPool:
    """매치별 상대 샘플링 + 배정 기록. env opponent 콜백으로 그대로 쓴다."""

    def __init__(
        self,
        store: SnapshotStore,
        sampler: OpponentSampler,
        ratings: EloBook,
        matrix: MatchupMatrix,
        learner_id: str = "learner",
        sample_rng: random.Random | None = None,
    ):
        self._store = store
        self._sampler = sampler
        self._ratings = ratings
        self._matrix = matrix
        self._learner_id = learner_id
        self._rng = sample_rng or random.Random(0)

        self._pool_ids: list[str] = []
        self._loaded: dict[str, PolicyOpponent] = {}
        self._assignments: dict[str, tuple[str, str]] = {}  # matchId -> (opponent_id, mode)

    @property
    def pool_ids(self) -> list[str]:
        return list(self._pool_ids)

    def add_snapshot(self, snapshot_id: str) -> None:
        from sb3_contrib import MaskablePPO  # 지연 import (테스트에서 torch 불필요)

        model = MaskablePPO.load(str(self._store.policy_path(snapshot_id)))
        self._loaded[snapshot_id] = PolicyOpponent(model)
        self._pool_ids.append(snapshot_id)

    def take_assignment(self, match_id: str) -> tuple[str, str] | None:
        return self._assignments.pop(match_id, None)

    def __call__(self, turn: dict, match_rng: random.Random) -> int:
        match_id = turn["matchId"]
        assignment = self._assignments.get(match_id)
        if assignment is None:
            if not self._pool_ids:
                assignment = ("random", "bootstrap")  # 첫 스냅샷 전: 랜덤 상대로 부트스트랩
            else:
                assignment = self._sampler.sample(
                    self._learner_id, self._pool_ids, self._ratings, self._matrix, self._rng
                )
            self._assignments[match_id] = assignment

        opponent_id, _ = assignment
        if opponent_id == "random":
            return random_action(turn, match_rng)
        return self._loaded[opponent_id].act(turn)
