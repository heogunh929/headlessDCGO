"""DcgoSeatEnv — seat 프로토콜 위의 gymnasium 환경 (L0 학습 슬라이스).

L0 형상: 에이전트가 한 좌석을 두고, 상대 좌석은 내부 랜덤(합법) 정책이 둔다.
좌석은 에피소드마다 교대(1↔2)해 선후공/덱 편향을 줄인다. 리그 상대(스냅샷)로의
승급은 L1의 몫 — 이 env는 opponent 콜백만 갈아끼우면 된다.

관측 = 호스트가 준 좌석 시점 정보집합 벡터 + 좌석 원핫 2 (자기 좌석 식별 —
프로토콜 §3의 turn.seat 를 관측에 반영하는 클라이언트측 관례).
"""

from __future__ import annotations

import random
from typing import Callable

import gymnasium as gym
import numpy as np

from dcgo_rl.bridge import BridgeClient
from dcgo_rl.seeding import derive_match_seed

OpponentPolicy = Callable[[dict, random.Random], int]
"""(turn 메시지, 매치 rng) → action index."""


def random_opponent(turn: dict, rng: random.Random) -> int:
    legal = [i for i, v in enumerate(turn["actionMask"]) if v == 1]
    return rng.choice(legal)


class DcgoSeatEnv(gym.Env):
    metadata = {"render_modes": []}

    def __init__(
        self,
        decks: dict | None = None,
        experiment_seed: int = 0,
        max_steps: int = 2000,
        opponent: OpponentPolicy = random_opponent,
        alternate_seats: bool = True,
        result_log: str | None = None,
        log_level: str = "OFF",
        event_log: str | None = None,
    ):
        super().__init__()
        self._client = BridgeClient(result_log=result_log, log_level=log_level, event_log=event_log)
        self._decks = decks or {"1": "starter:ST1", "2": "starter:ST2"}
        self._experiment_seed = experiment_seed
        self._max_steps = max_steps
        self._opponent = opponent
        self._alternate_seats = alternate_seats

        self._episode = 0
        self._agent_seat = 1
        self._match_rng = random.Random(0)
        self._current_turn: dict | None = None
        self._mask = np.zeros(self._client.action_size, dtype=bool)

        self.observation_space = gym.spaces.Box(
            low=-np.inf, high=np.inf, shape=(self._client.obs_size + 2,), dtype=np.float32
        )
        self.action_space = gym.spaces.Discrete(self._client.action_size)

    # --- gymnasium API ---------------------------------------------------------

    def reset(self, *, seed: int | None = None, options=None):
        super().reset(seed=seed)
        match_seed = (
            seed if seed is not None else derive_match_seed(self._experiment_seed, self._episode)
        )
        self._agent_seat = 1 if (not self._alternate_seats or self._episode % 2 == 0) else 2
        self._match_rng = random.Random(match_seed)
        self._episode += 1

        msg = self._client.reset(match_seed, self._decks, self._max_steps)
        msg = self._advance_opponent(msg)
        if msg["type"] != "turn":
            # 상대 랜덤 정책만으로 매치가 끝나버린 극단 케이스: 다음 시드로 재시도.
            return self.reset(seed=None, options=options)

        self._set_turn(msg)
        return self._observation(msg), {"seat": self._agent_seat}

    def step(self, action: int):
        assert self._current_turn is not None, "step before reset"
        if not self._mask[int(action)]:
            raise ValueError(f"masked policy emitted illegal index {action} (마스킹 계약 위반)")

        msg = self._client.act(self._agent_seat, int(action))
        msg = self._advance_opponent(msg)

        if msg["type"] == "result":
            reward = float(msg["rewards"][str(self._agent_seat)])
            truncated = msg["reason"] == "step_cap"
            terminated = not truncated
            observation = np.zeros(self.observation_space.shape, dtype=np.float32)
            self._current_turn = None
            self._mask = np.zeros(self._client.action_size, dtype=bool)
            info = {"result": msg, "seat": self._agent_seat}
            return observation, reward, terminated, truncated, info

        self._set_turn(msg)
        return self._observation(msg), 0.0, False, False, {"seat": self._agent_seat}

    def action_masks(self) -> np.ndarray:
        """sb3-contrib MaskablePPO 용 (ActionMasker 래퍼가 호출)."""
        return self._mask.copy()

    def close(self):
        self._client.close()

    # --- internals -----------------------------------------------------------------

    def _advance_opponent(self, msg: dict) -> dict:
        while msg["type"] == "turn" and msg["seat"] != self._agent_seat:
            index = self._opponent(msg, self._match_rng)
            msg = self._client.act(msg["seat"], index)
        return msg

    def _set_turn(self, turn: dict) -> None:
        self._current_turn = turn
        mask = np.asarray(turn["actionMask"], dtype=np.float64)
        self._mask = mask == 1.0

    def _observation(self, turn: dict) -> np.ndarray:
        base = np.asarray(turn["observation"], dtype=np.float32)
        seat_onehot = np.zeros(2, dtype=np.float32)
        seat_onehot[self._agent_seat - 1] = 1.0
        return np.concatenate([base, seat_onehot])
