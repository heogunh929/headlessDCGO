# Rebuild P3 — kind-class layer: verbatim-referenced missing / unresolved members

Generated during the P3 "kind-class layer" big-bang port of the 61 stub effect-kind classes
(`src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffects/*.cs`, mirrors of
`DCGO/Assets/Scripts/Script/CardEffects/*.cs`).

Each class was ported 1:1 from AS-IS. The members below are referenced **verbatim** in the ported
bodies but do **not** resolve cleanly against the current mirror. Per the fidelity rule they were kept
exactly as AS-IS (not stubbed, not simplified). They drive the next rebuild phase.

## Genuinely unported members (referenced verbatim, resolve to nothing)

- **`IEnumerable/Array .Map(...)` extension** — AS-IS `DCGO/Assets/Scripts/Script/IEnumerableExtension.cs`
  provides a `Map` extension; it is **not ported** to the mirror.
  - `AddJogressConditionClass.cs` — `jogressCondition.elements.Map((element) => …)` (AS-IS line ~148).

- **`Player.CanReduceCost(List<Permanent>, CardSource)`** — absent from mirror
  `CardEffectCommons/Player.cs`. Compounding: the mirror `CardSource.Owner` is a `HeadlessPlayerId`
  (not the AS-IS `Player`), so the whole `cardSource.Owner.CanReduceCost(...)` chain is unresolved.
  - `ChangeCostClass.cs` — `!cardSource.Owner.CanReduceCost(targetPermanents, cardSource)` (AS-IS line ~1472).

- **`Mathf.Abs(int)`** — `UnityEngine.Mathf`; the `using UnityEngine;` was stripped as substrate.
  Eventual substrate equivalent is `System.Math.Abs`, but it was **kept verbatim** as `Mathf.Abs`
  (no simplification).
  - `ChangeLinkMaxClass.cs` — `Mathf.Abs(changedLinkMax - LinkMax)`.
  - `ChangeSAttackClass.cs` — `Mathf.Abs(changedSAttack - SAttack)`.

## Namespace/class name collision — `CardEffectCommons.IgnoreRequirement`

The AS-IS static utility class `CardEffectCommons` maps, in the mirror, to a static class
`CardEffectCommons` **inside** namespace `HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons`.
Empirically (build), with `using …CardEffectCommons;` in scope the simple name `CardEffectCommons`
binds to the imported **class** (not the sibling namespace). Consequences:

- **`CardEffectCommons.IsPermanentExistsOnBattleArea(…)` / `…IsPermanentExistsOnOwnerBattleAreaDigimon(…)`**
  are **static methods of that class**, so they **resolve verbatim** — no action needed. (Used by
  `CanNotAttackTargetDefendingPermanentClass.cs`, `CanNotBeDestroyedByBattleClass.cs`,
  `CannotBlockClass.cs`, `VortexCanAttackPlayersClass.cs`, `AddJogressConditionClass.cs`.)

- **`CardEffectCommons.IgnoreRequirement`** does **not** resolve: `IgnoreRequirement` is a **top-level**
  type of the `…CardEffectCommons` namespace (`CardPortingFramework.cs:112`), **not** a nested type of
  the `CardEffectCommons` class, so `CardEffectCommons.IgnoreRequirement` binds `CardEffectCommons` to the
  class and then fails to find the nested type → **CS0426** (×3, at the field, the `SetUp…` param, and the
  `GetEvoCost` param), which in turn makes `GetEvoCost` not match the interface → **CS0535**.
  - `AddEvolutionConditionClass.cs` (class `AddDigivolutionRequirementClass`). Kept **verbatim** per the
    fidelity rule. Later-phase fix: reference the enum as bare `IgnoreRequirement` (already in scope via
    the `using`) or fully-qualify to the namespace — **not** as `CardEffectCommons.IgnoreRequirement`.

These 4 (1×CS0535 + 3×CS0426) are the **only** compiler-surfaced errors introduced by the 61 ported files.
Everything else's unresolved references above are **method-body-phase** errors that the compiler never
reaches, because the project is red at the declaration-binding phase (project-wide unported deps), so it
aborts before method-body analysis — exactly the expected-red condition for this rebuild stage.

## Notes

- All 61 classes are pure synchronous predicate/accessor kinds — no coroutines, no `Debug.Log`/`PlayLog`,
  no `DataBase.*` beyond none. No async translation was required for any of them.
- Class-vs-file name mismatches carried over verbatim from AS-IS: `AddEvolutionConditionClass.cs` contains
  `AddDigivolutionRequirementClass`; `ImmuneFromStackTrashingClass.cs` contains `ImmuneStackTrashingClass`.
