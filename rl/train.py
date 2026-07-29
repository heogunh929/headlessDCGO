"""L0 스모크 학습 (roadmap M4/L0, pump-era 재조준 B3): MaskablePPO + 카드-ID 임베딩, 상대=랜덤 합법 정책.

전제: dotnet build tools/RlBridgeHost/RlBridgeHost.csproj -c Release (호스트 빌드).
사용: python train.py [--steps 30000] [--n-envs 8] [--vec subproc] [--seed 42]
                      [--recipes decks/red_st1_bt.json ...] [--out ../runs/l0-pump]

게이트(B3): 스모크 전 구간 무크래시 + eval 승률이 랜덤(≈50%) 대비 통계적 우위 + 관측/액션 계약 무변.
--recipes 를 주면 매 에피소드 FixedPoolProvider 가 레시피 풀에서 매치업을 시드-결정적으로 샘플한다.
"""

from __future__ import annotations

import argparse
import json
import time
from pathlib import Path

from sb3_contrib import MaskablePPO
from sb3_contrib.common.wrappers import ActionMasker
from stable_baselines3.common.vec_env import DummyVecEnv, SubprocVecEnv

from dcgo_rl.bridge import BridgeClient
from dcgo_rl.cards import CardIndex
from dcgo_rl.decks.providers import FixedPoolProvider
from dcgo_rl.decks.recipe import load_recipe_file
from dcgo_rl.envs import DcgoSeatEnv
from dcgo_rl.policy.extractor import CardEmbeddingExtractor, card_id_indices
from evaluate import evaluate_winrate


def load_recipe_pool(paths: list[str]) -> FixedPoolProvider:
    index = CardIndex.load()
    return FixedPoolProvider([load_recipe_file(Path(p).resolve(), index) for p in paths])


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--steps", type=int, default=30_000)
    parser.add_argument("--n-envs", type=int, default=8)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--eval-matches", type=int, default=120)
    parser.add_argument("--eval-jobs", type=int, default=6, help="평가 병렬 워커 수 (워커당 호스트 1개, RL 워커=6)")
    parser.add_argument("--recipes", nargs="*", default=None,
                        help="덱 레시피 파일들(내부 JSON/외부 텍스트). 비면 starter:ST1 vs ST2 고정")
    parser.add_argument("--out", type=str, default="../runs/l0-pump")
    parser.add_argument("--log-level", choices=["OFF", "RESULT", "REPLAY", "ANALYSIS", "TRACE"],
                        default="OFF", help="매치 이벤트 로그 레벨(RlBridgeHost). OFF=끔")
    parser.add_argument("--event-log", type=str, default=None,
                        help="이벤트 로그 파일 경로 접두(비면 out/event-env<rank>.jsonl)")
    parser.add_argument("--vec", choices=["dummy", "subproc"], default="dummy",
                        help="subproc = env 스테핑을 워커 프로세스로 병렬화(env 수만큼 스루풋 확장)")
    args = parser.parse_args()

    # 스키마/vocab 프로브(cardId 채널 인덱스는 호스트 describe가 진실 — 이중 구현 금지).
    probe = BridgeClient()
    feature_names = probe.describe()
    card_indices = card_id_indices(feature_names)
    vocab_size = probe.vocab_size
    obs_schema_hash = probe.welcome["obsSchemaHash"]
    vocab_version = probe.welcome["vocabVersion"]
    probe_action_size = probe.action_size
    probe.close()
    print(f"obs={len(feature_names)}(+2 seat) action={probe_action_size} "
          f"cardId channels={len(card_indices)} vocab={vocab_size} obsSchemaHash={obs_schema_hash}")

    out_dir = Path(args.out).resolve()  # 호스트는 자기 cwd 기준으로 로그 경로를 해석 — 절대경로로 넘긴다.
    out_dir.mkdir(parents=True, exist_ok=True)

    recipe_paths = [str(Path(p).resolve()) for p in (args.recipes or [])]

    def make_env(rank: int):
        def _thunk():
            env = DcgoSeatEnv(
                experiment_seed=args.seed * 1000 + rank,
                result_log=str(out_dir / f"results-env{rank}.jsonl"),
                log_level=args.log_level,
                event_log=(str(out_dir / f"event-env{rank}.jsonl") if args.log_level != "OFF" else None),
                deck_provider=(load_recipe_pool(recipe_paths) if recipe_paths else None),
            )
            return ActionMasker(env, lambda e: e.action_masks())

        return _thunk

    vec_cls = SubprocVecEnv if args.vec == "subproc" else DummyVecEnv
    vec_env = vec_cls([make_env(rank) for rank in range(args.n_envs)])

    model = MaskablePPO(
        "MlpPolicy",
        vec_env,
        seed=args.seed,
        n_steps=256,
        batch_size=256,
        verbose=1,
        policy_kwargs={
            "features_extractor_class": CardEmbeddingExtractor,
            "features_extractor_kwargs": {
                "card_indices": card_indices,
                "vocab_size": vocab_size,
            },
        },
    )

    started = time.time()
    model.learn(total_timesteps=args.steps)
    elapsed = time.time() - started
    steps_per_sec = args.steps / elapsed
    vec_env.close()

    model_path = out_dir / "policy.zip"
    model.save(model_path)

    print(f"\ntraining: {args.steps} steps in {elapsed:.0f}s -> {steps_per_sec:.1f} steps/sec "
          f"({args.n_envs} envs, {vec_cls.__name__}, json+stdio)")

    eval_report = evaluate_winrate(
        model,
        n_matches=args.eval_matches,
        experiment_seed=args.seed + 777,
        deck_provider=(load_recipe_pool(recipe_paths) if recipe_paths else None),
        n_jobs=args.eval_jobs,
    )
    lo, hi = eval_report["ci95"]
    print(f"eval vs random: {eval_report['winrate']:.1%} "
          f"({eval_report['wins']}W/{eval_report['losses']}L, truncated {eval_report['truncated']}) "
          f"CI95=[{lo:.1%}, {hi:.1%}] over {args.eval_matches} matches")

    # 스냅샷 메타 최소형(dev design §5.1 선반영) — L1 SnapshotStore가 이 포맷을 승계한다.
    meta = {
        "snapshot_id": "l0-pump",
        "global_step": args.steps,
        "obs_schema_hash": obs_schema_hash,
        "vocab_version": vocab_version,
        "obs_size": len(feature_names),
        "action_size": probe_action_size,
        "arch": "mlp+card-embed",
        "deck_context": (
            [Path(p).stem for p in recipe_paths] if recipe_paths else ["starter:ST1", "starter:ST2"]
        ),
        "opponent": "random-legal",
        "train_steps_per_sec": round(steps_per_sec, 1),
        "eval_winrate_vs_random": eval_report["winrate"],
        "eval_ci95": eval_report["ci95"],
        "eval_record": {k: eval_report[k] for k in ("wins", "losses", "completed", "truncated")},
        "eval_matches": args.eval_matches,
    }
    (out_dir / "meta.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    print(f"saved: {model_path} + meta.json")


if __name__ == "__main__":
    main()
