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

from aiohttp import ClientSession, ClientTimeout, web

from . import arena, broker as broker_mod, db

HERE = Path(__file__).resolve().parent
STATIC = HERE / "static"
THRESHOLDS = HERE / "thresholds.json"   # M4에서 sqlite로 이관 예정(설계 §4)

_STARTED = time.time()        # 가동 시각(현황 탭 2026-08-01)
_runs_size: tuple[float, float] = (0.0, 0.0)   # (계산 시각, GB) — 60초 캐시
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
    # 참가자 대시보드 홈 탭(2026-08-01): 실시간 상태 동봉 — 데몬 접속/검증 통과/활성 덱/열린 방
    broker = request.app["broker"]
    active = db.conn().execute(
        "SELECT name FROM decks WHERE owner=? AND active=1 AND enabled=1", (p["id"],)).fetchone()
    room = next((code for code, r in broker.rooms.items() if r["pid"] == p["id"]), None)
    return web.json_response({"handle": p["handle"], "kind": p["kind"],
                              "lang": p["lang"] if "lang" in p.keys() else "ko",
                              "rating": round(rating["rating"]), "rd": round(rating["rd"]),
                              "games": rating["games"],
                              "verified": bool(p["verified"]),
                              "connected": p["id"] in broker.connections,
                              "inMatch": p["id"] in broker.by_participant,
                              "activeDeck": active["name"] if active else None,
                              "room": room})


async def arena_decks(request: web.Request) -> web.Response:
    p = arena_participant(request)
    if p is None:
        return web.json_response({"error": "key"}, status=401)
    if request.method == "GET":
        return web.json_response(arena.decks_of(p["id"]))
    deck = await request.json()
    return web.json_response(arena.register_deck(p["id"], deck, await cards_meta(request.app)))


async def arena_deck_parse(request: web.Request) -> web.Response:
    """클립보드 덱 코드 → 구성(참가자 표면) — AS-IS DeckCodeUtility 전사 파서."""
    if arena_participant(request) is None:
        return web.json_response({"error": "key"}, status=401)
    from opsd.deckcode import parse_clipboard
    data = await request.json()
    return web.json_response(parse_clipboard(str(data.get("text", "")), await cards_meta(request.app)))


async def arena_deck_update(request: web.Request) -> web.Response:
    p = arena_participant(request)
    if p is None:
        return web.json_response({"error": "key"}, status=401)
    deck = await request.json()
    return web.json_response(arena.update_deck(p["id"], int(request.match_info["deck_id"]),
                                               deck, await cards_meta(request.app)))


async def arena_deck_delete(request: web.Request) -> web.Response:
    p = arena_participant(request)
    if p is None:
        return web.json_response({"error": "key"}, status=401)
    return web.json_response(arena.delete_deck(p["id"], int(request.match_info["deck_id"])))


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
    ban = json.loads(db.setting("ban_list"))
    return web.json_response({"cards": meta, "pool": pool, "ban": ban})


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
        # 참가자 관리(사용자 지시 2026-08-01): Elo/판수/대전수 동봉 — 삭제 가능 여부(대전 0) 판단 근거
        rows = db.conn().execute(
            "SELECT p.id, p.handle, p.kind, p.status, p.created, p.policy_path, p.key_plain,"
            " ROUND(COALESCE(r.rating, 0)) AS rating, ROUND(COALESCE(r.rd, 0)) AS rd,"
            " COALESCE(r.games, 0) AS games,"
            " (SELECT COUNT(*) FROM matches m WHERE m.p1=p.id OR m.p2=p.id) AS matches"
            " FROM participants p LEFT JOIN ratings r ON r.participant=p.id AND r.season=?"
            " ORDER BY p.id", (db.active_season(),)).fetchall()
        # 실시간 현황(사용자 지시 2026-08-01): 접속/큐/대전/방 + PC 자원 + runner 잡 — 관리자 관측 확장
        brk = request.app["broker"]
        mem_total = mem_avail = 0
        try:
            for line in open("/proc/meminfo", encoding="ascii"):
                if line.startswith("MemTotal:"):
                    mem_total = int(line.split()[1]) // 1024
                elif line.startswith("MemAvailable:"):
                    mem_avail = int(line.split()[1]) // 1024
        except OSError:
            pass
        import shutil
        disk = shutil.disk_usage(HERE)
        status_counts = {r["status"]: r["n"] for r in db.conn().execute(
            "SELECT status, COUNT(*) AS n FROM participants GROUP BY status")}
        jobs_alive = jobs_queued = None
        jobs_detail: list[dict] = []
        runner_health: dict = {}
        try:
            async with ClientSession() as session:
                async with session.get(f"{request.app['runner']}/jobs",
                                       headers={"X-Ops-Token": token()},
                                       timeout=ClientTimeout(total=2)) as response:
                    jobs = await response.json()
                jobs_alive = sum(1 for j in jobs.values() if j.get("alive"))
                jobs_detail = [
                    {"id": jid, "script": j.get("script"),
                     "name": Path(str((j.get("args") or {}).get("out")
                                      or (j.get("args") or {}).get("config") or jid)).parent.name
                             if (j.get("args") or {}).get("config")
                             else Path(str((j.get("args") or {}).get("out") or jid)).name,
                     "steps": j.get("progress_steps"), "matches": j.get("matches_played"),
                     "rssMb": j.get("rss_mb"), "started": j.get("started")}
                    for jid, j in jobs.items() if j.get("alive")]
                async with session.get(f"{request.app['runner']}/jobs/queue",
                                       headers={"X-Ops-Token": token()},
                                       timeout=ClientTimeout(total=2)) as response:
                    jobs_queued = len((await response.json()).get("pending", []))
                async with session.get(f"{request.app['runner']}/health",
                                       headers={"X-Ops-Token": token()},
                                       timeout=ClientTimeout(total=2)) as response:
                    runner_health = await response.json()
        except Exception:
            pass   # runner 다운 = 현황에 '연결 안 됨'으로 표시
        # runs/ 판로그 누적 용량 — 60초 캐시(파일 수천 개 워크는 매 폴링마다 돌리지 않는다)
        global _runs_size
        if time.time() - _runs_size[0] > 60:
            total_bytes = 0
            for p in (HERE.parent.parent / "runs").rglob("*"):
                try:
                    if p.is_file():
                        total_bytes += p.stat().st_size
                except OSError:
                    pass
            _runs_size = (time.time(), round(total_bytes / 1024 ** 3, 2))
        # 활성 대전 목록(스톨 감지) + 최근 종료 피드(시작·종료 시각 포함, 사용자 요구 2026-08-01)
        active_list = [{"id": m.id, "p1": m.seats[1].participant["handle"],
                        "p2": m.seats[2].participant["handle"],
                        "practice": m.practice, "verification": m.verification,
                        "elapsedSec": round(time.time() - m.started), "step": m.step}
                       for m in brk.matches.values()]
        feed = [dict(r) for r in db.conn().execute(
            "SELECT m.id, m.winner, m.reason, m.started_ts, m.ts, m.verification, m.practice,"
            " m.log_run, m.log_path, pa.handle AS h1, pb.handle AS h2 FROM matches m"
            " JOIN participants pa ON pa.id=m.p1 JOIN participants pb ON pb.id=m.p2"
            " ORDER BY m.ts DESC LIMIT 5")]
        quality = db.conn().execute(
            "SELECT COUNT(*) AS n, SUM(reason='timeout') AS timeouts,"
            " AVG(CASE WHEN started_ts != '' THEN (julianday(ts) - julianday(started_ts)) * 86400 END) AS avgSec"
            " FROM matches WHERE ts LIKE ?", (time.strftime("%Y-%m-%d") + "%",)).fetchone()
        return web.json_response({
            "participants": [dict(r) for r in rows],
            "settings": {k: db.setting(k) for k in
                         ("auto_approve", "card_pool", "ban_list", "move_timeout_sec", "disconnect_grace_sec", "deck_limit_per_key")},
            "season": db.active_season(),
            "matches": db.conn().execute("SELECT COUNT(*) AS n FROM matches").fetchone()["n"],
            "matchesToday": db.conn().execute("SELECT COUNT(*) AS n FROM matches WHERE ts LIKE ?",
                                              (time.strftime("%Y-%m-%d") + "%",)).fetchone()["n"],
            "live": {"connected": len(brk.connections), "queued": len(brk.queue),
                     "activeMatches": len(brk.matches), "openRooms": len(brk.rooms),
                     "statusCounts": status_counts, "activeList": active_list},
            "system": {"load": list(os.getloadavg()), "cpus": os.cpu_count(),
                       "memUsedMB": max(0, mem_total - mem_avail), "memTotalMB": mem_total,
                       "diskFreeGB": round(disk.free / 1024 ** 3, 1),
                       "diskTotalGB": round(disk.total / 1024 ** 3, 1),
                       "runsSizeGB": _runs_size[1]},
            "runnerJobs": {"alive": jobs_alive, "queued": jobs_queued, "detail": jobs_detail},
            "alerts": {"count": len(_alerts), "recent": _alerts[-3:]},
            "versions": {"engineSha": runner_health.get("engineSha"),
                         "runnerStarted": runner_health.get("started"),
                         "opsdStarted": _STARTED},
            "feed": feed,
            "quality": {"today": quality["n"], "timeouts": quality["timeouts"] or 0,
                        "avgSec": round(quality["avgSec"]) if quality["avgSec"] else None},
        })

    data = await request.json()
    if action == "approve":
        return web.json_response(arena.approve(int(data["id"])))
    if action == "status":
        return web.json_response(arena.set_status(int(data["id"]), str(data["status"])))
    if action == "delete":
        return web.json_response(arena.delete_participant(int(data["id"]), force=bool(data.get("force"))))
    if action == "rekey":
        return web.json_response(arena.rekey(int(data["id"])))
    if action == "setting":
        db.set_setting(str(data["key"]), str(data["value"]))
        changed = {}
        if data["key"] in ("card_pool", "ban_list"):   # 금지/제한 변경도 전 덱 재감사
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


async def set_lang(request: web.Request) -> web.Response:
    """참가자 카드명 언어 설정(사용자 확정 2026-08-01) — SDK/스킬 상태 서술의 카드명이 이 값을 따른다."""
    p = arena_participant(request)
    if p is None:
        return web.json_response({"error": "key"}, status=401)
    lang = str((await request.json()).get("lang", "")).lower()
    if lang not in ("ko", "en"):
        return web.json_response({"error": "lang은 ko 또는 en"}, status=400)
    db.conn().execute("UPDATE participants SET lang=? WHERE id=?", (lang, p["id"]))
    db.conn().commit()
    return web.json_response({"ok": True, "lang": lang})


async def upload_app_exe(request: web.Request) -> web.Response:
    """관리자 exe 업로드(사용자 확정 2026-08-01: 윈도우 데스크탑 앱 exe 배포) — 운영 PC에서
    build_app.bat로 빌드한 DCGOArenaApp.exe를 받아 static/에 배치, 참가자 페이지가 직링크 노출."""
    if not authed(request):
        return web.json_response({"error": "token"}, status=401)
    body = await request.read()
    if len(body) < 1024 * 100 or body[:2] != b"MZ":   # PE 매직 — exe 아닌 파일 배포 방지
        return web.json_response({"error": "exe 파일이 아니거나 너무 작습니다"}, status=400)
    target = STATIC / "DCGOArenaApp.exe"
    tmp = STATIC / "DCGOArenaApp.exe.tmp"
    tmp.write_bytes(body)
    tmp.replace(target)   # 원자 교체 — 다운로드 중 잘린 파일 방지
    return web.json_response({"ok": True, "size": len(body)})


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


def build_single_file_sdk() -> None:
    """dcgo_arena 패키지 → static/dcgo_arena.py 단일 파일(참가자 배포, TextArena식 온보딩).

    참가자는 저장소 없이 `curl -O <서버>/static/dcgo_arena.py` 한 번으로 SDK를 받는다.
    기법: 모듈 소스를 문자열로 내장하고 로드 시 sys.modules에 실제 패키지로 조립 —
    `import dcgo_arena as da` 와 CLI(`python dcgo_arena.py play|daemon`) 둘 다 동작."""
    package_dir = HERE.parent / "dcgo_arena"
    order = ["state_text", "policies", "client", "config", "agents", "highlevel", "seatbridge", "app", "__main__"]
    sources = {name: (package_dir / f"{name}.py").read_text(encoding="utf-8") for name in order}
    init_source = (package_dir / "__init__.py").read_text(encoding="utf-8")

    out = [
        "# dcgo_arena.py — DCGO 아레나 참가 SDK 단일 파일 (서버 자동 생성 — 편집 금지)",
        "# 사용:  import dcgo_arena as da  /  python dcgo_arena.py daemon --config dcgo-arena.toml",
        "import sys, types",
        "_pkg = types.ModuleType('dcgo_arena'); _pkg.__path__ = []; sys.modules['dcgo_arena'] = _pkg",
        f"_SOURCES = {{'__init__': {init_source!r},",
    ]
    for name in order:
        out.append(f"  {name!r}: {sources[name]!r},")
    out += [
        "}",
        "for _name in %r:" % order,
        "    _mod = types.ModuleType(f'dcgo_arena.{_name}')",
        "    _mod.__package__ = 'dcgo_arena'",
        "    sys.modules[f'dcgo_arena.{_name}'] = _mod",
        "    exec(compile(_SOURCES[_name], f'dcgo_arena/{_name}.py', 'exec'), _mod.__dict__)",
        "exec(compile(_SOURCES['__init__'], 'dcgo_arena/__init__.py', 'exec'), _pkg.__dict__)",
    ]
    tail_sdk = [
        "if __name__ == '__main__':",
        "    sys.modules['dcgo_arena.__main__'].main()",
        "",
    ]
    # 데스크탑 앱(사용자 확정 2026-08-01): 같은 내장 SDK + 진입점만 GUI(app.main)
    tail_app = [
        "if __name__ == '__main__':",
        "    sys.modules['dcgo_arena.app'].main()",
        "",
    ]
    (STATIC / "dcgo_arena.py").write_text("\n".join(out + tail_sdk), encoding="utf-8")
    app_head = out[0:1] + [
        "# dcgo_arena_app.py — DCGO 아레나 데스크탑 참가 앱 (서버 자동 생성 — 편집 금지)",
        "# 사용:  pip install aiohttp  →  python dcgo_arena_app.py  (tkinter GUI)",
    ] + out[1:]
    app_text = "\n".join(app_head + tail_app)
    (STATIC / "dcgo_arena_app.py").write_text(app_text, encoding="utf-8")
    # 윈도우 기준(사용자 확정 2026-08-01): .pyw = 더블클릭 시 콘솔 창 없이 GUI만(pythonw 연결)
    (STATIC / "dcgo_arena_app.pyw").write_text(app_text, encoding="utf-8")
    # exe 빌드 킷(운영 PC 윈도우에서 1회 실행 → dist\DCGOArenaApp.exe → 관리자 페이지 업로드).
    # PyInstaller는 크로스 컴파일 불가 — 서버(리눅스)가 직접 exe를 만들 수 없다.
    bat = "\r\n".join([
        "@echo off",
        "chcp 65001 >nul",
        "if not exist dcgo_arena_app.py (",
        "  echo dcgo_arena_app.py 가 같은 폴더에 없습니다 — 참가자 페이지 커맨드로 먼저 받으세요.",
        "  pause",
        "  exit /b 1",
        ")",
        "py -m pip install --upgrade pyinstaller aiohttp || goto :err",
        "py -m PyInstaller --onefile --windowed --name DCGOArenaApp dcgo_arena_app.py || goto :err",
        "echo.",
        "echo 완료: dist\\DCGOArenaApp.exe",
        "echo 관리자 페이지(아레나 관리 탭)에서 이 exe를 업로드하면 참가자에게 배포됩니다.",
        "pause",
        "exit /b 0",
        ":err",
        "echo 빌드 실패 — 위 메시지를 확인하세요.",
        "pause",
        "exit /b 1",
        "",
    ])
    (STATIC / "build_app.bat").write_text(bat, encoding="utf-8")
    # 통합 스킬 배포(사용자 확정 2026-08-01: /digimonAiArena Api|Deck|Play|Plan) — 에이전트 앱 설치용
    skill_src = HERE.parent.parent / "skills" / "digimonAiArena" / "SKILL.md"
    if skill_src.exists():
        (STATIC / "digimonAiArena-SKILL.md").write_text(
            skill_src.read_text(encoding="utf-8"), encoding="utf-8")


async def on_start(app: web.Application) -> None:
    try:
        build_single_file_sdk()
    except OSError as ex:
        print(f"단일 파일 SDK 생성 실패(참가자 다운로드 불가): {ex}")
    app["watcher"] = asyncio.create_task(watch(app))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8791)
    parser.add_argument("--bind", default="0.0.0.0")
    parser.add_argument("--runner", default="http://127.0.0.1:8790")
    args = parser.parse_args()
    if not token():
        raise SystemExit("DCGO_OPS_TOKEN 미설정 — 운영자 표면은 무인증 금지(요구 §7)")

    app = web.Application(client_max_size=80 * 1024 ** 2)   # exe 업로드(관리자) — 기본 1MB로는 불가
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
    app.router.add_post("/api/arena/lang", set_lang)
    app.router.add_get("/api/arena/decks", arena_decks)
    app.router.add_post("/api/arena/decks", arena_decks)
    app.router.add_post("/api/arena/decks/parse", arena_deck_parse)
    app.router.add_put("/api/arena/decks/{deck_id}", arena_deck_update)
    app.router.add_post("/api/arena/decks/{deck_id}/delete", arena_deck_delete)
    app.router.add_post("/api/arena/decks/{deck_id}/activate", arena_deck_activate)
    app.router.add_get("/api/arena/history", arena_history)
    app.router.add_get("/api/arena/cards", arena_cards)
    app.router.add_get("/api/arena/log/{match_id}", arena_log)
    app.router.add_get("/arena", lambda request: request.app["broker"].handle_ws(request))
    # 아레나 관리(운영자 토큰)
    app.router.add_post("/api/arena/admin/upload-app", upload_app_exe)
    app.router.add_route("*", "/api/arena/admin/{action}", arena_admin)

    app.router.add_get(r"/static/{name:[^/]+\.html}", static_html)
    app.router.add_static("/static", STATIC)
    app.on_startup.append(on_start)
    web.run_app(app, host=args.bind, port=args.port)


if __name__ == "__main__":
    main()
