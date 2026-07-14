# Window SkillInfo cutover — adversarial review (2026-07-15)

Reviewer role: REFUTE. Scope: commits `394a8402..8f155d02` (13 commits, design→C2 flip). All AS-IS refs = `DCGO/…`; mirror = `.claude/worktrees/m2-onendattack/src/HeadlessDCGO.Engine/…`. Verified against source, not comments/commit messages.

## Verdict (up front)

**Keep the flip.** The load-bearing core — the `MultipleSkills` window loop, `AutoProcessing.GetSkillInfos`/`PutStackedSkill`/`TriggeredSkillProcess`/`AutoProcessCheck`, the resolution once-use path, the single-batch deletion transport — is a faithful 1:1 of AS-IS. The old `WindowResolver`/`CollectUnifiedSeed` path is dead (0 live callers). **No P0**: nothing currently-live produces wrong behavior.

But the flip is live now (not "DORMANT" as several stale in-file comments still claim), and it ships **three P1 latent correctness holes** that MUST be closed before C2b lands the dependent card populations, plus documented residuals. None is a hidden bug — all are consequences of decision #4's null payload and the RDW gaps — but a porter following the "1:1 recipe" over them would silently produce wrong cards.

---

## P1 — CONFIRMED — deletion window nulls `battle` + `cardEffect`; predicates read the payload, not the flag

- AS-IS: `OnDeletionHashtable(perms, cardEffect, battle, isDPZero)` (`HashtableSetting.cs:85`) adds the `"CardEffect"`/`"battle"` keys ONLY when non-null, so `null ⇔ "not deleted by that cause"` is a MEANINGFUL signal. `IsByBattle` = `GetBattleFromHashtable(ht) != null` (AS-IS `CanUseEffects/OnDeletion.cs:82`); `IsByEffect` reads `GetCardEffectFromHashtable` (`:89`).
- Mirror transport passes `cardEffect: null!, battle: null!` at BOTH deletion openers: sink `MatchStateMutationSink.cs:~1160` (`OnDeletionHashtable(deadPermanents, cardEffect: null!, battle: null!, anyDpZero)`) and battle `BattleResolver.cs:~225` (losers, `battle: null!`), and the deferred-finalize `GameFlowProcessor.cs:~330`.
- Mirror `IsByBattle` (`CanUseEffects/OnDeletion.cs:85`) STILL reads the payload — it was NOT rewired to the `deletedByBattle` flag. So a ported `ActivateClass` reactor keying on `IsByBattle`/`IsByEffect` at `OnDestroyedAnyone`/`OnLeaveFieldAnyone` gets a constant answer: `IsByBattle`→false, `IsByEffect`→false (under-trigger); `!IsByBattle`/`!IsByEffect`→true (over-trigger).
- **Why P1 not P0**: the KEYWORDS (Retaliation/Partition/Decode/Pierce) read `EffectContext.DeletedByBattle` (the bound-reactor flag — `KeyWordEffects/Retaliation.cs:17`, `Partition.cs:33`, `Pierce.cs:17`, `Decode.cs:32`), NOT the window predicate, so they are unaffected. And of the ~160 AS-IS card scripts that branch on `IsByBattle`/`IsByEffect`, **exactly 0 are ported today** (`grep IsByBattle|IsByEffect|GetBattleFromHashtable|GetCardEffectFromHashtable src/…/CardEffect/` → only `BT19_024`/`BT16_025`, both comment-only Decode/Partition cards on `WhenRemoveField` via the flag path). So nothing is live-wrong.
- Failure scenario (fires the moment the population lands): port `BT9_024` ("[When deleted in battle] …") 1:1 → its `CanActivate` calls `IsByBattle` → the battle transport passed `battle:null` → never fires. Symmetric over-trigger for the `!IsByBattle` set (~25 cards) and `!IsByEffect` Scapegoat-class set (~30 cards).
- Fix before C2b: carry `battle`/`cardEffect` on the deletion payload (close RDW-01 fully), OR rewire `IsByBattle`/`IsByEffect` to the `deletedByBattle`/cause flags the keywords already use. Documented as RDW-01 + RD-C1-CARDEFFECT-IDTHREAD + the `battle=null` ADAPTATION, but the "collect-before-removal" framing under-sold that the read side is unpatched.

## P1 — CONFIRMED — effect-driven attacks DROP their OnAllyAttack/OnEndAttack/OnCounterTiming window

- `SkillWindowSupply.TryBuildAttack` returns **false** when the event carries `attackCauseEffectId` (`SkillWindowSupply.cs:304-306`) — i.e. an effect-driven attack opens NO window at all. Plain declared attacks (`attackEffect==null`, `TurnStateMachine.cs:1250`) are faithful.
- The mirror `AttackProcess` did NOT add the inline `StackSkillInfos` inserts (only "DEFERRED to C2" comments remain at `AttackProcess.cs:280-287` / `:655-660`); the `TriggerEventEmitter.Emit(OnAllyAttack)` (`:273`) / `Emit(OnEndAttack)` (`:649`) is retained, so supply-conversion is the SOLE opener. Consequence: for an effect-driven attack, supply drops it → the window never opens.
- AS-IS opens OnAllyAttack/OnEndAttack for effect-driven attacks too, with the live `attackEffect` in the payload. Reactors that fire "when one of your Digimon attacks" MISS effect-driven attacks; resolution branches keyed on the driving effect are lost (e.g. AS-IS `EX11_068.cs:86` OnAllyAttack "Execute" bonus; the deletion-side analogue `EX11_060.cs` OnDestroyed "Overclock" bonus). Documented RDW-05, no fabrication (correct per no-simplification), but a real behavior hole on the live path once effect-driven-attack cards + attack reactors coincide.
- Note: the design plan said C2 would "activate attack inline inserts + remove the attack emit"; the code did the opposite (kept emit, no insert). The RESULT is still single-fire (no double), so it is coherent — but the design record and the code disagree.

## P1 — CONFIRMED/LATENT — interactive security-check reactor suspend is not resumable (RD-C2-SECCHECK-INTERACTIVE)

- `SecurityResolver.ResolveSecurityCheckWindowAsync` (`SecurityResolver.cs:~380`) drives `autoProcessing.AutoProcessCheck` WITHOUT a `WindowChoicePendingException` catch, and it is invoked from the security loop (seam 7), NOT from `GameFlowProcessor.AutoProcessAsync` (which does catch). If an OnSecurityCheck/OnLoseSecurity reactor needs an agent choice, the pending exception unwinds the security loop with no resume anchor.
- Latent today (the shipped security witnesses BT14_035/BT13_023/EX8_051/EX8_061 are non-interactive, suite green). Fires when an interactive security-check reactor lands. Design flags it as RD-C2-SECCHECK-INTERACTIVE; the adversarial point is that it is an **uncaught throw**, not a graceful drop — it can abort the attack/turn, not just skip a trigger.

---

## P2 findings

- **Security co-stack over-activates OnLoseSecurity BACKGROUND effects.** AS-IS security-check passes `ref triggeredSkillInfos` (non-null) → `IReduceSecurity.ReduceSecurity` merges OnLoseSecurity via `GetSkillInfos` only (`CardController.cs:5448`, foreground, NO `ActivateBackgroundEffects`). The mirror passes `refCollector: null` → the null branch runs `StackSkillInfos(…, OnLoseSecurity)` (mirror `IReduceSecurity`, AS-IS `:5444`) which ALSO calls `ActivateBackgroundEffects`. Divergence realizes only if an OnLoseSecurity `IsBackgroundProcess` effect exists. Not covered by RD-C2-SECCHECK-INTERACTIVE.
- **Security resolution ORDER: revealed card's `[Security]` effect vs OnSecurityCheck/OnLoseSecurity is inverted.** AS-IS resolves SecuritySkill first (`CardController.cs:4037-4102`) then the OnSecurityCheck/OnLoseSecurity window (`:4111/:4117`). Mirror resolves the window (`ResolveSecurityCheckWindowAsync`) then the `[Security]` effect (G7-004, after). PRE-EXISTING in the mirror (G7-004 already sat after the window pre-C2), not a C2 regression — flagged for completeness.
- **RD-C2-DEFERRED-DELETE-BATCH.** The deferred-finalize path (`GameFlowProcessor.cs:315-346`) opens the OnDeletion window PER-CARD, so a multi-card deferred batch (multiple members declined a PRE replacement) over-fires an anyone-scoped reactor vs AS-IS's single LoserPermanents-style batch. Single-card deferred (the common Evade/Scapegoat/Fragment/Decoy decline) is exact. Documented residual.
- **RDW-06 counter two-pass.** OnCounterTiming routing to the cut-in instance + the `IsCounterEffect` two-pass split is still the old mechanism. Latent — the cut-in pool stack is empty in every currently-exercised scenario (verified: only cut-in callers `CardController.cs:2623/2887/3258` over an empty stack).
- **Barrier drops the live causing effect** (spot-check, priority 10). Mirror `GainBarrier` bridge (`KeyWordEffects/Barrier.cs:13-17`) forwards only `activateClass?.EffectSourceCard` to the generic `GainKeywordToPermanent`, discarding the `ICardEffect`; AS-IS threads it into `BarrierEffect(rootCardEffect)`/`CanNotBeAffected` + a `WhenPermanentWouldBeDeleted` retaliation registration. Immunity is now keyed by source-card instead of effect identity. Within the known 18-keyword R2 rehousing debt, not introduced by C2 (C1c only restored the signature).
- **Stale "DORMANT" comments.** `MultipleSkills.cs:13-17` and `AutoProcessing.cs:1384`/`:1257` still assert the loop/`AutoProcessCheck` has no live caller. FALSE after C2: `GameFlowProcessor.AutoProcessAsync` now drives `autoProcessing.AutoProcessCheck` (`GameFlowProcessor.cs:~690`). Documentation defect only, but misleads the next reader about live-path exposure.
- **`AddEffectToPermanent` additive null-guards** (priority 10). Mirror `GiveEffectToPermanentOrPlayer.cs:31-36` adds `ThrowIfNull` + a silent `return` on empty/null permanent where AS-IS (non-nullable param) would NPE. Defensive, disjoint from the transitional registry branch (old-model `return`s before the bucket switch; new-model skips to it — no double). Behavior-safe.

---

## Priority targets that SURVIVED refutation (one line each)

1. **MultipleSkills loop** — 1:1: gate order (flag-stamp→CanActivate→skipCondition→ChainActivations/UsedMaxCount→cut-in), post-filter (`:341-342`), bounds quirk `Count < _skillIndex` (`:411`), Blast-vs-normal decision + `canDecline` (`:365-375`), `SetOnProcessCallbuck` (`:441-445`), cut-in/main body branches (`:464-481`), `endGame→RuleQueryService.IsTerminal()` (`:512`), decline `null→-1` (`:385`, the AS-IS `:0` fallback is dead in practice); the W1b RunPhases/PassLoop/ActivatePick/RunPickBody refactor preserves the pass body verbatim (resume re-enters at the recorded cursor; order-choice replay is safe because no body ran between suspend and resume).
2. **AutoProcessing** — `PutStackedSkill` (`:856`), 5-zone `GetSkillInfos` (`:930`, `!IsBackgroundProcess`+`CanTrigger`, faceup-security `IsFlipped` skip), `TriggeredSkillProcess` (`:1258`), `AutoProcessCheck` (`:1386`), cut-in accounting incl. `IsCutInEffectHasUsed`→false verbatim and the inverted `IsCutInEffectUsedMaxCount` (`:1459`) — all match AS-IS.
3. **Once-use** — `freshPick:false` + AS-IS fire-and-clear (`OnProcessCallbuck?.Invoke(); SetOnProcessCallbuck(null)`, mirror `ICardEffect.cs:1136-1137` = AS-IS `:1120-1121`) genuinely prevents the double `RegisterUseEffectThisTurn`; `ActivateEffectProcess` `CanActivate||IsDeclarative` gate + MainProcessingEffect bracket match (`:1419` = AS-IS `:1063`).
4. **Deletion single-batch + survivor set** — one `OnDeletion`/`OnLeaveField` pair per delete batch over the still-on-field entries; battle transport one pair for all losers = AS-IS single `DestroyPermanentsClass(LoserPermanents)`. (Payload nulling = the P1 above.)
5. **Double-fire sweep** — clean partition, exactly one opener per timing: R-timings = inline `StackSkillInfos` (supply drops them, RDW-02 UNHANDLED), M-timings = supply-convert (no inline insert), attack = supply-convert (emit kept, insert deferred), deletion = sink/battle inline (supply drops). `EndAttackTriggerHook` retired (no live caller). Old `CollectUnifiedSeed`/`WindowResolver.DriveAsync`/`RunSyncWindowAsync` = 0 live callers.
6. **New populations** — `GetSkillInfos` scans breeding (`GetFieldPermanents`=battle+breeding) + faceup security + trash/hand identically to AS-IS; registry window reads = 0, so no double from a permanent-bucket + registry-bound overlap.
7. **Test harness (priority 9)** — no assertion weakened: the D1/D2/F1-Tier2 edits are `SetPhase(Main)` preconditions (DoneStartGame gate) + retargeting deletion input to the real sink and the optional candidate to the AS-IS `EffectSourceCard.InstanceId`; every expected value byte-identical, validated by `ChoiceResult.ValidateSelectedIds`.
8. **SetFixedMemory / EffectList_Added / reset scope** — clamp delegates to `MemoryController.Set` (real ±10), `EffectList_Added` 9 buckets same order, per-turn use-count reset covers all match controllers = AS-IS `ActiveCardList` (both players), ambient self-Enter is save/restore-nested + `using`-scoped.

---

## Recommendation for C2b

Keep 8f155d02. Before/with C2b, in priority order:
1. Close the deletion payload read side — carry `battle`+`cardEffect` on `OnDeletionHashtable`, or point `IsByBattle`/`IsByEffect` at the `deletedByBattle`/cause flags — else the deletion-reactor card population ports silently wrong.
2. Gate/queue interactive OnSecurityCheck/OnLoseSecurity reactors (RD-C2-SECCHECK-INTERACTIVE) so a choice suspend does not throw out of the security loop.
3. Decide RDW-05: either land the effect-driven-attack inline inserts (the design's stated plan) or accept the documented drop and add a regression witness so it is not mistaken for a bug later.
4. Refresh the stale "DORMANT" comments in `MultipleSkills.cs`/`AutoProcessing.cs`.
