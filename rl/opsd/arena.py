"""아레나 업무 계층 — 가입/키, 덱 검증, Elo (설계 v1 §4·§5, 요구 §6.5~6.8).

덱 적법성의 정본은 AS-IS DeckData.IsValidDeckData(DeckData.cs:689) + MaxCountInDeck
(CardPrefab_CreateDeck.cs:377) — 여기 구현은 그 규칙의 전사이며, 아레나 고유 추가는
카드 풀 필터 하나다(요구 §6.6.5: 포맷·검증기 공용, 아레나만 풀 필터).
"""

from __future__ import annotations

import hashlib
import json
import math
import re
import secrets
from pathlib import Path

#: 핸들 허용 문자(보안 정리 2026-08-01) — 공개 순위표 렌더 대상이라 마크업 문자 차단
HANDLE_OK = re.compile(r"[\w가-힣ㄱ-ㅎㅏ-ㅣ .\-]+", re.UNICODE)

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
    # 저장형 XSS 차단(보안 정리 2026-08-01): 핸들은 공개 순위표에 그대로 렌더된다 —
    # 관리자 브라우저에서 스크립트가 돌면 운영 토큰(localStorage)이 나간다.
    if not HANDLE_OK.fullmatch(handle):
        return {"error": "handle은 한글·영문·숫자와 - _ . 공백만 가능"}
    c = db.conn()
    if c.execute("SELECT 1 FROM participants WHERE handle=?", (handle,)).fetchone():
        return {"error": "이미 존재하는 handle"}
    auto = db.setting("auto_approve") == "1"
    key = secrets.token_urlsafe(24) if auto else None
    claim = None if auto else secrets.token_urlsafe(12)
    c.execute("INSERT INTO participants(handle, key_hash, claim_hash, kind, status, created, key_plain) VALUES(?,?,?,?,?,?,?)",
              (handle, key_hash(key) if key else None, key_hash(claim) if claim else None,
               "llm", "active" if auto else "pending", db.now(), key or ""))
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
    c.execute("UPDATE participants SET key_hash=?, key_plain=? WHERE id=?", (key_hash(key), key, row["id"]))
    c.commit()
    return {"handle": row["handle"], "key": key,
            "notice": "키를 보관하세요 — 분실 시 관리자 페이지에서도 확인 가능합니다"}


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


def _recipe_to_deck(deck_recipe: str) -> dict:
    """rl/decks 레시피(언더스코어 카드번호) → 아레나 덱 JSON(대시) — 풀 검증 우회(운영자 산물)."""
    recipe_path = Path(__file__).resolve().parent.parent / "decks" / deck_recipe
    recipe = json.loads(recipe_path.read_text(encoding="utf-8"))
    return {"name": recipe.get("name") or deck_recipe.replace(".json", ""),
            "main": [{"card": e["card"].replace("_", "-"), "count": e["count"]} for e in recipe["main"]],
            "digitama": [{"card": e["card"].replace("_", "-"), "count": e["count"]}
                         for e in recipe.get("digitama", [])]}


def replace_policy_deck(participant_id: int, deck_recipe: str) -> dict:
    """상시 봇 덱 교체(관리자, 2026-08-01) — 기존 덱 비활성 후 레시피 덱을 활성으로."""
    c = db.conn()
    row = c.execute("SELECT handle FROM participants WHERE id=? AND kind='policy'", (participant_id,)).fetchone()
    if row is None:
        return {"error": "policy 참가자가 아님"}
    try:
        deck = _recipe_to_deck(deck_recipe)
    except (OSError, json.JSONDecodeError, KeyError) as ex:
        return {"error": f"레시피 로드 실패: {ex}"}
    c.execute("UPDATE decks SET active=0 WHERE owner=?", (participant_id,))
    c.execute("INSERT INTO decks(owner, name, cards_json, active, enabled, disabled_reason, created)"
              " VALUES(?,?,?,1,1,'',?)",
              (participant_id, deck["name"], json.dumps(deck, ensure_ascii=False), db.now()))
    c.commit()
    return {"ok": True, "deck": deck["name"]}


def reset_rating(participant_id: int) -> dict:
    """레이팅 초기화(관리자) — 현 시즌 행을 Glicko 시작값으로. 봇 교체·시즌 정리용."""
    c = db.conn()
    c.execute("UPDATE ratings SET rating=1500, rd=350, vol=0.06, games=0 WHERE participant=? AND season=?",
              (participant_id, db.active_season()))
    c.commit()
    return {"ok": True}


def register_policy_participant(handle: str, policy_path: str = "", deck_recipe: str = "") -> dict:
    """RL 스냅샷 참가자 등록 게이트(요구 §6.6.5) — kind=policy, 키 발급, 정책 경로 결속.
    deck_recipe(rl/decks/*.json 파일명)를 주면 그 레시피를 활성 덱으로 자동 등록 —
    상시 봇(2026-08-01)의 학습 덱은 풀 검증을 우회한다(운영자 산물, 정책과 짝)."""
    c = db.conn()
    if c.execute("SELECT 1 FROM participants WHERE handle=?", (handle,)).fetchone():
        return {"error": "이미 존재하는 handle"}
    key = secrets.token_urlsafe(24)
    c.execute("INSERT INTO participants(handle, key_hash, kind, status, created, policy_path, key_plain) VALUES(?,?,?,?,?,?,?)",
              (handle, key_hash(key), "policy", "active", db.now(), policy_path, key))
    pid = c.execute("SELECT id FROM participants WHERE handle=?", (handle,)).fetchone()["id"]
    deck_name = None
    if deck_recipe:
        try:
            deck = _recipe_to_deck(deck_recipe)
            c.execute("INSERT INTO decks(owner, name, cards_json, active, enabled, disabled_reason, created)"
                      " VALUES(?,?,?,1,1,'',?)",
                      (pid, deck["name"], json.dumps(deck, ensure_ascii=False), db.now()))
            deck_name = deck["name"]
        except (OSError, json.JSONDecodeError, KeyError) as ex:
            c.commit()
            return {"handle": handle, "key": key, "kind": "policy", "policyPath": policy_path,
                    "warning": f"덱 레시피 등록 실패({ex}) — 덱 없이 등록됨"}
    c.commit()
    return {"handle": handle, "key": key, "kind": "policy", "policyPath": policy_path, "deck": deck_name}


# ---------- 덱 (검증 = AS-IS 규칙 전사 + 풀 필터) ----------

def validate_deck(deck: dict, cards_meta: dict) -> list[str]:
    """오류 목록 반환(빈 목록 = 적법). 사유는 참가자 UI에 즉시 표시(요구 §6.6 ②)."""
    errors: list[str] = []
    pool = json.loads(db.setting("card_pool"))
    # 풀 = 세트 단위 + 개별 허용(cards: P-/LM- 등 번호 단위) + 개별 제외(excluded — 세트 허용에서 예외)
    # (사용자 확정 2026-08-01: 세트 단위만으로는 P-/LM 관리 불가)
    pool_sets = set(pool.get("sets", []))
    pool_cards = set(pool.get("cards", []))
    pool_excluded = set(pool.get("excluded", []))

    def in_pool(card_id: str) -> bool:
        if card_id in pool_excluded:
            return False
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

    # 금지/제한/금지페어 — AS-IS DeckBuildingRule.cs 의미론 이식(사용자 지시 2026-08-01):
    # CanAddCard = AllDeckCards(메인+디지타마) 합산 매수가 restriction.limit 초과 불가(limit 0=금지),
    # BannedPair = id와 pairs 중 어느 카드도 한 덱에 공존 불가(양방향 검사).
    ban = json.loads(db.setting("ban_list"))
    limits = {str(r.get("id", "")): int(r.get("limit", 0)) for r in ban.get("restrictions", [])}
    for card_id, total in counts.items():
        if card_id in limits and total > limits[card_id]:
            limit = limits[card_id]
            errors.append(f"{card_id}: 금지 카드" if limit == 0
                          else f"{card_id}: 제한 카드 — 최대 {limit}장(현재 {total})")
    for pair in ban.get("banned_pairs", []):
        anchor = str(pair.get("id", ""))
        partners = [p for p in pair.get("pairs", []) if p in counts]
        if anchor in counts and partners:
            errors.append(f"금지 페어: {anchor} ↔ {', '.join(partners)} 공존 불가")

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


def update_deck(owner_id: int, deck_id: int, deck: dict, cards_meta: dict) -> dict:
    """덱 덮어쓰기(웹 편집 저장) — 소유 검증 + 재검증. 판 시점 스냅샷 원칙이라 과거 이력 무영향."""
    c = db.conn()
    row = c.execute("SELECT * FROM decks WHERE id=? AND owner=?", (deck_id, owner_id)).fetchone()
    if row is None:
        return {"error": "없는 덱"}
    errors = validate_deck(deck, cards_meta)
    if errors:
        return {"error": "덱 검증 실패", "reasons": errors}
    name = str(deck.get("name") or row["name"])[:60]
    c.execute("UPDATE decks SET name=?, cards_json=?, enabled=1, disabled_reason='' WHERE id=?",
              (name, json.dumps(deck, ensure_ascii=False), deck_id))
    c.commit()
    return {"ok": True, "deck": name}


def delete_deck(owner_id: int, deck_id: int) -> dict:
    """덱 삭제 — 이력의 판 시점 스냅샷은 decks와 무관하게 보존되므로 안전(요구 §6.6 ③)."""
    c = db.conn()
    row = c.execute("SELECT * FROM decks WHERE id=? AND owner=?", (deck_id, owner_id)).fetchone()
    if row is None:
        return {"error": "없는 덱"}
    c.execute("DELETE FROM decks WHERE id=?", (deck_id,))
    c.commit()
    return {"ok": True, "deleted": row["name"], "wasActive": bool(row["active"])}


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
        if errors:
            reason = "; ".join(errors)[:300]
            if row["enabled"] or row["disabled_reason"] != reason:   # 이미 비활성이어도 사유는 최신으로
                c.execute("UPDATE decks SET enabled=0, disabled_reason=? WHERE id=?", (reason, row["id"]))
                changed["disabled"] += int(bool(row["enabled"]))
        elif not errors and not row["enabled"]:
            c.execute("UPDATE decks SET enabled=1, disabled_reason='' WHERE id=?", (row["id"],))
            changed["restored"] += 1
    c.commit()
    return changed


# ---------- Elo·이력 ----------

# ── Glicko-2 (Glickman 2013, 사용자 확정 2026-08-01: Elo → Glicko 전환) ─────────────────────
# 평가 기간 = 1판 근사(아레나는 연전 스트림이라 기간 묶음이 없다). 미접속 RD 증가는 미적용 —
# 판이 없으면 레이팅이 그대로인 보수적 단순화(공개 운영에서 필요해지면 기간 스케줄러로 확장).
GLICKO_SCALE = 173.7178
GLICKO_BASE = 1500.0
GLICKO_TAU = 0.5


def glicko2_update(rating: float, rd: float, vol: float,
                   opp_rating: float, opp_rd: float, score: float) -> tuple[float, float, float]:
    """한 판 결과(score 1/0.5/0)로 (rating, rd, vol) 갱신 — Glickman 논문 절차 그대로."""
    mu = (rating - GLICKO_BASE) / GLICKO_SCALE
    phi = rd / GLICKO_SCALE
    mu_j = (opp_rating - GLICKO_BASE) / GLICKO_SCALE
    phi_j = opp_rd / GLICKO_SCALE

    g = 1.0 / math.sqrt(1.0 + 3.0 * phi_j ** 2 / math.pi ** 2)
    expected = 1.0 / (1.0 + math.exp(-g * (mu - mu_j)))
    v = 1.0 / (g ** 2 * expected * (1.0 - expected))
    delta = v * g * (score - expected)

    # 변동성 반복(Illinois) — 수렴 실패 방지 상한 100회(실제 ~10회 내 수렴)
    a = math.log(vol ** 2)

    def f(x: float) -> float:
        ex = math.exp(x)
        return (ex * (delta ** 2 - phi ** 2 - v - ex)) / (2.0 * (phi ** 2 + v + ex) ** 2) \
            - (x - a) / GLICKO_TAU ** 2

    big_a = a
    if delta ** 2 > phi ** 2 + v:
        big_b = math.log(delta ** 2 - phi ** 2 - v)
    else:
        k = 1
        while f(a - k * GLICKO_TAU) < 0:
            k += 1
        big_b = a - k * GLICKO_TAU

    fa, fb = f(big_a), f(big_b)

    for _ in range(100):
        if abs(big_b - big_a) <= 1e-6:
            break
        big_c = big_a + (big_a - big_b) * fa / (fb - fa)
        fc = f(big_c)
        if fc * fb <= 0:
            big_a, fa = big_b, fb
        else:
            fa /= 2.0
        big_b, fb = big_c, fc

    new_vol = math.exp(big_a / 2.0)
    phi_star = math.sqrt(phi ** 2 + new_vol ** 2)
    new_phi = 1.0 / math.sqrt(1.0 / phi_star ** 2 + 1.0 / v)
    new_mu = mu + new_phi ** 2 * g * (score - expected)

    return GLICKO_BASE + GLICKO_SCALE * new_mu, GLICKO_SCALE * new_phi, new_vol


def rekey(participant_id: int) -> dict:
    """관리자 키 재발급(사용자 확정 2026-08-01: 관리자 키 열람) — 구키 즉시 무효.
    평문 저장 전 참가자(key_plain 공란)의 키 확인 수단이기도 하다."""
    c = db.conn()
    row = c.execute("SELECT handle FROM participants WHERE id=?", (participant_id,)).fetchone()
    if row is None:
        return {"error": "없는 참가자"}
    key = secrets.token_urlsafe(24)
    c.execute("UPDATE participants SET key_hash=?, key_plain=? WHERE id=?",
              (key_hash(key), key, participant_id))
    c.commit()
    return {"ok": True, "handle": row["handle"], "key": key}


def delete_participant(participant_id: int, force: bool = False) -> dict:
    """참가자 완전 삭제(관리자, 사용자 지시 2026-08-01) — 대전 기록이 있으면 기본 거부(이력 조인 보전,
    상대방의 이력·판 로그 링크가 깨진다). force=True면 그 참가자가 낀 대전 기록까지 함께 삭제
    (테스트 데이터 정리·시즌 초기화용 — 상대방 이력에서도 해당 판이 사라짐을 UI가 고지)."""
    c = db.conn()
    row = c.execute("SELECT handle FROM participants WHERE id=?", (participant_id,)).fetchone()
    if row is None:
        return {"error": "없는 참가자"}
    n_matches = c.execute("SELECT COUNT(*) AS n FROM matches WHERE p1=? OR p2=?",
                          (participant_id, participant_id)).fetchone()["n"]
    if n_matches and not force:
        return {"error": f"대전 기록 {n_matches}판 존재 — 기록 보전 삭제 불가. 기록까지 지우려면 강제 삭제"}
    if n_matches:
        c.execute("DELETE FROM matches WHERE p1=? OR p2=?", (participant_id, participant_id))
    c.execute("DELETE FROM ratings WHERE participant=?", (participant_id,))
    c.execute("DELETE FROM decks WHERE owner=?", (participant_id,))
    c.execute("DELETE FROM participants WHERE id=?", (participant_id,))
    c.commit()
    return {"ok": True, "deleted": row["handle"], "matchesDeleted": n_matches}


def rating_row(participant: int, season: str):
    c = db.conn()
    c.execute("INSERT OR IGNORE INTO ratings(participant, season) VALUES(?,?)", (participant, season))
    c.commit()   # 조회 경로(/me 등)에서 생성된 행도 즉시 영속 — 커밋 없인 프로세스 재시작에 유실
    return c.execute("SELECT * FROM ratings WHERE participant=? AND season=?", (participant, season)).fetchone()


def record_match(match_id: str, p1: int, p2: int, deck1: dict, deck2: dict,
                 winner: int | None, reason: str, log_run: str, log_path: str,
                 started_ts: str = "") -> dict:
    season = db.active_season()
    row1, row2 = rating_row(p1, season), rating_row(p2, season)
    score1 = 0.5 if winner is None else (1.0 if winner == 1 else 0.0)
    new1 = glicko2_update(row1["rating"], row1["rd"], row1["vol"], row2["rating"], row2["rd"], score1)
    new2 = glicko2_update(row2["rating"], row2["rd"], row2["vol"], row1["rating"], row1["rd"], 1.0 - score1)
    delta1 = round(new1[0] - row1["rating"], 1)
    delta2 = round(new2[0] - row2["rating"], 1)
    c = db.conn()
    for pid, new in ((p1, new1), (p2, new2)):
        c.execute("UPDATE ratings SET rating=?, rd=?, vol=?, games=games+1 WHERE participant=? AND season=?",
                  (new[0], new[1], new[2], pid, season))
    c.execute("INSERT OR REPLACE INTO matches(id, season, p1, p2, deck1_json, deck2_json, winner, reason,"
              " rating_delta_json, log_run, log_path, ts, started_ts) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)",
              (match_id, season, p1, p2, json.dumps(deck1, ensure_ascii=False), json.dumps(deck2, ensure_ascii=False),
               winner, reason, json.dumps({"1": delta1, "2": delta2}), log_run, log_path, db.now(), started_ts))
    c.commit()
    return {"1": delta1, "2": delta2}


def record_verification(match_id: str, participant_id: int, deck: dict,
                        winner: int | None, reason: str, log_run: str, log_path: str,
                        practice: bool = False, started_ts: str = "") -> None:
    """검증판/연습판 이력 기록(사용자 지시 2026-08-01) — 레이팅 무반영, [검증]/[연습] 배지 노출용.
    양좌석 모두 본인(상대=하우스 봇 대행)이라 p1=p2, 델타 없음. 행이 있어야 본인 뷰어 접근이 된다."""
    c = db.conn()
    c.execute("INSERT OR REPLACE INTO matches(id, season, p1, p2, deck1_json, deck2_json, winner, reason,"
              " rating_delta_json, log_run, log_path, ts, verification, practice, started_ts)"
              " VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
              (match_id, db.active_season(), participant_id, participant_id,
               json.dumps(deck, ensure_ascii=False), json.dumps(deck, ensure_ascii=False),
               winner, reason, "{}", log_run, log_path, db.now(),
               0 if practice else 1, 1 if practice else 0, started_ts))
    c.commit()


def rankings() -> list[dict]:
    """공개 순위표(무인증) — 로그 링크 없음(요구 §7: 공개 순위표는 로그 비공개).
    차단(banned) 참가자는 제외(사용자 지시 2026-08-01) — 복구되면 다시 나타난다."""
    season = db.active_season()
    rows = db.conn().execute(
        "SELECT p.handle, p.kind, r.rating, r.rd, r.games FROM ratings r JOIN participants p ON p.id=r.participant"
        " WHERE r.season=? AND p.status='active' ORDER BY r.rating DESC", (season,)).fetchall()
    return [{"rank": i + 1, "handle": r["handle"], "kind": r["kind"],
             "rating": round(r["rating"]), "rd": round(r["rd"]), "games": r["games"]}
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
        verification = bool(r["verification"])
        practice = bool(r["practice"])
        out.append({"matchId": r["id"], "ts": r["ts"],
                    "opponent": "하우스 봇(연습)" if practice
                                else "하우스 봇(검증)" if verification
                                else (r["h2"] if my_seat == 1 else r["h1"]),
                    "mySeat": my_seat, "winner": r["winner"], "reason": r["reason"],
                    "ratingDelta": delta, "verification": verification, "practice": practice,
                    "logRun": r["log_run"], "logPath": r["log_path"]})
    return out
