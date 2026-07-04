import random
import tempfile
import unittest
from pathlib import Path

from dcgo_rl.league.matchup import MatchupMatrix
from dcgo_rl.league.rating import EloBook
from dcgo_rl.league.sampler import OpponentSampler
from dcgo_rl.league.snapshots import SnapshotStore


class EloTests(unittest.TestCase):
    def test_win_moves_ratings_symmetrically(self):
        book = EloBook(k=32)
        book.update("a", "b", 1.0)
        self.assertGreater(book.rating("a"), 1200)
        self.assertLess(book.rating("b"), 1200)
        self.assertAlmostEqual(book.rating("a") + book.rating("b"), 2400, places=6)  # 제로섬

    def test_upset_moves_more_than_expected_win(self):
        book = EloBook(k=32)
        book.set_rating("strong", 1400)
        book.set_rating("weak", 1000)
        before = book.rating("weak")
        book.update("weak", "strong", 1.0)  # 업셋
        upset_gain = book.rating("weak") - before

        book2 = EloBook(k=32)
        book2.set_rating("strong", 1000)
        book2.set_rating("weak", 1400)
        before2 = book2.rating("weak")
        book2.update("weak", "strong", 1.0)  # 예상된 승리
        expected_gain = book2.rating("weak") - before2
        self.assertGreater(upset_gain, expected_gain)

    def test_save_load_roundtrip(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "ratings.json"
            book = EloBook()
            book.update("a", "b", 1.0)
            book.save(path)
            self.assertAlmostEqual(EloBook.load(path).rating("a"), book.rating("a"))


class MatchupMatrixTests(unittest.TestCase):
    def test_symmetric_record_and_winrate(self):
        with tempfile.TemporaryDirectory() as tmp:
            matrix = MatchupMatrix(Path(tmp) / "m.sqlite")
            matrix.record("me", "x", 1.0)
            matrix.record("me", "x", 1.0)
            matrix.record("me", "x", 0.0)
            self.assertEqual(3, matrix.games("me", "x"))
            self.assertEqual(3, matrix.games("x", "me"))
            self.assertAlmostEqual(2 / 3, matrix.winrate("me", "x"))
            self.assertAlmostEqual(1 / 3, matrix.winrate("x", "me"))
            matrix.close()

    def test_weakest_filters_by_min_games_and_sorts(self):
        with tempfile.TemporaryDirectory() as tmp:
            matrix = MatchupMatrix(Path(tmp) / "m.sqlite")
            for _ in range(5):
                matrix.record("me", "easy", 1.0)
            for _ in range(5):
                matrix.record("me", "hard", 0.0)
            matrix.record("me", "rare", 0.0)  # 표본 1 — min_games=5 미달

            weakest = matrix.weakest("me", ["easy", "hard", "rare"], min_games=5)
            self.assertEqual([("hard", 0.0), ("easy", 1.0)], weakest)
            matrix.close()


class SamplerTests(unittest.TestCase):
    def test_cold_start_falls_back_to_random_then_switches_to_weakness(self):
        with tempfile.TemporaryDirectory() as tmp:
            matrix = MatchupMatrix(Path(tmp) / "m.sqlite")
            ratings = EloBook()
            sampler = OpponentSampler(weakness_min_games=3)
            rng = random.Random(1)
            pool = ["s1", "s2"]

            modes = {sampler.sample("learner", pool, ratings, matrix, rng)[1] for _ in range(50)}
            self.assertIn("random", modes)      # 콜드스타트 폴백 발생
            self.assertNotIn("weakness", modes)  # 표본 없인 약점 축 없음

            for _ in range(3):
                matrix.record("learner", "s2", 0.0)  # s2가 약점(0%)
            picks = [sampler.sample("learner", pool, ratings, matrix, rng) for _ in range(100)]
            weakness_picks = [p for p, mode in picks if mode == "weakness"]
            self.assertTrue(weakness_picks, "표본 축적 후 약점 모드로 전환")
            self.assertTrue(all(p == "s2" for p in weakness_picks), "약점 모드는 최저 승률 상대")
            matrix.close()

    def test_near_mode_respects_rating_window(self):
        with tempfile.TemporaryDirectory() as tmp:
            matrix = MatchupMatrix(Path(tmp) / "m.sqlite")
            ratings = EloBook()
            ratings.set_rating("learner", 1200)
            ratings.set_rating("near", 1250)
            ratings.set_rating("far", 2000)
            sampler = OpponentSampler(near_rating=1.0, weakness=0.0, rating_window=200)
            rng = random.Random(2)
            for _ in range(20):
                opponent, mode = sampler.sample("learner", ["near", "far"], ratings, matrix, rng)
                self.assertEqual(("near", "near"), (opponent, mode))
            matrix.close()


class FakeModel:
    def save(self, path: str) -> None:
        Path(path).write_bytes(b"fake-policy")


class SnapshotStoreTests(unittest.TestCase):
    def test_save_list_and_lookup(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = SnapshotStore(Path(tmp) / "snapshots")
            meta = {
                "snapshot_id": "lin-s001", "lineage": "lin", "global_step": 1000,
                "rating": 1234.5, "obs_schema_hash": "h", "vocab_version": "v1",
                "arch": "mlp", "deck_context": ["starter:ST1"],
            }
            store.save(FakeModel(), meta)

            metas = store.list_metas()
            self.assertEqual(1, len(metas))
            self.assertEqual("lin-s001", metas[0]["snapshot_id"])
            self.assertIn("frozen_at", metas[0])
            self.assertTrue(store.policy_path("lin-s001").exists())

    def test_missing_meta_fields_fail(self):
        with tempfile.TemporaryDirectory() as tmp:
            store = SnapshotStore(Path(tmp) / "snapshots")
            with self.assertRaises(ValueError):
                store.save(FakeModel(), {"snapshot_id": "x"})


if __name__ == "__main__":
    unittest.main()
