"""dcgo-arena.toml 설정 로더 — 온보딩 개선 ①(요구 §8.7).

참가자는 파일 한 장으로 참가한다:

    # dcgo-arena.toml
    server = "http://서버:8791"
    key = "발급받은 API 키"

    [agent]                # 생략 시 무작위 합법 수
    kind = "llm"           # random | llm | file
    base_url = "http://localhost:11434/v1"
    api_key = "none"
    model = "llama3"
    # kind="file"이면: path = "my_agent.py"  (act(state_text, actions)->index 정의)

탐색 순서: --config 인자 > ./dcgo-arena.toml > ~/.config/dcgo-arena.toml
"""

from __future__ import annotations

import tomllib
from pathlib import Path

SEARCH = (Path("dcgo-arena.toml"), Path.home() / ".config" / "dcgo-arena.toml")


def load_config(path: str | None = None) -> dict:
    candidates = [Path(path)] if path else list(SEARCH)
    for candidate in candidates:
        if candidate.is_file():
            with open(candidate, "rb") as f:
                data = tomllib.load(f)
            data["_source"] = str(candidate)
            return data
    return {}


def build_agent(config: dict, seed: int = 0, verbose: bool = False):
    """설정의 [agent] → 정책 콜러블. kind=file은 사용자 파이썬 파일의 act()를 싣는다."""
    from dcgo_arena.policies import LLMPolicy, RandomPolicy

    agent = config.get("agent") or {}
    kind = agent.get("kind", "random")

    if kind == "llm":
        base = agent.get("base_url", "")
        model = agent.get("model", "")
        if not base or not model:
            raise SystemExit("[agent] kind=llm에는 base_url과 model이 필요합니다")
        return LLMPolicy(base, agent.get("api_key", "none"), model,
                         temperature=float(agent.get("temperature", 0.3)),
                         seed=seed, verbose=verbose,
                         **({"system_prompt": agent["system_prompt"]} if agent.get("system_prompt") else {}))

    if kind == "file":
        return load_agent_file(agent.get("path", ""), seed)

    return RandomPolicy(seed)


def load_agent_file(path: str, seed: int = 0):
    """커스텀 에이전트 파일: `def act(state_text, actions) -> index` 하나만 요구(온보딩 ② 규격).
    actions = [{"index", "desc"}]. 원하면 act(state_text, actions, turn)로 원본 turn까지 받을 수 있다."""
    import importlib.util
    import inspect

    from dcgo_arena.state_text import state_to_text

    file = Path(path)
    if not file.is_file():
        raise SystemExit(f"에이전트 파일 없음: {path}")
    spec = importlib.util.spec_from_file_location("dcgo_arena_user_agent", file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    act = getattr(module, "act", None)
    if not callable(act):
        raise SystemExit(f"{path}에 act(state_text, actions) 함수가 없습니다")
    wants_turn = len(inspect.signature(act).parameters) >= 3

    async def policy(turn: dict) -> int:
        args = [state_to_text(turn), turn.get("legalActions") or []]
        if wants_turn:
            args.append(turn)
        result = act(*args)
        if inspect.isawaitable(result):
            result = await result
        return int(result)

    return policy
