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


def ClaudeCode(command: str = "claude", model: str | None = None,
               strategy: str = "", seed: int = 0, verbose: bool = False):
    """Claude Code CLI(공식 도구·구독 과금) 헤드리스 브리지 — 착수마다 `claude -p`로 수를 고른다.

    구독(Pro/Max) 사용자용 정식 경로(2026-08-01): 서드파티 OAuth가 금지·차단된 뒤에도
    Claude Code 자체는 자사 제품이라 구독 사용이 허용된다. 전제 = `claude` CLI 설치·로그인.
    파싱 실패·타임아웃(50초)은 무작위 합법 수 폴백 — 착수 제한(기본 60초) 안에서 판을 지킨다."""
    import asyncio
    import json as _json
    import random as _random
    import re as _re

    from dcgo_arena.state_text import state_to_text

    rng = _random.Random(seed)

    async def agent(turn: dict) -> int:
        legal = [a["index"] for a in turn["legalActions"]]
        prompt = (
            "디지몬 카드 게임(DCGO) 한 수를 고른다. 아래 보드 상태와 합법 행동 목록을 보고 "
            "가장 유리한 행동 인덱스 하나를 고르라.\n"
            + (f"\n[전략 지침]\n{strategy}\n" if strategy.strip() else "")
            + "\n" + state_to_text(turn)
            + "\n\n반드시 다음 JSON 한 줄만 출력: {\"index\": <합법 인덱스>}"
        )
        args = [command, "-p", prompt]
        if model:
            args += ["--model", model]
        try:
            proc = await asyncio.create_subprocess_exec(
                *args, stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.DEVNULL)
            out, _ = await asyncio.wait_for(proc.communicate(), timeout=50)
            text = out.decode("utf-8", errors="replace")
            if verbose:
                print(f"[claude] {text[:120]}")
            found = _re.search(r'\{[^{}]*"index"\s*:\s*(\d+)[^{}]*\}', text)
            index = int(found.group(1)) if found else int(_json.loads(text)["index"])
            if index in legal:
                return index
        except Exception:
            pass
        return rng.choice(legal)   # 폴백 — 몰수패 방지가 우선

    return agent
