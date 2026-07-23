# Headless substrate audit — part 0/8 (hl_part_00.txt)

24 files, all read in full. Verdict: **0 files with a live game-rule divergence problem.** One file
(`PlayerRuleAdapter.cs`) carries a simplified/incomplete rule implementation, but it is confirmed
dead in production (test-only) — flagged below as a finding, not a "problem" verdict, per
dead-judgment-needs-AS-IS practice (its non-liveness is confirmed by exhaustive call-site grep, not
asserted). One class (`MandatoryEffectOrdering.cs`) is confirmed dead/test-pinned scaffolding, already
self-documented as such elsewhere in the repo — noted for completeness, not a problem.

---

## 1. Headless/Effects/MatchStateMutationSink.cs — OK (with 1 note)

2357 lines, read in full. This is the deletion/zone-move/keyword-grant application substrate (the
`IEffectMutationSink` that ported card effects call). Every game-RULE judgment in this file (deletion
immunity, suspend/return/security restrictions, ACE overflow, PRE would-be-deleted window, batch
sequencing) is a documented delegation to an AS-IS mirror getter/scan under `Assets/Scripts/Script/*`
(e.g. `Permanent.CanBeDestroyed()`, `Permanent.CanSuspend`, `TopCard.CanNotBeAffected`,
`NewModelContinuousScan.IsRestrictedByCauseNewModel`, `NewModelContinuousScan.IsRestrictedNewModel`),
each citation pointing at concrete AS-IS `CardController.cs` line ranges. Grepped the whole file for
stub markers (`return true;` / `=> true` unconditional, HACK/FIXME/placeholder) — every hit is inside a
real scan (`IsPlayerRestricted`, `IsRestrictedFromCause`, `IsRemovalBlockedByScan`,
`IsDeletionPreventedByContinuous`) that returns `true` only after walking field/player effect lists and
testing `CanUse` + the AS-IS predicate; none is a bare stub.

Note (not a problem): the KindToFlag write of `SetSecurityCheck → hasPiercing` and similar
grant-flag mappings are pure metadata plumbing (an effect-vocabulary → flag translation table), not
independent rule invention — the flags are read back by AS-IS-mirroring consumers elsewhere
(BattleResolver, DeletionReplacementGate, etc., outside this manifest slice).

## 2. Headless/Effects/EffectContextAdapter.cs — OK

Pure DTO/normalization layer (alias table, typed-value parsing for player/entity ids). No rule content.

## 3. Headless/Runtime/DeferredChoiceProvider.cs — OK

Choice-replay/suspension bridge to the agent. The one piece of judgment it makes —
`ChoiceCompletability.IsUnsatisfiableForcedChoice` demoting an unsatisfiable forced batch choice to an
empty Select — is explicitly cited against AS-IS `SelectHandEffect.cs:496-570/:595-608`'s bounded-search
failure path (`SetTargetHandCards(null) -> _noSelect`), and the demotion is recorded/drained for
observability rather than silently applied. Legitimate substrate translation of an AS-IS control-flow
outcome, not an invented rule.

## 4. Headless/State/PlayerRuleAdapter.cs — FINDING (dead code, not live)

`CanAddSecurity(playerId, count)` (line 87) returns `!IsSecurityLooking` only — it does **not** consult
the continuous `ICannotAddSecurityEffect` scan that AS-IS `Player.CanAddSecurity` performs (Player.cs
:1469-1517, scans every field permanent's and every player's `EffectList(EffectTiming.None)`). Likewise
`CanReduceSecurity` only checks `IsSecurityLooking` + a raw count (matches AS-IS `CanReduceSecurity()`,
which really is just `!IsSecurityLooking` — Player.cs:1521-1529, so that one is faithful), and
`CanDraw`/`CanPayMemoryCost`/`MaxMemoryCost`/`ExpectedMemory` never consult any `ICannotAddMemoryEffect`
restriction the way AS-IS `Player.CanAddMemory` does.

Verified this is **not reachable from any production code path**: repo-wide grep for
`.CanAddSecurity(`, `.CanReduceSecurity(`, `.CanDraw(`, `.CanPayMemoryCost(`, `.MaxMemoryCost(`,
`.ExpectedMemory(` on a `PlayerRuleAdapter`/adapter-typed receiver finds callers only inside
`tests/G2C-002.Memory.security.deck.loss.check.Tests/Program.cs`. The only production consumer of
`PlayerRuleAdapter` is `Headless/Runtime/TerminalEvaluator.cs`, which uses exclusively
`EvaluateDeckLossOnDraw` / `EvaluateSecurityAttack` / `EvaluateLoseFlag` / `EvaluatePlayerChecks` (the
terminal win/loss verdict path) — none of which touch `CanAddSecurity` et al. The real, LIVE
`CanAddSecurity` gate for the mirror layer is `Assets/Scripts/Script/CardController.cs`'s
`SecurityRuleGateSeam.CanAddSecurity`, consulted by `Player.CanAddSecurity` (Player.cs:468-479) and used
throughout ported card effects (`CardEffectFactory.cs`, `SelectPermanentEffect.cs`,
`Ascension.cs`) — that seam is NOT this adapter and is out of this manifest's scope (it lives under
`Assets/Scripts/`).

Net effect: `PlayerRuleAdapter`'s security/memory gate methods are an incomplete, unwired duplicate of a
rule the mirror layer already owns correctly elsewhere. Since it has zero production callers, it cannot
currently cause AS-IS divergence — flagged as a latent trap (if ever wired into a legal-action check, it
would silently under-restrict) rather than a live problem.

## 5. Headless/State/PlayerState.cs — OK

Pure immutable record (zones/flags/memory) + view projection. No rule judgment.

## 6. Headless/Runtime/SessionContext.cs — OK

Pure session bookkeeping (player list, turn pointer, fingerprint). No rule judgment.

## 7. Headless/Effects/EffectScheduler.cs — OK

Generic FIFO effect-resolution queue wrapper (enqueue/resolve/dequeue-on-fizzle). No game-rule content;
delegates resolution to an injected resolver function.

## 8. Headless/Effects/MandatoryEffectOrdering.cs — OK (dead/test-pinned, not a problem)

Implements turn-player-first / mandatory-before-optional trigger ordering. Confirmed **not called from
any production path**: `GameFlowProcessor.cs:877` only references the type in an XML doc comment (the
live trigger-window drive was replaced by the "Window SkillInfo cutover" — `AutoProcessing.StackSkillInfos`
driven directly per the same file's surrounding comments). The only real caller is
`tests/G2F-002.Mandatory.effect.ordering.Tests/Program.cs`, and a second test
(`tests/G3Z-001.Phase.3.aggregate.result.Tests/Program.cs:63`) explicitly annotates it as "0 production
consumers" — i.e. its dead status is already known and test-pinned by the codebase's own convention, not
a hidden regression. No divergence risk since it never executes.

## 9. Headless/Runtime/DeletionOutcomeWatcher.cs — OK

Parks a continuation until a set of deletion targets "settle" (destroyed vs. spared), classifying by
reading `GameFlowProcessor.PendingDeletionKey` metadata and battle-area zone membership. Pure
settlement-plumbing over state the AS-IS-mirroring delete pipeline (MatchStateMutationSink /
GameFlowProcessor) already produces; makes no independent rule decision.

## 10. Headless/Runtime/InMemoryHeadlessTurnController.cs — OK

Turn/phase state holder; phase sequencing is delegated to `HeadlessPhaseMapping.NextStep` (not in this
manifest). Player rotation is round-robin over the fixed player list — structural, not a game rule.

## 11. Headless/Runtime/ContinuousRestrictionGate.cs — OK

Every `Evaluate*` method (`EvaluateAttack/Block/Digivolve/BeBlocked/DeleteBySkill/BeAttacked`) is a
one-line delegation to `NewModelContinuousScan.IsRestrictedNewModel`, cited against the corresponding
AS-IS `Permanent.CanX` joint-predicate scan. The docstring explicitly documents that the former
registry-based evaluator arm was deleted (producer 0, permanently-empty union) — this is the retirement
of an invented layer in favor of the AS-IS-literal scan, not new invention.

## 12. Headless/Runtime/HeadlessActionParameterKeys.cs — OK

Pure string-constant catalogue for action/event metadata keys. No rule content.

## 13. Headless/Runtime/GameEventQueue.cs — OK

Generic drainable event queue with idempotent append-only sync cursor. No rule content.

## 14. Headless/Services/IllegalAction.cs — OK

Pure DTO (illegal-action record + metadata merge helper). No rule content.

## 15. Headless/Runtime/FreeDigivolveHelpers.cs — OK

Costless single-target digivolve (Blast/Arts Digivolve keyword mechanic), implemented by reusing the
shared `FusionDigivolveHelpers.FuseAsync` primitive with `payCost:false`. The one substrate decision —
inheriting the target's `enteredThisTurn` (summoning-sickness) state instead of resetting it — is
justified against AS-IS `CardController.cs:1372-1376` (`permanent = _targetPermanent; permanent.AddCardSource(card)`,
the normal-evolution arm where the Permanent object persists across the top swap).

## 16. Headless/Runtime/AttackTargetSwitchGate.cs — OK

`IsLocked` is a direct 1:1 port of AS-IS `Permanent.CanSwitchAttackTarget` (Permanent.cs:3745-3792):
scans every field permanent's and every player's `EffectList(EffectTiming.None)` for
`ICanNotSwitchAttackTargetEffect`, gated by `CanUse(null)`. Cites both AS-IS call sites it must guard
(block eligibility, `AttackProcess.SwitchDefender`). Faithful mirror.

## 17. Headless/Runtime/CheatActionGuard.cs — OK

Filters cheat/debug action types out of the legal-action surface for the RL/agent harness. This is a
harness-safety filter (which actions are exposed to an autonomous agent), not a game rule — explicitly
rehomed "VERBATIM" from a retired OLD file per its own comment, and the comment calls out that
breeding-step actions (`HatchDigitama`/`MoveBreedingToBattle`) were deliberately excluded from the
cheat set because they are legitimate agent actions, not silently dropped.

## 18. Headless/Runtime/HeadlessChoiceState.cs — OK

Pure choice-session snapshot record (candidates, pending/resolved state, partial-selection scratchpad).
No rule content; the partial-selection field is explicitly documented as unpopulated until a later slice
(B5-2), and current real trajectories keep it empty (bit-identical behavior preserved).

## 19. Headless/State/DpModifier.cs — OK

Pure typed-value record (Relative/Absolute DP delta + activation order), mirroring the AS-IS
`IChangeBaseDPEffect` up/down vs. not-up/down split. No independent rule logic — just a data carrier
consumed elsewhere.

## 20. Headless/Effects/EffectRequest.cs — OK

Pure DTO (effect id / controller / timing / context) with argument validation. No rule content.

## 21. Headless/Effects/WindowResolutionController.cs — OK

Explicitly a "retired shell" per its own docstring: the old registry-currency window parking and the
OLD step driver's once-per-turn drain marker were both retired at the SkillInfo cutover; `HasPending`
is hardcoded `false` and `ResetMatchState` is a no-op. Correctly self-documented as inert scaffolding
kept only until the registered-service seam is compacted — not a hidden rule stub (it decides nothing).

## 22. Headless/Runtime/IHeadlessPlayerStatusController.cs — OK

Pure interface contract (MarkLose/IsLose/TryGetLoseReason/LosingPlayers) mirroring AS-IS `Player.IsLose`.
No implementation, no rule content.

## 23. Headless/Diagnostics/MatchLogLevel.cs — OK

Pure logging-verbosity enum. No rule content.

## 24. Headless/Services/ILogSink.cs — OK

Pure logging interface (Info/Warn/Error). No rule content.

---

## Summary

- Files reviewed: 24/24 (no omissions).
- Live problems (substrate deciding a game rule on its own, unstubbed gate bypass, or AS-IS-diverging
  live behavior): **0**.
- Findings (not live-blocking, recorded for the record):
  1. `PlayerRuleAdapter.CanAddSecurity`/`CanDraw`/`CanPayMemoryCost`/`MaxMemoryCost`/`ExpectedMemory`
     (State/PlayerRuleAdapter.cs) implement a simplified rule that omits the AS-IS continuous
     restriction scan (`ICannotAddSecurityEffect`/`ICannotAddMemoryEffect`) the real gate
     (`SecurityRuleGateSeam`/mirror `Player`) performs — confirmed zero production callers (test-only),
     so currently inert; a latent trap if ever wired into a legal-action check.
  2. `MandatoryEffectOrdering` (Effects/MandatoryEffectOrdering.cs) is dead/test-pinned scaffolding
     (0 production consumers, already annotated as such by the test suite itself) — not currently
     exercised, no divergence risk.
