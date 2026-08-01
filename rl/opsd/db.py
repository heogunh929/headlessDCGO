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
  -- Glicko-2(사용자 확정 2026-08-01, 이연 해제): 표준 시작값 1500 / RD 350 / 변동성 0.06
  rating REAL NOT NULL DEFAULT 1500, rd REAL NOT NULL DEFAULT 350, vol REAL NOT NULL DEFAULT 0.06,
  games INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY(participant, season)
);
CREATE TABLE IF NOT EXISTS seasons(id TEXT PRIMARY KEY, name TEXT NOT NULL, state TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);
"""

DEFAULT_SETTINGS = {
    "auto_approve": "0",
    "card_pool": json.dumps({"sets": ["ST1", "ST2", "ST3"], "cards": []}),   # 초기=검증된 풀(요구 §6.7)
    # 금지/제한/금지페어 — AS-IS DeckBuildingRule.cs 의미론(limit 0=금지, 1=제한 1장, 페어=양방향 공존 금지)
    "ban_list": json.dumps({"restrictions": [], "banned_pairs": []}),
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
        cols = [r["name"] for r in c.execute("PRAGMA table_info(participants)")]
        if "claim_hash" not in cols:
            c.execute("ALTER TABLE participants ADD COLUMN claim_hash TEXT")
        # 정책 결속(정책 관리 탭 2026-07-30): policy 참가자 ↔ 정책 zip 경로(runs/ 상대) —
        # 하우스 봇 러너(M5)의 전제. 지금은 결속 저장·표시까지.
        if "policy_path" not in cols:
            c.execute("ALTER TABLE participants ADD COLUMN policy_path TEXT NOT NULL DEFAULT ''")
        # 검증 에피소드(온보딩 ③, 2026-07-31): 하우스 봇과 비랭킹 1판 통과 후 래더 진입.
        # policy 참가자는 파이프라인 산물이라 자동 검증 통과.
        if "verified" not in cols:
            c.execute("ALTER TABLE participants ADD COLUMN verified INTEGER NOT NULL DEFAULT 0")
            c.execute("UPDATE participants SET verified=1 WHERE kind='policy'")
        # 키 평문 보관(사용자 확정 2026-08-01: "관리자도 키를 볼 수 있게 — 개인정보 없음"):
        # 인증은 여전히 해시 경로, 평문은 관리자 표시·재안내용. 외부 공개 시 재검토 대상.
        if "key_plain" not in cols:
            c.execute("ALTER TABLE participants ADD COLUMN key_plain TEXT NOT NULL DEFAULT ''")
        # 카드명 언어(사용자 확정 2026-08-01): API 응답·상태 서술의 카드명을 ko/en으로 — 기본 한글
        if "lang" not in cols:
            c.execute("ALTER TABLE participants ADD COLUMN lang TEXT NOT NULL DEFAULT 'ko'")
        # 검증판 이력 노출(사용자 지시 2026-08-01): 검증 에피소드도 이력에 [검증] 배지로 보이게
        mcols = [r["name"] for r in c.execute("PRAGMA table_info(matches)")]
        if "verification" not in mcols:
            c.execute("ALTER TABLE matches ADD COLUMN verification INTEGER NOT NULL DEFAULT 0")
        # 연습판(botPlay)도 이력·뷰어 접근 가능하게(사용자 지시 2026-08-01) — 레이팅만 무반영
        if "practice" not in mcols:
            c.execute("ALTER TABLE matches ADD COLUMN practice INTEGER NOT NULL DEFAULT 0")
        # 플레이 시작 시각(현황 탭 피드 2026-08-01) — ts(종료)와 함께 판 길이 산출
        if "started_ts" not in mcols:
            c.execute("ALTER TABLE matches ADD COLUMN started_ts TEXT NOT NULL DEFAULT ''")
        # 마이그레이션: Elo → Glicko-2(사용자 확정 2026-08-01). 구(elo) 테이블은 재생성 —
        # 기존 행은 기준 평행이동(1000→1500, +500)에 RD 350(불확실성 재시작)으로 이관.
        rcols = [r["name"] for r in c.execute("PRAGMA table_info(ratings)")]
        if "rd" not in rcols:
            old_rows = c.execute("SELECT participant, season, elo, games FROM ratings").fetchall()
            c.execute("DROP TABLE ratings")
            c.executescript(SCHEMA)   # ratings만 새 스키마로 재생성(나머지는 IF NOT EXISTS no-op)
            for r in old_rows:
                c.execute("INSERT INTO ratings(participant, season, rating, games) VALUES(?,?,?,?)",
                          (r["participant"], r["season"], r["elo"] + 500, r["games"]))
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
