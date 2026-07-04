import random
import unittest

from dcgo_rl.decks.providers import FixedPoolProvider
from dcgo_rl.decks.recipe import Recipe, RecipeEntry


def recipe(name: str) -> Recipe:
    return Recipe(name=name, source="operator", main=(RecipeEntry("ST1_02", 4),), digitama=())


class FixedPoolProviderTests(unittest.TestCase):
    def test_requires_non_empty_pool(self):
        with self.assertRaises(ValueError):
            FixedPoolProvider([])

    def test_deterministic_under_seeded_rng(self):
        provider = FixedPoolProvider([recipe("a"), recipe("b"), recipe("c")])
        rng_a, rng_b = random.Random(123), random.Random(123)
        seq_a = [provider.next_matchup(rng_a) for _ in range(20)]
        seq_b = [provider.next_matchup(rng_b) for _ in range(20)]
        self.assertEqual(seq_a, seq_b)  # 같은 시드 = 같은 매치업 열 (NFR-3)

    def test_mirror_matchups_allowed(self):
        provider = FixedPoolProvider([recipe("solo")])
        deck_a, deck_b = provider.next_matchup(random.Random(1))
        self.assertEqual(deck_a.name, "solo")
        self.assertEqual(deck_b.name, "solo")

    def test_report_result_is_noop(self):
        provider = FixedPoolProvider([recipe("a")])
        provider.report_result("m-1", {"winner": 0})  # GA 전용 채널 — 예외 없이 무시


if __name__ == "__main__":
    unittest.main()
