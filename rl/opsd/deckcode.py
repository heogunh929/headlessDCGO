"""클립보드 덱 코드 파서 — AS-IS DeckCodeUtility 전사 (사용자 확정 2026-07-31).

원본: Assets/Scripts/Script/DeckCodeUtility.cs (+ CreateNewDeckButton.OnClickFromDeckCode).
관리자 덱 탭·참가자 덱 빌더가 같은 파서를 쓴다 — 형식·거동의 정본은 AS-IS.

두 형식을 원본과 같은 순서로 시도한다:
  1) DeckBuilder 줄 형식 (:103) — 숫자로 시작하는 줄만: 첫 공백 앞=매수, 줄 끝 마지막
     공백 뒤 토큰=카드 ID. ID 매칭 실패 시 다음 줄을 '/'로 이어붙여 재시도(최대 4회 —
     줄바꿈 낀 카드명 대응). 미지 토큰은 조용히 skip(원본 거동) — 단 skipped로 집계해
     UI가 안내할 수 있게 한다(등록 단계의 적법성 검증은 별도).
  2) TTS/배열 형식 (:76) — '[' ']' '\"' 제거 후 콤마 분리, 토큰당 1장(반복=매수).
     digimonmeta 내보내기가 이 형식이며 머리말("Exported from …")은 매칭 실패로 무시.

카드 매칭(:157): CardID 우선, 실패 시 CardSpriteName — cards_meta는 표시형 카드번호가
키라서 둘 다 그 키로 수렴한다(±일러 변형 _P는 표시형으로 정규화).
"""

from __future__ import annotations

import re

_VARIANT = re.compile(r"_P\d+$", re.IGNORECASE)


def _match(card_id: str, cards_meta: dict) -> str | None:
    """AS-IS GetCardFromCardID 등가 — 표시형 키 매칭(+일러 변형 접미 제거 재시도)."""
    token = card_id.strip()
    if token in cards_meta:
        return token
    canonical = _VARIANT.sub("", token.replace("_", "-"))
    return canonical if canonical in cards_meta else None


def _from_builder_lines(text: str, cards_meta: dict) -> tuple[list[tuple[str, int]], list[str]]:
    cards: list[tuple[str, int]] = []
    skipped: list[str] = []
    lines = text.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        i += 1
        if not line:
            continue
        if not line[0].isdigit():
            continue
        space = line.find(" ")
        if space < 0:
            continue
        try:
            count = int(line[:space].strip())
        except ValueError:
            continue
        matched = None
        for _attempt in range(4):                       # 원본: 4회 — 줄바꿈 낀 카드명 이어붙이기
            line = line.rstrip()
            last_space = line.rfind(" ")
            token = line[last_space + 1:]
            matched = _match(token, cards_meta)
            if matched is not None:
                cards.append((matched, count))
                break
            if i < len(lines):
                line += "/" + lines[i]
                i += 1
            else:
                break
        if matched is None:
            skipped.append(line[-40:])
    return cards, skipped


def _from_tts(text: str, cards_meta: dict) -> tuple[list[tuple[str, int]], list[str]]:
    cleaned = text.replace("[", "").replace("]", "").replace('"', "")
    counts: dict[str, int] = {}
    skipped: list[str] = []
    for token in cleaned.split(","):
        matched = _match(token, cards_meta)
        if matched is None:
            if token.strip():
                skipped.append(token.strip()[:40])
            continue
        counts[matched] = counts.get(matched, 0) + 1    # 원본: 토큰당 1장, 반복=매수
    return list(counts.items()), skipped


def parse_clipboard(text: str, cards_meta: dict) -> dict:
    """클립보드 텍스트 → {main, digitama, skipped}. AS-IS 순서: 빌더 형식 → 0장이면 TTS."""
    text = (text or "").strip()
    cards, skipped = _from_builder_lines(text, cards_meta)
    if not cards:
        cards, skipped = _from_tts(text, cards_meta)
    if not cards:
        return {"error": "덱 코드를 읽지 못했습니다 (AS-IS 두 형식 모두 실패)", "skipped": skipped}
    # 병합(같은 카드 반복 줄) 후 타입으로 메인/디지타마 분리 — 원본 cardKind(DigiEgg) 분기.
    merged: dict[str, int] = {}
    for card, count in cards:
        merged[card] = merged.get(card, 0) + count
    main, digitama = [], []
    for card, count in merged.items():
        target = digitama if (cards_meta.get(card) or {}).get("type") == "DigiEgg" else main
        target.append({"card": card, "count": count})
    return {"main": main, "digitama": digitama, "skipped": skipped}
