"""카드 데이터 접지 + CardVocabulary (설계 §1.3 vocab 단일 진실, §4.1).

카드 존재·타입의 원천은 엔진 CardDatabase가 읽는 것과 동일한
``src/HeadlessDCGO.Engine/Assets/CardBaseEntity/cards.json`` 하나다(이중 구현 금지).
브리지 핸드셰이크 vocab 교환(§4.1)이 붙으면 그쪽이 온라인 검증 경로가 되고,
이 모듈은 오프라인(로더·도구) 경로로 남는다.
"""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Iterable, Mapping

# 일러스트 변형 접미사: cards.json에 실존하는 형태는 _P0, _P1, _P2 … (예: ST1_01_P1, BT10_005_P0).
# 프로모 카드번호(P_016 등)는 접미사가 아니므로 collapse 대상이 아니다.
_VARIANT_SUFFIX = re.compile(r"_P\d+$")

DIGITAMA_CARD_TYPE = "DigiEgg"


def canonical_card_number(raw: str) -> str:
    """외부 표기 → 내부 표준: 공백 제거, 대문자화, 하이픈→언더스코어, 일러 변형 collapse.

    예: `` bt1-010_P1 `` → ``BT1_010``  (FR-3.2 단계 2·3)
    """
    normalized = raw.strip().upper().replace("-", "_")
    return _VARIANT_SUFFIX.sub("", normalized)


def default_cards_json_path() -> Path:
    """저장소 표준 위치의 cards.json (rl/dcgo_rl/cards.py 기준 상대 해석)."""
    repo_root = Path(__file__).resolve().parents[2]
    return repo_root / "src" / "HeadlessDCGO.Engine" / "Assets" / "CardBaseEntity" / "cards.json"


class CardIndex:
    """cards.json의 카드 존재·타입 조회 (canonical 카드번호 기준)."""

    def __init__(self, records: Iterable[Mapping[str, object]]):
        # 원본 카드번호 → 타입. 중복 카드번호는 뒤 레코드가 이긴다(엔진 CardDatabase.Upsert와 동일).
        raw_types: dict[str, str] = {}
        for record in records:
            raw_types[str(record["cardNumber"])] = str(record.get("cardType", "Unknown"))

        # canonical → 타입. canonical 엔트리 자신의 타입이 우선, 변형 엔트리는 빈자리만 채운다.
        self._types: dict[str, str] = {}
        for number, card_type in raw_types.items():
            canonical = canonical_card_number(number)
            if number == canonical:
                self._types[canonical] = card_type
            else:
                self._types.setdefault(canonical, card_type)

    @classmethod
    def load(cls, path: Path | None = None) -> "CardIndex":
        cards_path = path or default_cards_json_path()
        with open(cards_path, encoding="utf-8") as f:
            return cls(json.load(f))

    def __contains__(self, canonical: str) -> bool:
        return canonical in self._types

    def __len__(self) -> int:
        return len(self._types)

    def card_type(self, canonical: str) -> str:
        return self._types[canonical]

    def is_digitama(self, canonical: str) -> bool:
        return self._types[canonical] == DIGITAMA_CARD_TYPE

    def canonical_numbers(self) -> tuple[str, ...]:
        """결정적 순서(정렬)의 canonical 카드번호 전체 — vocab 구축 입력."""
        return tuple(sorted(self._types))


class CardVocabulary:
    """카드번호 → 정수 id (append-only, 설계 §4.3 / FR-5.3).

    - id 0 = PAD(빈 슬롯) 예약. 실카드는 1부터.
    - ``extend``는 기존 id를 절대 바꾸지 않고 새 카드만 뒤에 잇는다(기존 임베딩 보존 전제).
    """

    PAD_ID = 0

    def __init__(self, version: str, ids: Mapping[str, int]):
        self.version = version
        self._ids = dict(ids)

    @classmethod
    def build(cls, canonical_numbers: Iterable[str], version: str = "v1") -> "CardVocabulary":
        ordered = sorted(set(canonical_numbers))
        return cls(version, {number: i + 1 for i, number in enumerate(ordered)})

    def id_of(self, canonical: str) -> int:
        return self._ids[canonical]  # 미등록 = KeyError (조용한 fallback 금지)

    def __contains__(self, canonical: str) -> bool:
        return canonical in self._ids

    def __len__(self) -> int:
        return len(self._ids)

    def extend(self, new_numbers: Iterable[str], version: str) -> "CardVocabulary":
        """신카드 append — 기존 매핑 불변, 신규는 기존 max id 뒤에 정렬 순서로."""
        fresh = sorted(set(new_numbers) - set(self._ids))
        next_id = max(self._ids.values(), default=0) + 1
        merged = dict(self._ids)
        for offset, number in enumerate(fresh):
            merged[number] = next_id + offset
        return CardVocabulary(version, merged)

    def to_json(self) -> dict:
        return {"version": self.version, "ids": dict(sorted(self._ids.items(), key=lambda kv: kv[1]))}

    @classmethod
    def from_json(cls, data: Mapping) -> "CardVocabulary":
        return cls(str(data["version"]), {str(k): int(v) for k, v in data["ids"].items()})
