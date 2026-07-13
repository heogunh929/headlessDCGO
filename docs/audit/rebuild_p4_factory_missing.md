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
