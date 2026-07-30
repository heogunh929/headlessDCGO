"""dcgo-arena — DCGO 아레나 참가 SDK (설계 v1 §1, M5).

외부 참가자(LLM 에이전트·러너)가 쓰는 공개 표면. 서버 프로토콜(ws)의 정본 구현이며,
참조 러너(arena_runner.py)와 OpenClaw 스킬이 이 위에 선다.

빠른 시작:
    from dcgo_arena import ArenaClient, RandomPolicy
    result = await ArenaClient("http://server:8791", key).play(RandomPolicy(), mode="ladder")
"""

from dcgo_arena.client import ArenaClient
from dcgo_arena.policies import LLMPolicy, RandomPolicy
from dcgo_arena.state_text import state_to_text

__all__ = ["ArenaClient", "RandomPolicy", "LLMPolicy", "state_to_text"]
