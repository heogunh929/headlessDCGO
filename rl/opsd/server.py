"""opsd — 관리 툴 웹 서버 (설계 v1 §1). aiohttp: HTTP(대시보드) + (M4에서) 웹소켓 아레나.

M2 범위: 정적 대시보드 서빙 + runner 프록시 + 임계 규칙 저장/평가(배지·메일).
운영자 표면 — 모든 API가 X-Ops-Token 요구(브라우저는 최초 1회 입력, localStorage).

실행:  DCGO_OPS_TOKEN=<토큰> uv run python -m opsd.server [--port 8791] [--runner http://127.0.0.1:8790]
"""

from __future__ import annotations

import argparse
import asyncio
import json
import os
import smtplib
import time
from email.mime.text import MIMEText
from pathlib import Path

from aiohttp import ClientSession, web

HERE = Path(__file__).resolve().parent
STATIC = HERE / "static"
THRESHOLDS = HERE / "thresholds.json"   # M4에서 sqlite로 이관 예정(설계 §4)

_alerts: list[dict] = []
_mailed: set[str] = set()


def token() -> str:
    return os.environ.get("DCGO_OPS_TOKEN", "")


def authed(request: web.Request) -> bool:
    return bool(token()) and request.headers.get("X-Ops-Token") == token()


def thresholds() -> list[dict]:
    if THRESHOLDS.exists():
        return json.loads(THRESHOLDS.read_text(encoding="utf-8"))
    return [{"metric": "swallowed", "limit": 10, "action": "notify", "enabled": True}]


async def proxy(request: web.Request) -> web.Response:
    """runner API 패스스루 — 토큰은 브라우저 것을 그대로 전달(운영자 표면 단일 토큰)."""
    if not authed(request):
        return web.json_response({"error": "token"}, status=401)
    runner = request.app["runner"]
    path = request.match_info["path"]
    async with ClientSession() as session:
        async with session.request(
                request.method, f"{runner}/{path}",
                headers={"X-Ops-Token": request.headers.get("X-Ops-Token", "")},
                data=await request.read() or None) as upstream:
            body = await upstream.read()
            return web.Response(status=upstream.status, body=body,
                                content_type=upstream.content_type)


async def get_thresholds(request: web.Request) -> web.Response:
    if not authed(request):
        return web.json_response({"error": "token"}, status=401)
    return web.json_response(thresholds())


async def put_thresholds(request: web.Request) -> web.Response:
    if not authed(request):
        return web.json_response({"error": "token"}, status=401)
    data = await request.json()
    THRESHOLDS.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    return web.json_response(data)


async def get_alerts(request: web.Request) -> web.Response:
    if not authed(request):
        return web.json_response({"error": "token"}, status=401)
    return web.json_response(_alerts)


def send_mail(subject: str, body: str) -> None:
    """임계 알림 메일(요구 §7.5). SMTP 설정은 환경변수 — 미설정이면 조용히 화면 배지만."""
    host, to = os.environ.get("DCGO_SMTP_HOST"), os.environ.get("DCGO_ALERT_TO")
    if not host or not to:
        return
    msg = MIMEText(body, _charset="utf-8")
    msg["Subject"], msg["From"], msg["To"] = subject, os.environ.get("DCGO_SMTP_FROM", to), to
    with smtplib.SMTP(host, int(os.environ.get("DCGO_SMTP_PORT", "25")), timeout=10) as smtp:
        if os.environ.get("DCGO_SMTP_USER"):
            smtp.starttls()
            smtp.login(os.environ["DCGO_SMTP_USER"], os.environ["DCGO_SMTP_PASS"])
        smtp.send_message(msg)


async def watch(app: web.Application) -> None:
    """임계 평가 루프(설계 §6): 실행 중 잡의 stderr census를 30초 주기로 평가."""
    while True:
        try:
            async with ClientSession() as session:
                headers = {"X-Ops-Token": token()}
                async with session.get(f"{app['runner']}/jobs", headers=headers) as response:
                    jobs = await response.json()
                for job_id, status in jobs.items():
                    if not status.get("alive"):
                        continue
                    run = Path(status["meta"].get("config", {}).get("out", "")).name or None
                    if not run:
                        continue
                    async with session.get(f"{app['runner']}/runs/{run}/stderr", headers=headers) as response:
                        text = await response.text()
                    counts = {"swallowed": text.count("[coroutine-exception]"), "abort": text.count("[abort]")}
                    for rule in thresholds():
                        if not rule.get("enabled"):
                            continue
                        value = counts.get(rule["metric"], 0)
                        if value > rule["limit"]:
                            key = f"{job_id}:{rule['metric']}"
                            alert = {"ts": time.strftime("%H:%M:%S"), "job": job_id,
                                     "metric": rule["metric"], "value": value, "limit": rule["limit"],
                                     "action": rule.get("action", "notify")}
                            if key not in _mailed:
                                _mailed.add(key)
                                _alerts.append(alert)
                                try:
                                    send_mail(f"[DCGO] {rule['metric']} {value}>{rule['limit']}", json.dumps(alert))
                                except OSError:
                                    pass
                                if rule.get("action") == "notify+stop":
                                    await session.post(f"{app['runner']}/jobs/{job_id}/stop", headers=headers)
        except Exception:
            pass   # runner 다운은 대시보드가 health로 표시 — 루프는 계속
        await asyncio.sleep(30)


async def index(request: web.Request) -> web.Response:
    return web.FileResponse(STATIC / "index.html")


async def on_start(app: web.Application) -> None:
    app["watcher"] = asyncio.create_task(watch(app))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8791)
    parser.add_argument("--bind", default="0.0.0.0")
    parser.add_argument("--runner", default="http://127.0.0.1:8790")
    args = parser.parse_args()
    if not token():
        raise SystemExit("DCGO_OPS_TOKEN 미설정 — 운영자 표면은 무인증 금지(요구 §7)")

    app = web.Application()
    app["runner"] = args.runner
    app.router.add_get("/", index)
    app.router.add_get("/api/thresholds", get_thresholds)
    app.router.add_put("/api/thresholds", put_thresholds)
    app.router.add_get("/api/alerts", get_alerts)
    app.router.add_route("*", "/api/runner/{path:.*}", proxy)
    app.router.add_static("/static", STATIC)
    app.on_startup.append(on_start)
    web.run_app(app, host=args.bind, port=args.port)


if __name__ == "__main__":
    main()
