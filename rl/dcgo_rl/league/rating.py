"""온라인 Elo (FR-2.3의 '레이팅 근처' 축, dev design §5.2).

레이팅 = 리그 난이도 조절 용도. 상성 구조는 매치업 매트릭스가 1급 자료(레이팅으로 못 봄 —
요구사항 결정 §8). 교체 가능하도록 인터페이스는 update(a, b, score_a) 하나로 고정 —
Glicko/TrueSkill 승급은 아레나(M6) 결정과 함께.
"""

from __future__ import annotations

import json
from pathlib import Path


class EloBook:
    def __init__(self, k: float = 32.0, initial: float = 1200.0):
        self.k = k
        self.initial = initial
        self._ratings: dict[str, float] = {}

    def rating(self, player_id: str) -> float:
        return self._ratings.get(player_id, self.initial)

    def expected(self, a: str, b: str) -> float:
        return 1.0 / (1.0 + 10 ** ((self.rating(b) - self.rating(a)) / 400.0))

    def update(self, a: str, b: str, score_a: float) -> None:
        """score_a: 1.0 = a 승, 0.0 = a 패, 0.5 = 무."""
        if not 0.0 <= score_a <= 1.0:
            raise ValueError(f"score_a must be in [0,1], got {score_a}")
        expected_a = self.expected(a, b)
        self._ratings[a] = self.rating(a) + self.k * (score_a - expected_a)
        self._ratings[b] = self.rating(b) + self.k * ((1.0 - score_a) - (1.0 - expected_a))

    def set_rating(self, player_id: str, value: float) -> None:
        """신규 편입 좌표 지정 — 스냅샷은 얼린 시점의 학습기 레이팅을 물려받는다(FR-2.2)."""
        self._ratings[player_id] = float(value)

    def snapshot(self) -> dict[str, float]:
        return dict(self._ratings)

    def save(self, path: Path) -> None:
        path.write_text(json.dumps(self.snapshot(), indent=2), encoding="utf-8")

    @classmethod
    def load(cls, path: Path, k: float = 32.0, initial: float = 1200.0) -> "EloBook":
        book = cls(k=k, initial=initial)
        if path.exists():
            book._ratings = {str(p): float(r) for p, r in json.loads(path.read_text()).items()}
        return book
