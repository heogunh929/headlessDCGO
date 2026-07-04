# STOP 원장 (렌더 뷰) — BT1 Red

> **자동생성** (porting/scripts/ir_registry.py). 수기 편집 금지 — Canonical IR 이 진실원천.
> tableVer=catalog@2026-07-04

## 코드별 STOP 분기

### STOP_COMPLEX_TIMING (11)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_010 | OnEnterFieldAnyone | lowering:missing-rule | no coroutine intent mapping: CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect |
| BT1_011 | OnEnterFieldAnyone | lowering:missing-rule | no coroutine intent mapping: selectCardEffect.Activate |
| BT1_022 | OnBlockAnyone | lowering:missing-rule | no coroutine intent mapping: new DrawClass(card.Owner, 1, activateClass).Draw |
| BT1_023 | OnEnterFieldAnyone | lowering:missing-rule | no coroutine intent mapping: selectPermanentEffect.Activate |
| BT1_025 | None | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_025 | OnEnterFieldAnyone | lowering:missing-rule | no coroutine intent mapping: CardEffectCommons.ChangeDigimonSAttack |
| BT1_093 | SecuritySkill | lowering:tier-3 | activate mixed with other effects (multi-step) — 강모델 |
| BT1_094 | OptionSkill | lowering:missing-rule | no coroutine intent mapping: selectPermanentEffect.Activate |
| BT1_094 | SecuritySkill | lowering:tier-3 | coroutine/ActivateClass or non-factory effect (declarative translation needed) |
| BT1_095 | SecuritySkill | lowering:tier-3 | activate mixed with other effects (multi-step) — 강모델 |
| BT1_114 | OnAllyAttack | lowering:missing-rule | intent arg not a literal/known enum: {'opaque': '-5'} |

### STOP_MULTI_STEP_OPTIONAL (7)

| 카드 | 타이밍 | stage | detail |
|---|---|---|---|
| BT1_017 | OnEnterFieldAnyone | lowering:missing-rule | coroutine has 2 effects (multi-step) — 강모델 |
| BT1_021 | OnAllyAttack | lowering:missing-rule | coroutine has 2 effects (multi-step) — 강모델 |
| BT1_090 | OptionSkill | lowering:missing-rule | coroutine has 2 effects (multi-step) — 강모델 |
| BT1_091 | OptionSkill | lowering:missing-rule | coroutine has 2 effects (multi-step) — 강모델 |
| BT1_092 | OptionSkill | lowering:missing-rule | coroutine has 3 effects (multi-step) — 강모델 |
| BT1_093 | OptionSkill | lowering:missing-rule | coroutine has 3 effects (multi-step) — 강모델 |
| BT1_095 | OptionSkill | lowering:missing-rule | coroutine has 2 effects (multi-step) — 강모델 |

