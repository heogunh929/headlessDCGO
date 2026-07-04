# STOP 원장 (렌더 뷰) — BT1 Blue

> **자동생성** (porting/scripts/ir_registry.py). 수기 편집 금지 — Canonical IR 이 진실원천.
> tableVer=catalog@2026-07-04

## 코드별 STOP 분기

### STOP_COMPLEX_TIMING (14)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_003 | OnAllyAttack | lowering:missing-rule | no coroutine intent mapping: new DrawClass(card.Owner, 1, activateClass).Draw |
| BT1_029 | OnEnterFieldAnyone | lowering:missing-rule | no coroutine intent mapping: new DrawClass(card.Owner, 1, activateClass).Draw |
| BT1_036 | OnEnterFieldAnyone | lowering:missing-rule | no coroutine intent mapping: selectPermanentEffect.Activate |
| BT1_039 | OnAllyAttack | lowering:tier-3 | activate mixed with other effects (multi-step) — 강모델 |
| BT1_041 | OnEnterFieldAnyone | lowering:missing-rule | no coroutine intent mapping: new DrawClass(card.Owner, 2, activateClass).Draw |
| BT1_096 | SecuritySkill | lowering:tier-3 | activate mixed with other effects (multi-step) — 강모델 |
| BT1_097 | OptionSkill | lowering:missing-rule | no coroutine intent mapping: new DrawClass(card.Owner, 1, activateClass).Draw |
| BT1_097 | SecuritySkill | lowering:tier-3 | activate mixed with other effects (multi-step) — 강모델 |
| BT1_098 | SecuritySkill | lowering:tier-3 | activate mixed with other effects (multi-step) — 강모델 |
| BT1_100 | OptionSkill | lowering:missing-rule | no coroutine intent mapping: CardEffectCommons.GainCanNotAttackPlayerEffect |
| BT1_100 | SecuritySkill | lowering:tier-3 | activate mixed with other effects (multi-step) — 강모델 |
| BT1_101 | OptionSkill | lowering:missing-rule | no coroutine intent mapping: CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom |
| BT1_101 | SecuritySkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_115 | OnAllyAttack | lowering:tier-3 | activate mixed with other effects (multi-step) — 강모델 |

### STOP_MULTI_STEP_OPTIONAL (9)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_034 | None | lowering:missing-rule | predicate DefenderCondition param subject unresolved (not Func<Permanent>) |
| BT1_040 | OnAllyAttack | lowering:missing-rule | coroutine has 2 effects (multi-step) — 강모델 |
| BT1_043 | OnEnterFieldAnyone | lowering:missing-rule | coroutine has 2 effects (multi-step) — 강모델 |
| BT1_044 | OnAllyAttack | lowering:missing-rule | coroutine has 3 effects (multi-step) — 강모델 |
| BT1_086 | OnEnterFieldAnyone | lowering:missing-rule | coroutine has 3 effects (multi-step) — 강모델 |
| BT1_096 | OptionSkill | lowering:missing-rule | coroutine has 2 effects (multi-step) — 강모델 |
| BT1_098 | OptionSkill | lowering:missing-rule | coroutine has 2 effects (multi-step) — 강모델 |
| BT1_099 | OptionSkill | lowering:missing-rule | coroutine has 2 effects (multi-step) — 강모델 |
| BT1_115 | None | lowering:missing-rule | HasMatchConditionOwnersPermanent takes non-card args (needs lowering rule): [{'ref': 'card'}, {'lambda': {'params': ['permanent'], 'body': {'binop': '&&', 'lhs': {'call': 'permanent.TopCard.CardColors.Contains', 'args': [{'member': 'CardColor.Blue'}]}, 'rhs': {'member': 'permanent.IsTamer'}}}}] |

### STOP_RULE_AMBIGUOUS (2)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_033 | None | lowering:missing-rule | lambda member 'IsDigimon' has no exact CardEffectCommons(card,id) helper (→ LLM 후보) |
| BT1_041 | OnAllyAttack | lowering:missing-rule | lambda member 'IsDigimon' has no exact CardEffectCommons(card,id) helper (→ LLM 후보) |

### STOP_MISSING_PRIMITIVE (1)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_004 | None | lowering:missing-rule | predicate call not a CardEffectCommons helper: card.Owner.Enemy.GetBattleAreaDigimons().Count |

