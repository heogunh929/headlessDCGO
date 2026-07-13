# P6 dispatch-flip Stage A — registry/registrar/activation seam (2026-07-14)

Scope (per brief): make the ENGINE consume new-model `ICardEffect` objects for REGISTRATION +
ACTIVATED resolution. Continuous/restriction gates = stage B (untouched). RED baseline 59 CS0246
(`IActivatedCardEffect`, all DECLARATION-site errors — Roslyn skips method-body binding while
declaration errors exist, so the whole project's method bodies are currently UNCHECKED; lifting the
59 unmasks the true error surface).

## 1. Enumeration-vs-registry decision — ENUMERATION (no central registry), evidence

AS-IS has NO central effect registry. Authoritative anchors:

* `AutoProcessing.GetSkillInfos(hashtable, timing, cond)` (AS-IS AutoProcessing.cs:770-887) —
  every collection pass re-enumerates LIVE, in this order: (1) `player.EffectList(timing)` per
  `gameContext.Players_ForTurnPlayer`, (2) field permanents `permanent.EffectList(timing)`,
  (3) `player.TrashCards` cards' `EffectList`, (4) `player.HandCards`, (5) non-flipped
  `player.SecurityCards`. Filter per effect: `is ActivateICardEffect` && `!IsBackgroundProcess`
  && `CanTrigger(hashtable)` → `new SkillInfo(cardEffect, hashtable, timing)`.
* `CardSource.EffectList` → `cEntity_EffectController.GetCardEffects(timing, card)` →
  `cEntity_Effect.GetCardEffects` (per-card component attached at setup;
  AS-IS CEntity_EffectController.cs:179-241 `AddCardEffect` reflection). Card `CardEffects`
  overrides construct FRESH `ICardEffect` objects per call; per-turn caps survive because
  `isOverMaxCountPerTurn` counts by `IsSameEffect` (EffectSourceCard equality + HashString +
  RootCardEffect — AS-IS ICardEffect.cs:860-933), NOT object identity. (Mirror `CardSource`
  now has value equality — CARDSOURCE-EQUALITY resolved 2026-07-13 — so this works.)
* Registration-at-enter-play does not exist in AS-IS; the only per-card persistent state is
  `CEntity_EffectController` (use counts `UseEffectsThisTurn` + the `cEntity_Effect` reference).

DECISION: registration of ACTIVATED/new-model effects becomes a NO-OP for availability; effect
availability comes from live `EffectList`/`CardEffects(timing, card)` enumeration at collection
time. The one thing "registration" must guarantee is that the per-instance
`CEntity_EffectController.cEntity_Effect` is populated (the AS-IS AddCardEffect-at-setup analog)
— done lazily in `CEntity_EffectControllerStore.GetOrCreate` via `CardEffectDispatch`.

The substrate `EffectRegistry` (EffectBinding store) STAYS for stage B consumers (continuous /
restriction gates, runtime GRANTS from mutation helpers, scheduler-half triggered bindings) — it
is not the activated path any more.

## 2. What blocked the "retire old primitives" option

Brief said "real cards: 0" still consume the old model. grep says otherwise: **~80 real card files**
(witness corpus from the C/D/E/F goals — BT9_043/BT9_062/BT22_003/BT22_035/BT24_018/AD1_025/
EX1_072/EX8_074/ST4_13/… plus BT1/BT2 stragglers BT1_078/086/088/101/103, BT2_023/045/050/070/
080/081/085, ST2_15/ST3_13/14/ST4_11/13/14/ST5_09, BT8_057/090/092, BT15/BT16/BT19/EX10/ST15/
ST16…) still construct old-model types directly (`new ActivatedEffect(...)`, `new DrawEffect(...)`,
`new ActivatedSelectEffect(...)`, `SelectDestroyThenTrashSecurityBody`, …). These were ported in
old-model goal waves and never re-ported; only BT1/BT2/ST re-port batches went AS-IS-verbatim.
So `ActivatedEffects.cs`/`ActivatedEffect.cs` CANNOT be deleted in stage A without deleting live
witness-card behavior. Consumers of the old model after stage A:
  (a) ~80 real card files above (re-port batch backlog),
  (b) Tfx fixtures (~32),
  (c) `ActivatedEffectResolver`'s legacy switch cases (kept),
  (d) `CardEffectFactory.cs` legacy factory methods (kept while (a)/(b) call them).

Resolution instead: **resurrect `IActivatedCardEffect` as a legacy-bridge BASE CLASS deriving from
the new abstract `ICardEffect`** (`public abstract class IActivatedCardEffect : ICardEffect`).
Effect: the 59 declaration errors lift (unmask), old-model effects become type-compatible with the
new `List<ICardEffect>` card surface, and the old resolver switch keeps dispatching on concrete
types. The shim carries a `LEGACY-BRIDGE` header and dies with the old corpus.
Member hiding (old classes re-declare `MaxCountPerTurn`/`IsOptional`/`IsInheritedEffect`…) is
accepted: legacy paths read them through concrete types (resolver switch), new-model paths read the
base — no cross-reads. (`new`-modifier tidy-up only where warnings block.)

## 3. Activated resolution seam — AS-IS anchors

Execution heart (all mirrored in `ActivatedEffectResolver` new-model path):

* Collect gate = `CanTrigger(hashtable)` at GetSkillInfos time (AutoProcessing.cs:770-887) — maps
  to `CanCollectAt`.
* Stack stamping = `PutStackedSkill` (AutoProcessing.cs:57-118): set IsDigimonEffect/IsTamerEffect
  (inherited/linked ⇒ Digimon; else permanent.IsDigimon / IsTamer / SecurityDigimon), then
  `PermanentWhenTriggered`/`TopCardWhenTriggered` snapshot.
* Per-pass re-check = `CanActivate(skillInfo.Hashtable)` (MultipleSkills.cs:122, re-filter :164-165,
  pick-time :366) — maps to `CanActivateAt`.
* Execution = MultipleSkills.Activate (MultipleSkills.cs:339-395):
  `SetOnProcessCallbuck(() => { SkillInfos_used.Add(...); RegisterUseEffectThisTurn(cardEffect); })`
  then `AutoProcessing.ActivateEffectProcess` (AutoProcessing.cs:1063-1088):
  `if (CanActivate(hashtable) || IsDeclarative)` → `Activate_Optional_Effect_Execute(hashtable,
  isCheckOptional)`. The use-count register (= AddUse analog) fires INSIDE `Activate_Execute`
  (ICardEffect.cs:1116-1126) after the optional gate, before the body — register-before-body.
* Declaration legal-move gate = `CanUse(null)` (Permanent.CanDeclareSkillList, Permanent.cs:1618) —
  hashtable is NULL on the declaration path.

The window loop (WindowResolver = verified MultipleSkills mirror) is NOT restructured: it keeps
collecting bridge markers and calling `ActivatedEffectResolver` gates/ResolveAsync; only the
INSIDE of those entry points learns the new model.

## 4. Hashtable threading (event → AS-IS hashtable)

AS-IS threads ONE hashtable object from the emit site (StackSkillInfos payload) through collect
gate → per-pass gate → `Activate(hashtable)`. The mirror's driving artifact is a `GameEvent`; a
per-timing rebuild layer (`ActivatedHashtableBridge`) reconstructs the AS-IS payload from the event
(+ live state), each shape cited from its AS-IS emit site. Boundary timings pass NULL verbatim
(AS-IS `StackSkillInfos(null, OnEndTurn)` AutoProcessing.cs:699, OnStartTurn TurnStateMachine.cs:564,
OnStartMainPhase :905, AfterEffectsActivate ICardEffect.cs:1283). See the bridge file for the
per-timing anchor table.

## 5. Work log (2026-07-14, all landed)

1. **Legacy shim (unmask)** — `Script/CardEffectCommons/LegacyActivatedBridge.cs`:
   `abstract class IActivatedCardEffect : ICardEffect` (the dead marker resurrected AS a base class over the
   new abstract ICardEffect — see §2) + `LegacyBindingBridge.TryToBinding` (single reflective dispatch to the
   old classes' per-class `ToBinding(string)`, preserving the old registration path byte-identically with zero
   old-file churn; a new-model effect has no ToBinding → false). Result: 59 declaration errors lifted, the
   project's method bodies bound for the first time since the rebuild started → **431 unique latent errors
   surfaced** (the expected mask-lift).
2. **Controller population (the flip's availability wiring)** — `CEntity_EffectControllerStore.GetOrCreate`
   now populates `cEntity_Effect` from `CardEffectDispatch.TryCreateForCard` on creation (AS-IS setup-time
   `AddCardEffect` analog, CEntity_EffectController.cs:179-241) — `CardSource.EffectList(timing)` enumerates
   live for any card in any zone. Un-ported card = null component (the `EmptyEffectClass` behavioural
   equivalent).
3. **Registrar cutover** — `CardEffectRegistrar`:
   * activated skip = `is IActivatedCardEffect or ActivateICardEffect` (AS-IS never registers activated
     effects at all — availability is the live scan);
   * legacy non-activated effects still lower to EffectBindings via `LegacyBindingBridge` (byte-identical
     path for the stage-B gate consumers); NEW-model kind-class effects register NOTHING (stage-B is-scan
     serves them — documented RED until stage B);
   * `RegisterCard` also resets the NEW-model per-turn use store
     (`cEntity_EffectController.InitUseCountThisTurn()`, AS-IS CardSource.Init anchor) alongside OnceFlags.
4. **New-model surface gaps the flip's scan required (fixed)** —
   * `Player.TrashCards` / `Player.SecurityCards` (AS-IS Player.cs:510/518) + `Player.EffectList(timing)`
     (AS-IS Player.cs:830 — empty today, no new-model player-grant store: design item P6A-PLAYER-EFFECTLIST);
   * `Permanent.cardSources` (AS-IS Permanent.cs:880 — top + sources[top-most-first] + linked);
   * `Permanent.EffectList / EffectList_ForCard / EffectList_Added` (AS-IS Permanent.cs:1373-1573 — the
     per-card inherited/linked/top membership split, verbatim; EffectList_Added empty today: design item
     P6A-PERMANENT-EFFECTLIST-ADDED).
5. **AutoProcessing trigger-stack half (1:1)** — `StackedSkillInfos` (:24), `PutStackedSkill` (:57-118,
   SecurityDigimon compare adapted to instance id — gap1), `GetSkillInfos` (:770-887, the live scan),
   `ActivateBackgroundEffects` (:893-978), `StackSkillInfos` (:984-989), `ActivateEffectProcess` (:1063-1088).
   ICardEffect.cs:1244's verbatim tail (`StackSkillInfos(null, AfterEffectsActivate)`) now resolves; 0 ported
   cards return AfterEffectsActivate effects, so it stacks nothing today. The stacked list is not yet drained
   by the window loop — design item P6A-STACKED-DRAIN.
6. **OptionalSkill mirror** — `Script/OptionalSkill.cs` (was a 0-type skeleton; ICardEffect.cs:1108 was a
   latent CS0246): SelectOptional game-logic skeleton (owner decides, "Will you use ~?" message incl. the
   EffectTargets variant, ChoiceProvider yes/no, `SetUseOptional`) + `GManager.GetComponent<OptionalSkill>`
   branch. Anchors: AS-IS OptionalSkill.cs:14-133.
7. **ActivatedHashtableBridge** — `Script/CardEffectCommons/ActivatedHashtableBridge.cs`: rebuilds the AS-IS
   per-timing StackSkillInfos payload from the driving GameEvent (per-timing anchor table in the file:
   null-payload boundary timings verbatim; attack family {AttackingPermanent, CardEffect}; SwitchDefender
   shape; OnDeletionHashtable family; discard/lose/add-security/add-hand/move/linked/add-digivolution shapes,
   each cited). Cause identity threads as a minimal EffectSourceCard-carrying stub (P6A-HT-CAUSE).
8. **ActivatedEffectResolver seam (the behavioral heart)** —
   * enumeration switched from fresh `CardEffectDispatch`+`CardEffects(timing, card)` to the AS-IS
     `card.EffectList(timing)` under `AmbientMatchContext.Enter(context)` (all 6 entry points);
   * `MembershipKeeps` reads the AS-IS base `IsInheritedEffect` for new-model effects (legacy uniform keeps
     its own property; linked = C2-01 latent unchanged);
   * gates: CanCollectAt(new) = the GetSkillInfos filter (`!IsBackgroundProcess && CanTrigger(hashtable)`),
     CanActivateAt(new) = `CanActivate(hashtable)` (MultipleSkills per-pass), CanDeclareAt(new) =
     `CanUse(null)` (Permanent.CanDeclareSkillList / TurnStateMachine declared path);
   * execution: new `case ActivateICardEffect` = PutStackedSkill stamp (+ immediate un-stack, the
     MultipleSkills.Activate Remove), register-before-body via `SetOnProcessCallbuck(RegisterUseEffectThisTurn)`
     (MultipleSkills.cs:358-362; declared path registers at declaration per TurnStateMachine.cs:1183-1186),
     then `ActivateEffectProcess` → `Activate_Optional_Effect_Execute(hashtable)`;
   * recursion sites (mode branch / DigiBurst inner / nested option [Main] / Reuse*) thread the correct
     per-context hashtable + timing; the DigiBurst continuous-grant lowering routes through
     LegacyBindingBridge.
   WindowResolverWiring / GameFlowProcessor untouched — window ordering semantics (the verified MultipleSkills
   mirror) unchanged; only the resolver internals flipped.
9. **Retirement** — mirror-invented `CardEffectFactory.ReturnToLibraryBottomDigivolutionCardsClass` +
   `RevealLibraryClass` DELETED (0 consumers). The other 5 legacy `IActivatedCardEffect` factory methods
   CANNOT retire yet: `SimplifiedRevealDeckTopCardsAndSelect` has 6 REAL card consumers
   (BT1_010/BT2_044/ST4_03/ST4_10/…), `DrawCardsEffect` 10 Tfx + 1 test, `DrawThenDiscardEffect`/
   `AddThisCardToHandEffect`/`RevealDeckTopCardsAndSelect` test-project consumers.
   `ActivatedEffects.cs`/`ActivatedEffect.cs` retirement blocked by the ~80-file real-card old-model corpus
   (§2).

## 6. Error inventory (post-stage-A build)

Baseline 59 (all CS0246 declaration errors, bodies unbound) → unmask 431 → **stage-A end: 402 unique**
(what stage A consumed of the surfaced wave: the seam files it rewired + the flip-required surface gaps;
what it did NOT touch: the P5-ported mirror-layer bodies that were never bound before).

| class | count | nature |
|---|---|---|
| Script/ mirror layer (genuine new-model gaps) | 388 | P5/bridge-era files whose METHOD BODIES were never compiled: `PlayCardClass.cs` 121 (the biggest single gap — the play-pipeline port references many missing surfaces), `BlastDNADigivolution.cs` 48, `CardEffectFactory.cs` monolith bodies 38, `CardSource.cs` 14, `BlastDigivolution.cs` 11, KeyWordEffects partials (Link/ArtsDigivolve/Partition/Save/Fortitude/Alliance/… ~4-9 each), `HashtableSetting.cs` 8 (IBattle/ActiveCardList members), `CanUseEffects/OnDeletion.cs` 7, `CanUseEffectHelpers.cs` 6, gate-key constants (`Script/Permanent.cs:153` GetPermanentLevelKey) … — stage-B/card-batch material, dominant error codes CS1061 (missing member) / CS0117 (missing const) / CS1503-CS7036 (signature drift) |
| real cards (old-model witness corpus, re-port backlog) | 10 | BT9_109 5 (SelectHandEffect type, CanPlayCardTargetFrame), AD1_025 1, BT1_034/BT2_029 (CanNotBeBlockedStaticSelfEffect named-arg drift), EX8_051/EX8_061 (effectDescription named arg) |
| Tfx fixtures (out of scope) | 2 | TfxDigivolveCostGate, TfxOptionIgnoreColor (factory signature drift) |
| Headless engine | 2 | AttackTargetSwitchGate.cs reading `CanNotSwitchAttackTargetClass.CannotSwitchAttackTargetKey/AttackerConditionKey` — a stage-B gate scanning new kind-class constants that the AS-IS class does not carry (gate flip material) |

## 7. Design items (stage A)

* **P6A-PLAYER-EFFECTLIST** — mirror Player grant buckets (AS-IS Player.cs:830 PermanentEffects/Until*)
  empty until GiveEffectToPlayer flips from EffectBinding lowering to live ICardEffect storage.
* **P6A-PERMANENT-EFFECTLIST-ADDED** — same for Permanent grant buckets (AS-IS Permanent.cs:1380-1492).
* **P6A-STACKED-DRAIN** — AutoProcessing.StackedSkillInfos is stamped 1:1 but not drained by the mirror
  window loop (WindowResolver keeps its own queued-event seed); becomes load-bearing the moment a ported card
  returns an AfterEffectsActivate/cut-in effect (0 today).
* **P6A-STAMP-PERSISTENCE** — the mirror window holds MARKERS, not effect objects, so a freshly-enumerated
  new-model effect at the per-pass gate has no PermanentWhenTriggered snapshot (that AS-IS same-permanent
  re-check self-skips via its null guard). Execution stamps it (PutStackedSkill) before the final gate.
* **P6A-USED-JOURNAL** — AS-IS SkillInfos_used (the used-skill journal skipCondition reads) not mirrored on
  the execution callback.
* **P6A-DEFERRED-USECOUNT** — a new-model body suspending on a deferred choice AFTER its OnProcessCallbuck
  registered the use will re-register on the replayed re-run (the legacy uniform path has the OnceFlags
  cycle transaction; the CEntity_EffectController store has no journal yet).
* **P6A-HT-*** (see ActivatedHashtableBridge.cs header) — ENTERFIELD full payload (evoRoots/Root/oldLevels),
  USEOPTION Root/Cost, DIGISOURCE list payloads, ENDBATTLE battle payload, SECURITY skill payload, CAUSE
  stub (identity-only cause threading).
* **P6A-LEGACY-RETIREMENT** — LegacyActivatedBridge.cs + ActivatedEffects.cs/ActivatedEffect.cs + legacy
  factory methods + the registrar's LegacyBindingBridge lowering all die together when the ~80-file
  old-model card corpus + Tfx fixtures are re-ported.
