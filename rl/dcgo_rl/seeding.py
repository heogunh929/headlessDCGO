"""시드 유도 규약 (FR-6.2 / NFR-3, 설계 §7).

``match_seed = H(experiment_seed, match_index)`` — 실험 config + match_index만으로
어떤 매치든 재현 가능해야 한다. 결과는 C# ``EngineContext.CreateDefault(seed)``에
넘길 수 있도록 Int32 양수 범위로 접는다.
"""

from __future__ import annotations

import hashlib

_DOMAIN = "dcgo-rl-match"
INT32_MASK = 0x7FFFFFFF


def derive_match_seed(experiment_seed: int, match_index: int) -> int:
    payload = f"{_DOMAIN}:{experiment_seed}:{match_index}".encode()
    digest = hashlib.sha256(payload).digest()
    return int.from_bytes(digest[:4], "big") & INT32_MASK
