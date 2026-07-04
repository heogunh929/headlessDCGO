"""카드-ID 임베딩 features extractor (FR-1.1 / dev design §4.1, C-2 덱-조건부의 기반).

관측 벡터에서 `*.cardId` 채널(호스트 describe로 식별)을 정수 id로 보고 공유 임베딩
테이블에 통과시킨 뒤, 나머지 피처와 이어붙여 MLP 입력을 만든다. id 0 = PAD(빈 슬롯).
vocab append-only 확장(FR-5.3) 시 임베딩 행만 뒤에 늘리면 기존 행이 보존된다.
"""

from __future__ import annotations

import gymnasium as gym
import torch
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor
from torch import nn


def card_id_indices(feature_names: list[str]) -> list[int]:
    return [i for i, name in enumerate(feature_names) if name.endswith(".cardId")]


class CardEmbeddingExtractor(BaseFeaturesExtractor):
    def __init__(
        self,
        observation_space: gym.spaces.Box,
        card_indices: list[int],
        vocab_size: int,
        embed_dim: int = 16,
        features_dim: int = 256,
    ):
        super().__init__(observation_space, features_dim)
        obs_dim = int(observation_space.shape[0])
        if not card_indices:
            raise ValueError("card_indices is empty — describe()로 cardId 채널을 식별했는지 확인")

        self._card_index_tensor: torch.Tensor
        self.register_buffer(
            "_card_index_tensor", torch.as_tensor(card_indices, dtype=torch.long), persistent=False
        )
        other_indices = [i for i in range(obs_dim) if i not in set(card_indices)]
        self._other_index_tensor: torch.Tensor
        self.register_buffer(
            "_other_index_tensor", torch.as_tensor(other_indices, dtype=torch.long), persistent=False
        )

        self.embedding = nn.Embedding(vocab_size + 1, embed_dim, padding_idx=0)
        flat_dim = len(card_indices) * embed_dim + len(other_indices)
        self.mlp = nn.Sequential(
            nn.Linear(flat_dim, features_dim),
            nn.ReLU(),
        )

    def forward(self, observations: torch.Tensor) -> torch.Tensor:
        card_ids = observations.index_select(1, self._card_index_tensor).long().clamp(min=0)
        embedded = self.embedding(card_ids).flatten(start_dim=1)
        others = observations.index_select(1, self._other_index_tensor)
        return self.mlp(torch.cat([embedded, others], dim=1))
