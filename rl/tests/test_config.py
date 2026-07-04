import unittest
from pathlib import Path

from dcgo_rl.config import ConfigError, ExperimentConfig, load_experiment_config

RL_ROOT = Path(__file__).resolve().parents[1]


def base_config() -> dict:
    return {
        "experiment": "t",
        "seed": 1,
        "deck_source": {"type": "fixed", "recipes": ["decks/a.json"]},
        "policy": {"arch": "mlp", "init": None, "learn": True},
        "reward": {"type": "terminal"},
        "league": {"enabled": False},
        "log_level": "RESULT",
        "parallel": {"n_envs": 2},
    }


class ExperimentConfigTests(unittest.TestCase):
    def test_example_config_loads(self):
        config = load_experiment_config(RL_ROOT / "configs" / "l0_fixed_pair.yaml")
        self.assertEqual(config.experiment, "l0-fixed-pair")
        self.assertEqual(config.seed, 42)
        self.assertEqual(config.deck_source.type, "fixed")
        self.assertEqual(len(config.deck_source.recipes), 2)
        self.assertEqual(config.policy.arch, "mlp")
        self.assertTrue(config.policy.learn)
        self.assertFalse(config.league.enabled)
        self.assertEqual(config.log_level, "RESULT")

    def test_unknown_key_rejected(self):
        data = base_config()
        data["typo_key"] = 1
        with self.assertRaises(ConfigError):
            ExperimentConfig.parse(data)

    def test_unknown_nested_key_rejected(self):
        data = base_config()
        data["policy"]["archh"] = "mlp"
        with self.assertRaises(ConfigError):
            ExperimentConfig.parse(data)

    def test_fixed_requires_recipes(self):
        data = base_config()
        data["deck_source"] = {"type": "fixed", "recipes": []}
        with self.assertRaises(ConfigError):
            ExperimentConfig.parse(data)

    def test_non_fixed_must_not_set_recipes(self):
        data = base_config()
        data["deck_source"] = {"type": "random", "recipes": ["decks/a.json"]}
        with self.assertRaises(ConfigError):
            ExperimentConfig.parse(data)

    def test_eval_mode_requires_init_snapshot(self):
        data = base_config()
        data["policy"] = {"arch": "mlp", "init": None, "learn": False}
        with self.assertRaises(ConfigError):
            ExperimentConfig.parse(data)

    def test_bad_log_level_rejected(self):
        data = base_config()
        data["log_level"] = "VERBOSE"
        with self.assertRaises(ConfigError):
            ExperimentConfig.parse(data)

    def test_sampling_ratio_must_sum_to_one(self):
        data = base_config()
        data["league"] = {"enabled": True, "sampling": {"near_rating": 0.9, "weakness": 0.2}}
        with self.assertRaises(ConfigError):
            ExperimentConfig.parse(data)

    def test_league_defaults_applied(self):
        config = ExperimentConfig.parse(base_config())
        self.assertEqual(config.league.freeze.every_steps, 2_000_000)
        self.assertEqual(config.league.sampling.near_rating, 0.8)
        self.assertEqual(config.league.sampling.weakness_min_games, 200)


if __name__ == "__main__":
    unittest.main()
