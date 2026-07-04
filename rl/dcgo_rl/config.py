"""실험 config 스키마 (설계 §2, NFR-4 "모드 = 데이터").

덱 공급원 × 정책 학습여부 × 보상 × 로그레벨을 YAML 필드로 조합한다.
새 실험 = 새 config 파일이어야 하며, 알 수 없는 키는 즉시 실패한다(드리프트 방지 —
조용한 오타 무시는 "다른 실험을 돌리고도 모르는" 사고로 이어진다).

파일 존재 등 환경 검증은 실험 시작 시점의 몫 — 여기서는 스키마/모드 정합성만 본다.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Mapping

import yaml

DECK_SOURCE_TYPES = ("fixed", "random", "ga")
POLICY_ARCHS = ("mlp", "recurrent")
REWARD_TYPES = ("terminal",)  # 셰이핑은 FR-1.3 훅 — 트리거 발생 전 추가 금지(NFR-6)
LOG_LEVELS = ("OFF", "RESULT", "REPLAY", "ANALYSIS", "TRACE")


class ConfigError(ValueError):
    pass


def _require_keys(section: str, data: Mapping, allowed: set[str]) -> None:
    unknown = set(data) - allowed
    if unknown:
        raise ConfigError(f"{section}: unknown keys {sorted(unknown)} (allowed: {sorted(allowed)})")


@dataclass(frozen=True)
class DeckSourceConfig:
    type: str
    recipes: tuple[str, ...] = ()

    @classmethod
    def parse(cls, data: Mapping) -> "DeckSourceConfig":
        _require_keys("deck_source", data, {"type", "recipes"})
        source_type = str(data.get("type", ""))
        if source_type not in DECK_SOURCE_TYPES:
            raise ConfigError(f"deck_source.type must be one of {DECK_SOURCE_TYPES}, got {source_type!r}")
        recipes = tuple(str(r) for r in data.get("recipes", []) or [])
        if source_type == "fixed" and not recipes:
            raise ConfigError("deck_source.type=fixed requires non-empty recipes")
        if source_type != "fixed" and recipes:
            raise ConfigError(f"deck_source.type={source_type} must not set recipes")
        return cls(source_type, recipes)


@dataclass(frozen=True)
class PolicyConfig:
    arch: str = "mlp"
    init: str | None = None  # null=밑바닥 | 스냅샷 id | base 버전 (FR-1b, FR-5.5)
    learn: bool = True

    @classmethod
    def parse(cls, data: Mapping) -> "PolicyConfig":
        _require_keys("policy", data, {"arch", "init", "learn"})
        arch = str(data.get("arch", "mlp"))
        if arch not in POLICY_ARCHS:
            raise ConfigError(f"policy.arch must be one of {POLICY_ARCHS}, got {arch!r}")
        init = data.get("init")
        learn = bool(data.get("learn", True))
        if not learn and init is None:
            raise ConfigError("policy.learn=false requires policy.init (평가 모드에는 고정 스냅샷이 필요)")
        return cls(arch, None if init is None else str(init), learn)


@dataclass(frozen=True)
class RewardConfig:
    type: str = "terminal"

    @classmethod
    def parse(cls, data: Mapping) -> "RewardConfig":
        _require_keys("reward", data, {"type"})
        reward_type = str(data.get("type", "terminal"))
        if reward_type not in REWARD_TYPES:
            raise ConfigError(f"reward.type must be one of {REWARD_TYPES}, got {reward_type!r}")
        return cls(reward_type)


@dataclass(frozen=True)
class FreezeConfig:
    every_steps: int = 2_000_000
    skill_gate: float | None = None  # None=시간/스텝 기본만 (FR-2.2)

    @classmethod
    def parse(cls, data: Mapping) -> "FreezeConfig":
        _require_keys("league.freeze", data, {"every_steps", "skill_gate"})
        every_steps = int(data.get("every_steps", 2_000_000))
        if every_steps <= 0:
            raise ConfigError("league.freeze.every_steps must be positive")
        gate = data.get("skill_gate")
        return cls(every_steps, None if gate is None else float(gate))


@dataclass(frozen=True)
class SamplingConfig:
    near_rating: float = 0.8
    weakness: float = 0.2
    weakness_min_games: int = 200  # 미달 시 랜덤 폴백 (FR-2.3 콜드스타트)

    @classmethod
    def parse(cls, data: Mapping) -> "SamplingConfig":
        _require_keys("league.sampling", data, {"near_rating", "weakness", "weakness_min_games"})
        near = float(data.get("near_rating", 0.8))
        weak = float(data.get("weakness", 0.2))
        if abs(near + weak - 1.0) > 1e-9:
            raise ConfigError(f"league.sampling near_rating+weakness must sum to 1.0, got {near + weak}")
        min_games = int(data.get("weakness_min_games", 200))
        if min_games < 0:
            raise ConfigError("league.sampling.weakness_min_games must be >= 0")
        return cls(near, weak, min_games)


@dataclass(frozen=True)
class LeagueConfig:
    enabled: bool = False
    freeze: FreezeConfig = field(default_factory=FreezeConfig)
    sampling: SamplingConfig = field(default_factory=SamplingConfig)

    @classmethod
    def parse(cls, data: Mapping) -> "LeagueConfig":
        _require_keys("league", data, {"enabled", "freeze", "sampling"})
        return cls(
            enabled=bool(data.get("enabled", False)),
            freeze=FreezeConfig.parse(data.get("freeze") or {}),
            sampling=SamplingConfig.parse(data.get("sampling") or {}),
        )


@dataclass(frozen=True)
class ParallelConfig:
    n_envs: int = 1

    @classmethod
    def parse(cls, data: Mapping) -> "ParallelConfig":
        _require_keys("parallel", data, {"n_envs"})
        n_envs = int(data.get("n_envs", 1))
        if n_envs <= 0:
            raise ConfigError("parallel.n_envs must be positive")
        return cls(n_envs)


@dataclass(frozen=True)
class ExperimentConfig:
    experiment: str
    seed: int
    deck_source: DeckSourceConfig
    policy: PolicyConfig
    reward: RewardConfig
    league: LeagueConfig
    log_level: str
    parallel: ParallelConfig

    @classmethod
    def parse(cls, data: Mapping) -> "ExperimentConfig":
        _require_keys(
            "experiment config",
            data,
            {"experiment", "seed", "deck_source", "policy", "reward", "league", "log_level", "parallel"},
        )
        if not data.get("experiment"):
            raise ConfigError("experiment (이름) is required")
        if "seed" not in data:
            raise ConfigError("seed is required (NFR-3 — 재현 불가한 결과는 결과가 아님)")
        log_level = str(data.get("log_level", "RESULT"))
        if log_level not in LOG_LEVELS:
            raise ConfigError(f"log_level must be one of {LOG_LEVELS}, got {log_level!r}")
        return cls(
            experiment=str(data["experiment"]),
            seed=int(data["seed"]),
            deck_source=DeckSourceConfig.parse(data.get("deck_source") or {}),
            policy=PolicyConfig.parse(data.get("policy") or {}),
            reward=RewardConfig.parse(data.get("reward") or {}),
            league=LeagueConfig.parse(data.get("league") or {}),
            log_level=log_level,
            parallel=ParallelConfig.parse(data.get("parallel") or {}),
        )


def load_experiment_config(path: Path) -> ExperimentConfig:
    with open(path, encoding="utf-8") as f:
        data = yaml.safe_load(f)
    if not isinstance(data, Mapping):
        raise ConfigError(f"{path}: config root must be a mapping")
    return ExperimentConfig.parse(data)
