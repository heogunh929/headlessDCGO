"""참가 정책 — RandomPolicy(검증·바닥선) / LLMPolicy(OpenAI 호환 endpoint).

LLMPolicy는 chat completions 규격(base_url/api_key/model)만 전제 — OpenAI·로컬(vLLM,
Ollama의 /v1)·상용 호환 서버 전부 같은 코드로 붙는다. 응답에서 첫 정수를 착수 인덱스로
파싱하고, 실패는 무작위 합법 수 폴백(판을 죽이지 않는다 — 착수 제한 시간이 있으므로).
"""

from __future__ import annotations

import json
import random
import re

import aiohttp

from dcgo_arena.state_text import state_to_text


class RandomPolicy:
    def __init__(self, seed: int = 0):
        self._rng = random.Random(seed)

    def __call__(self, turn: dict) -> int:
        return self._rng.choice([a["index"] for a in turn["legalActions"]])


SYSTEM_PROMPT = """당신은 디지몬 카드 게임(DCGO) 플레이어입니다.
매 턴 현재 보드 상태와 가능한 행동 목록을 받습니다.
목표: 상대 시큐리티를 모두 깨고 마지막 공격을 성공시켜 승리하는 것.
반드시 가능한 행동 목록에 있는 인덱스 하나만 고르고, 다음 JSON만 출력하세요:
{"index": <番号>, "why": "<한 줄 근거>"}"""


class LLMPolicy:
    def __init__(self, base_url: str, api_key: str, model: str,
                 system_prompt: str = SYSTEM_PROMPT, temperature: float = 0.3,
                 seed: int = 0, verbose: bool = False):
        self.base_url = base_url.rstrip("/")
        self.api_key = api_key
        self.model = model
        self.system_prompt = system_prompt
        self.temperature = temperature
        self._rng = random.Random(seed)
        self.verbose = verbose

    async def __call__(self, turn: dict) -> int:
        legal = [a["index"] for a in turn["legalActions"]]
        try:
            async with aiohttp.ClientSession() as session:
                async with session.post(
                        f"{self.base_url}/chat/completions",
                        headers={"Authorization": f"Bearer {self.api_key}",
                                 "Content-Type": "application/json"},
                        json={"model": self.model, "temperature": self.temperature,
                              "messages": [{"role": "system", "content": self.system_prompt},
                                           {"role": "user", "content": state_to_text(turn)}]},
                        timeout=aiohttp.ClientTimeout(total=45)) as response:
                    body = await response.json()
            content = body["choices"][0]["message"]["content"]
            if self.verbose:
                print(f"[LLM] {content[:120]}")
            index = self._parse_index(content)
            if index in legal:
                return index
        except (aiohttp.ClientError, KeyError, json.JSONDecodeError, TimeoutError) as ex:
            if self.verbose:
                print(f"[LLM 오류 → 무작위 폴백] {ex}")
        return self._rng.choice(legal)

    @staticmethod
    def _parse_index(content: str) -> int | None:
        try:
            return int(json.loads(content).get("index"))
        except (json.JSONDecodeError, TypeError, ValueError):
            pass
        match = re.search(r'"index"\s*:\s*(\d+)', content) or re.search(r"\b(\d+)\b", content)
        return int(match.group(1)) if match else None
