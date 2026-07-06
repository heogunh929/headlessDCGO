"""RL 학습환경 로컬 대시보드 서버 — stdlib 전용 (http.server + sqlite3 + subprocess).

탭 3개를 API로 받친다:
  대시보드  GET /api/runs · /api/league · /api/results
  리플레이  GET /api/policies · POST /api/replay  (replay.py 서브프로세스, --json)
  런처     POST /api/train/start·stop · GET /api/train/status  (train*.py 서브프로세스)

보안 전제: 127.0.0.1 바인드(로컬 전용). 경로 인자는 전부 runs/ 아래로 강제(트래버설 차단),
런처는 스크립트 화이트리스트 + 숫자 인자만 통과.

실행:  cd rl && .venv/bin/python dashboard/server.py [--port 8787]
"""

from __future__ import annotations

import argparse
import json
import os
import signal
import sqlite3
import subprocess
import sys
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse

RL_DIR = Path(__file__).resolve().parents[1]
REPO = RL_DIR.parent
RUNS = REPO / "runs"
STATIC = Path(__file__).resolve().parent / "static"
PYTHON = str(RL_DIR / ".venv" / "bin" / "python")

TRAIN_SCRIPTS = {"train": "train.py", "train_league": "train_league.py"}
# 런처 폼 → CLI 인자 화이트리스트 (스크립트별 허용 키와 형 변환)
TRAIN_ARG_SPEC = {
    "train": {"steps": int, "n_envs": int, "seed": int, "eval_matches": int, "vec": str, "log_level": str},
    "train_league": {"steps": int, "n_envs": int, "seed": int, "freeze_every": int,
                     "weakness_min_games": int, "init": str, "log_level": str},
}
LOG_LEVELS = ("OFF", "RESULT", "REPLAY", "ANALYSIS", "TRACE")

_jobs: dict[int, dict] = {}  # pid -> {name, script, proc, log, out}


def safe_run_path(name: str) -> Path:
    path = (RUNS / name).resolve()
    if not str(path).startswith(str(RUNS.resolve())):
        raise ValueError(f"run path escapes runs/: {name}")
    return path


def safe_policy_path(raw: str) -> str:
    if raw == "random":
        return raw
    path = Path(raw).resolve()
    if not str(path).startswith(str(RUNS.resolve())) or path.suffix != ".zip" or not path.exists():
        raise ValueError(f"policy path must be an existing .zip under runs/: {raw}")
    return str(path)


# --- 데이터 수집 -----------------------------------------------------------------


def list_runs() -> list[dict]:
    runs = []
    if not RUNS.exists():
        return runs
    for directory in sorted(RUNS.iterdir()):
        if not directory.is_dir():
            continue
        entry: dict = {"name": directory.name}
        if (directory / "league_log.jsonl").exists():
            entry["kind"] = "league"
        elif (directory / "meta.json").exists():
            entry["kind"] = "l0"
        else:
            entry["kind"] = "other"
        meta_path = directory / "meta.json"
        if meta_path.exists():
            entry["meta"] = json.loads(meta_path.read_text(encoding="utf-8"))
        runs.append(entry)
    return runs


def league_summary(run: str, max_points: int = 1500) -> dict:
    root = safe_run_path(run)
    summary: dict = {"run": run}

    log_path = root / "league_log.jsonl"
    entries = []
    if log_path.exists():
        with open(log_path, encoding="utf-8") as f:
            entries = [json.loads(line) for line in f if line.strip()]
    stride = max(1, len(entries) // max_points)
    summary["curve"] = entries[::stride]
    summary["matches"] = len(entries)
    modes: dict[str, int] = {}
    for entry in entries:
        modes[entry["mode"]] = modes.get(entry["mode"], 0) + 1
    summary["modes"] = modes

    ratings_path = root / "ratings.json"
    summary["ratings"] = json.loads(ratings_path.read_text()) if ratings_path.exists() else {}

    summary["snapshots"] = [
        json.loads(p.read_text(encoding="utf-8")) for p in sorted(root.glob("snapshots/*/*/meta.json"))
    ]

    matrix_path = root / "matchup.sqlite"
    rows = []
    if matrix_path.exists():
        conn = sqlite3.connect(str(matrix_path))
        rows = [
            {"a": a, "b": b, "wins": w, "losses": l, "draws": d}
            for a, b, w, l, d in conn.execute("SELECT a, b, wins, losses, draws FROM matchup")
        ]
        conn.close()
    summary["matchup"] = rows
    return summary


def recent_results(run: str, limit: int = 200) -> list[dict]:
    root = safe_run_path(run)
    rows: list[dict] = []
    for path in sorted(root.glob("results-env*.jsonl")):
        with open(path, encoding="utf-8") as f:
            rows.extend(json.loads(line) for line in f if line.strip())
    return rows[-limit:]


def list_policies() -> list[dict]:
    if not RUNS.exists():
        return []
    found = []
    for path in sorted(RUNS.rglob("*.zip")):
        if path.name.endswith(".zip") and path.stat().st_size > 0:
            found.append({"path": str(path), "label": str(path.relative_to(RUNS))})
    return found


# --- 런처 --------------------------------------------------------------------------


def start_training(payload: dict) -> dict:
    script_key = payload.get("script")
    if script_key not in TRAIN_SCRIPTS:
        raise ValueError(f"unknown script: {script_key}")
    name = str(payload.get("name") or f"{script_key}-run")
    if any(c in name for c in "/\\.."):
        raise ValueError("run name must be a plain directory name")

    out_dir = safe_run_path(name)
    out_dir.mkdir(parents=True, exist_ok=True)

    cmd = [PYTHON, str(RL_DIR / TRAIN_SCRIPTS[script_key]), "--out", str(out_dir)]
    spec = TRAIN_ARG_SPEC[script_key]
    for key, caster in spec.items():
        if key in payload and payload[key] not in (None, ""):
            value = caster(payload[key])
            if key == "init" and value != "fresh":
                value = safe_policy_path(str(value))
            if key == "vec" and value not in ("dummy", "subproc"):
                raise ValueError("vec must be dummy|subproc")
            if key == "log_level":
                value = str(value).upper()
                if value not in LOG_LEVELS:
                    raise ValueError(f"log_level must be one of {'|'.join(LOG_LEVELS)}")
                if value == "OFF":
                    continue  # default; don't pass the flag
            cmd += [f"--{key.replace('_', '-')}", str(value)]

    log_path = out_dir / "train.log"
    log_file = open(log_path, "a", encoding="utf-8")
    env = dict(os.environ)
    env["PATH"] = f"{REPO / '.dotnet'}:{env.get('PATH', '')}"
    proc = subprocess.Popen(
        cmd, stdout=log_file, stderr=subprocess.STDOUT,
        cwd=str(RL_DIR), env=env, start_new_session=True,
    )
    _jobs[proc.pid] = {"name": name, "script": script_key, "proc": proc,
                       "log": str(log_path), "cmd": " ".join(cmd)}
    return {"id": proc.pid, "name": name, "log": str(log_path)}


def stop_training(job_id: int) -> dict:
    job = _jobs.get(job_id)
    if job is None:
        raise ValueError(f"unknown job {job_id}")
    proc: subprocess.Popen = job["proc"]
    if proc.poll() is None:
        os.killpg(os.getpgid(proc.pid), signal.SIGTERM)  # 자식(브리지 호스트들)까지 종료
    return {"id": job_id, "stopped": True}


def training_status(tail_lines: int = 15) -> list[dict]:
    status = []
    for pid, job in list(_jobs.items()):
        proc: subprocess.Popen = job["proc"]
        tail = ""
        log_path = Path(job["log"])
        if log_path.exists():
            lines = log_path.read_text(encoding="utf-8", errors="replace").splitlines()
            tail = "\n".join(lines[-tail_lines:])
        status.append({
            "id": pid, "name": job["name"], "script": job["script"],
            "running": proc.poll() is None, "returncode": proc.returncode,
            "log": job["log"], "tail": tail, "cmd": job["cmd"],
        })
    return status


def run_replay(payload: dict) -> dict:
    p1 = safe_policy_path(str(payload.get("p1", "random")))
    p2 = safe_policy_path(str(payload.get("p2", "random")))
    seed = int(payload.get("seed", 1))
    cmd = [PYTHON, str(RL_DIR / "replay.py"), "--p1", p1, "--p2", p2, "--seed", str(seed), "--json"]
    if payload.get("stochastic"):
        cmd.append("--stochastic")
    env = dict(os.environ)
    env["PATH"] = f"{REPO / '.dotnet'}:{env.get('PATH', '')}"
    completed = subprocess.run(
        cmd, capture_output=True, text=True, cwd=str(RL_DIR), env=env, timeout=300,
    )
    if completed.returncode != 0:
        raise RuntimeError(f"replay failed: {completed.stderr[-800:]}")
    return json.loads(completed.stdout)


# --- HTTP -------------------------------------------------------------------------


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):  # 콘솔 소음 억제
        pass

    def _send_json(self, payload, status: int = 200) -> None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _send_file(self, path: Path, content_type: str) -> None:
        body = path.read_bytes()
        self.send_response(200)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:  # noqa: N802
        parsed = urlparse(self.path)
        query = {k: v[0] for k, v in parse_qs(parsed.query).items()}
        try:
            if parsed.path in ("/", "/index.html"):
                self._send_file(STATIC / "index.html", "text/html; charset=utf-8")
            elif parsed.path == "/api/runs":
                self._send_json(list_runs())
            elif parsed.path == "/api/league":
                self._send_json(league_summary(query["run"]))
            elif parsed.path == "/api/results":
                self._send_json(recent_results(query["run"], int(query.get("limit", "200"))))
            elif parsed.path == "/api/policies":
                self._send_json(list_policies())
            elif parsed.path == "/api/train/status":
                self._send_json(training_status())
            else:
                self._send_json({"error": "not found"}, 404)
        except Exception as ex:  # noqa: BLE001 — API 오류는 JSON으로 노출
            self._send_json({"error": str(ex)}, 400)

    def do_POST(self) -> None:  # noqa: N802
        length = int(self.headers.get("Content-Length", "0"))
        try:
            payload = json.loads(self.rfile.read(length) or b"{}")
            if self.path == "/api/replay":
                self._send_json(run_replay(payload))
            elif self.path == "/api/train/start":
                self._require_loopback()
                self._send_json(start_training(payload))
            elif self.path == "/api/train/stop":
                self._require_loopback()
                self._send_json(stop_training(int(payload["id"])))
            else:
                self._send_json({"error": "not found"}, 404)
        except PermissionError as ex:
            self._send_json({"error": str(ex)}, 403)
        except Exception as ex:  # noqa: BLE001
            self._send_json({"error": str(ex)}, 400)

    def _require_loopback(self) -> None:
        """런처(프로세스 시작/중지)는 조작면 — 내부망 공개(--host) 시에도 이 PC에서만 허용."""
        if self.client_address[0] not in ("127.0.0.1", "::1"):
            raise PermissionError("launcher is loopback-only; 대시보드/리플레이는 내부망에서 사용 가능")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8787)
    parser.add_argument("--host", default="127.0.0.1",
                        help="0.0.0.0 = 내부망 공개(조회/리플레이). 런처는 어느 경우든 루프백 전용.")
    args = parser.parse_args()

    server = ThreadingHTTPServer((args.host, args.port), Handler)
    scope = "로컬 전용" if args.host == "127.0.0.1" else f"내부망 공개({args.host}) — 런처는 루프백 전용"
    print(f"dashboard: http://{args.host}:{args.port}  (runs dir: {RUNS}) [{scope}]")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        for pid in list(_jobs):
            try:
                stop_training(pid)
            except Exception:  # noqa: BLE001
                pass
        server.server_close()
        sys.exit(0)


if __name__ == "__main__":
    main()
