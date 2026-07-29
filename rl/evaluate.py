"""평가 러너 (L0/B3): 학습 정책 vs 랜덤 합법 정책 승률 + Wilson 95% 신뢰구간.

병렬 평가(n_jobs>1): 판을 워커 프로세스들에 나눈다 — 엔진이 프로세스-전역 싱글턴이라 워커당
호스트 1개가 강제이고(stage4 설계 §4.4), 평가 120판 직렬 ~1.5시간이 총시간을 지배하던 것의 해법.
워커 i의 시드는 experiment_seed + i*1_000_000 으로 분리 파생 — 같은 (seed, n_matches, n_jobs)면
같은 표본(재현적)이지만, 직렬(n_jobs=1)과는 표본 구성이 다르다(승률 집계의 의미는 동일).
"""

from __future__ import annotations

import math
import multiprocessing
import tempfile
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path

from dcgo_rl.envs import DcgoSeatEnv


def wilson_ci(wins: int, n: int, z: float = 1.96) -> tuple[float, float]:
    """이항 승률의 Wilson score 95% 구간 (n=0이면 (0,1))."""
    if n == 0:
        return (0.0, 1.0)
    p = wins / n
    denom = 1 + z * z / n
    center = (p + z * z / (2 * n)) / denom
    margin = (z / denom) * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n))
    return (max(0.0, center - margin), min(1.0, center + margin))


def _run_matches(model, n_matches: int, experiment_seed: int, deck_provider,
                 deterministic: bool = False) -> tuple[int, int, int]:
    """단일 env로 n_matches 판 — (wins, completed, truncated).

    기본은 표집(sampled) 평가다. argmax(deterministic=True)는 같은 상태에서 같은 무효행동
    (중단되는 플레이)을 무한 반복할 수 있고, 그 반복이 판 내부에서 엔진 객체를 GB급으로 누적시켜
    OOM까지 갔다 [실측 2026-07-29: eval 2판 만에 10G 상한 킬 ×2회]. 확률 정책의 승률 평가로도
    표집이 정직하다.
    """
    env = DcgoSeatEnv(experiment_seed=experiment_seed, deck_provider=deck_provider)
    wins = 0
    completed = 0
    truncated_n = 0
    try:
        for _ in range(n_matches):
            obs, _ = env.reset()
            while True:
                action, _ = model.predict(obs, action_masks=env.action_masks(), deterministic=deterministic)
                obs, reward, terminated, truncated, _ = env.step(int(action))
                if terminated or truncated:
                    if terminated:
                        completed += 1
                        if reward > 0:
                            wins += 1
                    else:
                        truncated_n += 1
                    break
    finally:
        env.close()
    return wins, completed, truncated_n


def _eval_worker(model_path: str, n_matches: int, experiment_seed: int, deck_provider,
                 deterministic: bool) -> tuple[int, int, int]:
    """워커 프로세스 본체 — 모델을 파일에서 로드해(프로세스 간 모델 객체 전달 회피) 자기 호스트로 돈다."""
    from sb3_contrib import MaskablePPO

    model = MaskablePPO.load(model_path, device="cpu")
    return _run_matches(model, n_matches, experiment_seed, deck_provider, deterministic)


def evaluate_winrate(
    model,
    n_matches: int,
    experiment_seed: int,
    deck_provider=None,
    n_jobs: int = 1,
    deterministic: bool = False,
) -> dict:
    """n_matches 판 대전(좌석 교대) — {winrate, wins, losses, completed, truncated, ci95} 반환.

    n_jobs>1: 판을 워커에 균등 배분(나머지는 앞 워커부터 +1), 워커별 시드 분리 파생.
    """
    if n_jobs <= 1 or n_matches <= 1:
        wins, completed, truncated_n = _run_matches(model, n_matches, experiment_seed, deck_provider, deterministic)
    else:
        n_jobs = min(n_jobs, n_matches)
        base, extra = divmod(n_matches, n_jobs)
        shares = [base + (1 if i < extra else 0) for i in range(n_jobs)]

        with tempfile.TemporaryDirectory() as tmp:
            model_path = str(Path(tmp) / "eval_policy.zip")
            model.save(model_path)

            # spawn 필수: 리눅스 기본 fork는 torch(특히 CUDA 초기화된 부모)를 포크하다 교착한다 — 실측
            # 2026-07-29(12판/6워커가 9분+ 무진행). spawn은 워커가 깨끗한 인터프리터로 시작한다.
            with ProcessPoolExecutor(max_workers=n_jobs, mp_context=multiprocessing.get_context("spawn")) as pool:
                futures = [
                    pool.submit(_eval_worker, model_path, share, experiment_seed + i * 1_000_000,
                                deck_provider, deterministic)
                    for i, share in enumerate(shares)
                ]
                parts = [f.result() for f in futures]

        wins = sum(p[0] for p in parts)
        completed = sum(p[1] for p in parts)
        truncated_n = sum(p[2] for p in parts)

    winrate = wins / completed if completed else 0.0
    return {
        "winrate": winrate,
        "wins": wins,
        "losses": completed - wins,
        "completed": completed,
        "truncated": truncated_n,
        "ci95": wilson_ci(wins, completed),
    }


if __name__ == "__main__":
    import argparse
    from pathlib import Path

    from sb3_contrib import MaskablePPO

    from dcgo_rl.cards import CardIndex
    from dcgo_rl.decks.providers import FixedPoolProvider
    from dcgo_rl.decks.recipe import load_recipe_file

    parser = argparse.ArgumentParser()
    parser.add_argument("model", type=str)
    parser.add_argument("--matches", type=int, default=120)
    parser.add_argument("--seed", type=int, default=819)
    parser.add_argument("--jobs", type=int, default=6, help="평가 병렬 워커 수 (워커당 호스트 1개)")
    parser.add_argument("--deterministic", action="store_true",
                        help="argmax 평가(기본=표집). argmax는 무효행동 루프→OOM 위험 — 모듈 주석 참조")
    parser.add_argument("--recipes", nargs="*", default=None,
                        help="덱 레시피 파일들 — 학습 때와 같은 풀로 평가해야 비교가 성립")
    args = parser.parse_args()

    provider = None
    if args.recipes:
        index = CardIndex.load()
        provider = FixedPoolProvider([load_recipe_file(Path(p).resolve(), index) for p in args.recipes])

    model = MaskablePPO.load(args.model, device="cpu")
    report = evaluate_winrate(model, args.matches, args.seed, deck_provider=provider, n_jobs=args.jobs,
                              deterministic=args.deterministic)
    lo, hi = report["ci95"]
    print(f"winrate vs random: {report['winrate']:.1%} "
          f"({report['wins']}W/{report['losses']}L over {report['completed']} completed, "
          f"truncated {report['truncated']}) CI95=[{lo:.1%}, {hi:.1%}]")
