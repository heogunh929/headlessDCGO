"""아레나 업무 계층 — 가입/키, 덱 검증, Elo (설계 v1 §4·§5, 요구 §6.5~6.8).

덱 적법성의 정본은 AS-IS DeckData.IsValidDeckData(DeckData.cs:689) + MaxCountInDeck
(CardPrefab_CreateDeck.cs:377) — 여기 구현은 그 규칙의 전사이며, 아레나 고유 추가는
카드 풀 필터 하나다(요구 §6.6.5: 포맷·검증기 공용, 아레나만 풀 필터).
"""

from __future__ import annotations

import hashlib
import json
import secrets

from . import db


# ---------- 참가자·키 (§5: 키가 신원이자 권한 전부, DB엔 해시만) ----------

def key_hash(key: str) -> str:
    return hashlib.sha256(key.encode()).hexdigest()


def signup(handle: str) -> dict:
    """가입 신청. 승인제면 신청 코드를 신청자에게 1회 발급 — 승인 후 그 코드로 본인이 키를
    수령한다(관리자는 키를 못 본다). 자동 승인이면 즉시 활성+키 발급(1회 표시)."""
    handle = handle.strip()
    if not handle or len(handle) > 40:
        return {"error": "handle은 1~40자"}
    c = db.conn()
    if c.execute("SELECT 1 FROM participants WHERE handle=?", (handle,)).fetchone():
        return {"error": "이미 존재하는 handle"}
    auto = db.setting("auto_approve") == "1"
    key = secrets.token_urlsafe(24) if auto else None
    claim = None if auto else secrets.token_urlsafe(12)
    c.execute("INSERT INTO participants(handle, key_hash, claim_hash, kind, status, created) VALUES(?,?,?,?,?,?)",
              (handle, key_hash(key) if key else None, key_hash(claim) if claim else None,
               "llm", "active" if auto else "pending", db.now()))
    c.commit()
    if auto:
        return {"handle": handle, "status": "active", "key": key, "notice": "키는 이번 1회만 표시됩니다"}
    return {"handle": handle, "status": "pending", "claim": claim,
            "notice": "신청 코드는 이번 1회만 표시됩니다 — 승인 후 이 코드로 API 키를 수령하세요"}


def approve(participant_id: int) -> dict:
    """관리자 승인 — 상태만 활성화. 키는 신청자가 신청 코드로 직접 수령(claim_key)."""
    c = db.conn()
    row = c.execute("SELECT * FROM participants WHERE id=?", (participant_id,)).fetchone()
    if row is None:
        return {"error": "없는 참가자"}
    c.execute("UPDATE participants SET status='active' WHERE id=?", (participant_id,))
    c.commit()
    return {"handle": row["handle"], "ok": True,
            "notice": "승인됨 — 참가자가 신청 코드로 키를 수령할 수 있습니다"}


def claim_key(handle: str, claim: str) -> dict:
    """승인된 신청자의 키 수령 — 신청 코드 검증, 1회만 발급."""
    c = db.conn()
    row = c.execute("SELECT * FROM participants WHERE handle=?", (handle.strip(),)).fetchone()
    if row is None or not claim or row["claim_hash"] != key_hash(claim):
        return {"error": "핸들 또는 신청 코드가 맞지 않습니다"}
    if row["status"] == "pending":
        return {"error": "아직 승인 대기 중입니다"}
    if row["status"] != "active":
        return {"error": "수령할 수 없는 상태입니다"}
    if row["key_hash"] is not None:
        return {"error": "이미 키가 발급되었습니다(재발급은 관리자 문의)"}
    key = secrets.token_urlsafe(24)
    c.execute("UPDATE participants SET key_hash=? WHERE id=?", (key_hash(key), row["id"]))
    c.commit()
    return {"handle": row["handle"], "key": key, "notice": "키는 이번 1회만 표시됩니다"}


def set_status(participant_id: int, status: str) -> dict:
    if status not in ("pending", "active", "banned"):
        return {"error": "status는 pending|active|banned"}
    db.conn().execute("UPDATE participants SET status=? WHERE id=?", (status, participant_id))
    db.conn().commit()
    return {"ok": True}


def auth(key: str | None):
    """API 키 → 활성 참가자 row (없으면 None)."""
    if not key:
        return None
    return db.conn().execute(
        "SELECT * FROM participants WHERE key_hash=? AND status='active'", (key_hash(key),)).fetchone()


def register_policy_participant(handle: str, policy_path: str = "") -> dict:
    """RL 스냅샷 참가자 등록 게이트(요구 §6.6.5) — kind=policy, 키 발급, 정책 경로 결속."""
    c = db.conn()
    if c.execute("SELECT 1 FROM participants WHERE handle=?", (handle,)).fetchone():
        return {"error": "이미 존재하는 handle"}
    key = secrets.token_urlsafe(24)
    c.execute("INSERT INTO participants(handle, key_hash, kind, status, created, policy_path) VALUES(?,?,?,?,?,?)",
              (handle, key_hash(key), "policy", "active", db.now(), policy_path))
    c.commit()
    return {"handle": handle, "key": key, "kind": "policy", "policyPath": policy_path}


# ---------- 덱 (검증 = AS-IS 규칙 전사 + 풀 필터) ----------

def validate_deck(deck: dict, cards_meta: dict) -> list[str]:
    """오류 목록 반환(빈 목록 = 적법). 사유는 참가자 UI에 즉시 표시(요구 §6.6 ②)."""
    errors: list[str] = []
    pool = json.loads(db.setting("card_pool"))
    pool_sets, pool_cards = set(pool.get("sets", [])), set(pool.get("cards", []))

    def in_pool(card_id: str) -> bool:
        return card_id.split("-")[0] in pool_sets or card_id in pool_cards

    main = deck.get("main") or []
    digitama = deck.get("digitama") or []
    counts: dict[str, int] = {}

    for section, entries, egg in (("main", main, False), ("digitama", digitama, True)):
        for entry in entries:
            card_id, count = str(entry.get("card", "")), int(entry.get("count", 0))
            meta = cards_meta.get(card_id)
            if meta is None:
                errors.append(f"{section}: 존재하지 않는 카드 {card_id}")
                continue
            if count < 1:
                errors.append(f"{section}: {card_id} 매수 {count}는 무효")
            is_egg = meta.get("type") == "DigiEgg"
            if egg and not is_egg:
                errors.append(f"digitama: {card_id}는 디지타마가 아님")
            if not egg and is_egg:
                errors.append(f"main: {card_id}는 디지타마 — 디지타마 덱에만")
            if not in_pool(card_id):
                errors.append(f"{section}: {card_id}는 현재 카드 풀 밖")
            counts[card_id] = counts.get(card_id, 0) + count

    main_total = sum(int(e.get("count", 0)) for e in main)
    egg_total = sum(int(e.get("count", 0)) for e in digitama)
    if main_total != 50:
        errors.append(f"메인 덱은 정확히 50장(현재 {main_total}) — AS-IS DeckData:692")
    if egg_total > 5:
        errors.append(f"디지타마 덱은 최대 5장(현재 {egg_total}) — AS-IS DeckData:697")

    for card_id, total in counts.items():
        max_count = cards_meta.get(card_id, {}).get("maxCount", 4)
        if total > max_count:
            errors.append(f"{card_id}: {total}장 > 최대 {max_count}장(MaxCountInDeck)")

    return errors


def register_deck(owner_id: int, deck: dict, cards_meta: dict) -> dict:
    errors = validate_deck(deck, cards_meta)
    if errors:
        return {"error": "덱 검증 실패", "reasons": errors}
    c = db.conn()
    limit = int(db.setting("deck_limit_per_key"))
    if c.execute("SELECT COUNT(*) AS n FROM decks WHERE owner=?", (owner_id,)).fetchone()["n"] >= limit:
        return {"error": f"덱 상한 {limit}개"}
    name = str(deck.get("name") or "무제 덱")[:60]
    first = c.execute("SELECT COUNT(*) AS n FROM decks WHERE owner=?", (owner_id,)).fetchone()["n"] == 0
    c.execute("INSERT INTO decks(owner, name, cards_json, active, created) VALUES(?,?,?,?,?)",
              (owner_id, name, json.dumps(deck, ensure_ascii=False), 1 if first else 0, db.now()))
    c.commit()
    return {"ok": True, "deck": name, "active": first}


def decks_of(owner_id: int) -> list[dict]:
    rows = db.conn().execute("SELECT * FROM decks WHERE owner=? ORDER BY id", (owner_id,)).fetchall()
    return [{"id": r["id"], "name": r["name"], "active": bool(r["active"]), "enabled": bool(r["enabled"]),
             "disabledReason": r["disabled_reason"], "cards": json.loads(r["cards_json"])} for r in rows]


def activate_deck(owner_id: int, deck_id: int) -> dict:
    c = db.conn()
    row = c.execute("SELECT * FROM decks WHERE id=? AND owner=?", (deck_id, owner_id)).fetchone()
    if row is None:
        return {"error": "없는 덱"}
    if not row["enabled"]:
        return {"error": f"비활성 덱: {row['disabled_reason']}"}
    c.execute("UPDATE decks SET active=0 WHERE owner=?", (owner_id,))
    c.execute("UPDATE decks SET active=1 WHERE id=?", (deck_id,))
    c.commit()
    return {"ok": True}


def active_deck(owner_id: int) -> dict | None:
    row = db.conn().execute(
        "SELECT * FROM decks WHERE owner=? AND active=1 AND enabled=1", (owner_id,)).fetchone()
    return json.loads(row["cards_json"]) if row else None


def reaudit_pool(cards_meta: dict) -> dict:
    """풀 변경 후 전 덱 재감사 — 풀 밖 덱은 비활성 표시, 복귀 시 자동 복원(요구 §6.6 ④)."""
    c = db.conn()
    changed = {"disabled": 0, "restored": 0}
    for row in c.execute("SELECT * FROM decks").fetchall():
        errors = validate_deck(json.loads(row["cards_json"]), cards_meta)
        if errors and row["enabled"]:
            c.execute("UPDATE decks SET enabled=0, disabled_reason=? WHERE id=?", ("; ".join(errors)[:300], row["id"]))
            changed["disabled"] += 1
        elif not errors and not row["enabled"]:
            c.execute("UPDATE decks SET enabled=1, disabled_reason='' WHERE id=?", (row["id"],))
            changed["restored"] += 1
    c.commit()
    return changed


# ---------- Elo·이력 ----------

K_FACTOR = 32


def rating_row(participant: int, season: str):
    c = db.conn()
    c.execute("INSERT OR IGNORE INTO ratings(participant, season) VALUES(?,?)", (participant, season))
    c.commit()   # 조회 경로(/me 등)에서 생성된 행도 즉시 영속 — 커밋 없인 프로세스 재시작에 유실
    return c.execute("SELECT * FROM ratings WHERE participant=? AND season=?", (participant, season)).fetchone()


def record_match(match_id: str, p1: int, p2: int, deck1: dict, deck2: dict,
                 winner: int | None, reason: str, log_run: str, log_path: str) -> dict:
    season = db.active_season()
    r1, r2 = rating_row(p1, season)["elo"], rating_row(p2, season)["elo"]
    expected1 = 1 / (1 + 10 ** ((r2 - r1) / 400))
    score1 = 0.5 if winner is None else (1.0 if winner == 1 else 0.0)
    delta1 = round(K_FACTOR * (score1 - expected1), 1)
    c = db.conn()
    c.execute("UPDATE ratings SET elo=elo+?, games=games+1 WHERE participant=? AND season=?", (delta1, p1, season))
    c.execute("UPDATE ratings SET elo=elo-?, games=games+1 WHERE participant=? AND season=?", (delta1, p2, season))
    c.execute("INSERT OR REPLACE INTO matches(id, season, p1, p2, deck1_json, deck2_json, winner, reason,"
              " rating_delta_json, log_run, log_path, ts) VALUES(?,?,?,?,?,?,?,?,?,?,?,?)",
              (match_id, season, p1, p2, json.dumps(deck1, ensure_ascii=False), json.dumps(deck2, ensure_ascii=False),
               winner, reason, json.dumps({"1": delta1, "2": -delta1}), log_run, log_path, db.now()))
    c.commit()
    return {"1": delta1, "2": -delta1}


def rankings() -> list[dict]:
    """공개 순위표(무인증) — 로그 링크 없음(요구 §7: 공개 순위표는 로그 비공개)."""
    season = db.active_season()
    rows = db.conn().execute(
        "SELECT p.handle, p.kind, r.elo, r.games FROM ratings r JOIN participants p ON p.id=r.participant"
        " WHERE r.season=? ORDER BY r.elo DESC", (season,)).fetchall()
    return [{"rank": i + 1, "handle": r["handle"], "kind": r["kind"], "elo": round(r["elo"], 1), "games": r["games"]}
            for i, r in enumerate(rows)]


def history_of(participant_id: int) -> list[dict]:
    rows = db.conn().execute(
        "SELECT m.*, pa.handle AS h1, pb.handle AS h2 FROM matches m"
        " JOIN participants pa ON pa.id=m.p1 JOIN participants pb ON pb.id=m.p2"
        " WHERE m.p1=? OR m.p2=? ORDER BY m.ts DESC LIMIT 200", (participant_id, participant_id)).fetchall()
    out = []
    for r in rows:
        my_seat = 1 if r["p1"] == participant_id else 2
        delta = json.loads(r["rating_delta_json"]).get(str(my_seat), 0)
        out.append({"matchId": r["id"], "ts": r["ts"], "opponent": r["h2"] if my_seat == 1 else r["h1"],
                    "mySeat": my_seat, "winner": r["winner"], "reason": r["reason"],
                    "ratingDelta": delta, "logRun": r["log_run"], "logPath": r["log_path"]})
    return out
