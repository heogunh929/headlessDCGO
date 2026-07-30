"""league.py — (덱, 정책) 조합 리그 오케스트레이터 (와이어프레임 확정 2026-07-30).

조합 = 덱 하나 + 정책 하나. 라운드마다 학습 대상 조합을 차례로 학습시키고(상대 = 나머지
조합들의 최신 동결 정책+그 덱), 라운드가 끝나면 조합 간 교차 평가 매트릭스를 기록한다.

사용:  python league.py --config <runs/league-X/config.json>

config.json:
  {"name", "rounds", "games", "seed", "n_envs", "eval_pairs", "record_mode",
   "combos": [{"id", "deck"(rl/decks 파일명), "init"(정책 zip 경로|null), "train"(bool)}, ...]}

상태 정본 = <out>/league.json — 대시보드 진행/매트릭스 표시가 이것을 읽는다.
SIGTERM = graceful: 진행 중 학습 서브프로세스에 SIGTERM 전달(체크포인트 저장은 train.py 몫) 후
상태 interrupted로 기록하고 종료.
"""

from __future__ import annotations

import argparse
import json
import random
import signal
import subprocess
import sys
import time
from pathlib import Path

RL_DIR = Path(__file__).resolve().parent


def now() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%S%z")


class League:
    def __init__(self, config_path: Path):
        self.config = json.loads(config_path.read_text(encoding="utf-8"))
        self.out = config_path.parent
        self.state_path = self.out / "league.json"
        self.combos = self.config["combos"]
        self.current: dict[str, str | None] = {}
        for combo in self.combos:
            self.current[combo["id"]] = combo.get("init") or None
        self.state = {
            "status": "running", "started": now(), "config": self.config,
            "round": 0, "phase": "", "training": None,
            "policies": dict(self.current), "matrix": {}, "log": [],
        }
        self.child: subprocess.Popen | None = None
        self.stopped = False
        signal.signal(signal.SIGTERM, self._on_term)
        signal.signal(signal.SIGINT, self._on_term)

    def _on_term(self, signum, frame):
        self.stopped = True
        if self.child is not None and self.child.poll() is None:
            self.child.send_signal(signal.SIGTERM)   # train.py가 체크포인트 저장 후 종료(M1 경로)

    def save(self):
        self.state["policies"] = dict(self.current)
        self.state_path.write_text(json.dumps(self.state, ensure_ascii=False, indent=2), encoding="utf-8")

    def log(self, message: str):
        line = f"{now()} {message}"
        print(line, flush=True)
        self.state["log"].append(line)
        self.save()

    def deck_path(self, combo) -> str:
        return str((RL_DIR / "decks" / combo["deck"]).resolve())

    # ---------- 라운드 학습 ----------

    def train_combo(self, round_no: int, index: int, combo) -> bool:
        others = [c for c in self.combos if c["id"] != combo["id"]]
        opponents = [{"id": c["id"], "recipe": self.deck_path(c), "model": self.current[c["id"]]}
                     for c in others]
        opp_path = self.out / f"opponents-{combo['id']}-r{round_no}.json"
        opp_path.write_text(json.dumps(opponents, ensure_ascii=False), encoding="utf-8")

        run_out = self.out / combo["id"] / f"round-{round_no}"
        cmd = [sys.executable, str(RL_DIR / "train.py"),
               "--games", str(self.config.get("games", 300)),
               "--steps", str(self.config.get("games", 300) * 400),
               "--n-envs", str(self.config.get("n_envs", 4)),
               "--seed", str(self.config.get("seed", 42) + round_no * 1000 + index),
               "--eval-matches", "8",
               "--vec", "dummy",
               "--record-mode", self.config.get("record_mode", "accident"),
               "--my-recipe", self.deck_path(combo),
               "--opponents-json", str(opp_path),
               "--out", str(run_out)]
        if self.current[combo["id"]]:
            cmd += ["--init-model", self.current[combo["id"]]]

        self.state.update(phase="train", training={"combo": combo["id"], "round": round_no,
                                                   "out": str(run_out)})
        self.log(f"라운드 {round_no} · {combo['id']} 학습 시작")
        run_out.parent.mkdir(parents=True, exist_ok=True)
        with open(run_out.parent / f"round-{round_no}.log", "ab") as log_file:
            self.child = subprocess.Popen(cmd, cwd=RL_DIR, stdout=log_file, stderr=subprocess.STDOUT)
            self.child.wait()
        self.child = None
        policy = run_out / "policy.zip"
        if policy.exists():
            self.current[combo["id"]] = str(policy)
            self.log(f"라운드 {round_no} · {combo['id']} 완료 → {policy}")
            return True
        self.log(f"라운드 {round_no} · {combo['id']} 실패(정책 미생성)")
        return False

    # ---------- 교차 평가 ----------

    def cross_eval(self, round_no: int):
        from dcgo_rl.bridge import BridgeClient
        from dcgo_rl.cards import CardIndex
        from dcgo_rl.decks.recipe import load_recipe_file
        from dcgo_rl.league.opponents import PolicyOpponent, random_action
        from sb3_contrib import MaskablePPO

        index = CardIndex.load()
        players = {}
        for combo in self.combos:
            recipe = load_recipe_file(Path(self.deck_path(combo)), index)
            model_path = self.current[combo["id"]]
            actor = PolicyOpponent(MaskablePPO.load(model_path)) if model_path else None
            players[combo["id"]] = (recipe, actor)

        pairs = [(a["id"], b["id"]) for i, a in enumerate(self.combos) for b in self.combos[i + 1:]]
        matches_per_pair = int(self.config.get("eval_pairs", 10))
        self.state.update(phase="eval", training=None)
        self.log(f"라운드 {round_no} 교차 평가: {len(pairs)}쌍 × {matches_per_pair}판")

        matrix: dict[str, dict] = {}
        client = BridgeClient(verify_vocab=False,
                              match_log_dir=str(self.out / "eval-matches"), record_mode="off")
        rng = random.Random(self.config.get("seed", 42) * 31 + round_no)
        try:
            for a_id, b_id in pairs:
                if self.stopped:
                    return
                record = {"wins": 0, "losses": 0, "draws": 0}   # a 관점
                for game in range(matches_per_pair):
                    a_seat = 1 if game % 2 == 0 else 2          # 좌석 교대(선공 편향 제거)
                    recipe_a, actor_a = players[a_id]
                    recipe_b, actor_b = players[b_id]
                    decks = {str(a_seat): recipe_a.to_json(), str(3 - a_seat): recipe_b.to_json()}
                    msg = client.reset(rng.randrange(1, 2 ** 30), decks, 2000)
                    while msg["type"] == "turn":
                        actor = actor_a if msg["seat"] == a_seat else actor_b
                        action = actor.act(msg) if actor else random_action(msg, rng)
                        msg = client.act(msg["seat"], action)
                    winner = msg.get("winnerSeat")
                    if winner is None:
                        record["draws"] += 1
                    elif winner == a_seat:
                        record["wins"] += 1
                    else:
                        record["losses"] += 1
                matrix[f"{a_id}|{b_id}"] = record
                self.log(f"  {a_id} vs {b_id}: {record['wins']}승 {record['draws']}무 {record['losses']}패")
        finally:
            client.close()
        self.state["matrix"][f"r{round_no}"] = matrix
        self.save()

    # ---------- 메인 ----------

    def run(self):
        self.save()
        rounds = int(self.config.get("rounds", 3))
        for round_no in range(1, rounds + 1):
            self.state["round"] = round_no
            for i, combo in enumerate(self.combos):
                if self.stopped:
                    break
                if combo.get("train", True):
                    self.train_combo(round_no, i, combo)
                else:
                    self.log(f"라운드 {round_no} · {combo['id']} 동결(학습 안 함) — 상대로만 참전")
            if self.stopped:
                break
            self.cross_eval(round_no)
        self.state.update(status="interrupted" if self.stopped else "done",
                          ended=now(), phase="", training=None)
        self.save()
        self.log("리그 중단" if self.stopped else "리그 완료")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", required=True)
    args = parser.parse_args()
    League(Path(args.config).resolve()).run()


if __name__ == "__main__":
    main()
