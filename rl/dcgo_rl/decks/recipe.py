"""덱 레시피 — 정규화 로더 (FR-3, 설계 §3).

정규화 파이프라인(§3.2, 순서 고정):
  1. 헤더/주석 행 제거   2. 구분자 정규화(하이픈→언더스코어)   3. 일러 변형(_P1 등) collapse
  4. 중복 행 = 매수 집계   5. 타입 기반 메인/디지타마 분리   6. 미지원 카드 = 명시 실패

덱 구성 적법성(장수·매수 상한)은 검증하지 않는다 — 단일 진실은 엔진 ``DeckValidator``
(이중 구현 금지). 이 로더의 책임은 "외부 표기 → 내부 표준 레시피" 변환뿐이다.
"""

from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Mapping, Sequence

from dcgo_rl.cards import CardIndex, canonical_card_number

# 카드번호 토큰: 세트코드(영문 1~4자 + 세트번호 0~2자리) + 구분자 + 카드번호 1~3자리 + 선택적 일러 접미.
# 실데이터 예: BT1-020, ST1-01, EX8-074, P-016, LM-001, AD1-023, BT10-005, ST1-01_P1 (하이픈/언더스코어 양쪽 허용)
_CARD_TOKEN = re.compile(r"\b[A-Za-z]{1,4}\d{0,2}[-_]\d{1,3}(?:_P\d+)?\b")
# 매수 표기(선택): 행 앞 "4 BT1-020" / "4x BT1-020" 또는 행 뒤 "BT1-020 x4"
_LEADING_COUNT = re.compile(r"^\s*(\d{1,2})\s*[xX]?\s+")
_TRAILING_COUNT = re.compile(r"\s+[xX](\d{1,2})\s*$")


@dataclass(frozen=True)
class RecipeEntry:
    card: str  # canonical 카드번호 (예: BT1_020)
    count: int


@dataclass(frozen=True)
class Recipe:
    """내부 표준 레시피 (설계 §3.1). GA 산출도 같은 형태를 쓴다(FR-3.4)."""

    name: str
    source: str  # operator | ga | random
    main: tuple[RecipeEntry, ...]
    digitama: tuple[RecipeEntry, ...]

    @property
    def main_count(self) -> int:
        return sum(entry.count for entry in self.main)

    @property
    def digitama_count(self) -> int:
        return sum(entry.count for entry in self.digitama)

    def all_cards(self) -> tuple[str, ...]:
        """덱 정체 채널(§4.1) 입력 — 레시피의 canonical 카드 멀티셋."""
        cards: list[str] = []
        for entry in (*self.main, *self.digitama):
            cards.extend([entry.card] * entry.count)
        return tuple(cards)

    def to_json(self) -> dict:
        return {
            "name": self.name,
            "source": self.source,
            "main": [{"card": e.card, "count": e.count} for e in self.main],
            "digitama": [{"card": e.card, "count": e.count} for e in self.digitama],
        }

    @classmethod
    def from_json(cls, data: Mapping) -> "Recipe":
        def entries(section: str) -> tuple[RecipeEntry, ...]:
            return tuple(RecipeEntry(str(e["card"]), int(e["count"])) for e in data.get(section, []))

        return cls(str(data["name"]), str(data.get("source", "operator")), entries("main"), entries("digitama"))


class RecipeError(ValueError):
    """미지원 카드 명시 실패(FR-3.2 단계 6) — 누락 카드번호 전체를 담는다. 조용한 skip 금지."""

    def __init__(self, recipe_name: str, unknown_cards: Sequence[str]):
        self.recipe_name = recipe_name
        self.unknown_cards = tuple(unknown_cards)
        super().__init__(f"recipe '{recipe_name}': unsupported cards: {', '.join(self.unknown_cards)}")


def parse_external(
    lines: Iterable[str],
    card_index: CardIndex,
    *,
    name: str,
    source: str = "operator",
) -> Recipe:
    """외부 시스템 덱 리스트(텍스트 행) → 내부 표준 Recipe.

    카드 토큰이 없는 행은 헤더/노이즈로 간주하고 건너뛴다(단계 1).
    기본 표기는 1행=1장이며, 행 앞 ``4 ``/뒤 `` x4`` 매수 표기를 허용한다(중복 행은 집계 — 단계 4).
    """
    counts: dict[str, int] = {}  # 삽입 순서 유지 = 첫 등장 순서 보존
    unknown: list[str] = []

    for raw_line in lines:
        line = raw_line.strip()
        if not line or line.startswith(("#", "//")):
            continue
        token_match = _CARD_TOKEN.search(line)
        if token_match is None:
            continue  # 헤더 행 (단계 1)

        count = 1
        leading = _LEADING_COUNT.match(line)
        trailing = _TRAILING_COUNT.search(line)
        if leading:
            count = int(leading.group(1))
        elif trailing:
            count = int(trailing.group(1))

        canonical = canonical_card_number(token_match.group(0))  # 단계 2·3
        if canonical not in card_index:
            if canonical not in unknown:
                unknown.append(canonical)
            continue
        counts[canonical] = counts.get(canonical, 0) + count  # 단계 4

    if unknown:
        raise RecipeError(name, unknown)  # 단계 6 — 전부 모아 한 번에 실패

    main: list[RecipeEntry] = []
    digitama: list[RecipeEntry] = []
    for card, count in counts.items():  # 단계 5
        (digitama if card_index.is_digitama(card) else main).append(RecipeEntry(card, count))

    return Recipe(name=name, source=source, main=tuple(main), digitama=tuple(digitama))


def load_external_file(path: Path, card_index: CardIndex, *, source: str = "operator") -> Recipe:
    with open(path, encoding="utf-8") as f:
        return parse_external(f, card_index, name=path.stem, source=source)


def load_recipe_file(path: Path, card_index: CardIndex) -> Recipe:
    """내부 표준 JSON(.json) 또는 외부 텍스트 리스트를 로드. JSON도 카드 존재를 재검증한다."""
    if path.suffix.lower() == ".json":
        with open(path, encoding="utf-8") as f:
            recipe = Recipe.from_json(json.load(f))
        unknown = [e.card for e in (*recipe.main, *recipe.digitama) if e.card not in card_index]
        if unknown:
            raise RecipeError(recipe.name, unknown)
        return recipe
    return load_external_file(path, card_index)
