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
* **RD-P6B-2** (keyword coverage) — `NewModelContinuousScan.HasKeyword` maps only Blocker/Jamming/Piercing/
  Reboot/Rush to a new-model interface today. The other `ContinuousKeywordGate` names (Iceclad has its own
  member already; Retaliation/Barrier/Collision/Fortitude/Evade/Raid/Save/Decoy/Fragment/Scapegoat/MindLink/
  Ascension/TreatAsDigimon/VortexCanAttackPlayers/Alliance/Overclock/Progress/ArmorPurge/Decode/Partition) fall
  through to the binding path — extend the switch (and add the per-keyword AS-IS `Has*` scan) as those cards are
  re-ported. No behaviour lost today (no new-model card ports them as self-statics yet).
* **RD-P6B-3** (restriction/immunity members) — the task listed restriction predicates (`CanNotSuspend`/
  `CanNotBlock`/`CanNotAttack`/…) and immunities (`CanNotBeAffected`). `Permanent.CanSuspend`/`CanMove`/… were
  ALREADY flipped to the `EffectList` scan in earlier clusters; the remaining restriction/immunity gates
  (`ContinuousRestrictionGate`/`ContinuousImmunityGate`) still read bindings and were NOT unioned this pass (no
  failing diagnostic; higher-risk intricate scans). Extend with the same UNION pattern when a new-model
  restriction/immunity kind-class card lands.

## 8. Note — session interruption

Mid-session the worktree's `src/` was reset to HEAD by an external `git checkout` (concurrent to this agent),
wiping all Stage-B edits and deleting the new file; they were re-applied identically and re-verified green.
