"""매치업 매트릭스 — (i, j) 승률의 1급 자료 (FR-2.4, 요구사항 결정 §8).

레이팅이 못 보는 상성/순환 구조를 담는다: 약점 우선 샘플링(FR-2.3)의 근거이자
밸런스 진단(비이행 사이클 = 건강한 메타, 단일 지배 = OP)의 원자료.
저장 = SQLite 단일 파일(동시 기록·집계 쿼리·백업 단순성 — dev design §5.2).
행은 (a, b) 순서쌍으로 대칭 이중 기록해 조회를 단순화한다.
"""

from __future__ import annotations

import sqlite3
from pathlib import Path


class MatchupMatrix:
    def __init__(self, path: Path | str):
        self._conn = sqlite3.connect(str(path))
        self._conn.execute(
            "CREATE TABLE IF NOT EXISTS matchup ("
            " a TEXT NOT NULL, b TEXT NOT NULL,"
            " wins INTEGER NOT NULL DEFAULT 0,"
            " losses INTEGER NOT NULL DEFAULT 0,"
            " draws INTEGER NOT NULL DEFAULT 0,"
            " PRIMARY KEY (a, b))"
        )
        self._conn.commit()

    def record(self, a: str, b: str, score_a: float) -> None:
        """score_a: 1.0 = a 승, 0.0 = a 패, 0.5 = 무. (a,b)/(b,a) 대칭 기록."""
        if score_a not in (0.0, 0.5, 1.0):
            raise ValueError(f"score_a must be 0/0.5/1, got {score_a}")
        a_cols = {1.0: "wins", 0.0: "losses", 0.5: "draws"}[score_a]
        b_cols = {1.0: "losses", 0.0: "wins", 0.5: "draws"}[score_a]
        for x, y, col in ((a, b, a_cols), (b, a, b_cols)):
            self._conn.execute(
                "INSERT INTO matchup (a, b) VALUES (?, ?) ON CONFLICT(a, b) DO NOTHING", (x, y)
            )
            self._conn.execute(
                f"UPDATE matchup SET {col} = {col} + 1 WHERE a = ? AND b = ?", (x, y)  # noqa: S608 — col은 내부 상수
            )
        self._conn.commit()

    def games(self, a: str, b: str) -> int:
        row = self._conn.execute(
            "SELECT wins + losses + draws FROM matchup WHERE a = ? AND b = ?", (a, b)
        ).fetchone()
        return int(row[0]) if row else 0

    def winrate(self, a: str, b: str) -> float | None:
        row = self._conn.execute(
            "SELECT wins, losses, draws FROM matchup WHERE a = ? AND b = ?", (a, b)
        ).fetchone()
        if row is None:
            return None
        wins, losses, draws = row
        total = wins + losses + draws
        return (wins + 0.5 * draws) / total if total else None

    def weakest(self, me: str, opponents: list[str], min_games: int) -> list[tuple[str, float]]:
        """표본이 충분한 상대 중 내 승률 오름차순 — 약점 우선 샘플링(FR-2.3)의 후보 목록."""
        scored = []
        for opponent in opponents:
            if self.games(me, opponent) >= min_games:
                rate = self.winrate(me, opponent)
                if rate is not None:
                    scored.append((opponent, rate))
        return sorted(scored, key=lambda pair: pair[1])

    def table(self) -> list[tuple[str, str, int, int, int]]:
        return list(self._conn.execute("SELECT a, b, wins, losses, draws FROM matchup ORDER BY a, b"))

    def close(self) -> None:
        self._conn.close()
