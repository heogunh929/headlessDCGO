"""아레나 DB — sqlite 단일 얇은 계층 (설계 v1 §4).

WAL 필수 + 접근은 이 모듈만 경유(다중 프로세스 규모가 오면 Postgres 이전 비용 최소화).
matches는 영구 — 어떤 보존 규칙도 이 테이블은 지우지 않는다(요구 §8.5: 이력 메타 영구).
시각은 전부 KST ISO8601(요구 §8.5).
"""

from __future__ import annotations

import json
import sqlite3
import threading
import time
from pathlib import Path

DB_PATH = Path(__file__).resolve().parent / "arena.db"

SCHEMA = """
CREATE TABLE IF NOT EXISTS participants(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  handle TEXT UNIQUE NOT NULL,
  key_hash TEXT UNIQUE,
  kind TEXT NOT NULL DEFAULT 'llm',            -- llm | policy (하우스 봇 투명 표기, 요구 §6.6.5)
  status TEXT NOT NULL DEFAULT 'pending',      -- pending | active | banned
  created TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS decks(
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  owner INTEGER NOT NULL REFERENCES participants(id),
  name TEXT NOT NULL,
  cards_json TEXT NOT NULL,                    -- {name, main:[{card,count}], digitama:[{card,count}]}
  active INTEGER NOT NULL DEFAULT 0,
  enabled INTEGER NOT NULL DEFAULT 1,          -- 풀 축소 시 비활성 표시(삭제 아님, 요구 §6.6 ④)
  disabled_reason TEXT NOT NULL DEFAULT '',
  created TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS matches(
  id TEXT PRIMARY KEY,
  season TEXT NOT NULL,
  p1 INTEGER NOT NULL, p2 INTEGER NOT NULL,
  deck1_json TEXT NOT NULL, deck2_json TEXT NOT NULL,   -- 판 시점 스냅샷 고정(요구 §6.6 ③)
  winner INTEGER,                              -- 1|2|NULL(무승부)
  reason TEXT NOT NULL,
  rating_delta_json TEXT NOT NULL DEFAULT '{}',
  log_run TEXT NOT NULL DEFAULT '',            -- runs/ 하위 런 이름 (arena-YYYYMMDD)
  log_path TEXT NOT NULL DEFAULT '',
  ts TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS ratings(
  participant INTEGER NOT NULL, season TEXT NOT NULL,
  elo REAL NOT NULL DEFAULT 1000, games INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY(participant, season)
);
CREATE TABLE IF NOT EXISTS seasons(id TEXT PRIMARY KEY, name TEXT NOT NULL, state TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);
"""

DEFAULT_SETTINGS = {
    "elo_base": "1000",
    "auto_approve": "0",
    "card_pool": json.dumps({"sets": ["ST1", "ST2", "ST3"], "cards": []}),   # 초기=검증된 풀(요구 §6.7)
    "move_timeout_sec": "60",
    "disconnect_grace_sec": "30",
    "deck_limit_per_key": "10",
}

_local = threading.local()


def now() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%S%z")


def conn() -> sqlite3.Connection:
    c = getattr(_local, "conn", None)
    if c is None:
        c = sqlite3.connect(DB_PATH)
        c.row_factory = sqlite3.Row
        c.execute("PRAGMA journal_mode=WAL")
        c.execute("PRAGMA foreign_keys=ON")
        c.executescript(SCHEMA)
        # 마이그레이션: 신청 코드(claim) — 승인 후 신청자 본인이 키를 수령하는 인증 수단
        # (2026-07-30 사용자 지적: 키가 관리자에게만 보이고 신청자는 못 받는 구조 교정).
        if "claim_hash" not in [r["name"] for r in c.execute("PRAGMA table_info(participants)")]:
            c.execute("ALTER TABLE participants ADD COLUMN claim_hash TEXT")
        # 마이그레이션: Elo 기준 1000(사용자 확정 2026-07-30) — 구 DB(기준 1200)는 일괄 -200 이동.
        if c.execute("SELECT 1 FROM settings WHERE key='elo_base'").fetchone() is None:
            c.execute("UPDATE ratings SET elo = elo - 200")
        for key, value in DEFAULT_SETTINGS.items():
            c.execute("INSERT OR IGNORE INTO settings(key, value) VALUES(?, ?)", (key, value))
        c.execute("INSERT OR IGNORE INTO seasons(id, name, state) VALUES('S1', 'Season 1', 'active')")
        c.commit()
        _local.conn = c
    return c


def setting(key: str) -> str:
    row = conn().execute("SELECT value FROM settings WHERE key=?", (key,)).fetchone()
    return row["value"] if row else DEFAULT_SETTINGS.get(key, "")


def set_setting(key: str, value: str) -> None:
    conn().execute("INSERT INTO settings(key, value) VALUES(?, ?) ON CONFLICT(key) DO UPDATE SET value=excluded.value",
                   (key, value))
    conn().commit()


def active_season() -> str:
    row = conn().execute("SELECT id FROM seasons WHERE state='active'").fetchone()
    return row["id"] if row else "S1"
