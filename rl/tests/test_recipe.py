import unittest

from dcgo_rl.cards import CardIndex
from dcgo_rl.decks.recipe import Recipe, RecipeError, parse_external


class RealDataIntegrationTests(unittest.TestCase):
    """실제 cards.json 인덱스로 외부 포맷 전체 경로 스모크 (ST1 실카드)."""

    def test_external_st1_lines_against_real_index(self):
        index = CardIndex.load()
        lines = ["// ST-1 sample", "4 ST1-01", "4 ST1-02_P1", "ST1-12 x4", "2 ST1-16"]
        recipe = parse_external(lines, index, name="st1_sample")
        self.assertEqual([(e.card, e.count) for e in recipe.digitama], [("ST1_01", 4)])
        self.assertEqual(
            [(e.card, e.count) for e in recipe.main],
            [("ST1_02", 4), ("ST1_12", 4), ("ST1_16", 2)],
        )


def fake_index() -> CardIndex:
    return CardIndex(
        [
            {"cardNumber": "ST1_01", "cardType": "DigiEgg"},
            {"cardNumber": "ST1_02", "cardType": "Digimon"},
            {"cardNumber": "ST1_03", "cardType": "Digimon"},
            {"cardNumber": "ST1_12", "cardType": "Tamer"},
            {"cardNumber": "ST1_14", "cardType": "Option"},
        ]
    )


class ParseExternalTests(unittest.TestCase):
    def test_full_pipeline(self):
        lines = [
            "// Digimon DeckList",       # 헤더 (단계 1)
            "Exported by SomeTool",       # 헤더 — 카드 토큰 없음
            "",
            "ST1-01",                     # 하이픈 → 언더스코어 (단계 2)
            "ST1-02_P1",                  # 일러 변형 collapse (단계 3)
            "ST1-02",                     # 중복 = 매수 집계 (단계 4)
            "4 ST1-03",                   # 행 앞 매수 표기
            "ST1-12 x2",                  # 행 뒤 매수 표기
            "st1-14",                     # 소문자 허용
        ]
        recipe = parse_external(lines, fake_index(), name="smoke")

        self.assertEqual([(e.card, e.count) for e in recipe.digitama], [("ST1_01", 1)])  # 타입 분리 (단계 5)
        self.assertEqual(
            [(e.card, e.count) for e in recipe.main],
            [("ST1_02", 2), ("ST1_03", 4), ("ST1_12", 2), ("ST1_14", 1)],  # 첫 등장 순서 보존
        )
        self.assertEqual(recipe.main_count, 9)
        self.assertEqual(recipe.digitama_count, 1)

    def test_unknown_cards_fail_explicitly_with_all_numbers(self):
        lines = ["ST1-01", "BT9-999", "EX9-888", "BT9-999"]
        with self.assertRaises(RecipeError) as ctx:
            parse_external(lines, fake_index(), name="bad")
        self.assertEqual(ctx.exception.unknown_cards, ("BT9_999", "EX9_888"))  # 전부 모아 한 번에 (단계 6)
        self.assertIn("BT9_999", str(ctx.exception))

    def test_all_cards_multiset_for_deck_identity_channel(self):
        recipe = parse_external(["ST1-02", "ST1-02", "ST1-01"], fake_index(), name="m")
        self.assertEqual(sorted(recipe.all_cards()), ["ST1_01", "ST1_02", "ST1_02"])

    def test_json_roundtrip(self):
        recipe = parse_external(["ST1-01", "3 ST1-02"], fake_index(), name="rt")
        restored = Recipe.from_json(recipe.to_json())
        self.assertEqual(restored, recipe)
        # §3.1 내부 표준 키 고정
        self.assertEqual(set(recipe.to_json()), {"name", "source", "main", "digitama"})


if __name__ == "__main__":
    unittest.main()
