"""seat 매치 프로토콜 v1 stdio 클라이언트 (docs/audit/rl_seat_protocol_v1.md).

호스트(C# RlBridgeHost)를 자식 프로세스로 띄우고 JSON-lines로 대화한다.
핸드셰이크에서 vocab 해시를 로컬(cards.json 기반) vocab과 대조한다 — 불일치 = 즉시 실패
(임베딩·레시피 desync 방지, 프로토콜 §2 클라이언트 의무).
"""

from __future__ import annotations

import hashlib
import json
import subprocess
from pathlib import Path

from dcgo_rl.cards import CardIndex, CardVocabulary


def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


def host_dll_path() -> Path:
    """호스트 DLL — Release 우선(펌프-era 처리량 §7: Release 2배), 없으면 Debug 폴백.

    빌드 구성 간 스텝열 동일(결정론)은 rl_env_design §7에서 실측 — 구성 선택은 속도만 바꾼다.
    """
    base = repo_root() / "tools" / "RlBridgeHost" / "bin"
    for config in ("Release", "Debug"):
        candidate = base / config / "net8.0" / "RlBridgeHost.dll"
        if candidate.exists():
            return candidate
    return base / "Release" / "net8.0" / "RlBridgeHost.dll"


def local_vocab_hash(vocab: CardVocabulary) -> str:
    entries = sorted(vocab.to_json()["ids"].items(), key=lambda kv: kv[1])
    joined = ",".join(f"{number}:{card_id}" for number, card_id in entries)
    return hashlib.sha256(joined.encode("utf-8")).hexdigest()


class BridgeError(RuntimeError):
    pass


class BridgeClient:
    """단일 호스트 프로세스와의 프로토콜 v1 세션. 1연결=2좌석(stdio 셀프플레이 토폴로지)."""

    def __init__(
        self,
        result_log: str | None = None,
        verify_vocab: bool = True,
        log_level: str = "OFF",
        event_log: str | None = None,
        match_log_dir: str | None = None,
        record_mode: str = "off",
        engine_sha: str = "",
    ):
        dll = host_dll_path()
        if not dll.exists():
            raise BridgeError(
                f"RlBridgeHost not built: {dll}\n"
                "run: dotnet build tools/RlBridgeHost/RlBridgeHost.csproj -c Release"
            )

        cmd = ["dotnet", str(dll)]
        if result_log:
            cmd += ["--result-log", result_log]
        if log_level.upper() != "OFF":
            cmd += ["--log-level", log_level]
        if event_log:
            cmd += ["--event-log", event_log]
        if match_log_dir and record_mode != "off":
            cmd += ["--match-log-dir", match_log_dir, "--record-mode", record_mode]
            if engine_sha:
                cmd += ["--engine-sha", engine_sha]
        # 호스트 stderr는 기본 폐기하되, DCGO_HOST_STDERR=<path>면 추기 리다이렉트 — 호스트 내부
        # 예외 스택(프로토콜 error에는 message만 실림)의 유일한 회수 경로다.
        import os

        stderr_path = os.environ.get("DCGO_HOST_STDERR")
        stderr_target = open(stderr_path, "a") if stderr_path else subprocess.DEVNULL

        self._proc = subprocess.Popen(
            cmd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=stderr_target,
            text=True,
            bufsize=1,
            cwd=str(repo_root()),
        )

        self.welcome = self._request({"type": "hello", "protocol": 1})
        if self.welcome.get("type") != "welcome":
            raise BridgeError(f"handshake failed: {self.welcome}")

        if verify_vocab:
            local = CardVocabulary.build(CardIndex.load().canonical_numbers(), version="v1")
            if local_vocab_hash(local) != self.welcome["vocabHash"]:
                raise BridgeError(
                    "vocab hash mismatch between host (engine cards.json) and local build — "
                    "canonicalization rule drift; do NOT continue (protocol §2)."
                )

        claimed = self._request({"type": "claim", "seats": [1, 2]})
        if claimed.get("type") != "claimed":
            raise BridgeError(f"claim failed: {claimed}")

    # --- protocol ----------------------------------------------------------

    @property
    def obs_size(self) -> int:
        return int(self.welcome["obsSize"])

    @property
    def action_size(self) -> int:
        return int(self.welcome["actionSize"])

    @property
    def vocab_size(self) -> int:
        return int(self.welcome["vocabSize"])

    def describe(self) -> list[str]:
        schema = self._request({"type": "describe"})
        if schema.get("type") != "schema":
            raise BridgeError(f"describe failed: {schema}")
        return list(schema["features"])

    def reset(self, seed: int, decks: dict, max_steps: int = 2000) -> dict:
        return self._request(
            {"type": "reset", "seed": seed, "maxSteps": max_steps, "decks": decks}
        )

    def act(self, seat: int, index: int) -> dict:
        msg = self._request({"type": "action", "seat": seat, "index": index})
        if msg.get("type") == "error":
            if msg.get("code") == "illegal_action":
                self._read_line()  # 재발행된 turn을 소비해 스트림 desync 방지 (프로토콜 §5)
            raise BridgeError(f"host rejected action: {msg}")
        return msg

    def close(self) -> None:
        if self._proc.stdin:
            self._proc.stdin.close()
        self._proc.wait(timeout=10)

    # --- plumbing ------------------------------------------------------------

    def _request(self, obj: dict) -> dict:
        assert self._proc.stdin
        self._proc.stdin.write(json.dumps(obj) + "\n")
        self._proc.stdin.flush()
        return self._read_line()

    def _read_line(self) -> dict:
        assert self._proc.stdout
        line = self._proc.stdout.readline()
        if not line:
            raise BridgeError("host closed the connection")
        return json.loads(line)
