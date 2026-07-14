# P6 dispatch-flip Stage B — continuous / restriction effects to the AS-IS interface-scan model (2026-07-14)

Scope (per brief): the mirror's continuous stat/query resolution (DP, Security Attack, keyword presence,
restrictions, immunities) read the OLD data-oriented gates, which scan `EffectBinding`s. New-model kind-class
continuous effects (`ChangeSAttackClass:IChangeSAttackEffect`, `ChangeDPClass:IChangeDPEffect`,
`BlockerClass:IBlockerEffect`, …) register NO binding (stage A: only legacy `ToBinding` effects lower to the
registry), so they were INERT. Symptom: ST7_10's `[All Turns] <Security Attack +1>` resolved 1 not 2; a ported
`<Blocker>` opened 0 block windows.

## 1. The two disjoint effect representations (the crux)

Mid-migration the mirror carries TWO continuous representations, **disjoint by interface**:

* **LEGACY** (`ContinuousAndRestrictionEffects.cs` classes) are `: ICardEffect` ONLY and lower to a substrate
  `EffectBinding` via `ToBinding`; the OLD gates (`ContinuousDpGate`/`ContinuousModifierGate`/
  `ContinuousKeywordGate`/…) read those bindings. They do NOT implement the 74 marker interfaces.
* **NEW-model kind-classes** (`CardEffects/*.cs`) implement the marker interfaces directly and register NOTHING.

Because they are interface-disjoint, the two can be **UNIONed** with zero double-count: the old gate keeps its
binding scan (serves legacy), and a new-model interface scan is added on top (serves kind-classes).

## 2. `NewModelContinuousScan` — the AS-IS interface scan (NEW FILE)

`src/…/Script/CardEffectCommons/NewModelContinuousScan.cs`. Each method mirrors its AS-IS `Permanent.*` property
body VERBATIM (scope + timing + interface + gate predicate + aggregation order), over the LIVE
`Permanent.EffectList(None)` / `player.EffectList(None)` / face-up `SecurityCards` objects. Anchors:

| member | AS-IS anchor | scope | interface | gate | aggregation |
|---|---|---|---|---|---|
| `FoldSAttack` | Permanent.cs:1817-1930 (`Strike_AllowMinus`) | Players_ForTurnPlayer field perms + players | `IChangeSAttackEffect` | `PermanentCondition(this) && CanUse(null) && !CanNotBeAffected` | split by `isUpDown()` → fold UpToConstant, UpDownValue, DownToConstant with `GetSAttack(strike,this,invert)` |
| `InvertSecurityValue` | Permanent.cs:1670-1729 | same | `IInvertSAttackEffect` | `CanUse(null) && !CanNotBeAffected` | fold `InversionValue`, clamp [-1,1] |
| `FoldDp` | Permanent.cs:499-692 (`DP`) | same | `IChangeDPEffect` | `PermanentCondition(this) && CanUse(null) && !CanNotBeAffected` | IsUpDown() group first, then the rest, `GetDP(dp,this)` |
| `HasBlocker` | Permanent.cs:2397-2483 | field perms + FACE-UP security + players | `IBlockerEffect` | `CanTrigger(null) && IsBlocker(this)` | any-match |
| `HasJamming` | Permanent.cs:2486-2540 | field perms + players | `ICanNotBeDestroyedByBattleEffect` | `CanTrigger(null) && EffectName=="Jamming" && PermanentCondition(this)` | any-match |
| `HasPierce` | Permanent.cs:2585-2611 | **SELF only**, timing `OnDetermineDoSecurityCheck` | `ActivateICardEffect` | IsDigimon-gated, `EffectName=="Pierce"\|"Piercing"` | any-match |
| `HasReboot` | Permanent.cs:2614+ | field perms + FACE-UP security + players | `IRebootEffect` | `CanTrigger(null) && HasReboot(this)` | any-match |
| `HasRush` | Permanent.cs (Has Rush) | field perms + players | `IRushEffect` | `CanTrigger(null) && HasRush(this)` | any-match |

`HasKeyword(string)` dispatches Blocker/Jamming/Piercing/Reboot/Rush to the above; any other keyword returns
false (binding path still serves it — design item RD-P6B-2).

### Substrate adaptations (logic verbatim)
1. AS-IS `TopCard.CanNotBeAffected(<ICardEffect>)` → mirror `CanNotBeAffected(<effect>.EffectSourceCard?.InstanceId)`.
2. AS-IS `Players_ForTurnPlayer` needs a live turn; when the TurnController is un-initialised (isolated unit
   context, PlayerOrder empty), the player set falls back to the distinct owners of all live instances
   (ordering-insensitive for the single-effect cases that hit it). In real play PlayerOrder is populated → AS-IS
   turn-first order verbatim.
3. `CanUse`/`CanTrigger` read game state through `GManager.instance`; the mirror resolves it from
   `AmbientMatchContext`, so each public entry point `AmbientMatchContext.Enter(context)` (nested enter is safe).
   Without this the disable-check (`CheckEffectDisabledClass`, AS-IS reads `GManager.instance.turnStateMachine
   .gameContext.Players`) NREs outside a match scope.

## 3. Gates rerouted (UNION with new-model scan) — KEPT, not retired

The gates could NOT be retired — tests + ~15 card files + `CardController` + `CardEffectCommons` still consume
them (grep). Instead each was rerouted to UNION the new-model scan over its binding result:

* `ContinuousModifierGate.ResolveSecurityAttack` → `NewModelContinuousScan.FoldSAttack(context, id, legacyResolved)`.
* `ContinuousKeywordGate.HasKeyword(context,id,kw)` → after the binding self/target check, `|| NewModelContinuousScan.HasKeyword`. `IsDigimon`'s `HasKeyword(TreatAsDigimon)` path inherits this (TreatAsDigimon → false today, latent).
* `ContinuousDpGate.ResolveDp` → `resolved = NewModelContinuousScan.FoldDp(context, id, resolved)` before the DPBoost fold + final clamp.

Members that were ALREADY flipped in earlier P6 clusters (`Permanent.Level`/`CanSuspend`/`HasIceclad`/`CanMove`/
`Player.CanReduceCost`/…) already scan `EffectList(None)` — untouched.

## 4. The AddCardEffect wiring gap (load-bearing)

`CardEffectRegistrar.RegisterOnEnterPlay` did not attach the passed `effect` to the card's controller, so
`Permanent/CardSource.EffectList → cEntity_Effect.GetCardEffects` returned nothing whenever the card DEFINITION
did not itself dispatch to that effect class (an effect-play; a test fixture with a synthetic definition id).
Added `card.cEntity_EffectController.cEntity_Effect = effect;` — the AS-IS setup-time `AddCardEffect` analog the
`CEntity_EffectController` header flagged as MISSING. Behaviour-neutral for a real card (RegisterCard already
passes the `dispatch(def)` instance — same class GetOrCreate would have lazily created), load-bearing for the
new-model scan.

## 5. Diagnostic tests — before / after

| test | before | after |
|---|---|---|
| G6-001 (SA +1 after play) | FAIL `SA +1: expected 2 got 1` | **3/3 PASS** |
| G8-002 (SA +1 after effect-play) | FAIL `expected 2 got 1` | **1/1 PASS** |
| GR-005 (keyword continuity) | 2 FAIL (Blocker/Jamming/Piercing inert; block candidate empty) | **3/3 PASS** |

Test-side corrections (documented in-file), needed because the tests encoded the pre-flip model:
* **live-phase setup** (Initialize + `SetPhase(Main)`): AS-IS `CanUse`/`CanTrigger` gate on `DoneStartGame`
  (mirror proxy = phase past None/Setup). The tests played/evaluated at phase None (unrealistic — a real board
  is in Main); the old binding gate ignored the precondition, the new-model scan honours it exactly as every
  other continuous member in the suite does.
* **G6/G8 `GetKeywordEffects("Piercing")` binding-count → `ContinuousKeywordGate.HasKeyword(…,"Piercing")`**: the
  flip retires keyword EffectBindings (a ported `<Pierce>` is a new-model `ActivateClass` registering none —
  stage A). The interface scan is the flip-correct query for the SAME behaviour.
* **TfxKeywords fixture**: moved `<Piercing>` from the `None` branch to `OnDetermineDoSecurityCheck` (where AS-IS
  Pierce lives and `HasPierce` scans), matching ST7_10.

## 6. Regression sample (coordinator runs the full suite)

Ran, all PASS: G2G-002 Block.timing (10), G3.5-RL-C2 BattleKeywords (6), G3.5-N2 ContinuousBattleDp (7),
G3H-001 DP.cost.security.attack.modifier.helper (11), G3.5-F5 PlayerScopeContinuous (4), G3.5-B2
ContinuousModifierGate (5), G3.5-D1 PiercingSecurityBattle (5), F1-Tier2-OnEndAttack (10), plus DPB / FAILd-04 /
G2G-003 / G3.5-D2 / security suite.
Pre-existing failures (NOT regressions — confirmed identical with the DP union disabled / files untouched):
FAILa-02.ImmuneFromDPMinusCause (2, a known design-item red), G3G-001/002 keyword-batch **scope guards**
(`UnityEngine` appears in Pierce.cs etc. — files I never touched; the worktree branch is in the known "빅뱅 red"
state). The full suite is the coordinator's.

## 7. Design items

* **RD-P6B-1** (DP two-pass ordering) — a permanent mixing LEGACY (binding) and NEW-model DP effects folds
  legacy-then-new (two ordered passes) rather than AS-IS's single interleaved pass. Result-identical unless a
  legacy up/down and a new-model up/down interact on the SAME permanent (no such card today; both models are
  homogeneous per card). Same latent caveat applies to SAttack.
* **RD-P6B-2** (keyword coverage) — ~~`NewModelContinuousScan.HasKeyword` maps only Blocker/Jamming/Piercing/
  Reboot/Rush~~ **RESOLVED (P7 keyword-coverage pass)**: `HasKeyword` now covers Iceclad/Raid/Retaliation/
  Ascension/Fortitude/Blitz/Evade/MindLink/Barrier/Alliance/Collision/Partition/Scapegoat (exact AS-IS
  `Permanent.Has*` anchors), plus Vortex/Overclock/Execute/Save/ArmorPurge/Decode/Fragment/Decoy/Progress/
  VortexCanAttackPlayers (no dedicated AS-IS getter — anchored to each keyword's `CardEffectFactory` literal/
  prefix EffectName + registration timing instead; Decoy is presence-only, no CanTrigger gate — its AS-IS
  `IsByEffect` causing-effect check needs a live causing ICardEffect a generic presence query doesn't have,
  design item **RD-P6B-5**). Residual gap: **RD-P6B-4** Fragment's trashValue COST is closed over
  `CanActivateCondition` with no retrievable property (`DeletionReplacementGate.FragmentCostOf` still reads the
  legacy registry key only) — presence (`HasKeyword`) works, the exact cost-gating consumer does not; fixing
  needs `DeletionReplacementGate.cs`, outside this pass's touch scope.
* **RD-P6B-3** (restriction/immunity members) — **RESOLVED (P7 restriction-immunity pass)**:
  `ContinuousRestrictionGate.JointResult` now UNIONs `NewModelContinuousScan.IsRestrictedNewModel`, covering
  CanNotUnsuspend/CanNotSuspend (`Permanent.CanUnsuspend`/`CanSuspend`), CanNotDigivolve
  (`CardSource.CanNotEvolve`), CanNotBlock/CanNotBeBlocked and CanNotAttack/CanNotBeAttacked (`Permanent.
  CanBlock`/`CanAttackTargetDigimon`, one joint interface each direction-agnostic). `ContinuousDpGate.ResolveDp`
  UNIONs `NewModelContinuousScan.HasImmuneFromDpMinus` (`Permanent.ImmuneFromDPMinus`). A missing counterpart id
  (the common calling convention — e.g. `EvaluateBeBlocked(ctx, attackerId)` with no specific blocker) falls
  back to the SUBJECT itself as a structurally-valid stand-in Permanent/CardSource for the missing joint-arg
  role — correct for the common null-condition ("any") grant, a documented compromise for a condition genuinely
  keyed on the counterpart's identity.
  Residual: **RD-P6B-7** cause-conditional checks (`CanNotBeDestroyedBySkill`/`ImmuneFromDPMinus`-cause/
  `CannotReturnToDeck`-cause — FAILa-01/02/04, G9-053) route through `MatchStateMutationSink`'s own
  `IsRestrictedFromCause`/delete-mutation gate, which reads the substrate registry only and is NOT unioned
  (outside `NewModelContinuousScan.cs`/`Continuous*Gate.cs` scope). **RD-P6B-8** blanket "cannot be destroyed"
  (`ICanNotBeDestroyedEffect`/`ICanNotBeDestroyedByBattleEffect` — G9-038 CanNotBeDestroyedStaticEffect, G9-050
  SetFormDelete/SetFormSuspend via the mutation sink, G9-054 CanNotBeDestroyedByBattleStaticEffect) is consulted
  via `BattleDeletionGate.PreventsBattleDeletion` + the mutation sink, both separate files outside this pass's
  touch scope — same class of gap as RD-P6B-7.
* **RD-P6B-6** (DigiBurst continuous-grant body misrouted) — `ActivatedEffectResolver.cs`'s `DigiBurstActivatedEffect`
  case (`if (burst.InnerEffect is IActivatedCardEffect or ActivateICardEffect)`) treats ANY `ActivateClass`-typed
  inner body as "resolve it now" — but keyword self-static grants (e.g. `PierceSelfEffect`) ALSO build
  `ActivateClass` (implements `ActivateICardEffect`), so a "[Digi-Burst N] gain <keyword>" body gets "activated"
  (its no-op `ActivateCoroutine` runs) instead of becoming a live continuous grant — PRIM.DigiBurst's continuous-
  inner-body subtest. The `else if (LegacyBindingBridge.TryToBinding(...))` branch the comment implies exists
  for this case is unreachable for `ActivateClass` (no `ToBinding` method — `TryToBinding` always returns false via
  reflection). No missing "permanent-grant store" is built yet (P6A-PERMANENT-EFFECTLIST-ADDED, prior cluster)
  to register such a grant against, so fixing needs both a narrower branch condition AND that store — both
  outside `NewModelContinuousScan.cs`/`Continuous*Gate.cs`.
* **RD-P6B-9** (InvertSAttack vs a LEGACY SA delta) — `NewModelContinuousScan.FoldSAttack` computes+applies
  `InvertSecurityValue` only within its OWN new-model `IChangeSAttackEffect` fold loop; a LEGACY
  `ContinuousSelfModifierEffect` SA delta is already resolved into `legacyResolved` (the `baseValue` FoldSAttack
  receives) by `ContinuousModifierGate.ResolveSecurityAttack` BEFORE FoldSAttack runs, so a new-model
  `InvertSAttackClass` grant never flips a legacy-sourced delta (AS-IS `InvertSecutiryValue` inverts the SAME
  final Strike computation regardless of source). Fixing needs restructuring
  `ContinuousModifierGate.ResolveSecurityAttack` to interleave invert with the legacy fold (structural).
* Also found and fixed as PLAIN TEST BUGS while chasing these (not engine gaps): several test fixtures passed
  `permanentCondition: null` / `attackerCondition: null` to factories whose null-handling is "reject" not
  "accept-any" (`AllianceStaticEffect`'s `CanTriggerOnPermanentAttack`, `BlockerStaticEffect`'s
  `BlockerClass.IsBlocker` — both have NO accept-all fallback, unlike most other null-means-any factories in
  this codebase) — corrected to `_ => true` / an explicit owner predicate at each call site (G9-038, G9-028).

## 7b. P7 follow-up — SEC + InvertSAttack GREEN, 3 STOP (consumer outside gate scope)

* **SEC-FaceUpSecuritySource — GREEN.** Two parts: (a) engine — `FoldDp` now scans FACE-UP security cards as an
  `IChangeDPEffect` source (AS-IS Permanent.DP:542-576), gated by `SecurityFaceState.IsFaceUpInSecurity` (the
  mirror's security-placement face flag, not the generic `CardSource.IsFlipped`). (b) fixture — `TfxSecurityDpBuff`
  passed `permanentCondition: null`, but AS-IS `ChangeDP.cs:128` treats null as "apply to ANY battle-area Digimon"
  of BOTH players (no owner filter on the effect source — same as the field scan), so the fixture's "your Digimon"
  intent (test asserts the opponent is NOT buffed) had to be spelled `permanent.OwnerId == card.Owner`. `null`
  would AS-IS-correctly buff the opponent too — the engine is faithful, the fixture was the bug. (Fixture is a
  SEC-exclusive test fixture, not a real card / not parallel-agent-owned.)
* **FAILb-01.InvertSAttack — GREEN.** The invert lives in `FoldSAttack` (AS-IS Permanent.Strike_AllowMinus): it
  computes `InvertSecurityValue` (over `IInvertSAttackEffect`) and applies it to each `IChangeSAttackEffect` via
  `ChangeSAttackClass.GetSAttack(strike, subject, invert)`. The test's SA change was a LEGACY
  `ContinuousSelfModifierEffect` binding, resolved by `ModifierHelpers` BEFORE `FoldSAttack` runs, so the invert
  could never flip it (design item RD-P6B-9). Fix: represent the SA change as a NEW-model `ChangeSAttackClass`
  (via `ChangeSelfSAttackStaticEffect`) + the invert as `InvertSAttackClass` (via `InvertSelfSAttackStaticEffect`),
  both attached to the card's controller through a multi-effect seam and the card placed on the battle area — the
  flip's real model, where an SA-change card IS a `ChangeSAttackClass`. `FoldSAttack` folds both and the invert
  flips the change. Assertions byte-identical. This is the RD-P6B-9 caveat closed for the pure-new-model case; a
  genuine legacy-SA-delta + new-model-invert mix remains latent (no such card).

* **RD-P6B-10 (STOP — CanNotBeDestroyed / battle & effect delete):** G9-038 "battle deletion prevented" +
  G9-050 "Lv3 ally survives (protected set)". `CanNotBeDestroyedStaticEffect` builds `CanNotBeDestroyedClass`
  (`ICanNotBeDestroyedEffect`, single-arg `CanNotBeDestroyed(permanent)`), which registers NO binding/replacement.
  Consumers: battle → `Headless/Runtime/BattleDeletionGate.PreventsBattleDeletion` (reads
  `ContinuousScopeEvaluation` Delete/Prevent replacements); effect-delete → `MatchStateMutationSink`
  `.IsDeletionPreventedByContinuous` (reads the same replacements). AS-IS members = `Permanent.CanBeDestroyed`
  (:3186) / `CanBeDestroyedByBattle` (:3233). Both consumers are OUTSIDE the mandated touch scope
  (`NewModelContinuousScan.cs` + the 5 `Continuous*Gate.cs`), and neither delegates to any of those 5 gates — so
  the new-model interface scan cannot be unioned in without editing `BattleDeletionGate.cs` /
  `MatchStateMutationSink.cs` (or the shared `ContinuousScopeEvaluation`). Heavy-substrate → STOP.
* **RD-P6B-11 (STOP — CanNotSuspend via the suspend mutation):** G9-050 "Lv3 ally NOT suspended (protected set)".
  `CantSuspendStaticEffect` → `CanNotSuspendClass` (`ICanNotSuspendEffect`), registers no binding. Consumer:
  `MatchStateMutationSink` `SuspendKind` → `HasSelfRestriction(Suspend)` → `ScopedResult` (`ContinuousScopeEvaluation`),
  NOT `ContinuousRestrictionGate.EvaluateSuspend` (which I DID union — but the sink bypasses it). AS-IS member =
  `Permanent.CanSuspend` (:3698). Fix needs the sink to consult `NewModelContinuousScan.CanNotSuspend` (already
  written) or route through `EvaluateSuspend` — both edit `MatchStateMutationSink.cs`, outside scope → STOP.
  (Note: tests that call `ContinuousRestrictionGate.Evaluate*` DIRECTLY — G9-035 CanNotBeBlocked, G9-041
  CanNotBeAttacked, G9-050 CanNotAttack(defenderCondition), G9-027/033/038 block/attack — all PASS via my union;
  only the mutation-sink-driven paths are blocked.)
* **RD-P6B-12 (STOP — CanNotBeDestroyedBySkill, cause-conditional, effect delete):** G9-035 "card survives effect
  deletion". `CanNotBeDestroyedBySkillClass` (`ICanNotBeDestroyedBySkillEffect.CanNotBeDestroyedBySkill(permanent,
  ICardEffect)`) registers no binding. Consumer: `MatchStateMutationSink.IsDeletionPreventedByContinuous` →
  `IsRestrictedFromCause(CannotBeDeletedBySkillKey)` → `RestrictionScan.IsRestricted` (the canonical scan the
  gate ALSO calls — but I unioned the new-model scan at `ContinuousRestrictionGate.JointResult`, one layer ABOVE
  `RestrictionScan`, so the sink's direct `RestrictionScan` call misses it). AS-IS member =
  `Permanent.CanBeDestroyedBySkill` (:3309). Two blockers: (1) the union would have to move DOWN into
  `RestrictionScan.IsRestricted` (Headless/Runtime, outside the 5 gates) to reach the sink; (2) the AS-IS
  interface takes the causing `ICardEffect`, which the sink has only as a source-card INSTANCE id — the test's
  unconditional (`cardEffectCondition: null`) case is evaluable with a dummy cause, a conditional one is not
  (RD-P6B-7). Heavy-substrate + cause-object gap → STOP.

## 7c. P8 follow-up — RD-P6B-10/11/12 RESOLVED, RD-P6B-13 (cause stand-in) + RD-P6B-14 (STOP) new

* **RD-P6B-10 — RESOLVED.** `NewModelContinuousScan.HasCanNotBeDestroyed` (AS-IS `Permanent.CanBeDestroyed`,
  Permanent.cs:3186) and `HasCanNotBeDestroyedByBattle` (AS-IS `Permanent.CanBeDestroyedByBattle`:3233, the
  `ICanNotBeDestroyedByBattleEffect` scan half only) are now UNIONed into both consumers:
  `BattleDeletionGate.PreventsBattleDeletion` (both) and `MatchStateMutationSink.IsDeletionPreventedByContinuous`
  (general only — battle-only immunity stays battle-path-exclusive, matching the AS-IS
  `preventBattleDeletion`-vs-effect-delete split already documented on `PreventBattleDeletionKey`). Witness:
  G9-038 CanNotBeDestroyed (battle+effect), G9-050 SetFormDelete (player-scope set-form), G9-054 (permanentCondition
  gating + self-form + live condition, battle path) — all green.
* **RD-P6B-11 — RESOLVED.** `MatchStateMutationSink`'s `SuspendKind` case now ORs
  `NewModelContinuousScan.CanNotSuspend` (already written, AS-IS `Permanent.CanSuspend`:3698) alongside
  `HasSelfRestriction`. Witness: G9-050 SetFormSuspend, green.
* **RD-P6B-12 — RESOLVED**, and generalised. `MatchStateMutationSink.IsRestrictedFromCause` (the SHARED helper
  behind `IsDeletionPreventedByContinuous`'s skill-cause check AND the ReturnToHand/ReturnToDeck mutation guards)
  now ORs a new dispatcher, `NewModelContinuousScan.IsRestrictedByCauseNewModel(context, kind, subjectId,
  causingSourceId)`, which routes `CannotBeDeletedBySkillKey` / `CannotReturnToHandKey` / `CannotReturnToDeckKey`
  to three new cause-conditional scans: `HasCanNotBeDestroyedBySkill` (AS-IS `Permanent.CanBeDestroyedBySkill`:3309),
  `HasCannotReturnToHand` (AS-IS `Permanent.CannotReturnToHand`:744), `HasCannotReturnToLibrary` (AS-IS
  `Permanent.CannotReturnToLibrary`:785). Witness: G9-035 DeleteBySkillPrevented, FAILa-01 (self/opponent/
  unconditional), FAILa-04 (deck-return self/opponent/unconditional), G9-053 (hand-return self/opponent/
  unconditional) — all green.
* **RD-P6B-13 (the cause-object gap, closed for the common case).** All three interfaces above (plus
  `IImmuneFromDPMinusEffect.ImmuneFromDPMinus`) take the REAL causing `ICardEffect` — in AS-IS always a live
  scanned object (no legacy/new-model split there); the mirror only has the causing SOURCE ENTITY ID at these
  call sites. Closed via `NewModelContinuousScan.CausingEffectStandIn` — a minimal `ICardEffect` subclass built
  ONLY to carry `EffectSourceCard` (via `RestrictionScan.MakeSource`). Correct for the overwhelmingly common
  shape (every real card's `cardEffectCondition` reads `src.EffectSourceCard.Owner` / `IsOpponentEffect`);
  a predicate inspecting other `ICardEffect` state (`EffectName`, `EffectDiscription`, `IsInheritedEffect`, …)
  would see defaults, not the real cause's — same tier as `HasPierce`'s no-hashtable presence check /
  `CanNotDigivolve`'s source fallback (§2). `HasImmuneFromDpMinus` gained a `causingSourceId` parameter (default
  = old self-stand-in presence-only behaviour, preserved for its one remaining 2-arg fast-path caller);
  `ContinuousDpGate.ResolveDp` now calls the 3-arg form PER REDUCING MODIFIER (`modifier.SourceEntityId`) instead
  of a blanket "any new-model immunity exists" flag — the previous blanket form over-immunized (a
  self-sourced reduction was wrongly dropped whenever ANY conditional opponent-only immunity existed anywhere on
  the card, since the presence check's self-stand-in evaluates the OPPONENT-only predicate against the immunity
  grant's OWN (self) source and fails, hiding the grant from the "does any immunity exist" gate entirely — the
  bug FAILa-02 pins down). Witness: FAILa-02 (opponent-sourced ignored / self-sourced still applies /
  unconditional blocks both) — all green.
* **Test-only phase-setup bug found while chasing these** (not an engine gap): `ICardEffect.CanTrigger` gates on
  `TurnStateMachine.DoneStartGame` (phase past None/Setup) UNCONDITIONALLY, before any specific `CanUseCondition`
  runs — several P7-authored fixture `Ctx()` helpers (FAILa-01/02/04, G9-053) never called
  `TurnController.SetPhase(HeadlessPhase.Main)`, so EVERY candidate effect's `CanUse`/`CanTrigger` trivially
  returned false and the new-model scan never fired regardless of the union — silently masquerading as "the
  union doesn't work" (self-caused/non-matching subtests passed by COINCIDENCE — protection never firing looks
  identical to "correctly not blocked"). Same class of bug as the one already documented in §5; fixed by adding
  the `SetPhase(Main)` call (the legacy/registry-only paths those same suites also exercise are unaffected — they
  never call `CanUse`/`CanTrigger` at all, reading registered replacement/binding context values directly).
* **RD-P6B-14 (STOP — DeletionReplacementGate keyword consumers never reach the EngineContext-aware union):**
  G9-058 (Evade/Barrier presence + Fragment's `trashValue` cost) and G9-055 (Decoy redirect, 3 of 7 subtests).
  `DeletionReplacementGate.HasReplacementKeyword(record, metadataFlag, keyword, EffectRegistry? effectRegistry)`
  has NO `EngineContext` parameter at all — it can only ever call `ContinuousKeywordGate.HasKeyword(EffectRegistry,
  …)` (the pure-legacy-registry overload, `ContinuousKeywordGate.cs:216`), never the already-unioned
  `HasKeyword(EngineContext, …)` (`ContinuousKeywordGate.cs:77`, which DOES consult
  `NewModelContinuousScan.HasKeyword`). Same for Decoy: `DeletionReplacementGate.HasDecoy` →
  `ContinuousKeywordGate.KeywordGrantAcceptsSubject(EffectRegistry, …, EngineContext? context)` — THIS one does
  receive a context, but only uses it to build a `CardSource` for a stored LEGACY `Func<CardSource,bool>`
  predicate read out of a `registry.GetKeywordEffects(keyword)` binding; a new-model grant registers no such
  binding, so the loop body never executes and `context` is never consulted for a live scan (also confirmed:
  Decoy's `permanentCondition` gates WHICH OTHER ally's would-be-deletion triggers the redirect — AS-IS
  `Decoy.cs`'s `CanSelectPermanentCondition`, a joint (holder, candidate) predicate embedded in the trigger's own
  `CanUseCondition`/`CanTriggerWhenPermanentRemoveField` hashtable check — NOT a simple presence-on-the-holder
  scan like `NewModelContinuousScan.HasDecoy` above; wiring it correctly needs a NEW joint scan mirroring that
  exact AS-IS shape, not just an EngineContext plumb-through). Both call sites live in `DeletionReplacementGate.cs`
  (not `BattleDeletionGate.cs` / `MatchStateMutationSink.cs` / `ContinuousScopeEvaluation.cs` / `RestrictionScan.cs`,
  and not fixable by editing `NewModelContinuousScan.cs` alone) — outside this pass's mandated touch scope. Test
  fixtures were still corrected with the seam (`cEntity_EffectController.cEntity_Effect` attach) + phase-setup,
  same as every other file in this pass, so they are AS-IS-faithful test setups now failing on a real,
  precisely-diagnosed, out-of-scope engine gap — not a test bug and not silently weakened.

## 8. Note — session interruption

Mid-session the worktree's `src/` was reset to HEAD by an external `git checkout` (concurrent to this agent),
wiping all Stage-B edits and deleting the new file; they were re-applied identically and re-verified green.
