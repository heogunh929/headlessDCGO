"""(덱, 정책) 조합 리그 공급자 — 와이어프레임 리그 학습의 코어 (사용자 확정 2026-07-30).

한 객체가 env의 deck_provider(좌석 덱)와 opponent 콜백(상대 착수)을 **동시에** 맡아
"상대 조합의 덱 = 상대 조합의 정책"을 판 단위로 묶는다. 기존 LeagueOpponentPool은 상대를
첫 상대 턴에서 늦게 샘플하므로 덱(리셋 시 확정)과 못 묶는다 — 여기서는 리셋 시점
next_matchup()이 상대를 확정하고 그 배정을 큐로 넘겨 opponent 콜백이 소비한다.

DummyVecEnv(인프로세스) 전제 — league 패키지 공통 제약. next_matchup 호출 순서와
매치 시작 순서가 같다는 사실(순차 리셋)에 기대며, matchId로 배정을 고정한다.
"""

from __future__ import annotations

import random
from collections import deque
from dataclasses import dataclass
from pathlib import Path

from dcgo_rl.decks.recipe import Recipe
from dcgo_rl.league.opponents import PolicyOpponent, random_action


@dataclass
class Combo:
    """리그 조합 1개 — 덱 하나에 정책 하나."""

    combo_id: str
    recipe: Recipe
    model_path: str | None      # None = 아직 정책 없음(1라운드 신규) → 상대로 나오면 랜덤 대체


class ComboOpponents:
    """내 조합 고정, 상대 = 나머지 조합 균등 순환. deck_provider + opponent 콜백 겸용."""

    def __init__(self, mine: Combo, others: list[Combo], rng: random.Random | None = None):
        if not others:
            raise ValueError("상대 조합이 최소 1개 필요")
        self.mine = mine
        self.others = list(others)
        self._rng = rng or random.Random(0)
        self._loaded: dict[str, PolicyOpponent] = {}
        self._pending: deque[Combo] = deque()      # next_matchup 순서 = 매치 시작 순서(순차 리셋)
        self._by_match: dict[str, Combo] = {}
        self.assignments: list[str] = []           # 판별 상대 combo_id 기록(진단용)

    # ---- DeckProvider 인터페이스 ----

    def next_matchup(self, rng: random.Random) -> tuple[Recipe, Recipe]:
        mine, theirs = self.next_matchup_seated(rng, agent_seat=1)
        return mine, theirs

    def next_matchup_seated(self, rng: random.Random, agent_seat: int) -> tuple[Recipe, Recipe]:
        """좌석 인지 경로(envs.py reset): 내 덱은 에이전트 좌석에, 상대 덱은 반대 좌석에."""
        opponent = self.others[len(self.assignments) % len(self.others)]  # 균등 순환
        self._pending.append(opponent)
        self.assignments.append(opponent.combo_id)
        if agent_seat == 1:
            return self.mine.recipe, opponent.recipe
        return opponent.recipe, self.mine.recipe

    def report_result(self, matchup_id: str, result: object) -> None:
        pass

    # ---- env opponent 콜백 ----

    def __call__(self, turn: dict, match_rng: random.Random) -> int:
        match_id = turn["matchId"]
        combo = self._by_match.get(match_id)
        if combo is None:
            combo = self._pending.popleft() if self._pending else self.others[0]
            self._by_match[match_id] = combo
            if len(self._by_match) > 64:            # 끝난 매치 배정은 잊는다(무한 성장 방지)
                for stale in list(self._by_match)[:-32]:
                    if stale != match_id:
                        del self._by_match[stale]
        if combo.model_path is None:
            return random_action(turn, match_rng)
        opponent = self._loaded.get(combo.combo_id)
        if opponent is None:
            from sb3_contrib import MaskablePPO   # 지연 import
            opponent = PolicyOpponent(MaskablePPO.load(str(Path(combo.model_path))))
            self._loaded[combo.combo_id] = opponent
        return opponent.act(turn)
