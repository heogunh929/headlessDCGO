# Headless Source Origin Mapping

This document maps each file under `src/HeadlessDCGO.Engine/Headless/` to the AS-IS Unity source locations or dependency patterns it is intended to replace.

## Summary

- Headless files documented: 81
- Primary AS-IS roots:
  - `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`
  - `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
  - `DCGO/Assets/Scripts/Script/CardController.cs`
  - `DCGO/Assets/Scripts/Script/CardObjectController.cs`
  - `DCGO/Assets/Scripts/Script/GManager.cs`
  - `DCGO/Assets/Scripts/Script/ContinuousController.cs`
  - `DCGO/Assets/Scripts/Script/Player.cs`
  - `DCGO/Assets/Scripts/Script/Select*Effect.cs`
  - `DCGO/Assets/CardBaseEntity`

## Mapping Rules

- `Bridge/` files come from AS-IS global singleton access such as `GManager.instance`, `ContinuousController.instance`, and `GetComponent<T>()`.
- `Coroutines/` files come from `IEnumerator`, `StartCoroutine`, `WaitForSeconds`, `WaitWhile`, and `StopAllCoroutines`.
- `Choices/` files come from UI selection flows such as `SelectCardEffect`, `SelectPermanentEffect`, `SelectCountEffect`, `SelectHandEffect`, `UserSelectionManager`, and main-phase click/action queues.
- `Effects/` files come from `AutoProcessing`, `MultipleSkills`, `SkillInfo`, `CardEffectCommons`, `CardEffectFactory`, and `Hashtable`-based effect contexts.
- `Services/` files come from cross-cutting Unity-dependent operations: zone movement, random, logging, legal-action query, card/effect lookup.
- `Runtime/` files come from `GManager`, `TurnStateMachine`, `GameContext`, `Player`, `AutoProcessing`, and `AttackProcess`.
- `DataLoading/` files come from `ContinuousController` deck/option loading plus converted `Assets/CardBaseEntity` data.
- `Diagnostics/` files come from `PlayLog`, `Debug.Log`, and visual/log side effects that need deterministic trace output in headless mode.

## Detailed CSV

See `docs/headless_source_origin_mapping.csv` for the row-level mapping:

- `headless_path`
- `headless_area`
- `purpose`
- `asis_source_paths`
- `asis_dependency`
- `porting_notes`

## First-Port Guidance

1. Start with `Bridge/EngineContext.cs`, `Bridge/GManagerBridge.cs`, and `Bridge/ContinuousContext.cs` to stop new code from depending directly on AS-IS singletons.
2. Next port `Choices/` because `SelectCardEffect.Root` and selection constraints are rule-relevant, not just UI.
3. Then port `Coroutines/` and `Effects/` together because AS-IS effect processing is coroutine-driven.
4. Port `Services/IZoneMover.cs` before deep `CardController.cs` work, because card movement is spread across many effect classes.
5. Use `Runtime/DcgoMatch.cs` and `Runtime/StepResult.cs` as the eventual public API for RL simulation.
