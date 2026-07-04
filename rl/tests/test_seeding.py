import unittest

from dcgo_rl.seeding import INT32_MASK, derive_match_seed


class SeedingTests(unittest.TestCase):
    def test_deterministic(self):
        self.assertEqual(derive_match_seed(42, 0), derive_match_seed(42, 0))

    def test_distinct_across_indices_and_experiments(self):
        seeds = {derive_match_seed(42, i) for i in range(1000)}
        self.assertEqual(len(seeds), 1000)  # 충돌 없음 (1000개 규모)
        self.assertNotEqual(derive_match_seed(42, 0), derive_match_seed(43, 0))

    def test_int32_positive_range(self):
        # C# EngineContext.CreateDefault(seed) 인자(Int32) 호환.
        for i in range(100):
            seed = derive_match_seed(7, i)
            self.assertGreaterEqual(seed, 0)
            self.assertLessEqual(seed, INT32_MASK)


if __name__ == "__main__":
    unittest.main()
