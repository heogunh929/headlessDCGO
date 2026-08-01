"""데스크탑 참가 앱(tkinter GUI) — 스크립트 없이 키·서버 입력 → 에이전트 선택 → 시작.

배포는 SDK와 같은 단일 파일 방식: 서버가 `static/dcgo_arena_app.py`(SDK 내장 + 이 GUI)를
생성하고, 참가자는 `python dcgo_arena_app.py` 한 번으로 창을 띄운다(사용자 확정 2026-08-01).
의존성 = SDK와 동일(aiohttp) + tkinter(파이썬 표준 배포판 포함; 리눅스는 python3-tk).

덱은 여기서도 무관여 — 웹 참가자 페이지의 활성 덱이 자동 적용된다.
"""

from __future__ import annotations

import asyncio
import json
import queue
import random
import threading
from pathlib import Path

from dcgo_arena.agents import OpenAICompat, from_file
from dcgo_arena.client import ArenaClient, ArenaError
from dcgo_arena.policies import SYSTEM_PROMPT, RandomPolicy

CONFIG_PATH = Path.home() / ".dcgo_arena_app.json"

#: 공용 AI 프리셋(사용자 확정 2026-08-01: 실행기 = 클로드/GPT 등 공용 AI용) — base_url/모델 예시
PRESETS = {
    "Anthropic (Claude)": ("https://api.anthropic.com/v1", "claude-sonnet-5"),
    "OpenAI (GPT)": ("https://api.openai.com/v1", "gpt-4o"),
    "OpenRouter": ("https://openrouter.ai/api/v1", "openrouter/auto"),
    "Ollama (로컬)": ("http://localhost:11434/v1", "llama3"),
}


def build_llm_agent(base_url: str, model: str, api_key: str, context: str):
    """전략 컨텍스트(사용자 지침)를 규칙 프롬프트에 덧붙여 LLM 에이전트 구성.
    기본 SYSTEM_PROMPT는 응답 형식(JSON index) 계약을 담고 있어 대체가 아니라 추가여야 한다."""
    prompt = SYSTEM_PROMPT
    if context.strip():
        prompt += "\n\n[플레이어 전략 지침 — 아래 방침을 우선 따르되, 응답 형식은 위 규칙을 유지]\n" + context.strip()
    return OpenAICompat(base_url=base_url, model=model, api_key=api_key or "none", system_prompt=prompt)


class Runner:
    """워커 스레드에서 asyncio 루프를 돌리는 참가 실행기 — 시작/정지 안전."""

    def __init__(self, log):
        self._log = log
        self._thread: threading.Thread | None = None
        self._loop: asyncio.AbstractEventLoop | None = None
        self._task: asyncio.Task | None = None
        self.wins = 0
        self.losses = 0
        self.draws = 0

    @property
    def running(self) -> bool:
        return self._thread is not None and self._thread.is_alive()

    def start(self, server, key, agent_factory, mode, join_code, daemon):
        if self.running:
            return
        self._thread = threading.Thread(
            target=self._worker, args=(server, key, agent_factory, mode, join_code, daemon), daemon=True)
        self._thread.start()

    def stop(self):
        if self._loop is not None and self._task is not None:
            self._loop.call_soon_threadsafe(self._task.cancel)

    def _worker(self, server, key, agent_factory, mode, join_code, daemon):
        self._loop = asyncio.new_event_loop()
        asyncio.set_event_loop(self._loop)
        self._task = self._loop.create_task(self._session(server, key, agent_factory, mode, join_code, daemon))
        try:
            self._loop.run_until_complete(self._task)
        except asyncio.CancelledError:
            self._log("정지됨")
        except Exception as ex:                                    # GUI는 죽지 않는다 — 로그로
            self._log(f"오류: {ex}")
        finally:
            self._loop.close()
            self._loop = None
            self._log("세션 종료")

    async def _session(self, server, key, agent_factory, mode, join_code, daemon):
        client = ArenaClient(server, key)
        me = await client.me()
        if me.get("error"):
            raise ArenaError("API 키가 유효하지 않습니다 — 참가자 페이지에서 확인하세요")
        self._log(f"인증 OK: {me['handle']} · 레이팅 {me.get('rating', '?')}±{me.get('rd', '?')}"
                  + ("" if me.get("activeDeck") else " · 활성 덱 없음(웹에서 지정 필요!)"))
        agent = agent_factory()

        my_seat = {"n": None}   # match_end에는 좌석이 없다 — match_start에서 포획

        def on_event(msg: dict) -> None:
            kind = msg.get("type")
            if kind == "room_created":
                self._log(f"★ 방 코드: {msg['code']} — 상대에게 전달하세요")
            elif kind == "queued":
                self._log("큐 대기" + (f" — {msg['notice']}" if msg.get("notice") else ""))
            elif kind == "match_start":
                my_seat["n"] = msg.get("seat")
                opp = msg.get("opponent") or {}
                self._log(f"매치 시작 — 좌석 {msg.get('seat')}, 상대 {opp.get('handle')}({opp.get('rating')})"
                          + (" [검증판]" if msg.get("verification") else ""))

        backoff = 3
        while True:
            try:
                result = await client.play(agent, mode=mode, room_code=join_code, on_event=on_event)
                if result.get("verification"):
                    self._log("검증판 결과: " + ("통과" if result.get("passed") else "실패")
                              + (f" — {result.get('notice')}" if result.get("notice") else ""))
                    if not daemon or mode != "ladder":
                        return
                    continue
                winner, my = result.get("winnerSeat"), my_seat["n"]
                if winner is None:
                    self.draws += 1
                    verdict = "무승부"
                elif winner == my:
                    self.wins += 1
                    verdict = "승리"
                else:
                    self.losses += 1
                    verdict = "패배"
                self._log(f"종료: {verdict} (사유 {result.get('reason')}, Δ{result.get('ratingDelta')})"
                          f" — 전적 {self.wins}승 {self.draws}무 {self.losses}패")
                backoff = 3
                if not daemon or mode != "ladder":
                    return
            except asyncio.CancelledError:
                raise
            except (ArenaError, OSError) as ex:
                if not daemon:
                    raise
                self._log(f"연결 문제({ex}) — {backoff}초 후 재접속")
                await asyncio.sleep(backoff)
                backoff = min(backoff * 2, 60)


def load_config() -> dict:
    try:
        return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}


def save_config(cfg: dict) -> None:
    try:
        CONFIG_PATH.write_text(json.dumps(cfg, ensure_ascii=False, indent=2), encoding="utf-8")
    except OSError:
        pass


def main() -> None:
    import tkinter as tk
    from tkinter import filedialog, scrolledtext, ttk

    cfg = load_config()
    root = tk.Tk()
    root.title("DigimonCardgame AI 아레나 참가 앱")
    root.geometry("640x560")

    logq: queue.Queue[str] = queue.Queue()
    runner = Runner(lambda line: logq.put(line))

    frm = ttk.Frame(root, padding=10)
    frm.pack(fill="both", expand=True)

    # ── 접속 정보 ─────────────────────────────────────────────────────────────
    ttk.Label(frm, text="서버").grid(row=0, column=0, sticky="w")
    server_v = tk.StringVar(value=cfg.get("server", "http://192.168.0.48:8791"))
    ttk.Entry(frm, textvariable=server_v, width=42).grid(row=0, column=1, columnspan=2, sticky="we")
    ttk.Label(frm, text="API 키").grid(row=1, column=0, sticky="w")
    key_v = tk.StringVar(value=cfg.get("key", ""))
    ttk.Entry(frm, textvariable=key_v, width=42, show="•").grid(row=1, column=1, columnspan=2, sticky="we")

    # ── 에이전트 ──────────────────────────────────────────────────────────────
    ttk.Label(frm, text="에이전트").grid(row=2, column=0, sticky="w", pady=(8, 0))
    agent_v = tk.StringVar(value=cfg.get("agent", "random"))
    row_a = ttk.Frame(frm)
    row_a.grid(row=2, column=1, columnspan=2, sticky="w", pady=(8, 0))
    for value, label in (("random", "무작위"), ("openai", "OpenAI 호환 LLM"), ("file", "커스텀 파일")):
        ttk.Radiobutton(row_a, text=label, variable=agent_v, value=value).pack(side="left", padx=(0, 10))

    llm = ttk.LabelFrame(frm, text="OpenAI 호환 설정 (에이전트=LLM일 때)", padding=6)
    llm.grid(row=3, column=0, columnspan=3, sticky="we", pady=4)
    preset_v = tk.StringVar(value=cfg.get("preset", ""))
    ttk.Label(llm, text="프리셋").grid(row=0, column=0, sticky="w")
    preset_box = ttk.Combobox(llm, textvariable=preset_v, values=list(PRESETS), state="readonly", width=42)
    preset_box.grid(row=0, column=1, sticky="we")
    base_v = tk.StringVar(value=cfg.get("base_url", "http://localhost:11434/v1"))
    model_v = tk.StringVar(value=cfg.get("model", "llama3"))
    apikey_v = tk.StringVar(value=cfg.get("llm_key", ""))
    for i, (label, var) in enumerate((("base_url", base_v), ("model", model_v), ("api key", apikey_v)), start=1):
        ttk.Label(llm, text=label).grid(row=i, column=0, sticky="w")
        ttk.Entry(llm, textvariable=var, width=44).grid(row=i, column=1, sticky="we")

    def apply_preset(_event=None):
        if preset_v.get() in PRESETS:
            base, model = PRESETS[preset_v.get()]
            base_v.set(base)
            model_v.set(model)
    preset_box.bind("<<ComboboxSelected>>", apply_preset)

    # 전략 컨텍스트(사용자 확정 2026-08-01): AI에게 줄 방침 — 덱 플랜·우선순위·금기 등.
    # 게임 규칙/응답 형식 프롬프트에 '추가'로 들어간다(대체 아님).
    ctx = ttk.LabelFrame(frm, text="전략 컨텍스트 (선택 — AI에게 줄 플레이 방침·덱 플랜)", padding=6)
    ctx.grid(row=6, column=0, columnspan=3, sticky="we", pady=4)
    ctx_t = tk.Text(ctx, height=5, wrap="word", font=("Consolas", 9))
    ctx_t.pack(fill="both", expand=True)
    ctx_t.insert("1.0", cfg.get("context", ""))

    file_row = ttk.Frame(frm)
    file_row.grid(row=4, column=0, columnspan=3, sticky="we")
    file_v = tk.StringVar(value=cfg.get("agent_file", ""))
    ttk.Label(file_row, text="커스텀 파일").pack(side="left")
    ttk.Entry(file_row, textvariable=file_v, width=40).pack(side="left", padx=4)
    ttk.Button(file_row, text="찾기…",
               command=lambda: file_v.set(filedialog.askopenfilename(filetypes=[("Python", "*.py")]) or file_v.get())
               ).pack(side="left")

    # ── 모드 ─────────────────────────────────────────────────────────────────
    ttk.Label(frm, text="모드").grid(row=5, column=0, sticky="w", pady=(8, 0))
    mode_v = tk.StringVar(value=cfg.get("mode", "daemon"))
    row_m = ttk.Frame(frm)
    row_m.grid(row=5, column=1, columnspan=2, sticky="w", pady=(8, 0))
    # 방 만들기/참가: 비공개(사용자 결정 2026-08-01) — 백엔드는 유지, 노출만 보류
    for value, label in (("daemon", "래더 상주"), ("one", "래더 1판")):
        ttk.Radiobutton(row_m, text=label, variable=mode_v, value=value).pack(side="left", padx=(0, 8))
    join_v = tk.StringVar(value="")

    # ── 실행 ─────────────────────────────────────────────────────────────────
    log = scrolledtext.ScrolledText(frm, height=14, state="disabled", font=("Consolas", 9))   # 윈도우 기준(미보유 환경은 Tk가 대체)
    log.grid(row=7, column=0, columnspan=3, sticky="nsew", pady=(8, 4))
    frm.rowconfigure(7, weight=1)
    frm.columnconfigure(1, weight=1)

    status_v = tk.StringVar(value="대기")
    ttk.Label(frm, textvariable=status_v).grid(row=8, column=0, columnspan=2, sticky="w")

    def append(line: str) -> None:
        log.configure(state="normal")
        log.insert("end", line + "\n")
        log.see("end")
        log.configure(state="disabled")

    def agent_factory():
        kind = agent_v.get()
        if kind == "openai":
            return build_llm_agent(base_v.get(), model_v.get(), apikey_v.get(), ctx_t.get("1.0", "end"))
        if kind == "file":
            return from_file(file_v.get())
        return RandomPolicy(random.randrange(1, 2 ** 30))

    def on_start():
        if runner.running:
            append("이미 실행 중")
            return
        save_config({"server": server_v.get(), "key": key_v.get(), "agent": agent_v.get(),
                     "base_url": base_v.get(), "model": model_v.get(), "llm_key": apikey_v.get(),
                     "agent_file": file_v.get(), "mode": mode_v.get(), "preset": preset_v.get(),
                     "context": ctx_t.get("1.0", "end").strip()})
        m = mode_v.get()
        mode = {"daemon": "ladder", "one": "ladder", "create": "create_room", "join": "join_room"}[m]
        join_code = join_v.get().strip() if m == "join" else None
        if m == "join" and not join_code:
            append("방 코드를 입력하세요")
            return
        append(f"시작 — {server_v.get()} ({m})")
        status_v.set("실행 중")
        runner.start(server_v.get().rstrip("/"), key_v.get().strip(), agent_factory,
                     mode, join_code, daemon=(m == "daemon"))

    def on_stop():
        runner.stop()
        status_v.set("정지 요청됨")

    btns = ttk.Frame(frm)
    btns.grid(row=8, column=2, sticky="e")
    ttk.Button(btns, text="시작", command=on_start).pack(side="left", padx=4)
    ttk.Button(btns, text="정지", command=on_stop).pack(side="left")

    def drain():
        while True:
            try:
                append(logq.get_nowait())
            except queue.Empty:
                break
        if not runner.running and status_v.get() == "실행 중":
            status_v.set("대기")
        root.after(200, drain)

    drain()
    root.mainloop()


if __name__ == "__main__":
    main()
