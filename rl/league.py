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
        try:
            engine_sha = subprocess.run(["git", "rev-parse", "--short=12", "HEAD"],
                                        capture_output=True, text=True, cwd=RL_DIR).stdout.strip()
        except OSError:
            engine_sha = ""
        self.state = {
            "status": "running", "started": now(), "config": self.config,
            "engine_sha": engine_sha,
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

    def set_alias(self, path: Path, alias: str) -> None:
        """정책 논리명 등록 — runner의 별칭 장부(model-aliases.json)에 직접 기입(같은 머신 전제)."""
        if not alias:
            return
        book = RL_DIR / "opsd" / "model-aliases.json"
        try:
            aliases = json.loads(book.read_text(encoding="utf-8")) if book.exists() else {}
        except json.JSONDecodeError:
            aliases = {}
        runs_root = RL_DIR.parent / "runs"
        aliases[str(path.resolve().relative_to(runs_root))] = alias
        book.write_text(json.dumps(aliases, ensure_ascii=False, indent=2), encoding="utf-8")

    def train_combo(self, round_no: int, index: int, combo) -> bool:
        # 상대 순환 = 자기 동결본 포함 전 조합(사용자 확정 2026-07-31: a vs a도 한 종류) —
        # 자기 동결본이 아직 없으면(1라운드 신규) ComboOpponents가 랜덤으로 대체한다.
        opponents = [{"id": c["id"], "recipe": self.deck_path(c), "model": self.current[c["id"]]}
                     for c in self.combos]
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
            if combo.get("alias"):
                self.set_alias(policy, f"{combo['alias']}-r{round_no}")
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

        # 순서쌍 전체 N×N(사용자 확정 2026-07-31): 행=선공(P1), 열=후공(P2) 고정 — 좌석 교대 없음.
        # 대각선(미러)도 실측: 미러 선공 승률 = 순수 선공 이득의 측정치.
        pairs = [(a["id"], b["id"]) for a in self.combos for b in self.combos]
        matches_per_pair = int(self.config.get("eval_pairs", 10))
        self.state.update(phase="eval", training=None)
        self.log(f"라운드 {round_no} 교차 평가: 매치업 {len(pairs)}종 × {matches_per_pair}판 (행=선공)")

        matrix: dict[str, dict] = {}
        # .../matches/ 하위에 기록 — runner의 판 로그 서빙 규칙(run/matches/파일)과 정합.
        eval_dir = self.out / "eval-matches" / f"r{round_no}" / "matches"
        eval_dir.mkdir(parents=True, exist_ok=True)
        # 평가 판은 상시 기록(사용자 확정: 배틀 로그 열람) — 조합 vs 조합 실전이 리그의 1급 관찰물.
        client = BridgeClient(verify_vocab=False,
                              match_log_dir=str(eval_dir), record_mode="all",
                              engine_sha=self.state.get("engine_sha", ""))
        rng = random.Random(int(self.config.get("seed", 42)) * 31 + round_no)
        try:
            for a_id, b_id in pairs:
                if self.stopped:
                    return
                record = {"wins": 0, "losses": 0, "draws": 0, "matches": []}   # a(선공) 관점
                for game in range(matches_per_pair):
                    recipe_a, actor_a = players[a_id]
                    recipe_b, actor_b = players[b_id]
                    decks = {"1": recipe_a.to_json(), "2": recipe_b.to_json()}
                    msg = client.reset(rng.randrange(1, 2 ** 30), decks, 2000)
                    match_id = msg.get("matchId", "")
                    while msg["type"] == "turn":
                        actor = actor_a if msg["seat"] == 1 else actor_b
                        action = actor.act(msg) if actor else random_action(msg, rng)
                        msg = client.act(msg["seat"], action)
                    winner = msg.get("winnerSeat")
                    match_id = msg.get("matchId", match_id)
                    record["matches"].append(match_id)
                    if winner is None:
                        record["draws"] += 1
                    elif winner == 1:
                        record["wins"] += 1
                    else:
                        record["losses"] += 1
                matrix[f"{a_id}|{b_id}"] = record
                self.log(f"  선공 {a_id} vs {b_id}: {record['wins']}승 {record['draws']}무 {record['losses']}패")
        finally:
            client.close()
        self.state["matrix"][f"r{round_no}"] = matrix
        self.save()

    # ---------- 메인 ----------

    def run(self):
        self.save()
        rounds = int(self.config.get("rounds", 3))
        final_round = 0
        for round_no in range(1, rounds + 1):
            self.state["round"] = round_no
            for i, combo in enumerate(self.combos):
                if self.stopped:
                    break
                if combo.get("train", True):
                    if self.train_combo(round_no, i, combo):
                        final_round = round_no
                else:
                    self.log(f"라운드 {round_no} · {combo['id']} 동결(학습 안 함) — 상대로만 참전")
            if self.stopped:
                break
            self.cross_eval(round_no)
        done = not self.stopped
        if done:
            # 완주: 각 조합의 최종 정책에 무접미 논리명(중간 라운드는 -rN 유지)
            for combo in self.combos:
                if combo.get("alias") and self.current[combo["id"]]:
                    self.set_alias(Path(self.current[combo["id"]]), combo["alias"])
        self.state.update(status="done" if done else "interrupted",
                          ended=now(), phase="", training=None)
        self.save()
        self.log("리그 완료" if done else "리그 중단")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", required=True)
    args = parser.parse_args()
    League(Path(args.config).resolve()).run()


if __name__ == "__main__":
    main()
