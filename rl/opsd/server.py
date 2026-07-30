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
import re
import smtplib
import time
from email.mime.text import MIMEText
from pathlib import Path

from aiohttp import ClientSession, web

from . import arena, broker as broker_mod, db

HERE = Path(__file__).resolve().parent
STATIC = HERE / "static"
THRESHOLDS = HERE / "thresholds.json"   # M4에서 sqlite로 이관 예정(설계 §4)

_alerts: list[dict] = []
_mailed: set[str] = set()
_signup_hits: dict[str, list[float]] = {}   # 공유 표면 rate limit(요구 §7)
_cards_meta_cache: dict | None = None


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


# ==================== 아레나 (M4) ====================

async def cards_meta(app: web.Application) -> dict:
    """runner의 /cards-meta 캐시 — 덱 검증기 데이터원."""
    global _cards_meta_cache
    if _cards_meta_cache is None:
        async with ClientSession() as session:
            async with session.get(f"{app['runner']}/cards-meta",
                                   headers={"X-Ops-Token": token()}) as response:
                _cards_meta_cache = await response.json()
    return _cards_meta_cache


def arena_participant(request: web.Request):
    return arena.auth(request.headers.get("X-Arena-Key") or request.query.get("key"))


async def signup(request: web.Request) -> web.Response:
    """공유 표면 유일 쓰기(요구 §7) — IP당 분당 5회."""
    ip = request.remote or "?"
    window = [t for t in _signup_hits.get(ip, []) if t > time.time() - 60]
    if len(window) >= 5:
        return web.json_response({"error": "rate limit"}, status=429)
    _signup_hits[ip] = window + [time.time()]
    data = await request.json()
    return web.json_response(arena.signup(str(data.get("handle", ""))))


async def claim(request: web.Request) -> web.Response:
    """승인 후 키 수령(공유 표면, 신청 코드가 인증) — signup과 같은 rate limit."""
    ip = request.remote or "?"
    window = [t for t in _signup_hits.get(ip, []) if t > time.time() - 60]
    if len(window) >= 5:
        return web.json_response({"error": "rate limit"}, status=429)
    _signup_hits[ip] = window + [time.time()]
    data = await request.json()
    return web.json_response(arena.claim_key(str(data.get("handle", "")), str(data.get("claim", ""))))


async def rankings(request: web.Request) -> web.Response:
    return web.json_response({"season": db.active_season(), "rows": arena.rankings()})


async def arena_me(request: web.Request) -> web.Response:
    p = arena_participant(request)
    if p is None:
        return web.json_response({"error": "key"}, status=401)
    rating = arena.rating_row(p["id"], db.active_season())
    return web.json_response({"handle": p["handle"], "kind": p["kind"],
                              "elo": round(rating["elo"], 1), "games": rating["games"]})


async def arena_decks(request: web.Request) -> web.Response:
    p = arena_participant(request)
    if p is None:
        return web.json_response({"error": "key"}, status=401)
    if request.method == "GET":
        return web.json_response(arena.decks_of(p["id"]))
    deck = await request.json()
    return web.json_response(arena.register_deck(p["id"], deck, await cards_meta(request.app)))


async def arena_deck_activate(request: web.Request) -> web.Response:
    p = arena_participant(request)
    if p is None:
        return web.json_response({"error": "key"}, status=401)
    return web.json_response(arena.activate_deck(p["id"], int(request.match_info["deck_id"])))


async def arena_history(request: web.Request) -> web.Response:
    p = arena_participant(request)
    if p is None:
        return web.json_response({"error": "key"}, status=401)
    return web.json_response(arena.history_of(p["id"]))


async def arena_cards(request: web.Request) -> web.Response:
    """덱 빌더용 카드 사전(풀 표시 포함) — 키 인증."""
    if arena_participant(request) is None:
        return web.json_response({"error": "key"}, status=401)
    meta = await cards_meta(request.app)
    pool = json.loads(db.setting("card_pool"))
    return web.json_response({"cards": meta, "pool": pool})


async def arena_log(request: web.Request) -> web.Response:
    """본인 판 로그만(요구 §6.8: 본인+관리자) — runner에서 gz를 받아 전달."""
    p = arena_participant(request)
    if p is None:
        return web.json_response({"error": "key"}, status=401)
    match_id = request.match_info["match_id"]
    row = db.conn().execute("SELECT * FROM matches WHERE id=?", (match_id,)).fetchone()
    if row is None or p["id"] not in (row["p1"], row["p2"]):
        return web.json_response({"error": "본인 판이 아님"}, status=403)
    async with ClientSession() as session:
        async with session.get(f"{request.app['runner']}/runs/{row['log_run']}/matches/{match_id}.jsonl.gz",
                               headers={"X-Ops-Token": token()}) as upstream:
            if upstream.status != 200:
                return web.json_response({"error": "판 로그 준비 중/없음"}, status=404)
            return web.Response(body=await upstream.read(), content_type="application/gzip")


async def arena_admin(request: web.Request) -> web.Response:
    """관리자 표면(ops 토큰): 참가자 승인/차단, 설정(자동승인·카드 풀·타임아웃), 시즌 조회."""
    if not authed(request):
        return web.json_response({"error": "token"}, status=401)
    action = request.match_info["action"]

    if request.method == "GET" and action == "decks":
        # 아레나 덱 전체(관리자) — "아레나 덱에서 복사"(학습 레시피로 내려받기)용.
        rows = db.conn().execute(
            "SELECT d.id, d.name, d.cards_json, p.handle FROM decks d JOIN participants p ON p.id=d.owner"
            " ORDER BY d.id").fetchall()
        return web.json_response([{"id": r["id"], "name": r["name"], "owner": r["handle"],
                                   "cards": json.loads(r["cards_json"])} for r in rows])

    if request.method == "GET" and action == "overview":
        rows = db.conn().execute(
            "SELECT id, handle, kind, status, created, policy_path FROM participants ORDER BY id").fetchall()
        return web.json_response({
            "participants": [dict(r) for r in rows],
            "settings": {k: db.setting(k) for k in
                         ("auto_approve", "card_pool", "move_timeout_sec", "disconnect_grace_sec", "deck_limit_per_key")},
            "season": db.active_season(),
            "matches": db.conn().execute("SELECT COUNT(*) AS n FROM matches").fetchone()["n"],
        })

    data = await request.json()
    if action == "approve":
        return web.json_response(arena.approve(int(data["id"])))
    if action == "status":
        return web.json_response(arena.set_status(int(data["id"]), str(data["status"])))
    if action == "setting":
        db.set_setting(str(data["key"]), str(data["value"]))
        changed = {}
        if data["key"] == "card_pool":
            changed = arena.reaudit_pool(await cards_meta(request.app))
        return web.json_response({"ok": True, **changed})
    if action == "register-policy":
        return web.json_response(arena.register_policy_participant(str(data["handle"]),
                                                                   str(data.get("policyPath", ""))))
    if action == "bind-policy":
        db.conn().execute("UPDATE participants SET policy_path=? WHERE id=? AND kind='policy'",
                          (str(data.get("policyPath", "")), int(data["id"])))
        db.conn().commit()
        return web.json_response({"ok": True})
    return web.json_response({"error": "unknown action"}, status=404)


async def index(request: web.Request) -> web.Response:
    return static_page("index.html")


def static_page(name: str) -> web.FileResponse:
    # 모바일 브라우저 캐시로 구버전 UI가 남는 문제 방지 — 정적 페이지는 항상 재검증.
    return web.FileResponse(STATIC / name, headers={"Cache-Control": "no-cache"})


async def static_html(request: web.Request) -> web.Response:
    name = request.match_info["name"]
    if not re.fullmatch(r"[\w.-]+\.html", name):
        raise web.HTTPNotFound
    return static_page(name)


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
    app["broker"] = broker_mod.Broker(args.runner, token())
    app.router.add_get("/", index)
    app.router.add_get("/api/thresholds", get_thresholds)
    app.router.add_put("/api/thresholds", put_thresholds)
    app.router.add_get("/api/alerts", get_alerts)
    app.router.add_route("*", "/api/runner/{path:.*}", proxy)

    # 아레나 — 공유 표면(무인증 읽기 + 가입 신청)
    app.router.add_post("/api/arena/signup", signup)
    app.router.add_post("/api/arena/claim", claim)
    app.router.add_get("/api/arena/rankings", rankings)
    # 아레나 표면(API 키)
    app.router.add_get("/api/arena/me", arena_me)
    app.router.add_get("/api/arena/decks", arena_decks)
    app.router.add_post("/api/arena/decks", arena_decks)
    app.router.add_post("/api/arena/decks/{deck_id}/activate", arena_deck_activate)
    app.router.add_get("/api/arena/history", arena_history)
    app.router.add_get("/api/arena/cards", arena_cards)
    app.router.add_get("/api/arena/log/{match_id}", arena_log)
    app.router.add_get("/arena", lambda request: request.app["broker"].handle_ws(request))
    # 아레나 관리(운영자 토큰)
    app.router.add_route("*", "/api/arena/admin/{action}", arena_admin)

    app.router.add_get(r"/static/{name:[^/]+\.html}", static_html)
    app.router.add_static("/static", STATIC)
    app.on_startup.append(on_start)
    web.run_app(app, host=args.bind, port=args.port)


if __name__ == "__main__":
    main()
