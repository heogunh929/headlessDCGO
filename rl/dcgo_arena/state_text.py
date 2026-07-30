"""your_turn 페이로드 → LLM용 텍스트 상태 서술 (설계 §3.2: legalActions 서술형이 1차 요건)."""

from __future__ import annotations


def _perm(p: dict) -> str:
    bits = [p.get("top") or "?"]
    if p.get("level"):
        bits.append(f"Lv{p['level']}")
    if p.get("dp"):
        bits.append(f"DP{p['dp']}")
    if p.get("suspended"):
        bits.append("레스트")
    if p.get("roots"):
        bits.append(f"진화원{len(p['roots'])}")
    return "(" + " ".join(str(b) for b in bits) + ")"


def _side(name: str, s: dict, mine: bool) -> list[str]:
    lines = [f"[{name}]"]
    lines.append(f"  덱 {s.get('deckCount', '?')}장 · 시큐리티 {s.get('securityCount', '?')}장 · 트래시 {len(s.get('trash', []))}장")
    if mine and "hand" in s:
        lines.append(f"  손패({len(s['hand'])}): {', '.join(s['hand']) or '없음'}")
    elif "handCount" in s:
        lines.append(f"  손패 {s['handCount']}장(비공개)")
    field = s.get("field") or []
    lines.append(f"  배틀에어리어({len(field)}): {' '.join(_perm(p) for p in field) or '없음'}")
    breeding = s.get("breeding") or []
    if breeding:
        lines.append(f"  육성: {' '.join(_perm(p) for p in breeding)}")
    return lines


def state_to_text(turn: dict) -> str:
    """관측 필터된 state + 결정 종류 + 합법 수 목록을 사람이/LLM이 읽는 한 덩어리로."""
    state = turn.get("state") or {}
    seat = turn.get("seat")
    me, foe = ("p1", "p2") if seat == 1 else ("p2", "p1")
    memory = state.get("memory", 0)
    my_memory = -memory if me == "p1" else memory   # 음수 = P1 보유(뷰어 관례)

    lines = [f"턴 {state.get('turn')} · {state.get('phase')} · 내 좌석 P{seat}",
             f"메모리: 내 쪽 {my_memory:+d}" if isinstance(memory, int) else "메모리: ?"]
    lines += _side("나", state.get(me) or {}, mine=True)
    lines += _side("상대", state.get(foe) or {}, mine=False)
    lines.append(f"결정 종류: {turn.get('kind')}")
    lines.append("가능한 행동:")
    for action in turn.get("legalActions") or []:
        lines.append(f"  {action['index']}: {action['desc']}")
    return "\n".join(lines)
