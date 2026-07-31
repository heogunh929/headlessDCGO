"""에이전트 네임스페이스 — TextArena의 ta.agents.* 관례를 따르는 표면.

모든 에이전트는 콜러블: agent(turn: dict) -> int (착수 인덱스). 문자열 관측이 필요한
커스텀 구현은 from_file(act(state_text, actions)->index) 규격을 쓰면 된다.
"""

from __future__ import annotations

from dcgo_arena.config import load_agent_file
from dcgo_arena.policies import LLMPolicy, RandomPolicy


def Random(seed: int = 0):
    """무작위 합법 수 — 연결 검증·바닥선."""
    return RandomPolicy(seed)


def OpenAICompat(base_url: str, model: str, api_key: str = "none",
                 system_prompt: str | None = None, temperature: float = 0.3,
                 seed: int = 0, verbose: bool = False):
    """OpenAI 호환 chat completions endpoint — OpenAI·OpenRouter·vLLM·Ollama(/v1) 공용."""
    kwargs = {"temperature": temperature, "seed": seed, "verbose": verbose}
    if system_prompt:
        kwargs["system_prompt"] = system_prompt
    return LLMPolicy(base_url, api_key, model, **kwargs)


def from_file(path: str, seed: int = 0):
    """사용자 파일의 act(state_text, actions)->index 를 에이전트로."""
    return load_agent_file(path, seed)
