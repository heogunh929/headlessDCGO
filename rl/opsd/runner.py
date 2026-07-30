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
from urllib.parse import urlparse

RL_DIR = Path(__file__).resolve().parents[1]
REPO = RL_DIR.parent
RUNS = REPO / "runs"
CONFIG_PATH = RL_DIR / "opsd" / "runner-config.json"
PYTHON = str(RL_DIR / ".venv" / "bin" / "python")   # uv 래퍼 우회 — SIGTERM이 python에 직행 [M1 실측]

# 화이트리스트(요구 §8): 스크립트와 허용 인자·형만 통과. 임의 셸 불가.
SCRIPTS = {
    "train": {
        "file": "train.py",
        "args": {"steps": int, "games": int, "n_envs": int, "seed": int, "eval_matches": int, "vec": str,
                 "record_mode": str, "checkpoint_every": int, "checkpoint_keep": int, "out": str},
    },
}

DEFAULT_CONFIG = {"worker_cap": 6, "notes": "worker_cap: rl-workers-six 규약 + OOM 이력(2026-07-29) 기준값"}

_jobs: dict[str, dict] = {}   # job_id -> {proc, script, args, out, log, started}


def config() -> dict:
    if CONFIG_PATH.exists():
        return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
    return dict(DEFAULT_CONFIG)


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
        path = urlparse(self.path).path
        try:
            if path == "/health":
                return self._send(200, {"ok": True, "worker_cap": config().get("worker_cap")})
            if path == "/config":
                return self._send(200, config())
            if path == "/cards":
                f = REPO / "src/HeadlessDCGO.Engine/Assets/CardBaseEntity/cards.json"
                return self._send(200, f.read_bytes())
            if path == "/jobs":
                return self._send(200, {jid: job_status(j) for jid, j in _jobs.items()})
            if m := re.fullmatch(r"/jobs/([\w.-]+)/status", path):
                job = _jobs.get(m.group(1))
                return self._send(200, job_status(job)) if job else self._send(404, {"error": "no job"})
            if path == "/runs":
                runs = [run_summary(d) for d in sorted(RUNS.iterdir()) if d.is_dir()]
                return self._send(200, runs)
            if m := re.fullmatch(r"/runs/([\w.-]+)/meta", path):
                return self._send(200, run_summary(RUNS / m.group(1)))
            if m := re.fullmatch(r"/runs/([\w.-]+)/matches/([\w.-]+\.jsonl\.gz)", path):
                f = (RUNS / m.group(1) / "matches" / m.group(2)).resolve()
                if not str(f).startswith(str(RUNS.resolve())) or not f.exists():
                    return self._send(404, {"error": "no match log"})
                return self._send(200, f.read_bytes(), content_type="application/gzip")
            if m := re.fullmatch(r"/runs/([\w.-]+)/stderr", path):
                f = RUNS / m.group(1) / "host-stderr.log"
                return self._send(200, f.read_bytes() if f.exists() else b"", content_type="text/plain; charset=utf-8")
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
        path = urlparse(self.path).path
        body = self.rfile.read(int(self.headers.get("Content-Length", 0)) or 0)
        data = json.loads(body) if body else {}

        if path == "/jobs":
            return self._start_job(data)
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
        script = data.get("script")
        spec = SCRIPTS.get(script)
        if spec is None:
            return self._send(400, {"error": f"script whitelist: {list(SCRIPTS)}"})
        # 동시 1개 강제(요구 §4) — 학습 잡이 살아 있으면 거부.
        for job in _jobs.values():
            if job["proc"].poll() is None:
                return self._send(409, {"error": "job already running", "job": job["script"]})

        args: list[str] = []
        n_envs = None
        for key, value in (data.get("args") or {}).items():
            caster = spec["args"].get(key)
            if caster is None:
                return self._send(400, {"error": f"arg whitelist: {list(spec['args'])}"})
            value = caster(value)
            if key == "n_envs":
                n_envs = value
            args += [f"--{key.replace('_', '-')}", str(value)]

        cap = int(config().get("worker_cap", 6))
        if n_envs is not None and n_envs > cap:
            return self._send(400, {"error": f"worker_cap {cap} 초과 (n_envs={n_envs}) — 러너 설정이 강제"})

        job_id = time.strftime("job-%Y%m%d-%H%M%S")
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
        return self._send(201, {"job": job_id, "out": str(out_abs), "pid": proc.pid})

    def log_message(self, fmt, *args):   # 조용히 — 접근 로그는 필요 시 확장
        pass


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8790)
    parser.add_argument("--bind", default="0.0.0.0")
    args = parser.parse_args()
    if not os.environ.get("DCGO_OPS_TOKEN"):
        raise SystemExit("DCGO_OPS_TOKEN 미설정 — 운영자 표면은 무인증 금지(요구 §7)")
    ThreadingHTTPServer((args.bind, args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()
