"""리그 상대 샘플러 — 80% 레이팅 근처 + 20% 약점 우선 (FR-2.3, 요구사항 결정 §8).

콜드스타트: 약점 축은 매치업 표본(min_games) 충족 상대가 없으면 랜덤으로 폴백하고,
데이터가 쌓이면 자동으로 약점 우선으로 전환된다. 반환하는 mode 문자열이 그 전환의
관측 지표(로그로 남겨 L1 게이트 검증에 쓴다).
"""

from __future__ import annotations

import random

from dcgo_rl.league.matchup import MatchupMatrix
from dcgo_rl.league.rating import EloBook


class OpponentSampler:
    def __init__(
        self,
        near_rating: float = 0.8,
        weakness: float = 0.2,
        weakness_min_games: int = 200,
        rating_window: float = 200.0,
    ):
        if abs(near_rating + weakness - 1.0) > 1e-9:
            raise ValueError("near_rating + weakness must sum to 1.0")
        self.near_rating = near_rating
        self.weakness = weakness
        self.weakness_min_games = weakness_min_games
        self.rating_window = rating_window

    def sample(
        self,
        learner_id: str,
        pool: list[str],
        ratings: EloBook,
        matrix: MatchupMatrix,
        rng: random.Random,
    ) -> tuple[str, str]:
        """(opponent_id, mode) — mode ∈ near|weakness|random(폴백)."""
        if not pool:
            raise ValueError("opponent pool is empty")

        if rng.random() < self.weakness:
            weakest = matrix.weakest(learner_id, pool, self.weakness_min_games)
            if weakest:
                return weakest[0][0], "weakness"
            return rng.choice(pool), "random"  # 콜드스타트 폴백

        my_rating = ratings.rating(learner_id)
        near = [p for p in pool if abs(ratings.rating(p) - my_rating) <= self.rating_window]
        return rng.choice(near or pool), "near"
