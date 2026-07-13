# Effect-model rebuild — P6 missing-type foundation port: notes / missing / collisions

Scope: 4 small missing types referenced by the ported factory/Hashtable layer but absent on the mirror,
blocking declaration-level compilation (verified via `dotnet build ... CS0246`): `OptionResolutionClass`,
`SkillInfo`, `PlayCardClass`, `OnEnterFieldHashtableParams`. 1:1 ports, same style/rules as
P1 (ICardEffect.cs / CardEffectInterfaces.cs) — reference unresolved AS-IS members VERBATIM, do NOT
stub/simplify; the project stays intentionally RED.

## 1. OptionResolutionClass — DCGO/Assets/Scripts/Script/OptionResolutionClass.cs (34 lines)
Ported to `src/HeadlessDCGO.Engine/Assets/Scripts/Script/OptionResolutionClass.cs`, namespace
`...Script.CardEffectCommons`. `: ICardEffect, IOptionResolutionEffect` — both already exist there
(ICardEffect.cs / CardEffectInterfaces.cs). `Func<CardSource, IEnumerator>` -> `Func<CardSource, Task>`
(consistent with `IOptionResolutionEffect.Resolve` already being `Task Resolve(CardSource)` per its own
mirror translation note). `ContinuousController.instance.StartCoroutine(ResolutionCoroutine(optionCard))`
-> `await ResolutionCoroutine(optionCard)`. No masked-verbatim members — every type this class touches
(`ICardEffect`, `CardSource`, `IOptionResolutionEffect`) already resolves on the mirror. `using UnityEngine;`
/ `using Photon;` stripped (unused by the class body).

## 2. SkillInfo — DCGO/Assets/Scripts/Script/SkillInfo.cs (15 lines)
Ported to `src/HeadlessDCGO.Engine/Assets/Scripts/Script/SkillInfo.cs`, namespace
`...Script.CardEffectCommons`. Pure-data holder (`ICardEffect`/`Hashtable`/`EffectTiming`), no adaptation
needed beyond the namespace — all three referenced types already exist there.

**Pre-existing file at this exact path**: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/SkillInfo.cs`
already existed, but held only a migration-scaffold skeleton (no namespace declaration, no members, a
"Skeleton only — port later" comment) — NOT real code, so replaced in place (not a stub-over-real-code
edit). **No duplicate-type conflict**: the OTHER `SkillInfo` the task brief flagged
(`src/HeadlessDCGO.Engine/Headless/Effects/SkillInfo.cs`) is a structurally different `record` in namespace
`HeadlessDCGO.Engine.Headless.Effects` (substrate `EffectRequest`/`CardEffectDefinition`-based scheduling
type) — different namespace, different shape, left untouched. The two `SkillInfo` types coexist without
collision (distinct fully-qualified names); callers under `...Script.CardEffectCommons` (AutoProcessing,
PlayCardClass) resolve to the new one via namespace, exactly as the task brief anticipated.

## 3. PlayCardClass — DCGO/Assets/Scripts/Script/CardController.cs:118-933 (933 lines, NOT "small")
Nested inside AS-IS `CardController.cs`'s `#region Play cards`. Ported to
`src/HeadlessDCGO.Engine/Assets/Scripts/Script/PlayCardClass.cs`, namespace `...Script.CardEffectCommons`.
Far larger than the task brief's "small nested/helper class" expectation — it is the top-level
digivolution-cost/DigiXros/Assembly/Burst/AppFusion/cut-in-window orchestrator for playing a card batch, one
~750-line coroutine (`PlayCard()`) plus 2 nested local coroutines and a handful of small sync helpers. Ported
in FULL, unabridged, per the task's explicit instruction ("Port the WHOLE class as AS-IS defines it").

### Mechanical adaptations applied (see file header for the same list, terser here)
- `IEnumerator` -> `Task` on all 4 coroutines in the class (`PlayCard`, nested `SelectCost`, nested-nested
  `SelectCountCoroutine`, `OffMemoryPredictionLine`).
- `yield return ContinuousController.instance.StartCoroutine(X)` -> `await X` (established rule, ~24 sites).
- Two FIRE-AND-FORGET (non-yielded) `ContinuousController.instance.StartCoroutine(OffMemoryPredictionLine())`
  calls (AS-IS :801/:861) -> `_ = OffMemoryPredictionLine();` (wrapper dropped, no `await` added — AS-IS never
  awaited these either).
- Lone `yield return null;` (AS-IS :444, inside `SelectCountCoroutine`) -> `await Task.CompletedTask;`.
- `card.PermanentOfThisCard()` / `cardSource.PermanentOfThisCard()` -> `ICardEffect.
  ResolvePermanentOfThisCard(card)` / `(cardSource)` (3 sites: AS-IS :325/:327/:489 — the mirror
  `CardSource.PermanentOfThisCard()` returns a `PermanentView`, not a `Permanent`; same bridge ICardEffect.cs
  itself established).
- `PlayLog.OnAddLog?.Invoke(...)` (AS-IS :785) stripped (Debug.Log/PlayLog = UI, per task brief).
- `yield return new WaitForSeconds(0.5f);` (AS-IS :929, inside `OffMemoryPredictionLine`) stripped to
  `await Task.CompletedTask;` — a Unity `YieldInstruction` has no `Task` equivalent; same established
  convention as ICardEffect.cs's `Activate_Effect` / BlastDNADigivolution.cs. The actual
  `GManager.instance.memoryObject.OffMemoryPredictionLine()` call the delay guarded is KEPT VERBATIM
  (masked-missing, not stripped — only the bare timing yield has no Task analog).

### Masked-verbatim (referenced exactly as AS-IS text, NOT on the mirror — genuinely missing, logged here,
### NOT stubbed):
- `ContinuousController` — the whole coroutine-runner singleton type (every `ContinuousController.instance.*`
  access not already unwrapped by the StartCoroutine->await rule above: `.autoMinDigivolutionCost`).
- `GManager.instance.GetComponent<T>()` — generic component lookup; the mirror `GManager` (GManager.cs) only
  exposes `turnStateMachine`/`autoProcessing`/`attackProcess`/`Context`. ~13 call sites
  (`GetComponent<SelectDigiXrosClass>()`, `<SelectAssemblyClass>()`, `<SelectDNACondition>()`, `<Effects>()`,
  `<SelectCountEffect>()`).
- `GManager.instance.memoryObject` / `.autoProcessing_CutIn` / `.selectBurstDigivolutionEffect` /
  `.selectAppFusionEffect` / `.IsAI` — GManager fields not yet ported.
- `Effects`, `SelectDigiXrosClass`, `SelectDNACondition` — GManager component types themselves absent
  (`SelectAssemblyClass`/`SelectCountEffect` DO already exist on the mirror at `...Script`, but with
  substrate-adapted `SetUp`/method signatures that do not match the AS-IS named-argument call shape used
  here — expect CS7036/CS1739-family errors at those call sites too, not just CS0246).
- `CardObjectController` — static zone-move helper (`RemoveFromAllArea`/`AddHandCards`/`AddTrashCard`).
- `HandCard` / `FieldPermanentCard` — Unity display component types (`card.ShowingHandCard`,
  `card.Owner.brainStormObject.BrainStormHandCards`, `player.FieldPermanentObjects`).
- `PlayPermanentClass` / `UseOptionClass` — sibling nested CardController classes this class hands the
  filtered permanent/option card lists to at the tail of `PlayCard()`. Out of this port's 4-type scope
  (not requested by the task brief); left as masked-verbatim references, same as everything else above.

None of the above are stubbed, replaced, or simplified — they are referenced with the exact AS-IS
identifier, exactly per the FOUNDATION brief's "reference, do not stub-replace" rule (same posture as
ICardEffect.cs's own MISSING.md-style header note for `GManager.instance`/`CardEffectCommons.*Hashtable`
builders/`EffectList`).

## 4. OnEnterFieldHashtableParams — DCGO/Assets/Scripts/Script/CardController.cs:1100-1146 (47 lines)
Nested inside AS-IS `CardController.cs`'s `#region Play permanents` / `#region Hashtable setting class`.
Ported to `src/HeadlessDCGO.Engine/Assets/Scripts/Script/OnEnterFieldHashtableParams.cs`, namespace
`...Script.CardEffectCommons`. Pure-data holder (ctor + `.Clone()`-copied lists + auto-properties) describing
the digivolution-root/level/root-kind context of a just-entered-field permanent. No Unity/Photon references
in the AS-IS body; `.Clone()` is the already-ported `IEnumerableExtension.Clone` (`...Script` namespace,
`using`d). `SelectCardEffect.Root` (`...Script`, `using`d) referenced verbatim, already resolves. No masked
members — everything this class touches already exists on the mirror.

## Build result (see task report for exact before/after `dotnet build` grep counts)
The 4 target types now resolve at their declaration sites and at every pre-existing reference site in the
already-ported factory/Hashtable layer (the specific CS0246 instances for `PlayCardClass` (×2),
`OptionResolutionClass` (×2), `SkillInfo` (×1), `OnEnterFieldHashtableParams` (×1) are gone). `PlayCardClass`
itself introduces a new batch of CS0246/CS1061-family errors from the masked-verbatim references enumerated
above (ContinuousController, Effects, CardObjectController, SelectDigiXrosClass, SelectDNACondition, HandCard,
FieldPermanentCard, PlayPermanentClass, UseOptionClass, plus GManager/AutoProcessing/SelectCountEffect member
gaps) — this is the expected, intentional cost of porting a 933-line class 1:1 into a codebase where most of
its Unity/GManager dependency surface has not been built yet. No `CS0111`/`CS0101` (duplicate type) errors
were introduced by this pass.
