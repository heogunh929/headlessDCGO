# P6 dispatch-flip unmask remediation — cluster 2: KeyWordEffects factory partials (2026-07-14)

Scope: `Script/CardEffectFactory/KeyWordEffects/*.cs` (minus `BlastDNADigivolution.cs`, cluster 1) + their
`Script/CardEffectCommons/KeyWordEffects/*.cs` helper siblings, per the P6 stageA error inventory
(docs/audit/rebuild_p6_stageA_notes.md). Bar: behavioral identity with AS-IS, no simplification — every gap
either ported 1:1 or STOPped with a design item, never silently stubbed.

Result: **KeyWordEffects error count 106 -> 0** (post-fix full-project build: 113 errors remain, all outside
this cluster's file set — CardEffectFactory.cs monolith/CardSource.cs/other pre-existing P6 gaps, out of scope).

## 1. Foundation additions (shared, additive-only)

* **`Script/DataBase.cs`** — added the ~20 missing `*EffectDiscription`/`*EffectDescription` keyword-text
  helpers (Blocker/Reboot/Pierce/Retaliation/Bilitz/ArmorPurge/Save/Evade/Raid/Barrier/BlastDigivolve/
  BlastDNADigivolve/Fortitude/Alliance/Ascension/Partition/Collision/Vortex/Overclock/Training/Decode/
  Execute/Progress/Link), verbatim from AS-IS `Script/DataBase.cs:446-566`. Pure string constants.
* **`Script/Utils.cs`** — was a migration-scaffold skeleton; ported the one AS-IS member
  (`PluralFormSuffix`), verbatim.
* **`card.CardID` -> `card.CardNumber`** (9 factory files: ArmorPurge/Barrier/Decoy/Evade/Fortitude/
  Fragment/MaterialSave/Partition/Scapegoat) — `CardID` doesn't exist on the mirror `CardSource`;
  `CardNumber` is the established mirror equivalent (same purpose: a unique per-print identifier folded
  into `SetHashString`'s identity key).
* **`new Permanent(new List<CardSource>() { card })` -> `new Permanent(card.Context, card.InstanceId, card.Owner)`**
  (Blitz/Retaliation/Fortitude factory files) — AS-IS's bare-list ad-hoc-Permanent ctor (used when the card
  has no live field permanent, e.g. resolving from the trash) has no mirror overload; the mirror's
  `Permanent.TopCard => new(_context, InstanceId, OwnerId)` makes wrapping the card's own id as its own
  "permanent" identity-equivalent (verified: this is exactly what the AS-IS single-card-list ctor produces).
* **`card.Owner.X` (HandCards/LibraryCards/SecurityCards/Enemy/GetBattleAreaPermanents) -> `new Player(card.Context, card.Owner).X`**
  — `CardSource.Owner`/`Permanent.OwnerId` are `HeadlessPlayerId` (a raw id), not the AS-IS `Player` object;
  the established bridge (already used elsewhere, e.g. Player.cs's own doc comment citing the "BT2_023
  `.Enemy` route") is `new Player(context, playerId)`.
* **`PermanentOf(CardSource, HeadlessEntityId)`** (defined once, `Script/CardEffectCommons/KeyWordEffects/Save.cs`) —
  a live `Permanent` view over an id (owner resolved via the card-instance repository), bridging AS-IS
  `Func<Permanent,bool>` predicate bodies back from the mirror's established `Func<HeadlessEntityId,bool>`
  predicate idiom (`SelectPermanentEffect.SetUp`'s `canTargetCondition`, `HasMatchConditionPermanent`'s id
  overload) — used by Alliance/Save/MaterialSave/Scapegoat/Decoy/Raid.
* **`MatchConditionPermanentCount(CardSource, Func<Permanent,bool>, bool)`** (same file) — AS-IS's
  `Func<Permanent,bool>` global-scan overload of this name; only the `Func<HeadlessEntityId,bool>` overload
  existed on the mirror (`CardEffectCommons.cs:4337`). `HasMatchConditionPermanent`'s Permanent-based
  overload already existed (`CardEffectCommons.cs:4087`) — only the count sibling was missing.
* **`IsOpponentEffect`/`IsOwnerEffect(cardEffect, card)` -> `(cardEffect.EffectSourceCard, card)`** (Progress/
  Scapegoat factory files) — the mirror overloads take `CardSource?`, not `ICardEffect`.
* **Hard rule confirmed**: `Script/CardEffectCommons.cs` (the monolith) is NOT edited — per
  `docs/audit/rebuild_p5_gates_missing.md`'s standing prohibition (found mid-task; 3 pre-existing CS0111
  near-collisions there document the same rule). All additions instead land in the AS-IS-path per-keyword
  sibling files, using the established "second braced namespace block in the same file" convention (bridge
  W1/W3 precedent, `docs/audit/effect_model_rebuild_design_2026-07-13.md` §11.3) for files squatted by the
  `KeywordBaseBatch{1,2}Effect` kind-classes.

## 2. Ported 1:1 (compiles + behaviorally faithful)

Ascension (CanTrigger/CanActivate only, via existing `CanTriggerOnDeletion`/`CanActivateOnDeletion`),
ArmorPurge (`CanActivateArmorPurge` + `ArmorPurgeProcess` via `DeDigivolveHelpers.ArmorPurgeTopAsync` — AS-IS's
own `IDegeneration` doc comment cites this exact helper as the "RemoveFromAllArea + AddTrashCard — top-trash +
promote-under-source" mirror), Barrier (`CanActivateBarrier`/`BarrierProcess` via `IDestroySecurity`), Raid
(`CanActivateRaid`/`RaidProcess` via the existing `AttackProcess.SwitchDefender`), Evade (`CanTriggerEvade`/
`CanActivateEvade`/`EvadeProcess` via `SuspendPermanentsClass`), Pierce (`CanTriggerPierce`/`CanActivatePierce`/
`PierceProcess` via `AttackProcess`/`CanTriggerWhenDeleteOpponentDigimonByBattle`, already fully ported
Hashtable-based), Fortitude (`CanTriggerFortitude`/`CanActivateFortitude`/`FortitudeProcess`, Hashtable-shaped
siblings of the monolith's ctx-shaped ones), Fragment (`FragmentProcess` via
`DigivolutionStackHelpers.TrashSpecificSourcesAsync`, whose own doc header cites AS-IS `ITrashDigivolutionCards`
as its model), Decoy (`DecoyProcess`), Save, Scapegoat, MaterialSave (agnostic to the STOPped predicate — see
§3), Alliance (`CanActivateAlliance`/`AllianceProcess`), Overclock (`CanActivateOverclock` only), Partition
(`CanTriggerPartition`/`CanActivatePartition` only), Progress (`CanActivateProgress`), Blitz
(`CanActivateBlitz(cardSource, activateClass)` AS-IS-signature overload delegating to the existing 1-arg
substrate version), Decode (`CanActivateDecode`/`DecodeProcess`), Jamming (factory-file `==` operator fix
only — the commons `GainJamming` already existed), Training (factory-file adaptations only — `TrainingClass`
itself already existed at the AS-IS path... actually ported inline in the factory `ActivateCoroutine`).

Several of these AS-IS ActivateClass paths (Vortex/Overclock/Partition/Alliance/Decode/Progress/Blitz — all
squatted `KeywordBaseBatch2Effect` files) are **dead-relative to live play**: their own file headers document
that the real behavior is already fully implemented independently via the newer substrate
(`EndOfTurnEffectAttack`/`EffectDrivenAttack`/`DeletionReplacementGate`/`DeletionReplacementTiming`/
`ContinuousKeywordGate`/`AllianceAttackBoost`/`ProgressImmunity`). Porting them here is for old-model
compile-completeness (a few real card/Tfx files — e.g. EX8_074, TfxVortex — still construct these
`CardEffectFactory.X*Effect` objects directly for keyword-grant *registration*), not because they're on the
live resolution path.

## 3. STOPs (heavy/unported subsystems, out of this cluster's KeyWordEffects/CanUseEffects/kind-class/DataBase
   scope — `CardSource.cs`/`Permanent.cs` edits excluded by the task brief)

| design item | function(s) | missing dependency |
|---|---|---|
| RD-P6C2-1 | `AscensionProcess` | `CardObjectController` (the AS-IS static zone-move helper class) doesn't exist on the mirror at all |
| RD-P6C2-2 | `RetaliationProcess` | `DestroyPermanentsClass` (AS-IS batch-delete helper) has no mirror |
| RD-P6C2-3 | `CanActivateDecoy`, `CanActivateFragment` | `Permanent.CanBeDestroyedBySkill` (general effect-deletion immunity scan, Permanent.cs:3309) unported |
| RD-P6C2-4 | `MaterialSaveEffect.CanSelectCardCondition` (factory closure) | `CardSource.IsContainDigiXrosCondition` (+ its `digiXrosCondition`/`HasDigiXros` scan) unported |
| RD-P6C2-5 | `OverclockProcess` | `Permanent.CanAttack` + `SelectAttackEffect` (no mirror component) |
| RD-P6C2-6 | `PartitionProcess` | `PartitionClass` (AS-IS selection/play orchestrator) has no mirror |
| RD-P6C2-7 | `LinkEffect.ActivateCoroutine` | `ILinkCard` (WhenWouldLink trigger window via `autoProcessing_CutIn` + link-cost payment via `GetChangedLinkCost`, itself an existing gap C2-02/MIG5-CANLINK-PAYCOST + `IPlacePermanentToLinkCards`) — all unported |
| RD-P6C2-8 | `CanActivateVortex`, `VortexProcess` | `Permanent.CanAttack`/`CanAttackTargetDigimon` + `SelectAttackEffect` |
| RD-P6C2-9 | `CanActivateExecute`, `ExecuteProcess` | same as RD-P6C2-8 |
| RD-P6C2-10 | `ArtsDigivolveEffect.CanResolveCondition`/`ResolutionCoroutine` | `CardSource.CanPlayCardTargetFrame`/`Permanent.PermanentFrame` + `ContinuousController`/Hashtable-ctor `PlayCardClass` (pre-flagged VERBATIM-MISSING by the file's own P4 header, `docs/audit/rebuild_p4_factory_missing.md`) |
| RD-P6C2-11 | `BlastDigivolveEffect.CanSelectPermanentCondition`/`CanActivateCondition`/`ActivateCoroutine` | same `CanPlayCardTargetFrame`/`PermanentFrame` gap + Hashtable-ctor `PlayCardClass` |

Each STOP is a `throw new NotSupportedException(...)` at the exact function that needs the missing member
(matching the established codebase convention, e.g. `GManager.GetComponent<T>`/`CardSource.cs:511`/
`Permanent.cs:467` etc.) — never a silent no-op, never a behavior guess. Declaration-time gates
(`CanUseCondition`) that don't touch the missing member are ported and portable independently (so a card
using e.g. Link still evaluates "is this legal to declare" correctly; only the resolution body STOPs).

## 4. Non-STOP fidelity notes

* `EvadeProcess`/`DecoyProcess`/`ScapegoatProcess`/`FragmentProcess`/`BarrierProcess` drop the AS-IS trailing
  `willBeRemoveField = false; HideDeleteEffect();` (cancelling a pending-deletion coroutine race + its VFX) —
  no mirror field exists for either. `Headless.Runtime.DeletionReplacementGate`'s own header cites these
  exact AS-IS methods as its behavioral model and already owns the live "does this keyword save the
  permanent" answer independently — so the old-model path performs only the real state mutation (the
  trash/suspend/delete), consistent across all five.
* `PlayPermanentCards`'s mirror signature drops the AS-IS `root: SelectCardEffect.Root` parameter (resolves
  the source zone live off the card) — `Fortitude`/`Decode` adapted accordingly.
* `SelectPermanentEffect.SetUp`/`SelectCardEffect.SetUp`'s mirror `canTargetCondition` is
  `Func<HeadlessEntityId,bool>` (the established entity-id predicate idiom throughout the ported card corpus,
  e.g. BT2_025.cs), not `Func<Permanent,bool>` as AS-IS reads locally — bridged via `PermanentOf` at every
  `SetUp` call site that needed a Permanent-level predicate.

## 5. Build result

`dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj` — KeyWordEffects-scoped errors: 106 -> 0.
Full-project remaining errors (113) are entirely outside this cluster's file set (CardEffectFactory.cs
monolith, CardSource.cs, CanUseEffects/OnDeletion.cs's `PermanentJustBeforeRemoveField` gap,
CanUseEffects/CanSuspend.cs's `Permanent.CanSuspend` gap, HashtableSetting.cs, real-card files BT9_109/
EX8_051/EX8_061/BT1_034/BT2_029/AD1_025, Tfx fixtures) — pre-existing per the stageA inventory, not
introduced by this cluster's work.
