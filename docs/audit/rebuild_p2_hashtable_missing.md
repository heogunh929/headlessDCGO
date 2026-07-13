# Effect-model rebuild — Phase 2 (Hashtable layer) missing-member work-list

Written alongside the P2 pass of `docs/audit/effect_model_rebuild_design_2026-07-13.md` step 2 ("Hashtable
layer"). Every symbol below is referenced **verbatim** (AS-IS-named, per the porting brief: "reference, do NOT
stub-replace") in one of the three P2 files because it is not yet ported / not present on the mirror. Nothing
here is fixed by this pass — it is the work-list for the following rebuild steps (3: per-kind effect classes,
4: factory, and the CardController.cs `IBattle` slice). The project is intentionally RED; these are the
declaration/body errors expected from the P2 files.

## Files written this pass

- `Assets/Scripts/Script/CardEffectCommons/HashtableSetting.cs` — 1:1 port of AS-IS HashtableSetting.cs,
  17 builder methods, `public static partial class CardEffectCommons`.
- `Assets/Scripts/Script/CardEffectCommons/GetFromHashtable.cs` — 1:1 port of AS-IS GetFromHashtable.cs,
  39 accessor methods, `public static partial class CardEffectCommons`.
- `Assets/Scripts/Script/CardEffectCommons/IBattle.cs` — new; AS-IS `IBattle` data-holder type
  (ctor + AttackingPermanent/DefendingPermanent/DefendingCard/IsWithoutAttack/hashtable + enemyPermanent +
  CompareStats). `Battle()` coroutine intentionally not re-housed here (RD-P2-01 below).

Builder/accessor key-string contract (the strings the two partial files share) verified identical between
HashtableSetting.cs and GetFromHashtable.cs: `CardEffect`, `hashtables`, `battle`, `Permanent`, `TopCard`,
`Card`, `AttackingPermanent`, `Permanents`, `WinnerPermanents_real`, `LoserPermanents`, `DiscardedCards`,
`CardSources`, `DigivolutionSources`, `Root`, `isEvolution`, `isJogress`, `digixros`, `DigiXrosCount`,
`DPZero`, `PlayCardClass`, `evoRoots`, `evoRootTops`, `oldLevels`.

## Substrate adaptations applied (NOT missing — recorded for traceability)

- **Transient single-card permanent**: AS-IS `new Permanent(new List<CardSource>(){ cardSource })` ->
  `new Permanent(cardSource.Context, cardSource.InstanceId, cardSource.Owner)` (same as ICardEffect.IsOnDeletion).
  Sites: `PierceCheckHashtableOfPermanent` (LoserPermanent, LoserPermanents_real from `opponentCard`),
  `OnPlayCheckHashtableOfCard`, `WhenDigivolvingCheckHashtableOfCard`, and the `??` fallback of
  `OnAttackCheckHashtableOfCard`.
- **PermanentView->Permanent bridge**: AS-IS `cardSource.PermanentOfThisCard() ?? new Permanent(...)`
  (`OnAttackCheckHashtableOfCard`, HashtableSetting.cs:303) -> `ICardEffect.ResolvePermanentOfThisCard(cardSource)
  ?? new Permanent(cardSource.Context, cardSource.InstanceId, cardSource.Owner)`.

## MISSING types (referenced verbatim, no mirror type)

| Type | AS-IS anchor | Referenced by |
|---|---|---|
| `IBattle` (parameters/casts) | new type this pass | The type itself is now created, but its `Battle()` slice is deferred — see RD-P2-01. Referenced as a param type in `WhenPermanentWouldRemoveFieldCheckHashtable`/`OnDeletionHashtable` and cast in `GetBattleFromHashtable`. |
| `SelectCardEffect.Root` | GetFromHashtable.cs:50 / HashtableSetting.cs:178,198 | `GetRootFromHashtable`, `WouldEnterFieldHashtable`, `WouldLinkHashtable`. Nested enum `SelectCardEffect.Root` (with `.None`) — no mirror `SelectCardEffect` type yet (rebuild step 3, Select* kind classes). |
| `PlayCardClass` | HashtableSetting.cs:178 / GetFromHashtable.cs:399,414 | `WouldEnterFieldHashtable`, `IsOnly1CardPlayed`, `GetPlayCardClassFromHashtable`. `.CardSources` member also referenced. No mirror type. |
| `OnEnterFieldHashtableParams` | HashtableSetting.cs:135 | `OnEnterFieldHashtable` param type — **whole builder's parameter type is unported** (method ported with the type named verbatim in signature). Members referenced: `.IsFromDigimonDigivolutionCards`, `.Permanent`, `.EvoRoots`, `.EvoRootTops`, `.Root`, `.OldLevels`, `.DigixrosCount`, `.AssemblyCount`. |
| `SkillInfo` | GetFromHashtable.cs:30 | `GetSkillFromHashtable` (`List<SkillInfo>`). No mirror type. |
| `Player` | GetFromHashtable.cs:457,477 | `GetPlayerFromHashtable`, `GetPlayersFromHashtable`. A mirror `Player` exists (CardEffectCommons/GameContext/Player) — verify the `is Player` / `List<Player>` cast target matches; listed pending confirmation. |
| `CardColor` | HashtableSetting.cs:111 | `OnDeletionHashtable` (`List<CardColor> cardColors = permanent.TopCard.CardColors.Clone()`). Mirror `CardSource.CardColors` returns `IReadOnlyList<string>`, not `List<CardColor>` — the color-model reconciliation is the same rebuild-step-3 decision noted in rebuild_p1_missing.md. |

## MISSING extension methods (custom AS-IS LINQ-style helpers, no mirror definition)

Referenced verbatim; the mirror has no `Clone`/`Filter`/`Map`/`Some` list extensions (AS-IS ships them as a
static extension class not yet ported):

- `List<T>.Clone()` — HashtableSetting.cs (`permanents.Clone()`, `permanent.cardSources.Clone()`,
  `CardNames.Clone()`, `CardColors.Clone()`, `DigivolutionCards.Clone()`, `EvoRoots.Clone()`,
  `EvoRootTops.Clone()`, `OldLevels.Clone()`, `hashtableParams.Clone()`).
- `List<T>.Filter(Func<T,bool>)` — HashtableSetting.cs (`OnDeletionHashtable`, `OnEnterFieldHashtable`);
  GetFromHashtable.cs (`GetPlayedPermanentsFromEnterFieldHashtable`).
- `List<T>.Map<U>(Func<T,U>)` — HashtableSetting.cs (`OnDeletionHashtable`, `OnEnterFieldHashtable`);
  GetFromHashtable.cs (`GetPlayedPermanentsFromEnterFieldHashtable`).
- `List<T>.Some(Func<T,bool>)` — HashtableSetting.cs:144 (`OnEnterFieldHashtable`).

## MISSING members on already-ported mirror types

### `Permanent` (Assets/Scripts/Script/CardEffectCommons/Permanent.cs)

- `Permanent.cardSources` (property) — HashtableSetting.cs (`permanent.cardSources`, `new Permanent(permanent.cardSources)`);
  GetFromHashtable.cs:827 (`permanent.cardSources.Contains(card)`). Mirror `Permanent` is instanceId-backed and
  exposes no `cardSources` list.
- `new Permanent(List<CardSource>)` — the **full-stack** transient ctor (distinct from the single-card
  adaptation). HashtableSetting.cs:26,57 (`new Permanent(permanent.cardSources)`). No such mirror ctor — kept
  verbatim (deliberately NOT lowered, since the mirror ctor needs `EngineContext` which `Permanent` does not
  expose publicly).
- `Permanent.IsDestroyedByBattle` (settable) — HashtableSetting.cs:31 (object initializer). Not on mirror.
- `Permanent.HasIceclad` — IBattle.cs `CompareStats`. Not on mirror `Permanent`.

### `CardSource` (Assets/Scripts/Script/CardEffectCommons/CardSource.cs)

- `CardSource.HasSaveText` — HashtableSetting.cs:121. Not on mirror `CardSource`.
- `CardSource.Owner.Enemy` — HashtableSetting.cs:18 (`permanent.TopCard.Owner.Enemy`). Mirror `CardSource.Owner`
  is a `HeadlessPlayerId` (no `.Enemy`); AS-IS `Owner` is a `Player`. Kept verbatim.

### `GManager` (Assets/Scripts/Script/CardEffectCommons/GManager.cs)

- `GManager.instance.turnStateMachine.gameContext.ActiveCardList` — HashtableSetting.cs:18
  (`.ActiveCardList.Find(...)`). `gameContext` exists on the mirror; `ActiveCardList` member not confirmed present.

### `Mathf` shim (later step)

- `Mathf.Clamp(int, int, int)` — IBattle.cs `CompareStats`. Kept verbatim per brief (a mirror `Mathf` shim is a
  later step); NOT lowered to `System.Math`.

## Design items

- **RD-P2-01** — AS-IS `IBattle.Battle()` (`IEnumerator`, CardController.cs:4474-4772) is not re-ported in
  `IBattle.cs`. Its combat-pipeline logic is already migrated to the mirror substrate
  (`Headless/Runtime/BattleResolver.cs` + `SecurityResolver.cs`, driven by mirror `AttackProcess.cs`); IBattle
  is a remaining CardController.cs migration slice (CardController.cs:19 header). Re-housing `Battle()` here now
  would duplicate the migrated pipeline. When the CardController.cs `IBattle` slice lands, `Battle()` rehouses
  into this type and BattleResolver becomes the substrate it calls.
