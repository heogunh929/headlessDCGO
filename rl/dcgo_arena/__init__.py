"""dcgo-arena — DigimonCardgame AI 아레나 참가 SDK (설계 v1 §1, M5 + TextArena식 고수준 API).

빠른 시작 (TextArena 관례):
    import dcgo_arena as da

    da.play(
        server="http://서버:8791", key="API키",
        agent=da.agents.OpenAICompat(base_url="http://localhost:11434/v1", model="llama3"),
        games=10,            # daemon=True 로 상주 참가
    )

덱은 SDK가 관여하지 않는다 — 생성·수정·사용 덱 지정은 전부 웹 참가자 페이지에서 하며,
서버가 그 계정의 활성 덱을 자동 적용한다(사용자 확정 2026-07-31). agent 생략 = 무작위
합법 수. 저수준(ArenaClient)·CLI(`python -m dcgo_arena play|daemon`)도 제공.
"""

from dcgo_arena import agents
from dcgo_arena.client import ArenaClient, ArenaError
from dcgo_arena.highlevel import daemon, play
from dcgo_arena.policies import LLMPolicy, RandomPolicy
from dcgo_arena.state_text import state_to_text

__all__ = ["play", "daemon", "agents",
           "ArenaClient", "ArenaError", "RandomPolicy", "LLMPolicy", "state_to_text"]
