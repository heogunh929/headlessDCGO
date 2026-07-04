import unittest

from dcgo_rl.cards import CardIndex, CardVocabulary, canonical_card_number, default_cards_json_path


class CanonicalCardNumberTests(unittest.TestCase):
    def test_hyphen_to_underscore_and_upper(self):
        self.assertEqual(canonical_card_number(" bt1-020 "), "BT1_020")

    def test_variant_suffix_collapse(self):
        self.assertEqual(canonical_card_number("ST1-01_P1"), "ST1_01")
        self.assertEqual(canonical_card_number("BT10_005_P0"), "BT10_005")

    def test_promo_card_number_not_collapsed(self):
        # 프로모 카드번호 P_016은 접미사가 아니다 — collapse 대상 아님.
        self.assertEqual(canonical_card_number("P-016"), "P_016")


class CardIndexRealDataTests(unittest.TestCase):
    """실데이터(cards.json) 접지 — 엔진과 같은 파일을 읽는다."""

    @classmethod
    def setUpClass(cls):
        cls.index = CardIndex.load()

    def test_known_cards_present(self):
        self.assertIn("ST1_01", self.index)
        self.assertIn("AD1_023", self.index)

    def test_card_types(self):
        self.assertEqual(self.index.card_type("AD1_023"), "Tamer")
        self.assertTrue(self.index.is_digitama("BT10_002"))

    def test_variant_collapses_to_existing_canonical(self):
        # cards.json의 변형 엔트리(ST1_01_P1)는 canonical(ST1_01)로 접힌다.
        self.assertIn(canonical_card_number("ST1_01_P1"), self.index)

    def test_canonical_numbers_deterministic_and_deduped(self):
        numbers = self.index.canonical_numbers()
        self.assertEqual(list(numbers), sorted(numbers))
        self.assertEqual(len(numbers), len(set(numbers)))
        self.assertNotIn("ST1_01_P1", numbers)


class CardVocabularyTests(unittest.TestCase):
    def test_build_deterministic_ids_from_one(self):
        vocab = CardVocabulary.build(["B_002", "A_001", "B_002"], version="v1")
        self.assertEqual(len(vocab), 2)
        self.assertEqual(vocab.id_of("A_001"), 1)  # id 0 = PAD 예약
        self.assertEqual(vocab.id_of("B_002"), 2)

    def test_unknown_card_raises(self):
        vocab = CardVocabulary.build(["A_001"])
        with self.assertRaises(KeyError):
            vocab.id_of("Z_999")

    def test_extend_is_append_only(self):
        v1 = CardVocabulary.build(["A_001", "B_002"], version="v1")
        v2 = v1.extend(["C_003", "A_001"], version="v2")  # 기존 카드 재등록은 무시
        self.assertEqual(v2.version, "v2")
        self.assertEqual(v2.id_of("A_001"), v1.id_of("A_001"))  # 기존 id 불변 (FR-5.3)
        self.assertEqual(v2.id_of("B_002"), v1.id_of("B_002"))
        self.assertEqual(v2.id_of("C_003"), 3)
        self.assertEqual(len(v1), 2)  # 원본 불변

    def test_json_roundtrip(self):
        v1 = CardVocabulary.build(["A_001", "B_002"], version="v1")
        restored = CardVocabulary.from_json(v1.to_json())
        self.assertEqual(restored.version, "v1")
        self.assertEqual(restored.id_of("B_002"), v1.id_of("B_002"))


if __name__ == "__main__":
    unittest.main()
