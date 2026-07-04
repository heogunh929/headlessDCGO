# STOP 원장 (렌더 뷰) — BT1 Blue

> **자동생성** (porting/scripts/ir_registry.py). 수기 편집 금지 — Canonical IR 이 진실원천.
> tableVer=catalog@2026-07-04

## 코드별 STOP 분기

### STOP_COMPLEX_TIMING (24)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_003 | OnAllyAttack | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_029 | OnEnterFieldAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_030 | OnDestroyedAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_035 | OnDestroyedAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_036 | OnEnterFieldAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_039 | OnAllyAttack | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_040 | OnAllyAttack | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_041 | OnAllyAttack | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_041 | OnEnterFieldAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_043 | OnEnterFieldAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_044 | OnAllyAttack | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_086 | OnEnterFieldAnyone | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_096 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_096 | SecuritySkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_097 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_097 | SecuritySkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_098 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_098 | SecuritySkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_099 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_100 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_100 | SecuritySkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_101 | OptionSkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_101 | SecuritySkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_115 | OnAllyAttack | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |

### STOP_MULTI_STEP_OPTIONAL (3)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_033 | None | lowering:missing-rule | HasMatchConditionOpponentsPermanent takes non-card args (needs lowering rule): [{'ref': 'card'}, {'lambda': {'params': ['permanent'], 'body': {'binop': '&&', 'lhs': {'member': 'permanent.IsDigimon'}, 'rhs': {'member': 'permanent.HasNoDigivolutionCards'}}}}] |
| BT1_034 | None | lowering:missing-rule | predicate DefenderCondition takes params (Permanent/id) — needs id-rewrite rule |
| BT1_115 | None | lowering:missing-rule | HasMatchConditionOwnersPermanent takes non-card args (needs lowering rule): [{'ref': 'card'}, {'lambda': {'params': ['permanent'], 'body': {'binop': '&&', 'lhs': {'call': 'permanent.TopCard.CardColors.Contains', 'args': [{'member': 'CardColor.Blue'}]}, 'rhs': {'member': 'permanent.IsTamer'}}}}] |

### STOP_RULE_AMBIGUOUS (1)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_004 | None | lowering:tier-3 | unrecognized predicate node: {'binop': '>=', 'lhs': {'call': 'card.Owner.Enemy.GetBattleAreaDigimons().Count', 'args': [{'lambda': {'params': ['permanent'], 'body': {'member': 'permanent.HasNoDigivolutionCards'}}}]}, 'rhs': {'lit': 2}} |

