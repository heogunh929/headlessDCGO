"""runner — 엔진 PC 상주 데몬 (설계 v1 §1·§3.1). stdlib 전용: 엔진 PC에 추가 의존성 0.

역할: 잡 실행(화이트리스트)·graceful 정지(SIGTERM)·강제 종료·진행/RSS 감시·runs/ 아티팩트 서빙·
러너 설정(워커 상한 등) 원격 관리. 인증: 운영자 표면 공유 토큰(X-Ops-Token, 요구 §7 3분류).

실행:  DCGO_OPS_TOKEN=<토큰> uv run python -m opsd.runner [--port 8790]
"""

from __future__ import annotations

import argparse
import json
import os
import re
import signal
import subprocess
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlparse

RL_DIR = Path(__file__).resolve().parents[1]
REPO = RL_DIR.parent
RUNS = REPO / "runs"
CONFIG_PATH = RL_DIR / "opsd" / "runner-config.json"
ALIASES_PATH = RL_DIR / "opsd" / "model-aliases.json"   # 정책 별칭 {상대경로: 별칭} — 정책 관리 탭
HIDDEN_PATH = RL_DIR / "opsd" / "model-hidden.json"     # 논리삭제(보관) 목록 [상대경로] — 파일은 보존
PYTHON = str(RL_DIR / ".venv" / "bin" / "python")   # uv 래퍼 우회 — SIGTERM이 python에 직행 [M1 실측]

# 화이트리스트(요구 §8): 스크립트와 허용 인자·형만 통과. 임의 셸 불가.
# recipes/init_model은 경로 인자 — 각각 rl/decks/·runs/ 밑으로 잠근다(_start_job의 전용 검증).
SCRIPTS = {
    "train": {
        "file": "train.py",
        "args": {"steps": int, "games": int, "n_envs": int, "seed": int, "eval_matches": int, "vec": str,
                 "record_mode": str, "checkpoint_every": int, "checkpoint_keep": int, "out": str,
                 "recipes": list, "init_model": str, "eval_only": int},
    },
    "league": {
        "file": "league.py",
        "args": {"config": str},   # config 경로는 runs/ 하위로 잠금(launch_job 전용 검증)
    },
}

DEFAULT_CONFIG = {"worker_cap": 6, "arena_cap": 2,
                  "notes": "worker_cap: rl-workers-six 규약 + OOM 이력(2026-07-29) 기준값 / arena_cap: 동시 아레나 판 상한(설계 R-b)"}

_jobs: dict[str, dict] = {}   # job_id -> {proc, script, args, out, log, started}
_arena: dict[str, "ArenaMatch"] = {}   # matchId -> 진행 중 아레나 판 (릴레이 §3.1 /arena/match)
_queue: list[dict] = []       # 순차 잡 큐(덱별 정책 배치 등) — 슬롯이 비면 스케줄러가 하나씩 기동
_queue_done: list[dict] = []  # 큐에서 소화된 항목의 기록 {job|error, script, args}


def config() -> dict:
    if CONFIG_PATH.exists():
        return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
    return dict(DEFAULT_CONFIG)


_cards_meta: dict | None = None


#: AS-IS CEntity_Base.CardColor 열거 순서 그대로(CEntity_Base.cs:381) — 자산 hex 디코드용
CARD_COLORS = ["Red", "Blue", "Yellow", "Green", "White", "Black", "Purple", "None"]


def cards_meta() -> dict:
    """카드번호 → {name, type, maxCount, colors} — 덱 검증기(opsd)·뷰어의 데이터원. cards.json(엔진
    export)에 자산의 MaxCountInDeck·cardColors를 보강해 합친다. cardColors는 Unity 직렬화 hex
    (리틀엔디언 int32/색). 규칙 자체는 AS-IS DeckData.IsValidDeckData가 정본."""
    global _cards_meta
    if _cards_meta is None:
        base: dict[str, dict] = {}
        cards_path = REPO / "src/HeadlessDCGO.Engine/Assets/CardBaseEntity/cards.json"
        for entry in json.loads(cards_path.read_text(encoding="utf-8")):
            base.setdefault(entry["cardNumber"], {"name": entry["name"], "type": entry["cardType"]})
        for asset in (REPO / "DCGO/Assets/CardBaseEntity").rglob("*.asset"):
            cid, maxc, colors_hex = None, None, None
            try:
                for line in asset.read_text(encoding="utf-8", errors="replace").splitlines():
                    if line.startswith("  CardID:"):
                        cid = line.split(":", 1)[1].strip()
                    elif line.startswith("  MaxCountInDeck:"):
                        maxc = int(line.split(":", 1)[1])
                    elif line.startswith("  cardColors:"):
                        colors_hex = line.split(":", 1)[1].strip()
            except (OSError, ValueError):
                continue
            if cid in base and maxc is not None and "maxCount" not in base[cid]:
                base[cid]["maxCount"] = maxc
            if cid in base and colors_hex and "colors" not in base[cid]:
                colors = [CARD_COLORS[int(colors_hex[k:k+2], 16)]
                          for k in range(0, len(colors_hex), 8)
                          if int(colors_hex[k:k+2], 16) < len(CARD_COLORS)]
                base[cid]["colors"] = [c for c in colors if c != "None"]
        _cards_meta = base
    return _cards_meta


def recipe_display(canonical: str) -> str:
    return canonical.replace("_", "-", 1)


def validate_recipe(main: list, digitama: list) -> list[str]:
    """학습 덱 검증 — AS-IS 규칙(실존·메인 50·디지타마 ≤5·MaxCountInDeck·타입 분리)만.
    카드 풀 필터 없음(요구 §6.6.5: RL 덱은 파이프라인 소유·풀 제약 없음)."""
    meta = cards_meta()
    errors: list[str] = []
    counts: dict[str, int] = {}
    for section, entries, egg in (("main", main, False), ("digitama", digitama, True)):
        for entry in entries:
            display = recipe_display(str(entry.get("card", "")))
            count = int(entry.get("count", 0))
            info = meta.get(display)
            if info is None:
                errors.append(f"{section}: 존재하지 않는 카드 {display}")
                continue
            if count < 1:
                errors.append(f"{section}: {display} 매수 {count} 무효")
            is_egg = info.get("type") == "DigiEgg"
            if egg != is_egg:
                errors.append(f"{section}: {display}는 {'디지타마' if is_egg else '일반 카드'} — 구역이 틀림")
            counts[display] = counts.get(display, 0) + count
    main_total = sum(int(e.get("count", 0)) for e in main)
    egg_total = sum(int(e.get("count", 0)) for e in digitama)
    if main_total != 50:
        errors.append(f"메인 덱은 정확히 50장(현재 {main_total})")
    if egg_total > 5:
        errors.append(f"디지타마 덱은 최대 5장(현재 {egg_total})")
    for display, total in counts.items():
        cap_count = meta.get(display, {}).get("maxCount", 4)
        if total > cap_count:
            errors.append(f"{display}: {total}장 > 최대 {cap_count}장")
    return errors


def recipe_summary(path: Path) -> dict:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as ex:
        return {"file": path.name, "name": path.stem, "error": str(ex)}
    meta = cards_meta()
    def section(key):
        return [{"card": e["card"], "display": recipe_display(e["card"]),
                 "name": meta.get(recipe_display(e["card"]), {}).get("name", "?"),
                 "count": int(e["count"])} for e in data.get(key, [])]
    main, digitama = section("main"), section("digitama")
    reasons = validate_recipe(main, digitama)
    return {"file": path.name, "name": data.get("name", path.stem),
            "main": main, "digitama": digitama,
            "mainCount": sum(e["count"] for e in main), "eggCount": sum(e["count"] for e in digitama),
            "valid": not reasons, "reasons": reasons}


def parse_deck_text(text: str, name: str) -> dict:
    """클립보드 덱리스트 → 레시피 구성. AS-IS DeckCodeUtility 전사 파서(opsd/deckcode.py) 공용 —
    관리자·참가자가 같은 형식(빌더 줄/TTS·digimonmeta 배열)·같은 거동(사용자 확정 2026-07-31)."""
    from opsd.deckcode import parse_clipboard
    parsed = parse_clipboard(text, cards_meta())
    if parsed.get("error"):
        return parsed
    return {"name": name or "가져온 덱", "main": parsed["main"], "digitama": parsed["digitama"],
            "skipped": parsed.get("skipped", [])}


def model_aliases() -> dict:
    if ALIASES_PATH.exists():
        try:
            return json.loads(ALIASES_PATH.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            return {}
    return {}


def engine_head() -> str:
    """엔진에 영향 주는 마지막 커밋(src/·tools/) — stale 판정 기준. HEAD 비교는 UI 커밋에도
    구엔진 오판을 낸다."""
    try:
        return subprocess.run(["git", "log", "-1", "--format=%H", "--", "src", "tools"],
                              cwd=REPO, capture_output=True, text=True, timeout=10).stdout.strip()
    except OSError:
        return ""


_stale_cache: dict[str, bool] = {}


def sha_is_stale(recorded: str, head: str) -> bool:
    """recorded가 엔진 헤드를 포함하면 신선(False). 포함 안 하면 그 뒤 엔진이 바뀐 것 — stale."""
    if not recorded or not head:
        return False
    key = f"{recorded}:{head[:12]}"
    if key not in _stale_cache:
        try:
            ok = subprocess.run(["git", "merge-base", "--is-ancestor", head, recorded],
                                cwd=REPO, capture_output=True, timeout=10).returncode == 0
        except OSError:
            ok = True
        _stale_cache[key] = not ok
    return _stale_cache[key]


def hidden_models() -> set:
    if HIDDEN_PATH.exists():
        try:
            return set(json.loads(HIDDEN_PATH.read_text(encoding="utf-8")))
        except json.JSONDecodeError:
            return set()
    return set()


def save_hidden(hidden: set) -> None:
    HIDDEN_PATH.write_text(json.dumps(sorted(hidden), ensure_ascii=False, indent=2), encoding="utf-8")


def models_detail() -> dict:
    """정책 전수 열거 + 메타 조인(정책 관리 탭). 경로는 runs/ 상대. kind = single|league|checkpoint.
    메타는 그 정책이 태어난 런 폴더의 meta.json(리그 라운드 포함 — train.py가 라운드 폴더에 씀)."""
    aliases = model_aliases()
    hidden = hidden_models()
    current_sha = engine_head()[:12]
    models = []

    def meta_of(directory: Path) -> dict:
        f = directory / "meta.json"
        if f.exists():
            try:
                m = json.loads(f.read_text(encoding="utf-8"))
                return {"engine_sha": m.get("engine_sha", ""), "deck_context": m.get("deck_context"),
                        "steps_done": m.get("steps_done"), "eval_winrate": m.get("eval_winrate_vs_random"),
                        "seed": (m.get("config") or {}).get("seed"), "status": m.get("status")}
            except json.JSONDecodeError:
                pass
        return {}

    def add(path: Path, kind: str, meta_dir: Path):
        rel = str(path.relative_to(RUNS))
        info = meta_of(meta_dir)
        models.append({"path": rel, "kind": kind, "alias": aliases.get(rel, ""),
                       "hidden": rel in hidden,
                       "created": time.strftime("%Y-%m-%dT%H:%M:%S%z", time.localtime(path.stat().st_mtime)),
                       "stale": sha_is_stale(info.get("engine_sha", ""), engine_head()),
                       **info})

    for run_dir in sorted(RUNS.iterdir()):
        if not run_dir.is_dir():
            continue
        if run_dir.name.startswith("league-"):
            for combo_dir in sorted(p for p in run_dir.iterdir() if p.is_dir() and p.name not in ("eval-matches",)):
                for round_dir in sorted(combo_dir.glob("round-*")):
                    if (round_dir / "policy.zip").exists():
                        add(round_dir / "policy.zip", "league", round_dir)
            continue
        if (run_dir / "policy.zip").exists():
            add(run_dir / "policy.zip", "single", run_dir)
        for ck in sorted((run_dir / "checkpoints").glob("step-*.zip")):
            add(ck, "checkpoint", run_dir)
    return {"currentSha": current_sha, "models": models}


def engine_sha() -> str:
    try:
        return subprocess.run(["git", "rev-parse", "HEAD"], cwd=REPO, capture_output=True,
                              text=True, timeout=10).stdout.strip()[:12]
    except OSError:
        return ""


class ArenaMatch:
    """아레나 판 1개 = RlBridgeHost 1프로세스. runner는 stdio를 큐로 릴레이만 한다 —
    좌석 라우팅·타임아웃·Elo는 opsd(브로커) 소관(설계 §3.2.5)."""

    def __init__(self, match_id: str, seed: int, decks: dict, max_steps: int, out_dir: Path):
        import queue as _queue
        import threading
        self.id = match_id
        self.out_dir = out_dir
        self.queue: _queue.Queue = _queue.Queue()
        self.done = False
        out_dir.mkdir(parents=True, exist_ok=True)
        dll = REPO / "tools/RlBridgeHost/bin/Release/net8.0/RlBridgeHost.dll"
        self.proc = subprocess.Popen(
            ["dotnet", str(dll), "--describe",
             "--match-log-dir", str(out_dir), "--record-mode", "all", "--engine-sha", engine_sha()],
            cwd=REPO, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=open(out_dir.parent / "host-stderr.log", "ab"), text=True, start_new_session=True)
        threading.Thread(target=self._reader, daemon=True).start()
        self._send({"type": "hello"})
        self._await("welcome")
        self._send({"type": "claim", "seats": [1, 2]})
        self._await("claimed")
        self._send({"type": "reset", "seed": seed, "decks": decks, "maxSteps": max_steps, "matchId": match_id})

    def _send(self, obj: dict) -> None:
        self.proc.stdin.write(json.dumps(obj) + "\n")
        self.proc.stdin.flush()

    def _reader(self) -> None:
        for line in self.proc.stdout:
            try:
                msg = json.loads(line)
            except json.JSONDecodeError:
                continue
            if msg.get("type") == "result":
                self.done = True
            self.queue.put(msg)
        self.done = True
        self.queue.put({"type": "host_exit", "returncode": self.proc.poll()})

    def _await(self, type_: str, timeout: float = 60.0) -> dict:
        import queue as _queue
        try:
            while True:
                msg = self.queue.get(timeout=timeout)
                if msg.get("type") == type_:
                    return msg
        except _queue.Empty:
            raise RuntimeError(f"host {type_} 응답 없음")

    def next_message(self, wait: float) -> dict | None:
        import queue as _queue
        try:
            return self.queue.get(timeout=wait)
        except _queue.Empty:
            return None

    def act(self, seat: int, index: int) -> None:
        self._send({"type": "action", "seat": seat, "index": index})

    def close(self) -> None:
        try:
            os.killpg(os.getpgid(self.proc.pid), signal.SIGKILL)
        except (ProcessLookupError, OSError):
            pass
        self.done = True


def rss_mb(root_pid: int) -> float:
    """잡 프로세스 트리 RSS 합(MB). /proc 직독 — 워커·호스트까지 합산."""
    children: dict[int, list[int]] = {}
    for stat in Path("/proc").glob("[0-9]*/stat"):
        try:
            parts = stat.read_text().rsplit(")", 1)[1].split()
            children.setdefault(int(parts[1]), []).append(int(stat.parent.name))
        except (OSError, IndexError, ValueError):
            continue
    total, queue = 0.0, [root_pid]
    while queue:
        pid = queue.pop()
        try:
            rss_pages = int((Path(f"/proc/{pid}/statm").read_text()).split()[1])
            total += rss_pages * 4096 / 1e6
        except (OSError, IndexError, ValueError):
            pass
        queue.extend(children.get(pid, []))
    return round(total, 1)


def job_status(job: dict) -> dict:
    proc: subprocess.Popen = job["proc"]
    alive = proc.poll() is None
    out = Path(job["out"])
    meta = {}
    if (out / "meta.json").exists():
        try:
            meta = json.loads((out / "meta.json").read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            pass
    # 진행률: 학습 로그의 total_timesteps 마지막 값 (SB3 표 출력 tail 파싱).
    progress = None
    log = Path(job["log"])
    if log.exists():
        try:
            tail = log.read_text(encoding="utf-8", errors="replace")[-4000:]
            hits = re.findall(r"total_timesteps\s*\|\s*(\d+)", tail)
            if hits:
                progress = int(hits[-1])
        except OSError:
            pass
    # 판수: 결과 jsonl 라인 합 — "스텝"보다 직관적인 진행 단위(사용자 요구 2026-07-30).
    matches_played = 0
    for f in out.glob("results-env*.jsonl"):
        try:
            matches_played += sum(1 for _ in open(f, encoding="utf-8"))
        except OSError:
            pass
    return {
        "alive": alive,
        "matches_played": matches_played,
        "returncode": proc.poll(),
        "pid": proc.pid,
        "rss_mb": rss_mb(proc.pid) if alive else 0,
        "progress_steps": progress,
        "meta": meta,
        "started": job["started"],
        "script": job["script"],
        "args": job["args"],
    }


def run_summary(run_dir: Path) -> dict:
    meta = {}
    if (run_dir / "meta.json").exists():
        try:
            meta = json.loads((run_dir / "meta.json").read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            pass
    matches = sorted(p.name for p in (run_dir / "matches").glob("*.jsonl.gz")) if (run_dir / "matches").exists() else []
    return {"name": run_dir.name, "meta": meta, "matches": matches}


def launch_job(data: dict) -> tuple[int, dict]:
    """잡 기동 코어 — HTTP 핸들러와 큐 스케줄러가 공용."""
    script = data.get("script")
    spec = SCRIPTS.get(script)
    if spec is None:
        return 400, {"error": f"script whitelist: {list(SCRIPTS)}"}
    # 동시 1개 강제(요구 §4) — 학습 잡이 살아 있으면 거부.
    for job in _jobs.values():
        if job["proc"].poll() is None:
            return 409, {"error": "job already running", "job": job["script"]}

    args: list[str] = []
    n_envs = None
    for key, value in (data.get("args") or {}).items():
        caster = spec["args"].get(key)
        if caster is None:
            return 400, {"error": f"arg whitelist: {list(spec['args'])}"}
        if key == "recipes":
            paths = []
            for name in (value or []):
                p = (RL_DIR / "decks" / str(name)).resolve()
                if not str(p).startswith(str((RL_DIR / "decks").resolve())) or not p.exists():
                    return 400, {"error": f"레시피는 rl/decks/ 하위만: {name}"}
                paths.append(str(p))
            if paths:
                args += ["--recipes", *paths]
            continue
        if key == "init_model":
            p = (RUNS / str(value)).resolve()
            if not str(p).startswith(str(RUNS.resolve())) or not p.exists():
                return 400, {"error": f"시작 정책은 runs/ 하위만: {value}"}
            args += ["--init-model", str(p)]
            continue
        if key == "config":
            p = (RUNS / str(value)).resolve()
            if not str(p).startswith(str(RUNS.resolve())) or not p.exists():
                return 400, {"error": f"config는 runs/ 하위만: {value}"}
            args += ["--config", str(p)]
            continue
        if key == "eval_only":
            if int(value):
                args += ["--eval-only"]
            continue
        value = caster(value)
        if key == "n_envs":
            n_envs = value
        args += [f"--{key.replace('_', '-')}", str(value)]

    cap = int(config().get("worker_cap", 6))
    if n_envs is not None and n_envs > cap:
        return 400, {"error": f"worker_cap {cap} 초과 (n_envs={n_envs}) — 러너 설정이 강제"}

    job_id = time.strftime("job-%Y%m%d-%H%M%S")
    if "config" in (data.get("args") or {}):
        out_abs = (RUNS / str(data["args"]["config"])).resolve().parent   # league: 산출 = config 폴더
    else:
        out = data.get("args", {}).get("out") or f"../runs/{job_id}"
        out_abs = (RL_DIR / out).resolve()
        if "out" not in (data.get("args") or {}):
            args += ["--out", out]
    log_path = out_abs / "job.log"
    out_abs.mkdir(parents=True, exist_ok=True)

    with open(log_path, "ab") as log:
        proc = subprocess.Popen(
            [PYTHON, spec["file"], *args], cwd=RL_DIR,
            stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
    _jobs[job_id] = {"proc": proc, "script": script, "args": data.get("args") or {},
                     "out": str(out_abs), "log": str(log_path), "started": time.strftime("%Y-%m-%dT%H:%M:%S%z")}
    return 201, {"job": job_id, "out": str(out_abs), "pid": proc.pid}


def queue_scheduler() -> None:
    """순차 잡 큐(덱별 정책 배치): 잡 슬롯이 비면 큐 머리부터 하나씩 기동. 실패는 기록하고 다음으로."""
    while True:
        time.sleep(4)
        if not _queue:
            continue
        if any(job["proc"].poll() is None for job in _jobs.values()):
            continue
        entry = _queue.pop(0)
        code, payload = launch_job(entry)
        _queue_done.append({"script": entry.get("script"), "args": entry.get("args"),
                            **({"job": payload.get("job")} if code == 201 else {"error": payload.get("error")})})


class Handler(BaseHTTPRequestHandler):
    server_version = "dcgo-runner/1"

    def _send(self, code: int, payload, content_type="application/json"):
        body = payload if isinstance(payload, bytes) else json.dumps(payload, ensure_ascii=False).encode()
        self.send_response(code)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Headers", "X-Ops-Token, Content-Type")
        self.end_headers()
        self.wfile.write(body)

    def _authed(self) -> bool:
        token = os.environ.get("DCGO_OPS_TOKEN", "")
        return bool(token) and self.headers.get("X-Ops-Token") == token

    def do_OPTIONS(self):  # CORS preflight (opsd가 다른 포트일 때)
        self._send(204, b"")

    def do_GET(self):
        if not self._authed():
            return self._send(401, {"error": "token"})
        path = unquote(urlparse(self.path).path)
        try:
            if path == "/health":
                return self._send(200, {"ok": True, "worker_cap": config().get("worker_cap")})
            if path == "/config":
                return self._send(200, config())
            if path == "/cards":
                f = REPO / "src/HeadlessDCGO.Engine/Assets/CardBaseEntity/cards.json"
                return self._send(200, f.read_bytes())
            if path == "/cards-meta":
                return self._send(200, cards_meta())
            if path == "/recipes":
                # 학습 덱 레시피 라이브러리(런처 드롭다운용) — RL 덱은 파이프라인 소유(요구 §6.6.5)
                return self._send(200, sorted(p.name for p in (RL_DIR / "decks").glob("*.json")))
            if path == "/recipes/detail":
                details = [recipe_summary(p) for p in sorted((RL_DIR / "decks").glob("*.json"))]
                return self._send(200, details)
            if path == "/models/detail":
                return self._send(200, models_detail())
            if path == "/models":
                # 기존 정책 열거(이어 학습·평가 전용의 시작점) — 보관(논리삭제)분 제외
                hidden = hidden_models()
                models = []
                for run_dir in sorted(RUNS.iterdir()):
                    if not run_dir.is_dir():
                        continue
                    if (run_dir / "policy.zip").exists():
                        models.append(f"{run_dir.name}/policy.zip")
                    for ck in sorted((run_dir / "checkpoints").glob("step-*.zip")):
                        models.append(f"{run_dir.name}/checkpoints/{ck.name}")
                return self._send(200, [m for m in models if m not in hidden])
            if path == "/jobs":
                return self._send(200, {jid: job_status(j) for jid, j in _jobs.items()})
            if path == "/jobs/queue":
                return self._send(200, {"pending": _queue, "done": _queue_done[-20:]})
            if m := re.fullmatch(r"/jobs/([\w.-]+)/status", path):
                job = _jobs.get(m.group(1))
                return self._send(200, job_status(job)) if job else self._send(404, {"error": "no job"})
            if path == "/runs":
                runs = [run_summary(d) for d in sorted(RUNS.iterdir()) if d.is_dir()]
                return self._send(200, runs)
            if m := re.fullmatch(r"/arena/match/([\w.-]+)/turn", path):
                match = _arena.get(m.group(1))
                if match is None:
                    return self._send(404, {"error": "no match"})
                query = urlparse(self.path).query
                wait = float(re.search(r"wait=([\d.]+)", query).group(1)) if "wait=" in query else 25.0
                msg = match.next_message(min(wait, 55.0))
                return self._send(200, msg if msg is not None else {"type": "none"})
            # 런 폴더명은 공백·한글 포함 전 범위 허용([^/]+) — 문자class가 좁아 "퍼플 테스트-…" 판로그가
            # 404였다(실측 2026-08-01). 탈출은 각 지점의 resolve+prefix(is_relative_to) 검증으로 차단.
            if m := re.fullmatch(r"/runs/([^/]+)/meta", path):
                d = (RUNS / m.group(1)).resolve()
                if not d.is_relative_to(RUNS.resolve()) or not d.is_dir():
                    return self._send(404, {"error": "no run"})
                return self._send(200, run_summary(d))
            if m := re.fullmatch(r"/runs/([^/]+)/league", path):
                f = (RUNS / m.group(1) / "league.json").resolve()
                if not f.is_relative_to(RUNS.resolve()) or not f.exists():
                    return self._send(404, {"error": "league.json 없음"})
                state = json.loads(f.read_text(encoding="utf-8"))
                # 판수 단위 진행(사용자 요구 2026-07-31): 학습 중 라운드 폴더의 결과 라인 수 동봉.
                training = state.get("training")
                if state.get("status") == "running" and training and training.get("out"):
                    games = 0
                    for rf in Path(training["out"]).glob("results-env*.jsonl"):
                        try:
                            games += sum(1 for _ in open(rf, encoding="utf-8"))
                        except OSError:
                            pass
                    training["games"] = games
                return self._send(200, state)
            if path == "/leagues":
                # 시작 시각 내림차순 — 폴더명 알파벳순으로 주면 UI의 "최근 N개" 창이
                # 새 리그를 밀어냈다(실측 2026-08-01: league-Bt1_4가 대문자 B라 창 밖).
                out = []
                for d in sorted(RUNS.glob("league-*")):
                    if (d / "league.json").exists():
                        try:
                            s = json.loads((d / "league.json").read_text(encoding="utf-8"))
                            out.append({"run": d.name, "status": s.get("status"), "round": s.get("round"),
                                        "rounds": (s.get("config") or {}).get("rounds"),
                                        "started": s.get("started")})
                        except json.JSONDecodeError:
                            pass
                out.sort(key=lambda l: l.get("started") or "", reverse=True)
                return self._send(200, out)
            if m := re.fullmatch(r"/runs/((?:[^/]+/)*[^/]+)/matches/([\w.-]+\.jsonl\.gz)", path):
                # 중첩 런 경로 허용(리그: league-X/red/round-1, 평가: league-X/eval-matches/r1) —
                # 탈출은 resolve+prefix 검증으로 차단.
                f = (RUNS / m.group(1) / "matches" / m.group(2)).resolve()
                if not f.is_relative_to(RUNS.resolve()) or not f.exists():
                    return self._send(404, {"error": "no match log"})
                return self._send(200, f.read_bytes(), content_type="application/gzip")
            if m := re.fullmatch(r"/runs/([^/]+)/defects", path):
                # 결함 요약: stderr에서 삼킴/abort의 발생 지점(첫 AS-IS 프레임)별 집계 (결함 탭용).
                kinds: dict[str, int] = {}
                KNOWN = {"CardObjectController.AddTrashCard": "구(舊) 고아응답 사슬 유래 — 1a8bded6에서 근절, 재등장 시 신규 조사"}
                run_dir = (RUNS / m.group(1)).resolve()
                if not run_dir.is_relative_to(RUNS.resolve()):
                    return self._send(404, {"error": "no run"})
                lines: list[str] = []
                for f in sorted(run_dir.glob("host-stderr.log*")):
                    lines += f.read_text(encoding="utf-8", errors="replace").splitlines()
                if lines:
                    for i, ln in enumerate(lines):
                        if "[coroutine-exception]" in ln or "[abort]" in ln:
                            exc = ln.split("root=")[-1].split(":")[-1].strip() if "root=" in ln else ln.split("]")[-1].strip()[:40]
                            origin = ""
                            for nxt in lines[i + 1:i + 8]:
                                nxt = nxt.strip()
                                if nxt.startswith("at ") and "Headless" not in nxt and not nxt.startswith("at System."):
                                    origin = nxt[3:].split("(")[0]
                                    break
                            kind = ("[abort] " if "[abort]" in ln else "") + (origin or "?")
                            if origin in KNOWN:
                                kind += f" — {KNOWN[origin]}"
                            kinds[kind] = kinds.get(kind, 0) + 1
                return self._send(200, {"kinds": kinds})
            if m := re.fullmatch(r"/runs/([^/]+)/stderr", path):
                d = (RUNS / m.group(1)).resolve()
                if not d.is_relative_to(RUNS.resolve()):
                    return self._send(404, {"error": "no run"})
                blob = b"".join(f.read_bytes() for f in sorted(d.glob("host-stderr.log*")))
                return self._send(200, blob, content_type="text/plain; charset=utf-8")
            return self._send(404, {"error": "not found"})
        except OSError as ex:
            return self._send(500, {"error": str(ex)})

    def do_PUT(self):
        if not self._authed():
            return self._send(401, {"error": "token"})
        if urlparse(self.path).path == "/config":
            body = self.rfile.read(int(self.headers.get("Content-Length", 0)))
            data = json.loads(body)
            CONFIG_PATH.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
            return self._send(200, data)
        return self._send(404, {"error": "not found"})

    def do_POST(self):
        if not self._authed():
            return self._send(401, {"error": "token"})
        path = unquote(urlparse(self.path).path)
        body = self.rfile.read(int(self.headers.get("Content-Length", 0)) or 0)
        data = json.loads(body) if body else {}

        if path == "/jobs":
            return self._start_job(data)
        if path == "/jobs/batch":
            # 덱별 정책 배치(사용자 요구 2026-07-30): 잡 목록을 순차 큐에 적재 — 스케줄러가 하나씩.
            jobs = data.get("jobs") or []
            for entry in jobs:
                if entry.get("script") not in SCRIPTS:
                    return self._send(400, {"error": f"script whitelist: {list(SCRIPTS)}"})
            _queue.extend(jobs)
            return self._send(201, {"queued": len(jobs), "queue_len": len(_queue)})
        if path == "/jobs/queue/clear":
            cleared = len(_queue)
            _queue.clear()
            return self._send(200, {"cleared": cleared})
        if path == "/league":
            # 리그 기동(와이어프레임 확정): config를 runs/league-<name>/에 쓰고 league.py 잡으로 기동.
            # 시드 미지정 = 랜덤 발급 + config에 기록(재현은 기록된 시드 재입력으로 — 사용자 확정 2026-07-31).
            import random as _random
            if not data.get("seed"):
                data["seed"] = _random.randrange(1, 2 ** 31)
            name = re.sub(r"[^\w가-힣-]", "_", str(data.get("name") or time.strftime("league-%m%d-%H%M")))
            for combo in data.get("combos") or []:
                deck = (RL_DIR / "decks" / str(combo.get("deck", ""))).resolve()
                if not str(deck).startswith(str((RL_DIR / "decks").resolve())) or not deck.exists():
                    return self._send(400, {"error": f"없는 덱: {combo.get('deck')}"})
                if combo.get("init"):
                    model = (RUNS / str(combo["init"])).resolve()
                    if not str(model).startswith(str(RUNS.resolve())) or not model.exists():
                        return self._send(400, {"error": f"없는 시작 정책: {combo['init']}"})
                    combo["init"] = str(model)
            if len(data.get("combos") or []) < 2:
                return self._send(400, {"error": "조합이 최소 2개 필요"})
            # 리그 학습 워커도 러너 상한 강제(요구 §8.5) — league.py가 train.py에 직접 넘기므로
            # 여기서 막지 않으면 cap 우회가 된다.
            cap = int(config().get("worker_cap", 6))
            if int(data.get("n_envs", 4)) > cap:
                return self._send(400, {"error": f"worker_cap {cap} 초과 (n_envs={data.get('n_envs')})"})
            out_dir = RUNS / f"league-{name}"
            out_dir.mkdir(parents=True, exist_ok=True)
            # 주의: 이름을 config로 지으면 모듈 함수 config()를 함수 전체에서 가린다
            # (실측 2026-07-31: /arena/match 분기 UnboundLocalError) — league_config로.
            league_config = {k: data[k] for k in ("rounds", "games", "seed", "n_envs", "eval_pairs", "record_mode")
                             if k in data}
            league_config.update(name=name, combos=data["combos"])
            (out_dir / "config.json").write_text(json.dumps(league_config, ensure_ascii=False, indent=2),
                                                 encoding="utf-8")
            code, payload = launch_job({"script": "league", "args": {"config": f"league-{name}/config.json"}})
            return self._send(code, {**payload, "league": f"league-{name}"})
        if path == "/models/alias":
            rel = str(data.get("path", ""))
            target = (RUNS / rel).resolve()
            if not str(target).startswith(str(RUNS.resolve())) or not target.exists():
                return self._send(404, {"error": "없는 정책"})
            aliases = model_aliases()
            alias = str(data.get("alias", "")).strip()[:40]
            if alias:
                aliases[rel] = alias
            else:
                aliases.pop(rel, None)
            ALIASES_PATH.write_text(json.dumps(aliases, ensure_ascii=False, indent=2), encoding="utf-8")
            return self._send(200, {"path": rel, "alias": alias})
        if path == "/models/hide":
            # 논리삭제(보관, 사용자 확정 2026-07-31): 파일은 그대로, 목록·드롭다운에서만 제외.
            # hidden=false로 복원. 물리 삭제는 제공하지 않는다 — 디스크 정리는 별도 수동 작업.
            rel = str(data.get("path", ""))
            target = (RUNS / rel).resolve()
            if not str(target).startswith(str(RUNS.resolve())) or not target.exists():
                return self._send(404, {"error": "없는 정책"})
            hidden = hidden_models()
            if bool(data.get("hidden", True)):
                hidden.add(rel)
            else:
                hidden.discard(rel)
            save_hidden(hidden)
            return self._send(200, {"path": rel, "hidden": rel in hidden})
        if path == "/recipes":
            # 레시피 저장 — 항목은 canonical/표시형 어느 쪽도 허용, 검증 결과를 함께 반환(위반이어도 저장).
            from dcgo_rl.cards import canonical_card_number
            name = str(data.get("name") or "무제 덱").strip()[:60]
            fname = re.sub(r"[^\w가-힣-]", "_", data.get("file") or name) + ".json"
            target = (RL_DIR / "decks" / fname).resolve()
            if not str(target).startswith(str((RL_DIR / "decks").resolve())):
                return self._send(400, {"error": "잘못된 파일명"})
            def norm(entries):
                return [{"card": canonical_card_number(str(e["card"])), "count": int(e["count"])}
                        for e in (entries or []) if int(e.get("count", 0)) > 0]
            main, digitama = norm(data.get("main")), norm(data.get("digitama"))
            target.write_text(json.dumps({"name": name, "source": "operator", "main": main, "digitama": digitama},
                                         ensure_ascii=False, indent=2), encoding="utf-8")
            return self._send(200, recipe_summary(target))
        if path == "/recipes/parse":
            return self._send(200, parse_deck_text(str(data.get("text", "")), str(data.get("name", ""))))
        if m := re.fullmatch(r"/recipes/([\w가-힣.-]+\.json)/delete", path):
            target = (RL_DIR / "decks" / m.group(1)).resolve()
            if not str(target).startswith(str((RL_DIR / "decks").resolve())) or not target.exists():
                return self._send(404, {"error": "없는 레시피"})
            target.unlink()
            return self._send(200, {"deleted": m.group(1)})
        if path == "/arena/match":
            live = [a for a in _arena.values() if not a.done]
            cap = int(config().get("arena_cap", 2))
            if len(live) >= cap:
                return self._send(429, {"error": f"arena_cap {cap} — 진행 중 {len(live)}판"})
            match_id = data["matchId"]
            day_dir = RUNS / time.strftime("arena-%Y%m%d") / "matches"
            try:
                _arena[match_id] = ArenaMatch(match_id, int(data.get("seed", 0)), data["decks"],
                                              int(data.get("maxSteps", 2000)), day_dir)
            except (OSError, RuntimeError, KeyError) as ex:
                return self._send(500, {"error": str(ex)})
            return self._send(201, {"match": match_id, "run": day_dir.parent.name,
                                    "log": f"{day_dir.parent.name}/matches/{match_id}.jsonl.gz"})
        if m := re.fullmatch(r"/arena/match/([\w.-]+)/act", path):
            match = _arena.get(m.group(1))
            if match is None:
                return self._send(404, {"error": "no match"})
            match.act(int(data["seat"]), int(data["index"]))
            return self._send(200, {"ok": True})
        if m := re.fullmatch(r"/arena/match/([\w.-]+)/end", path):
            match = _arena.pop(m.group(1), None)
            if match is not None:
                match.close()
            return self._send(200, {"ok": True})
        if m := re.fullmatch(r"/jobs/([\w.-]+)/(stop|kill)", path):
            job = _jobs.get(m.group(1))
            if not job:
                return self._send(404, {"error": "no job"})
            sig = signal.SIGTERM if m.group(2) == "stop" else signal.SIGKILL
            try:
                if m.group(2) == "kill":
                    os.killpg(os.getpgid(job["proc"].pid), sig)   # 강제: 워커까지 그룹 사살
                else:
                    job["proc"].send_signal(sig)                   # graceful: 본체만 — 본체가 정리(M1)
            except ProcessLookupError:
                pass
            return self._send(200, {"sent": m.group(2)})
        return self._send(404, {"error": "not found"})

    def _start_job(self, data: dict):
        code, payload = launch_job(data)
        return self._send(code, payload)

    def log_message(self, fmt, *args):   # 조용히 — 접근 로그는 필요 시 확장
        pass


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8790)
    parser.add_argument("--bind", default="0.0.0.0")
    args = parser.parse_args()
    if not os.environ.get("DCGO_OPS_TOKEN"):
        raise SystemExit("DCGO_OPS_TOKEN 미설정 — 운영자 표면은 무인증 금지(요구 §7)")
    import threading
    threading.Thread(target=queue_scheduler, daemon=True).start()
    ThreadingHTTPServer((args.bind, args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()
