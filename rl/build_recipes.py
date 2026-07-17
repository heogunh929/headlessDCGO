"""B3 — 209장 포팅 풀에서 학습용 덱 레시피 생성 (클린 풀만, STOP 카드 제외).

풀 산출은 엔진 dispatch 규약을 미러한다(CardEffectDispatch: 카드번호=클래스명인
CEntity_Effect 서브클래스 = 포팅; skeleton은 클래스 부재로 자연 제외). STOP 마커
카드는 롤아웃 중 NotSupportedException 소스이므로 학습 풀에서 뺀다(퍼징 캠페인 B4는
반대로 이들을 포함해 수확한다).

덱 규칙(AS-IS DeckBuildingRule/EditDeck: "50+5", 기본 4장 제한):
  메인 정확히 50장, 카드당 최대 4장, 디지타마 덱 최대 5장.

구성: 모노컬러 4종(적/청/황/녹 — ST1/ST2/ST3/ST4 스타터 코어 + 동색 BT 필)
     + 잔여 커버리지 덱(미사용 클린 카드 2장씩, 전 카드가 최소 1덱에 등장할 때까지).

사용: python build_recipes.py [--out decks]   (rl/ 에서 실행; 결정적 — diff 가능)
"""

from __future__ import annotations

import argparse
import json
import re
from collections import OrderedDict
from pathlib import Path

from dcgo_rl.cards import default_cards_json_path

CARD_NUMBER_CLASS = re.compile(r"^(ST|BT|EX|LM|P|AD)\d*_\d+$")

# 파일에 STOP 마커가 없어도 인프라 경유로 NotSupportedException을 격발하는 잠복-STOP 카드
# (B3 검증 롤아웃 실측). 퍼징 캠페인(B4)에서는 반대로 포함해 수확한다.
LATENT_STOP = {
    "AD1_025": "AddAssemblyConditionClass 부여 → CardController Assembly play arm throw (RD-P6C1-5)",
}

# StarterDecks.cs (엔진 정본)의 공식 매수 — 클린 풀에 있는 엔트리만 실제로 쓰인다.
STARTER_MAIN_COUNTS = {
    "ST1": [("ST1_02", 4), ("ST1_03", 4), ("ST1_04", 4), ("ST1_05", 4), ("ST1_06", 4), ("ST1_07", 2),
            ("ST1_08", 4), ("ST1_09", 4), ("ST1_10", 2), ("ST1_11", 2), ("ST1_12", 4), ("ST1_13", 4),
            ("ST1_14", 4), ("ST1_15", 2), ("ST1_16", 2)],
    "ST2": [("ST2_02", 4), ("ST2_03", 4), ("ST2_04", 4), ("ST2_05", 4), ("ST2_06", 2), ("ST2_07", 4),
            ("ST2_08", 4), ("ST2_09", 4), ("ST2_10", 2), ("ST2_11", 2), ("ST2_12", 4), ("ST2_13", 4),
            ("ST2_14", 4), ("ST2_15", 2), ("ST2_16", 2)],
    "ST3": [("ST3_02", 4), ("ST3_03", 4), ("ST3_04", 4), ("ST3_05", 2), ("ST3_06", 4), ("ST3_07", 4),
            ("ST3_08", 4), ("ST3_09", 4), ("ST3_10", 2), ("ST3_11", 2), ("ST3_12", 4), ("ST3_13", 4),
            ("ST3_14", 2), ("ST3_15", 4), ("ST3_16", 2)],
    # ST4는 엔진 StarterDecks 미수록 — 포팅된 ST4 카드를 4장씩 코어로 쓴다.
    "ST4": [(f"ST4_{n:02d}", 4) for n in range(2, 17)],
}

MAIN_DECK_SIZE = 50
DIGITAMA_MAX = 5
COPY_LIMIT = 4


def card_effect_root() -> Path:
    return (Path(__file__).resolve().parents[1]
            / "src" / "HeadlessDCGO.Engine" / "Assets" / "Scripts" / "CardEffect")


def scan_pool() -> tuple[list[str], list[str]]:
    """(클린 카드번호들, STOP 카드번호들) — 포팅 = 파일명과 동명의 class 정의 존재."""
    clean: list[str] = []
    stopped: list[str] = []
    for path in sorted(card_effect_root().rglob("*.cs")):
        name = path.stem
        if not CARD_NUMBER_CLASS.match(name):
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        if not re.search(rf"class {re.escape(name)}[^A-Za-z0-9_]", text):
            continue  # skeleton — 클래스 부재
        (stopped if ("STOP" in text or name in LATENT_STOP) else clean).append(name)
    return clean, stopped


def load_card_meta() -> dict[str, dict]:
    with open(default_cards_json_path(), encoding="utf-8") as f:
        records = json.load(f)
    meta: dict[str, dict] = {}
    for record in records:
        meta.setdefault(str(record["cardNumber"]), record)
    return meta


def colors_of(card: dict) -> list[str]:
    return list(card.get("colors") or ([card["color"]] if card.get("color") else []))


def template_count(card: dict) -> int:
    if card["cardType"] == "Digimon":
        level = int(card.get("level") or 0)
        return {3: 4, 4: 4, 5: 3, 6: 2, 7: 2}.get(level, 2)
    return 2  # Option / Tamer


def clip_to_size(entries: "OrderedDict[str, int]", size: int) -> OrderedDict:
    """누적 매수를 정확히 size로 — 뒤에서부터 줄인다(코어 우선 보존)."""
    total = sum(entries.values())
    if total < size:
        raise ValueError(f"deck underfilled: {total} < {size}")
    for card in reversed(list(entries)):
        while total > size and entries[card] > 0:
            entries[card] -= 1
            total -= 1
        if entries[card] == 0:
            del entries[card]
    return entries


def build_mono_deck(color: str, starter: str, clean: set[str], meta: dict[str, dict],
                    egg_queue: list[str]) -> dict:
    main: OrderedDict[str, int] = OrderedDict()
    for number, count in STARTER_MAIN_COUNTS[starter]:
        if number in clean:
            main[number] = min(count, COPY_LIMIT)

    fillers = sorted(
        (n for n in clean
         if color in colors_of(meta[n])
         and meta[n]["cardType"] != "DigiEgg"
         and n not in main),
        key=lambda n: (meta[n]["cardType"] != "Digimon", int(meta[n].get("level") or 0), n),
    )
    for number in fillers:
        if sum(main.values()) >= MAIN_DECK_SIZE:
            break
        main[number] = template_count(meta[number])
    clip_to_size(main, MAIN_DECK_SIZE)

    digitama: OrderedDict[str, int] = OrderedDict()
    starter_egg = f"{starter}_01"
    if starter_egg in clean:
        digitama[starter_egg] = 4
        if starter_egg in egg_queue:
            egg_queue.remove(starter_egg)
    for number in list(egg_queue):
        if sum(digitama.values()) >= DIGITAMA_MAX:
            break
        if color in colors_of(meta[number]):
            digitama[number] = 1
            egg_queue.remove(number)

    return {
        "name": f"{color.lower()}_{starter.lower()}_bt",
        "source": "operator",
        "main": [{"card": c, "count": k} for c, k in main.items()],
        "digitama": [{"card": c, "count": k} for c, k in digitama.items()],
    }


def build_coverage_decks(unused: list[str], meta: dict[str, dict],
                         egg_queue: list[str], fallback_eggs: list[str],
                         all_mains: list[str]) -> list[dict]:
    decks = []
    mains = [n for n in unused if meta[n]["cardType"] != "DigiEgg"]
    index = 0
    while index < len(mains):
        main: OrderedDict[str, int] = OrderedDict()
        while index < len(mains) and sum(main.values()) < MAIN_DECK_SIZE:
            number = mains[index]
            main[number] = 2
            index += 1
        while sum(main.values()) < MAIN_DECK_SIZE:  # 마지막 덱 부족분은 매수 증량으로 채움
            grown = False
            for number in main:
                if main[number] < COPY_LIMIT and sum(main.values()) < MAIN_DECK_SIZE:
                    main[number] += 1
                    grown = True
            if not grown:
                break
        for number in all_mains:  # 그래도 모자라면 기-커버 카드로 패드(50장 규칙이 우선)
            if sum(main.values()) >= MAIN_DECK_SIZE:
                break
            if number not in main:
                main[number] = min(2, MAIN_DECK_SIZE - sum(main.values()))
        clip_to_size(main, MAIN_DECK_SIZE)

        digitama: OrderedDict[str, int] = OrderedDict()
        while egg_queue and sum(digitama.values()) < DIGITAMA_MAX - 1:
            number = egg_queue.pop(0)
            digitama[number] = min(3, DIGITAMA_MAX - sum(digitama.values()) - 1)
        if not digitama:  # 남은 미사용 알이 없으면 이미 쓴 알 재사용(적법)
            digitama[fallback_eggs[len(decks) % len(fallback_eggs)]] = 4

        decks.append({
            "name": f"coverage_rest_{len(decks) + 1}",
            "source": "operator",
            "main": [{"card": c, "count": k} for c, k in main.items()],
            "digitama": [{"card": c, "count": k} for c, k in digitama.items()],
        })
    return decks


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=str, default="decks")
    args = parser.parse_args()

    clean_list, stop_list = scan_pool()
    clean = set(clean_list)
    meta = load_card_meta()
    missing = sorted(n for n in clean if n not in meta)
    if missing:
        raise SystemExit(f"clean cards missing from cards.json: {missing}")

    egg_queue = sorted(n for n in clean if meta[n]["cardType"] == "DigiEgg")
    decks = [
        build_mono_deck("Red", "ST1", clean, meta, egg_queue),
        build_mono_deck("Blue", "ST2", clean, meta, egg_queue),
        build_mono_deck("Yellow", "ST3", clean, meta, egg_queue),
        build_mono_deck("Green", "ST4", clean, meta, egg_queue),
    ]

    used = {e["card"] for d in decks for s in ("main", "digitama") for e in d[s]}
    unused = sorted(clean - used)
    fallback_eggs = sorted(n for n in used if meta[n]["cardType"] == "DigiEgg")
    all_mains = sorted(n for n in clean if meta[n]["cardType"] != "DigiEgg")
    decks += build_coverage_decks(unused, meta, egg_queue, fallback_eggs, all_mains)

    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    for deck in decks:
        path = out_dir / f"{deck['name']}.json"
        path.write_text(json.dumps(deck, indent=2) + "\n", encoding="utf-8")
        main_n = sum(e["count"] for e in deck["main"])
        egg_n = sum(e["count"] for e in deck["digitama"])
        print(f"{path}: main {main_n} ({len(deck['main'])} distinct) + digitama {egg_n}")

    covered = {e["card"] for d in decks for s in ("main", "digitama") for e in d[s]}
    print(f"\npool: ported {len(clean) + len(stop_list)} = clean {len(clean)} + STOP {len(stop_list)}")
    print(f"coverage: {len(covered)}/{len(clean)} clean cards in at least one deck")
    leftover = sorted(clean - covered)
    if leftover:
        print(f"uncovered: {leftover}")


if __name__ == "__main__":
    main()
