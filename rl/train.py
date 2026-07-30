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
import os
import signal
import subprocess
import time
from collections import Counter
from pathlib import Path

from sb3_contrib import MaskablePPO
from sb3_contrib.common.wrappers import ActionMasker
from stable_baselines3.common.callbacks import BaseCallback
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
    # graceful 정지: 명시적 핸들러 설치 — 비대화형 셸 배경 프로세스는 SIGINT가 SIG_IGN으로
    # 상속돼 기본 KeyboardInterrupt가 영영 안 온다(실측 2026-07-30). runner의 정지는 SIGTERM.
    def _graceful(signum, frame):
        raise KeyboardInterrupt

    signal.signal(signal.SIGINT, _graceful)
    signal.signal(signal.SIGTERM, _graceful)

    parser = argparse.ArgumentParser()
    parser.add_argument("--steps", type=int, default=30_000)
    parser.add_argument("--games", type=int, default=0,
                        help="완주할 판 수 — 지정 시 이 수의 판을 채우면 학습 종료(스텝은 상한으로만 동작)")
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
    parser.add_argument("--record-mode", default="accident",
                        help="판 기록 모드: off|all|accident|sample:N (설계 v1 §2, 기본=사고판만)")
    parser.add_argument("--checkpoint-every", type=int, default=2000, help="체크포인트 주기(스텝)")
    parser.add_argument("--checkpoint-keep", type=int, default=5, help="체크포인트 보존 개수")
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

    # 안전망 1 — 메타 2단계(설계 v1 §6): 시작 시 config·엔진 sha·상태를 먼저 박는다.
    try:
        engine_sha = subprocess.run(["git", "rev-parse", "--short=12", "HEAD"],
                                    capture_output=True, text=True, cwd=Path(__file__).parent).stdout.strip()
    except OSError:
        engine_sha = ""
    meta_path = out_dir / "meta.json"
    meta = {
        "status": "running",
        "started": time.strftime("%Y-%m-%dT%H:%M:%S%z"),
        "engine_sha": engine_sha,
        "config": {k: v for k, v in vars(args).items()},
        "obs_schema_hash": obs_schema_hash,
        "vocab_version": vocab_version,
    }
    meta_path.write_text(json.dumps(meta, indent=2), encoding="utf-8")

    # 안전망 2 — 호스트 stderr 상시 수집(abort 스택·swallowed census의 유일 회수 경로).
    os.environ.setdefault("DCGO_HOST_STDERR", str(out_dir / "host-stderr.log"))

    recipe_paths = [str(Path(p).resolve()) for p in (args.recipes or [])]

    def make_env(rank: int):
        def _thunk():
            env = DcgoSeatEnv(
                experiment_seed=args.seed * 1000 + rank,
                result_log=str(out_dir / f"results-env{rank}.jsonl"),
                log_level=args.log_level,
                event_log=(str(out_dir / f"event-env{rank}.jsonl") if args.log_level != "OFF" else None),
                deck_provider=(load_recipe_pool(recipe_paths) if recipe_paths else None),
                match_log_dir=str(out_dir / "matches"),
                record_mode=args.record_mode,
                engine_sha=engine_sha,
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

    # 안전망 3 — 체크포인트 + SIGINT graceful(설계 v1 §6). SB3의 KeyboardInterrupt 전파를 이용:
    # SIGINT 수신 → learn 탈출 → 최종 저장 경로로 합류. 체크포인트는 주기 저장·최근 K개 보존.
    class Checkpoint(BaseCallback):
        games_done = 0

        def _on_step(self) -> bool:
            if self.num_timesteps % args.checkpoint_every < vec_env.num_envs:
                ckdir = out_dir / "checkpoints"
                ckdir.mkdir(exist_ok=True)
                self.model.save(ckdir / f"step-{self.num_timesteps:08d}.zip")
                kept = sorted(ckdir.glob("step-*.zip"))
                for old in kept[:-args.checkpoint_keep]:
                    old.unlink()
            # 게임 단위 종료(사용자 요구 2026-07-30): done 신호로 완주 판수를 세어 N판에서 멈춘다.
            if args.games:
                self.games_done += int(sum(self.locals.get("dones", ())))
                if self.games_done >= args.games:
                    return False
            return True

    started = time.time()
    interrupted = False
    try:
        model.learn(total_timesteps=args.steps if not args.games else max(args.steps, args.games * 400),
                    callback=Checkpoint())
    except KeyboardInterrupt:
        interrupted = True
        print("SIGINT — graceful 정지: 현재 정책 저장 후 종료")
    elapsed = time.time() - started
    steps_per_sec = args.steps / elapsed

    # 저장이 close보다 먼저 — vec_env.close()는 워커 상태에 따라 remote.recv()에서 무기한 블록할 수
    # 있다(실측 2026-07-30, graceful 정지 중 행). 정책부터 확보하고 close는 방어적으로.
    model_path = out_dir / "policy.zip"
    model.save(model_path)

    # 안전망 4 — 결과 census(사유 분포)를 메타에 병합할 준비.
    reasons = Counter()
    for rl in out_dir.glob("results-env*.jsonl"):
        for line in rl.read_text(encoding="utf-8").splitlines():
            try:
                reasons[json.loads(line).get("reason", "?")] += 1
            except json.JSONDecodeError:
                pass
    swallowed = 0
    for stderr_log in out_dir.glob("host-stderr.log*"):
        swallowed += stderr_log.read_text(encoding="utf-8", errors="replace").count("[coroutine-exception]")

    if interrupted:
        meta.update(status="interrupted", ended=time.strftime("%Y-%m-%dT%H:%M:%S%z"),
                    steps_done=int(model.num_timesteps), census={"reasons": dict(reasons), "swallowed": swallowed})
        meta_path.write_text(json.dumps(meta, indent=2), encoding="utf-8")
        print(f"saved: {model_path} (interrupted at {model.num_timesteps} steps) + meta.json", flush=True)
        # vec_env.close()는 중단 상태의 워커에서 remote.recv() 무한 블록 [실측 2026-07-30].
        # 산출물은 전부 기록됐으므로 즉시 종료 — 데몬 워커는 본체와 함께 소멸한다.
        os._exit(0)

    vec_env.close()

    print(f"\ntraining: {args.steps} steps in {elapsed:.0f}s -> {steps_per_sec:.1f} steps/sec "
          f"({args.n_envs} envs, {vec_cls.__name__}, json+stdio)")

    # 평가 단계 표시 — 학습 종료 후 eval이 도는 동안 "안 멈춤"으로 오인되는 문제(2026-07-30).
    meta.update(status="evaluating")
    meta_path.write_text(json.dumps(meta, indent=2), encoding="utf-8")

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
    meta.update(status="done", ended=time.strftime("%Y-%m-%dT%H:%M:%S%z"),
                census={"reasons": dict(reasons), "swallowed": swallowed})
    meta_legacy = {
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
    meta.update(meta_legacy)
    (out_dir / "meta.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    print(f"saved: {model_path} + meta.json")


if __name__ == "__main__":
    main()
