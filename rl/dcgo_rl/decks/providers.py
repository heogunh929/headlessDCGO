"""덱 공급원 — 단일 인터페이스 (FR-3.4, 설계 §3.3).

fixed / random / GA 가 전부 같은 시그니처로 들어온다 → GA on·off는 config 한 줄.
``report_result``는 GA만 소비하는 채널이며 나머지 provider는 no-op.
"""

from __future__ import annotations

import random
from typing import Protocol, Sequence

from dcgo_rl.decks.recipe import Recipe


class DeckProvider(Protocol):
    def next_matchup(self, rng: random.Random) -> tuple[Recipe, Recipe]: ...

    def report_result(self, matchup_id: str, result: object) -> None: ...


class FixedPoolProvider:
    """운영자가 던진 고정 레시피 풀에서 균등 샘플(미러전 허용). — Mode A/C/E의 공급원."""

    def __init__(self, recipes: Sequence[Recipe]):
        if not recipes:
            raise ValueError("FixedPoolProvider requires at least one recipe.")
        self._recipes = tuple(recipes)

    @property
    def recipes(self) -> tuple[Recipe, ...]:
        return self._recipes

    def next_matchup(self, rng: random.Random) -> tuple[Recipe, Recipe]:
        return rng.choice(self._recipes), rng.choice(self._recipes)

    def report_result(self, matchup_id: str, result: object) -> None:
        pass  # GA 전용 채널 (§3.3)
