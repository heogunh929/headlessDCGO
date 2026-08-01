"""your_turn 페이로드 → LLM용 텍스트 상태 서술 (설계 §3.2: legalActions 서술형이 1차 요건).

names 사전(카드번호→한글명)을 주면 카드가 "가브몬(BT1-029)"처럼 병기된다
(사용자 확정 2026-08-01: 기본 한글 — 스킬/LLM 경로의 카드 이해도 직결)."""

from __future__ import annotations

import re

_CARD_ID = re.compile(r"\b([A-Z]+\d*[-_]\d+\w*)\b")


def _card(card_id: str, names: dict | None) -> str:
    if names and card_id in names:
        return f"{names[card_id]}({card_id})"
    return card_id


def _perm(p: dict, names: dict | None) -> str:
    bits = [_card(p["top"], names) if p.get("top") else "?"]
    if p.get("level"):
        bits.append(f"Lv{p['level']}")
    if p.get("dp"):
        bits.append(f"DP{p['dp']}")
    if p.get("suspended"):
        bits.append("레스트")
    if p.get("roots"):
        bits.append(f"진화원{len(p['roots'])}")
    return "(" + " ".join(str(b) for b in bits) + ")"


def _side(name: str, s: dict, mine: bool, names: dict | None) -> list[str]:
    lines = [f"[{name}]"]
    lines.append(f"  덱 {s.get('deckCount', '?')}장 · 시큐리티 {s.get('securityCount', '?')}장 · 트래시 {len(s.get('trash', []))}장")
    if mine and "hand" in s:
        lines.append(f"  손패({len(s['hand'])}): {', '.join(_card(c, names) for c in s['hand']) or '없음'}")
    elif "handCount" in s:
        lines.append(f"  손패 {s['handCount']}장(비공개)")
    field = s.get("field") or []
    lines.append(f"  배틀에어리어({len(field)}): {' '.join(_perm(p, names) for p in field) or '없음'}")
    breeding = s.get("breeding") or []
    if breeding:
        lines.append(f"  육성: {' '.join(_perm(p, names) for p in breeding)}")
    return lines


def state_to_text(turn: dict, names: dict | None = None) -> str:
    """관측 필터된 state + 결정 종류 + 합법 수 목록을 사람이/LLM이 읽는 한 덩어리로."""
    state = turn.get("state") or {}
    seat = turn.get("seat")
    me, foe = ("p1", "p2") if seat == 1 else ("p2", "p1")
    memory = state.get("memory", 0)
    my_memory = -memory if me == "p1" else memory   # 음수 = P1 보유(뷰어 관례)

    lines = [f"턴 {state.get('turn')} · {state.get('phase')} · 내 좌석 P{seat}",
             f"메모리: 내 쪽 {my_memory:+d}" if isinstance(memory, int) else "메모리: ?"]
    lines += _side("나", state.get(me) or {}, mine=True, names=names)
    lines += _side("상대", state.get(foe) or {}, mine=False, names=names)
    lines.append(f"결정 종류: {turn.get('kind')}")
    if turn.get("kind") == "Selection":
        # 멀티 선택 안내(사용자 결함 보고 2026-08-01): 반복 구조를 모르면 에이전트가 오판한다
        picked = turn.get("selectedCount")
        lines.append(f"※ 선택 창: 후보를 하나 고르면 같은 창이 다시 온다(여러 장 선택 구조)."
                     f" 원하는 만큼 고른 뒤 '선택 종료'로 확정."
                     + (f" 지금까지 {picked}장 선택됨." if isinstance(picked, int) else ""))
    lines.append("가능한 행동:")
    for action in turn.get("legalActions") or []:
        desc = action["desc"]
        if names:
            # 서술 속 카드번호(ST1_15/BT1-029 양식 공존)에 한글명 병기
            desc = _CARD_ID.sub(lambda m: _card(m.group(1).replace("_", "-"), names), desc)
        lines.append(f"  {action['index']}: {desc}")
    return "\n".join(lines)
