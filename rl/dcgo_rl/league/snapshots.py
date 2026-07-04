"""SnapshotStore — 스냅샷 = 공통 화폐 (C-7, dev design §5.1).

물리 배치: <root>/<lineage>/<snapshot_id>/{policy.zip, meta.json}.
좌표는 레이팅 + 글로벌 스텝(FR-2.2 — 세대 번호는 기록만 하고 정렬 축으로 안 씀).
메타에 obs 스키마 해시·vocab 버전을 태깅해 아레나 봇 배포 호환(FR-2.6)을 지킨다.
"""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

REQUIRED_META = (
    "snapshot_id", "lineage", "global_step", "rating",
    "obs_schema_hash", "vocab_version", "arch", "deck_context",
)


class SnapshotStore:
    def __init__(self, root: Path | str):
        self.root = Path(root)
        self.root.mkdir(parents=True, exist_ok=True)

    def save(self, model, meta: dict) -> Path:
        """model은 .save(path)를 가진 객체(SB3 모델). 메타 필수 필드는 REQUIRED_META."""
        missing = [key for key in REQUIRED_META if key not in meta]
        if missing:
            raise ValueError(f"snapshot meta missing fields: {missing}")

        directory = self.root / str(meta["lineage"]) / str(meta["snapshot_id"])
        directory.mkdir(parents=True, exist_ok=True)
        model.save(str(directory / "policy.zip"))

        enriched = dict(meta)
        enriched.setdefault("frozen_at", datetime.now(timezone.utc).isoformat())
        (directory / "meta.json").write_text(json.dumps(enriched, indent=2), encoding="utf-8")
        return directory

    def list_metas(self) -> list[dict]:
        metas = []
        for meta_path in sorted(self.root.glob("*/*/meta.json")):
            metas.append(json.loads(meta_path.read_text(encoding="utf-8")))
        return metas

    def path_of(self, snapshot_id: str) -> Path:
        matches = [p for p in self.root.glob(f"*/{snapshot_id}") if (p / "policy.zip").exists()]
        if len(matches) != 1:
            raise KeyError(f"snapshot '{snapshot_id}': found {len(matches)} entries")
        return matches[0]

    def policy_path(self, snapshot_id: str) -> Path:
        return self.path_of(snapshot_id) / "policy.zip"

    def meta_of(self, snapshot_id: str) -> dict:
        return json.loads((self.path_of(snapshot_id) / "meta.json").read_text(encoding="utf-8"))
