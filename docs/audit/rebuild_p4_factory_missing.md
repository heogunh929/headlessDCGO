# Rebuild P4 — factory vertical-slice work-list (restriction/immunity batch)

Batch: `CanNotSuspend, CanNotUnsuspend, CanNotBlock, CanNotAttack, CanNotBeAttacked, CanNotBeBlocked,
CanNotBeRemoved, CanNotBeDeleted, CanNotBeDeletedByBattle, CanNotBeDeletedByEffect, CanNotBeTrashedByEffect,
CanNotDigivolve, CanNotReturnToHand, CanNoReturnToDeck, TreatAsDigimon, ImmuneFromDPMinus`.

Each mirror stub at `src/.../Script/CardEffectFactory/<name>.cs` was filled with a 1:1 port of the AS-IS
`DCGO/Assets/Scripts/Script/CardEffectFactory/<name>.cs`, returning the ported kind-class. The old
mirror-invented monolith methods of the same name were removed from `CardEffectFactory.cs`.

## Missing `CardEffectCommons.*` / other members

None. Every member referenced by the ported methods already exists on the mirror:

- `CardEffectCommons.IsExistOnBattleAreaDigimon(CardSource)` — CardEffectCommons.cs
- `CardEffectCommons.IsPermanentExistsOnBattleArea(Permanent?)` — CardEffectCommons.cs
- `CardEffectCommons.IsExistOnField(CardSource)` — CardEffectCommons.cs
- `CardEffectCommons.IsPermanentExistsOnField(Permanent?)` — CardEffectCommons.cs
- Base `ICardEffect` surface: `SetUpICardEffect`, `SetIsInheritedEffect`, `SetIsLinkedEffect`,
  `EffectSourceCard`, `ResolvePermanentOfThisCard` — ICardEffect.cs
- All kind-class `SetUp*` methods present in `Script/CardEffects/*.cs`.

## Adaptations applied (substrate only; logic verbatim) — same as ChangeDP.cs template

- `card.PermanentOfThisCard()` (returns PermanentView on mirror) → `ICardEffect.ResolvePermanentOfThisCard(card)`
  in the self-forms (CanNotBlock/CanNotAttack/CanNotBeAttacked/CanNotBeBlocked/CanNotDigivolve).
- `permanent.TopCard.CanNotBeAffected(<ICardEffect>)` → `permanent.TopCard.CanNotBeAffected(<class>.EffectSourceCard?.InstanceId)`
  everywhere the AS-IS passes the effect object.
- Stripped `using UnityEngine;`.

## CARD-MIGRATION-NEEDED

The AS-IS factory signatures DIFFER from the old monolith mirror-invented ones. Callers that bound to the OLD
signatures will not compile against the AS-IS 1:1 methods and must be migrated (NOT fixed in this batch).
Two divergence classes:

### (A) Arity / parameter-shape change — old monolith had fewer/different params

- `CantUnsuspendStaticEffect` — AS-IS `(Func<Permanent,bool> permanentCondition, bool isInheritedEffect,
  CardSource card, Func<bool> condition, string effectName)`; old monolith `(bool isInheritedEffect,
  CardSource card, Func<bool> condition)`.
- `CanNotBlockStaticSelfEffect` — AS-IS `(Func<Permanent,bool> attackerCondition, bool, CardSource, Func<bool>,
  string effectName)`; old `(bool isInheritedEffect, CardSource card, Func<bool> condition)`.
- `CanNotBlockStaticEffect` — AS-IS `(Func<Permanent,bool> attackerCondition, Func<Permanent,bool>
  defenderCondition, bool, CardSource, Func<bool>, string effectName)`; old `(HeadlessPlayerId scopePlayerId,
  bool, CardSource, Func<bool>)`.
- `CanNotAttackStaticEffect` — AS-IS `(Func<Permanent,bool> attackerCondition, Func<Permanent,bool>
  defenderCondition, bool, CardSource, Func<bool>, string effectName)`; old `(HeadlessPlayerId scopePlayerId,
  bool, CardSource, Func<bool>, string effectName)`.
- `CanNotBeAttackedSelfStaticEffect` — AS-IS `(Func<Permanent,bool> attackerCondition, bool, CardSource,
  Func<bool>, string effectName)`; old `(bool isInheritedEffect, CardSource card, Func<bool> condition)`.
- `CanNotBeRemovedStaticEffect` — AS-IS `(Func<Permanent,bool> permanentCondition, bool isInheritedEffect,
  CardSource card, Func<bool> condition, string effectName)`; old `(Func<CardSource,bool> predicate,
  CardSource card, Func<bool> condition = null)`.
- `CanNotDigivolveStaticSelfEffect` — AS-IS `(Func<CardSource,bool> cardCondition, bool, CardSource,
  Func<bool>, string effectName)`; old `(bool isInheritedEffect, CardSource card, Func<bool> condition)`.
- `CanNotDigivolveStaticEffect` — AS-IS `(Func<Permanent,bool> permanentCondition, Func<CardSource,bool>
  cardCondition, bool, CardSource, Func<bool>, string effectName)`; old `(HeadlessPlayerId scopePlayerId,
  string scopeCardType, bool, CardSource, Func<bool>)`.

### (B) `cardEffectCondition` parameter TYPE change — `Func<CardSource,bool>` → `Func<ICardEffect,bool>`

The AS-IS methods take `Func<ICardEffect,bool> cardEffectCondition`; the old monolith mirror lowered this to
`Func<CardSource,bool>`. Callers passing a lambda whose body reads `CardSource` members will not re-infer
against `ICardEffect` and must be migrated.

- `CanNotBeDestroyedBySkillStaticEffect`
- `CanNotBeTrashedBySkillStaticEffect`
- `CannotReturnToHandStaticEffect`
- `CannotReturnToDeckStaticEffect`
- `ImmuneFromDPMinusStaticEffect`

### Compatible (no migration needed) — old monolith signature was a superset/match

`CantSuspendStaticEffect`, `CanNotAttackSelfStaticEffect`, `CanNotBeBlockedStaticSelfEffect`,
`CanNotBeDestroyedStaticEffect`, `CanNotBeDestroyedByBattleStaticEffect`, `TreatAsDigimonStaticEffect`.

## Note — retained mirror-invented alias

`CardEffectFactory.ImmuneStackTrashingClass(bool, CardSource, Func<bool>)` (a METHOD, distinct name) was left
in the monolith; it does not collide with the ported `ImmuneStackTrashingClass` kind-class TYPE because the
type is only referenced in type-position (declaration / `new`), where C# namespace-or-type-name lookup ignores
method members.

---

# Rebuild P4 — factory vertical-slice work-list (stat / cost / requirement batch)

Batch: `ChangeSAttack, ChangeCardDP, ChangeOriginDP, ChangeLinkMax, ChangePlayCost, ChangeDigivolutionCost,
AddLinkRequirement, AddDigivolutionRequirement, AddAppfusionMethod, VortexCanAttackPlayers`.

Each mirror stub at `src/.../Script/CardEffectFactory/<name>.cs` was filled with a 1:1 port of the AS-IS
`DCGO/Assets/Scripts/Script/CardEffectFactory/<name>.cs`, returning the ported kind-class (all kind-classes
in `Script/CardEffects/*.cs` are already ported: `ChangeSAttackClass`, `InvertSAttackClass`,
`ChangeCardDPClass`, `ChangeBaseDPClass`, `ChangeLinkMaxClass`, `ChangeCostClass`, `AddLinkConditionClass`,
`AddDigivolutionRequirementClass` [in `AddEvolutionConditionClass.cs`], `AddAppFusionConditionClass`,
`VortexCanAttackPlayersClass`). The old mirror-invented monolith methods of the same name were deleted from
`CardEffectFactory.cs` (replaced with `// (P4 slice) … moved to …` markers). `ChangePlayCost.cs` replaced a
prior *simplified* `ContinuousSelfModifierEffect`-based stub with the AS-IS `ChangeCostClass` shape verbatim.

Adaptations applied (same template as ChangeDP.cs): `card.PermanentOfThisCard()` →
`ICardEffect.ResolvePermanentOfThisCard(card)` (ChangeSAttack self+invert, ChangeLinkMax self, Vortex
self); `permanent.TopCard.CanNotBeAffected(<ICardEffect>)` →
`…CanNotBeAffected(<class>.EffectSourceCard?.InstanceId)` (ChangeSAttack, ChangeOriginDP, ChangeLinkMax,
Vortex); stripped `using UnityEngine;` (and AddLinkRequirement's stray `using System.Net.NetworkInformation;`).

## Build impact

Zero new errors: build stays at the baseline `70 CS0246 + 274 CS0508` (`-clp:ErrorsOnly`). No CS0111/CS0121
(all same-name monolith duplicates deleted).

## Diverged / verbatim-kept body-level substrate (MASKED — real latent gaps)

These AS-IS expressions were ported VERBATIM (no simplification). They do **not** currently raise a compile
error only because `CardEffectFactory` is a `partial class` whose monolith part carries type-level CS0246
(`IActivatedCardEffect` return types), which suppresses method-BODY diagnostics across every partial of the
class. They are real substrate gaps that will surface once that part binds cleanly:

- **ChangeCardDP** — `GManager.instance.attackProcess.SecurityDigimon == cardSource`. `GManager` /
  `AttackProcess` exist, but the mirror `AttackProcess.SecurityDigimon` is a `HeadlessEntityId?` (AS-IS: a
  `CardSource`), so the `== cardSource` comparison has no applicable operator.
- **AddDigivolutionRequirement** — `cardSource.Owner.CanIgnoreDigivolutionRequirement(permanent, cardSource)`
  (×3). Mirror `CardSource.Owner` is a `HeadlessPlayerId` (a `readonly record struct`); no
  `CanIgnoreDigivolutionRequirement` member/extension exists (only a private static in `DigivolveAction.cs`).
- **AddDigivolutionRequirement** — `permanent.TopCard.CardColors.Contains(cardColor)`. Mirror `CardColors` is
  `IReadOnlyList<string>`; AS-IS compares against a `CardColor` enum value (no string↔enum bridge).
- **AddAppfusionMethod** — `if (permanent.LinkedCards.Find(x => cardConditions[j](x)))`. Relies on
  UnityEngine.Object implicit-bool (`List<CardSource>.Find` returns a `CardSource`; Unity treats non-null as
  truthy). Mirror `CardSource` has no implicit-bool operator.

## CARD-MIGRATION-NEEDED

AS-IS factory signatures that DIFFER from the old monolith mirror-invented ones in a caller-breaking way
(callers bound to the OLD shape must be migrated — NOT fixed in this batch; card files untouched). Note these
call-site breaks are likewise currently masked by the partial-class type-error state.

- `ChangeSAttackStaticEffect` — old 6th param `bool scopeAnyPlayer = false`; AS-IS 6th param is
  `string hashstring = null` (then `bool isLinkedEffect = false`). A caller passing `scopeAnyPlayer:` positionally
  or a 6th bool breaks.
- `ChangeDigivolutionCostStaticEffect` — old had extra SCALAR self overloads `(int/Func<int>, bool, CardSource,
  Func<bool>)` (now gone) and its full overload took `Func<ChoiceZone,bool>? rootCondition`; AS-IS is a single
  generic taking `Func<SelectCardEffect.Root,bool> rootCondition`. Callers of the scalar overload or passing a
  `ChoiceZone` root predicate break.
- `AddDigivolutionRequirementStaticEffect` — old `(string fromColor, int fromLevel, bool, CardSource,
  Func<bool>?)`; AS-IS `(Func<Permanent,bool> permanentCondition, Func<CardSource,bool> cardCondition, bool
  ignoreDigivolutionRequirement, int digivolutionCost, bool isInheritedEffect, CardSource, Func<bool>, string
  effectName, Func<int> costEquation=null, CardColor=None, int level=-1, int minLevel=-1, int maxLevel=-1)`.
  Entirely different — all callers migrate.
- `AddSelfDigivolutionRequirementStaticEffect` — AS-IS inserts a `CardColor cardColor = CardColor.None` param
  (between `costEquation` and `level`) absent from the old mirror; callers passing level/minLevel/maxLevel
  positionally shift.
- `VortexCanAttackPlayersStaticEffect` — AS-IS adds a REQUIRED `string effectName` (no default); old
  `(Func<Permanent,bool>?, bool, CardSource, Func<bool>?)`. Callers omitting `effectName` break. (New
  `VortexCanAttackPlayersSelfStaticEffect` has no old equivalent.)
- `ChangeSecurityDigimonCardDPStaticEffect` — AS-IS `effectName` is REQUIRED (old had `string? effectName =
  null`) and adds `bool islinkedEffect = false`; callers omitting `effectName` break.
- `AddAppfuseMethodByCondition` / `AddAppfuseMethodByName` — param narrowed `IReadOnlyList<…>` → `List<…>`
  (AS-IS types). Callers passing a non-`List` (array / `IReadOnlyList`) must `.ToList()`.

### Compatible (generic `<T>` infers the old scalar; extra params optional) — no migration expected

`ChangeSelfSAttackStaticEffect`, `InvertSAttackStaticEffect`, `ChangeSelfLinkMaxStaticEffect`,
`ChangeLinkMaxStaticEffect`, `ChangeBaseDPGlobalEffect`, `ChangePlayCostStaticEffect`,
`MandatorySelfPlayCostReduction`, `AddSelfLinkConditionStaticEffect`. (Also net-new AS-IS forms with no old
counterpart: `ChangeTargetSAttackStaticEffect`, `InvertSelfSAttackStaticEffect`,
`InvertTargetSAttackStaticEffect`, `ChangeBaseDPStaticEffect`, `ChangeTargetLinkMaxStaticEffect`,
`AddLinkConditionStaticEffect`.)

---

# Rebuild P4 — factory ACTIVATED timing-builders batch

Batch: the `ActivateClass` base builder + all ACTIVATED-effect timing wrappers from AS-IS
`DCGO/Assets/Scripts/Script/CardEffectFactory.cs:910-1462` and `AddDetailClass` (:1523), ported 1:1 IN the
monolith `src/.../Script/CardEffectFactory.cs`:

`ActivateClass, WhenMovingClass, OnPlayClass, WhenDigivolvingClass, WhenAttackingClass, OnDeletionClass,
WhenLinkingClass, SecurityClass, EndOfAttackClass, CounterClass, TurnTimingClass, StartOfYourTurnClass,
StartOfYourMainPhaseClass, EndOfYourTurnClass, YourTurnClass, StartOfOpponentsTurnClass,
StartOfYourOpponentsMainPhaseClass, EndOfYourOpponentsTurnClass, OpponentsTurnClass, EndOfAllTurnsClass,
AllTurnsClass, AddDetailClass`.

Every method now returns the ported `ActivateClass` kind-class (`Script/CardEffects/ActivateClass.cs`) instead
of the old `AsUniformActivated(...)`/`ActivatedEffect`. **21 were ADDED** (the monolith had NO same-name
method — the old model built these inline via `AsUniformActivated`, which remains for other primitives).
**1 was REPLACED in place** (`AddDetailClass`: old `ICardEffect …(Func<bool>?, …) => new DisplayDetailEffect`
→ AS-IS `AddDetailClass …(Func<Hashtable,bool>, …)` returning the kind-class via `SetUpAddDetailClass`).
`EoTLose3Memory` (:1467) is NOT in the batch (coroutine body) — its monolith version was left untouched.

Usings added to the monolith: `using System.Collections;` (Hashtable — not in the project's implicit global
usings) and `using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;` (the `ActivateClass` kind-class
type). No CS0104 ambiguity introduced.

## Async translation (coroutine → Task)

The ONLY substrate edit in every builder: `Func<Hashtable, ActivateClass, IEnumerator> activateCoroutine` →
`Func<Hashtable, ActivateClass, Task>`. The builders have no coroutine body of their own — they pass the
caller's delegate straight through — so the pass-through lambdas
(`hashtable => activateCoroutine(hashtable, activateClass)`) are byte-for-byte verbatim; only the delegate TYPE
changed. The mirror `ActivateClass.SetUpActivateClass` already accepts `Func<Hashtable,Task>`, so this lines up.

One extra substrate adaptation (WhenMovingClass only): `permanent == card.PermanentOfThisCard()` →
`permanent == ICardEffect.ResolvePermanentOfThisCard(card)` — the PermanentView→Permanent bridge, identical to
`ChangeDP.cs` ADAPTATION (1); equality is mirror `Permanent` value equality (CARDSOURCE-EQUALITY).

## Build impact

Zero new errors: build stays at the baseline **70 CS0246 + 274 CS0508** (`-clp:ErrorsOnly`). No CS0111 /
CS0121 (the 21 ADDs collide with nothing; `AddDetailClass` REPLACE is 1-for-1). Verified via a canary
(`int x = "s";` → normally CS0029) planted in the new `ActivateClass` body: it did NOT appear, confirming the
partial-class type-level CS0246 state masks ALL method-BODY diagnostics in this file (same phenomenon noted for
the stat/cost batch above). Therefore the divergent gate calls below are latent, not counted.

## Diverged / verbatim-kept gate calls (MASKED — real latent gaps)

Ported VERBATIM per the fidelity directive. The mirror `CardEffectCommons.CanTrigger*` family was rebuilt
`CardEffectResolveContext ctx`-based, but the AS-IS builders operate on a `Hashtable` — so these will raise
call-site errors (CS1503 arg-type, CS1501 arg-count) once the file binds bodies. Two gates have NO mirror
counterpart at all (would be CS0117). All are kept as AS-IS wrote them (no ctx-bridge invented):

- `CardEffectCommons.CanTriggerOnMove(hashtable, PermanentCondition)` — mirror `(ctx, card, permCond=null)`:
  arg1 `Hashtable`→`CardEffectResolveContext` mismatch + AS-IS omits the `card` arg the mirror requires.
- `CardEffectCommons.CanTriggerOnPlay(hashtable, card)` — mirror `(ctx, card, rootCond=null)`: arg1 type.
- `CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card)` — mirror `(ctx, card, rootCond=null)`: arg1 type.
- `CardEffectCommons.CanTriggerOnAttack(hashtable, card)` — mirror `(ctx, card)`: arg1 type.
- `CardEffectCommons.CanTriggerOnDeletion(hashtable, card)` — mirror `(ctx, card)`: arg1 type.
- `CardEffectCommons.CanActivateOnDeletion(hashtable, card)` — mirror `(ctx, card)`: arg1 type.
- `CardEffectCommons.CanTriggerWhenLinking(hashtable, null, card)` — mirror `(ctx, card, permCond=null)`:
  AS-IS arg order/shape `(hashtable, permCond, card)` vs mirror `(ctx, card, permCond)`.
- `CardEffectCommons.CanTriggerSecurityEffect(hashtable, card)` — mirror `(ctx, card)`: arg1 type.
- `CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, permCond)` — mirror `(ctx, card, permCond)`:
  arg1 type + AS-IS omits the `card` arg the mirror requires.
- `CardEffectCommons.IsExistOnBattleAreaDigimonTrigger(card, activateClass)` — **NO mirror member** (WhenLinkingClass).
- `CardEffectCommons.IsExistOnBattleAreaDigimonActivate(card, activateClass)` — **NO mirror member** (WhenLinkingClass).

Gates that DO match the mirror (no gap): `IsExistOnBattleAreaTrigger(card, cardEffect)`,
`IsExistOnBattleAreaActivate(card, cardEffect)`, `IsOwnerTurn(CardSource)`, `IsOpponentTurn(CardSource)`,
`IsOpponentPermanent(permanent, card)`, and the full base `ICardEffect` setter surface
(`SetUpICardEffect(string, Func<Hashtable,bool>, CardSource)`, `SetUpActivateClass`, `SetHashString`,
`SetIsSecurityEffect`, `SetIsSkippableFunction`, `SetIsSkippable`, `SetIsCounterEffect`) +
`ActivateClass.SetUpActivateClass`. The whole `Hashtable`↔`ctx` impedance is the design item to resolve when
this layer is un-masked (either add `Hashtable` gate overloads or thread a ctx through the ActivateClass model).

## CARD-MIGRATION-NEEDED

- `AddDetailClass` — `canUseCondition` param TYPE changed `Func<bool>?` → `Func<Hashtable,bool>` (AS-IS) and
  return type `ICardEffect` → `AddDetailClass`. Any caller passing a `Func<bool>` lambda breaks. Currently
  ZERO card-file callers (and zero engine callers), so no migration needed today.
- The 21 timing builders take `Func<Hashtable, ActivateClass, Task> activateCoroutine`. Cards written against
  the OLD `AsUniformActivated`/`IEffectBody` model (which never called these same-name methods — they didn't
  exist) are unaffected; cards ported from AS-IS supply an `async Task (Hashtable, ActivateClass)` body and
  match directly. No existing caller breaks (the methods are net-new to the monolith).

# Rebuild P4 — KeyWord factory SYNC batch (kind-class returns)

Ported 9 AS-IS `CardEffectFactory/KeyWordEffects/*.cs` factory partials 1:1 (returning the ported kind-class),
replacing the monolith's old mirror-invented `<Keyword>Self/StaticEffect` wrappers (deleted to avoid
CS0111/CS0121). Files: ArtsDigivolve, Ascension, Blocker, Collision, Iceclad, Jamming, Progress, Reboot, Rush.

## Build impact
- Baseline `70 CS0246 + 274 CS0508` (344) UNCHANGED after the batch. No new CS0111/CS0121/CS0101 (no duplicate
  types/methods). Net new declaration errors = 0 at the aggregate.
- Only ONE error surfaces in the 9 new files: `ArtsDigivolve.cs(18): CS0246 'OptionResolutionClass'` — the AS-IS
  return kind-class (DCGO/Assets/Scripts/Script/OptionResolutionClass.cs) is NOT YET mirror-ported.
- All member-level references in the OTHER 8 files bind cleanly. Note: body-level SEMANTIC errors in these
  files are SUPPRESSED by the pervasive baseline CS0508 error-type cascade (verified: a parse error IS reported,
  but a bogus-type body reference is NOT). So the genuinely-missing members below are MASKED latent gaps found
  by source grep, not by the compiler.

## Missing `CardEffectCommons.*` / `DataBase.*` / other members (verbatim-kept per no-simplification directive)
- `CardEffectCommons.CanTriggerAscension(Hashtable, CardSource)` — MISSING (Ascension gate). Masked.
- `CardEffectCommons.CanActivateAscension(Hashtable, CardSource)` — MISSING (Ascension activate gate). Masked.
- `CardEffectCommons.AscensionProcess(Hashtable, ActivateClass, CardSource)` returning `Task` — MISSING
  (Ascension resolution coroutine; IEnumerator→Task per the ActivateClass substrate convention). Masked.
- `DataBase.AscensionEffectDescription()` — MISSING (Ascension effect description). Masked.
- `OptionResolutionClass` (+ instance `SetUpOptionResolutionClass(Func<CardSource,IEnumerator>, Func<CardSource,bool>)`)
  — MISSING kind-class (ArtsDigivolve). SURFACED as CS0246. Blocks binding the rest of the ArtsDigivolve body.
- `ContinuousController` (`.instance.StartCoroutine`) — MISSING (ArtsDigivolve coroutine runner). Masked behind
  the OptionResolutionClass CS0246 (AS-IS coroutine substrate; kept verbatim, no Task adaptation because the
  whole OptionResolutionClass infra is absent).
- `PlayCardClass` (ctor + `.PlayCard()`) — MISSING (ArtsDigivolve cost-free play). Masked.
- Present & bound (no gap): kind-classes Blocker/Collision/Iceclad/Reboot/Rush/CanNotBeDestroyedByBattle/
  CanNotAffected/ActivateClass + their SetUp* methods; `CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect`
  (Jamming); `GManager.instance.attackProcess.{SecurityDigimon,IsAttacking,AttackingPermanent}` (Jamming/Progress);
  `CardEffectCommons.{IsExistOnBattleAreaDigimon,IsPermanentExistsOnBattleArea,IsExistOnBattleArea,
  CanActivateProgress,IsOpponentEffect,IsExistOnExecutingArea,HasMatchConditionPermanent,IsOwnerPermanent}`;
  `SelectPermanentEffect` / `SelectCardEffect.Root.Execution`; `ICardEffect.ResolvePermanentOfThisCard`.

## Adaptations applied (substrate only; logic verbatim)
- `card.PermanentOfThisCard()` (AS-IS treated as `Permanent`) → `ICardEffect.ResolvePermanentOfThisCard(card)` in
  Blocker/Collision/Iceclad/Jamming/Reboot/Rush self-`PermanentCondition` and Progress `CardCondition`.
- Ascension `ActivateCoroutine`: AS-IS `IEnumerator` → `Task` (mirror `ActivateClass.SetUpActivateClass` takes
  `Func<Hashtable,Task>`; documented IEnumerator→Task coroutine adaptation).
- Stripped `using UnityEngine;` (Ascension/Blocker/Iceclad/Jamming/Progress/Rush). No Photon/Debug/PlayLog present.
- ArtsDigivolve kept fully verbatim (IEnumerator coroutines + ContinuousController/PlayCardClass) because its
  OptionResolutionClass substrate model is entirely mirror-absent.

## Iceclad / Progress note
- The monolith previously had ONLY the `*SelfStaticEffect` wrapper for Iceclad and Progress (no non-self
  `IcecladStaticEffect`/`ProgressStaticEffect`). The AS-IS files declare BOTH; the ported files ADD the non-self
  variants (net-new, no conflict).

## KEYWORD-STATICCLASS-CONSUMERS
- NONE. The old mirror-invented `static class {Blocker,Jamming,Reboot,Rush,Progress}` (with a single `.Create`
  method, namespace `...CardEffectFactory.KeyWordEffects`) had ZERO consumers in `src/` (`*.Create` grep = 0), so
  overwriting them with the `partial class CardEffectFactory` port left no masked body errors.

---

# P4 KeyWord ASYNC batch (2026-07-13)

Batch (23 files): `Alliance, ArmorPurge, Barrier, BlastDNADigivolution, BlastDigivolution, Blitz, Decode, Decoy,
Evade, Execute, Fortitude, Fragment, Link, MaterialSave, Overclock, Partition, Pierce, Raid, Retaliation, Save,
Scapegoat, Training, Vortex`. Each mirror `KeyWordEffects/<name>.cs` overwritten with the 1:1 AS-IS port
(`partial class CardEffectFactory`, namespace ...CardEffectCommons). Old mirror-invented monolith methods of the
same name removed from CardEffectFactory.cs. Build after: **68 CS0246 + 274 CS0508** (baseline 70 CS0246 + 274
CS0508 — CS0246 fell by 2, no rise; **zero CS0111/CS0121/CS0101**, zero errors in any of the 23 ported files —
their bodies are masked as expected).

## ASYNC translation applied (ActivateClass.SetUpActivateClass takes `Func<Hashtable,Task>`)
- Coroutine `IEnumerator ActivateCoroutine` whose body is a pure delegating `return CardEffectCommons.XProcess(...)`
  (no yields) -> non-async `Task ActivateCoroutine` (return-type swap, body verbatim): Alliance(x2), ArmorPurge,
  Barrier, Blitz, Decode, Decoy, Execute, Fragment, MaterialSave, Overclock, Partition, Pierce, Raid, Retaliation,
  Save, Scapegoat, Vortex.
- Coroutine WITH yields -> `async Task` + `await`: Evade (`await CardEffectCommons.EvadeProcess`), Fortitude
  (`await CardEffectCommons.FortitudeProcess`), Training (`await new SuspendPermanentsClass(...).Tap()` /
  `await ...AddDigivolutionCardsBottom(...)`), BlastDigivolution / BlastDNADigivolution / Link (nested
  `IEnumerator Select*Coroutine` -> `async Task`; `yield return StartCoroutine(X)` -> `await X`; lone
  `yield return null` -> `await Task.CompletedTask;`).
- Signature param `Func<IEnumerator> beforeOnAttackCoroutine` -> `Func<Task>` (Blitz).

## Auxiliary-type handling (NOT re-declared / preserved)
- `PartitionCondition` (AS-IS top-level, Partition.cs): the MIRROR already carries it (string-typed colours) with
  a `PartitionConditionsKey` const consumed across ~15 files. PRESERVED VERBATIM in namespace
  ...CardEffectFactory.KeyWordEffects (block-namespace kept in the same file); the old invented `static class
  Partition` (.Create) dropped. Ported `PartitionEffect` resolves it via `using ...KeyWordEffects;`.
- `Decode`: AS-IS Decode.cs has NO aux type, but the mirror-invented `static class Decode.DecodeSourceConditionKey`
  const HAS external consumers (DeletionReplacementTiming.cs, CardLeavePlayCleanup.cs). PRESERVED (const-only
  holder) in namespace ...KeyWordEffects (block namespace); its old `.Create` dropped.
- `BlastDNACondition` (AS-IS top-level): the mirror already has a `BlastDNACondition` record in
  ...CardEffectCommons (CardPortingFramework.cs) with a DIFFERENT shape (Matches/Label vs Name/Permanents/
  CardSources). NOT re-declared (would be CS0101). Ported body's `.Name/.Permanents/.CardSources` field accesses
  are masked verbatim-missing members.

## Distinct missing members referenced verbatim (EXPECTED — kept, bodies masked)
Mutation / process helpers on `CardEffectCommons` (absent on mirror): `AllianceProcess, ArmorPurgeProcess,
BarrierProcess, BlitzProcess, DecodeProcess, DecoyProcess, EvadeProcess, ExecuteProcess, FortitudeProcess,
FragmentProcess, MaterialSaveProcess, OverclockProcess, PartitionProcess, PierceProcess, RaidProcess,
RetaliationProcess, SaveProcess, ScapegoatProcess, VortexProcess`; gate helpers `CanActivate{Alliance,ArmorPurge,
Barrier,Blitz,Decode,Decoy,Evade,Execute,Fortitude,Fragment,MaterialSave,Overclock,Partition,Pierce,Raid,
Retaliation,Save,Scapegoat,Vortex,SuspendCostEffect}`, `CanTrigger{OnPermanentAttack,WhenPermanentRemoveField,
Evade,Fortitude,Pierce,Partition,OnPermanentDeleted,WhenRemoveField,OnDeletion,OnAttack,WhenDigivolving,OnPlay}`,
plus `GetAttackerFromHashtable, HasMatchConditionOwnersPermanent, IsPermanentExistsOnOwnerBattleAreaDigimon,
IsByBattle, IsByEffect, IsOwnerEffect, IsOpponentPermanent, HasMatchConditionPermanent, MatchConditionPermanentCount,
CardEffectHashtable`; `DataBase.<Keyword>EffectDiscription` (all); `ActivateClass.SetIsCounterEffect/
SetEffectSourcePermanent/SetRootCardEffect`; substrate types `SelectPermanentEffect, SelectHandEffect, GManager,
CardObjectController, PlayCardClass, ILinkCard, SuspendPermanentsClass, FieldCardFrame, SelectCardEffect(.Root),
Utils.PluralFormSuffix`; CardSource/Permanent members `linkCondition, IsLinked, PreferredFrame, fieldCardFrames,
CanPlayJogress, CanPlayCardTargetFrame, PermanentFrame, DigivolutionCards.Clone/Filter, HasCardColor, HasLevel,
Level, EqualsCardName, ContainsCardName, CardNames, IsContainDigiXrosCondition, CanNotEvolve, GetBattleAreaDigimons,
GetBattleAreaPermanents, LibraryCards, HandCards, AddDigivolutionCardsBottom, CanPlayJogress`. All are masked (no
visible CS error) inside the ported partial-class bodies.

## KEYWORD-STATICCLASS-CONSUMERS (ASYNC batch)
- The old mirror-invented `static class {Alliance, ArmorPurge, Blitz, Overclock, Pierce, Retaliation, Vortex}`
  (each a single `.Create`, namespace ...KeyWordEffects) had **ZERO** consumers in `src/` — safely replaced.
- `static class Partition` (.Create): ZERO consumers — dropped (but sibling `PartitionCondition` PRESERVED).
- `static class Decode`: its `.Create` had zero consumers (dropped), but its `DecodeSourceConditionKey` const
  DOES have consumers (PRESERVED — see aux-type handling).

## P4 FACTORY — ACTIVATED inline-mutation methods (coroutine-body factory methods)
1:1 rewrite from AS-IS CardEffectFactory.cs of the 14 factory methods whose bodies are inline coroutines calling
mutation helpers (excluded from the earlier timing-builder port). Coroutine->async Task
(`yield return ContinuousController.instance.StartCoroutine(X)` -> `await X`; lone `yield return null` ->
`await Task.CompletedTask`); `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)`;
`x.CanNotBeAffected(effect)` -> `x.CanNotBeAffected(effect.EffectSourceCard?.InstanceId)`; PlaySE/WaitForSeconds =
UI (stripped, per mirror-wide convention).

REWRITTEN in place (old mirror-invented declarative version replaced):
- PlaySelfTamerSecurityEffect (AS-IS :148)  — async ActivateCoroutine
- PlayMindLinkTamerFromDigivolutionCards (AS-IS :196) — async ActivateCoroutine + async SelectCardCoroutine; PermanentOfThisCard bridged
- PlaySelfDigimonAfterBattleSecurityEffect (AS-IS :285) — 3 nested async coroutines; PermanentOfThisCard + CanNotBeAffected bridged; PlaySE/WaitForSeconds stripped; removed its now-unused DeleteTimingString helper
- PlaceSelfDelayOptionSecurityEffect (AS-IS :512) — async ActivateCoroutine
- ReplaceBottomSecurityWithFaceUpOptionMainEffect (AS-IS :599) — coroutine lambda returns Task
- ReplaceTopSecurityWithFaceUpOptionMainEffect (AS-IS :622) — coroutine lambda returns Task
- ReplaceBottomSecurityWithFaceUpOptionEffect (AS-IS :645) — IEnumerator -> async Task
- UseRequirements (AS-IS :722) — verbatim (returns IgnoreColorConditionClass; sig now takes bare Func<CardSource,bool>)
- GetJogressConditionClass (AS-IS :752) — verbatim (returns AddJogressConditionClass; canUseCondition is Func<Hashtable,bool>)
- DigiXrosEffectFromNames (AS-IS :784) — verbatim (returns AddDigiXrosConditionClass)

ADDED (absent on mirror):
- ActivateMainOptionSecurityEffect (AS-IS :551) — Func<ICardEffect,IEnumerator> afterMainEffect -> Func<ICardEffect,Task>; async ActivateCoroutine
- ReplaceTopSecurityWithFaceUpOptionEffect (AS-IS :684) — IEnumerator -> async Task
- ActivateClassesForSharedEffects (AS-IS :828) — activateCoroutine param IEnumerator -> Task (matches the mirror timing-builder classes' signature); body verbatim (dispatches to the existing WhenMoving/OnPlay/.../Counter Class builders)
- PlaceToSecurityEffect (AS-IS :1497) — IEnumerator ResolutionCoroutine -> async Task; returns OptionResolutionClass

SKIPPED:
- AddDetailClass (AS-IS :1523) — mirror version already AS-IS-shaped 1:1 (Func<Hashtable,bool> canUseCondition, returns AddDetailClass kind-class). No change.

### Distinct genuinely-missing member introduced by this work (verbatim-kept, RED)
- `OptionResolutionClass` (CS0246) — the kind-class returned by PlaceToSecurityEffect. No mirror declaration exists
  (its port is a separate kind-class slice). This is the ONLY new missing type; all other substrate/mutation
  members these bodies reference (CardEffectCommons.{CanTriggerSecurityEffect, PlayPermanentCards, IsExistOnBattleArea,
  IsExistOnExecutingArea, CanPlayAsNewPermanent, PlaceDelayOptionCards, OptionMainEffect, OptionMainCheckHashtable,
  CanTriggerOptionMainEffect, HasMatchConditionOwnersPermanent, HasMatchConditionOwnersBreedingPermanent,
  IsPermanentExistsOnOwnerBattleArea, IsPermanentExistsOnBattleArea, IsOwnerTurn, IsOpponentTurn, CardEffectHashtable,
  GetJogressConditions, GetDigiXrosConditionsFromNames}, CardObjectController.{AddHandCards, AddSecurityCard},
  ContinuousController.{instance, nullSkillInfos}, GManager.instance.GetComponent<Effects>().CreateRecoveryEffect,
  new DestroyPermanentsClass(...).Destroy(), SelectCardEffect.{Root.*, Mode.Custom, SetUp, ...}, IReduceSecurity,
  IAddSecurity, ActivateClass.{SetIsSecurityEffect, SetIsInheritedEffect, SetEffectSourcePermanent, EffectName,
  EffectDiscription, Activate}, IgnoreColorConditionClass, AddJogressConditionClass, AddDigiXrosConditionClass,
  JogressCondition, DigiXrosCondition) already RESOLVE on the mirror (verified by clean build — zero errors on them).

### Build after this slice
65 CS0246 + 274 CS0508 (from ~68 CS0246 + 274 CS0508 baseline — CS0246 FELL by 3; the only monolith-file CS0246 are
7x pre-existing IActivatedCardEffect on untouched declarative methods + 1x new OptionResolutionClass). No CS0111/CS0121
duplicates. CS0508 unchanged.
