"""Local model router for card porting.

역할:
- OpenAI-compatible 엔드포인트(Ollama, vLLM, SGLang, LiteLLM, LM Studio)를 통해 모델 호출
- planner / coder / reviewer 역할 분리
- Python 하네스가 오케스트레이터가 되도록 모델을 함수처럼 제공

환경변수 예시:
  LOCAL_LLM_BASE_URL=http://127.0.0.1:11434/v1
  LOCAL_LLM_API_KEY=ollama
  PLANNER_MODEL=gemma4:31b
  CODER_MODEL=qwen3-coder-next:latest
  REVIEWER_MODEL=gemma4:31b
"""

from __future__ import annotations

import os
import re
from dataclasses import dataclass
from typing import Literal


Role = Literal["planner", "coder", "reviewer"]


@dataclass(frozen=True)
class ModelConfig:
    base_url: str
    api_key: str
    planner_model: str
    coder_model: str
    reviewer_model: str
    timeout_seconds: float = 600.0
    coder_max_tokens: int = 8000
    planner_max_tokens: int = 3000
    reviewer_max_tokens: int = 2000

    @classmethod
    def from_env(cls) -> "ModelConfig":
        return cls(
            base_url=os.environ.get("LOCAL_LLM_BASE_URL", "http://127.0.0.1:11434/v1"),
            api_key=os.environ.get("LOCAL_LLM_API_KEY", "ollama"),
            planner_model=os.environ.get("PLANNER_MODEL", "gemma4:31b"),
            coder_model=os.environ.get("CODER_MODEL", "qwen3-coder-next:latest"),
            reviewer_model=os.environ.get("REVIEWER_MODEL", os.environ.get("PLANNER_MODEL", "gemma4:31b")),
            timeout_seconds=float(os.environ.get("LOCAL_LLM_TIMEOUT", "600")),
            coder_max_tokens=int(os.environ.get("CODER_MAX_TOKENS", "8000")),
            planner_max_tokens=int(os.environ.get("PLANNER_MAX_TOKENS", "3000")),
            reviewer_max_tokens=int(os.environ.get("REVIEWER_MAX_TOKENS", "2000")),
        )


class LocalModelRouter:
    def __init__(self, config: ModelConfig | None = None):
        self.config = config or ModelConfig.from_env()
        try:
            from openai import OpenAI
        except ImportError as ex:  # pragma: no cover
            raise RuntimeError("pip install openai 후 다시 실행하세요.") from ex
        self._client = OpenAI(
            base_url=self.config.base_url,
            api_key=self.config.api_key,
            timeout=self.config.timeout_seconds,
        )

    def model_for(self, role: Role) -> str:
        if role == "planner":
            return self.config.planner_model
        if role == "coder":
            return self.config.coder_model
        if role == "reviewer":
            return self.config.reviewer_model
        raise ValueError(f"unknown role: {role}")

    def max_tokens_for(self, role: Role) -> int:
        if role == "planner":
            return self.config.planner_max_tokens
        if role == "coder":
            return self.config.coder_max_tokens
        return self.config.reviewer_max_tokens

    def call(self, role: Role, system: str, user: str, *, extract_code: bool = False) -> str:
        """역할 모델 호출. coder는 보통 extract_code=True로 사용."""
        resp = self._client.chat.completions.create(
            model=self.model_for(role),
            temperature=0,
            max_tokens=self.max_tokens_for(role),
            messages=[
                {"role": "system", "content": system},
                {"role": "user", "content": user},
            ],
        )
        text = resp.choices[0].message.content or ""
        return extract_csharp_code(text) if extract_code else text.strip()

    def code(self, system: str, user: str) -> str:
        return self.call("coder", system, user, extract_code=True)

    def plan(self, system: str, user: str) -> str:
        return self.call("planner", system, user, extract_code=False)

    def review(self, system: str, user: str) -> str:
        return self.call("reviewer", system, user, extract_code=False)


def extract_csharp_code(text: str) -> str:
    # 펜스 블록 우선(언어표기·공백·개행 변형 허용). 없으면 전체를 대상으로.
    m = re.search(r"```[ \t]*(?:csharp|cs|c#)?[ \t]*\r?\n?(.*?)```", text, re.S | re.I)
    code = m.group(1) if m else text
    # 남은 코드펜스 라인(``` — CS1056 예기치 않은 '`' 원인) 제거: 매칭 실패/중첩/미종료 모두 방어.
    code = re.sub(r"(?m)^[ \t]*```.*$", "", code)
    return code.strip()
