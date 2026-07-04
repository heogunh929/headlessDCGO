# STOP 원장 (렌더 뷰) — BT1 Red

> **자동생성** (porting/scripts/ir_registry.py). 수기 편집 금지 — Canonical IR 이 진실원천.
> tableVer=catalog@2026-07-04

## 코드별 STOP 분기

### STOP_COMPLEX_TIMING (20)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_001 | OnAllyAttack | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_010 | OnEnterFieldAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_011 | OnEnterFieldAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_012 | OnBlockAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_017 | OnEnterFieldAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_021 | OnAllyAttack | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_022 | OnBlockAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_023 | OnEnterFieldAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_025 | None | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_025 | OnEnterFieldAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_090 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_091 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_092 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_093 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_093 | SecuritySkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_094 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_094 | SecuritySkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_095 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_095 | SecuritySkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_114 | OnAllyAttack | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |

### STOP_RULE_AMBIGUOUS (2)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_002 | None | lowering:tier-3 | unrecognized predicate node: {'memberOf': {'call': 'card.PermanentOfThisCard', 'args': []}, 'name': 'HasPierce'} |
| BT1_018 | None | lowering:tier-3 | unrecognized predicate node: {'binop': '>=', 'lhs': {'member': 'card.Owner.MemoryForPlayer'}, 'rhs': {'lit': 3}} |

### STOP_MULTI_STEP_OPTIONAL (1)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_085 | None | lowering:missing-rule | predicate PermanentCondition takes params (Permanent/id) — needs id-rewrite rule |

