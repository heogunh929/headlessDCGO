# Headless substrate audit — Part 03 (`hl_part_03.txt`)

Base path: `src/HeadlessDCGO.Engine/` (the manifest lists `Headless/...` relative to it).
AS-IS ground truth: `DCGO/Assets/Scripts/Script/` (original Unity). Mirror layer: `src/HeadlessDCGO.Engine/Assets/Scripts/`.
Method: full read of each file; every rule-shaped decision traced to AS-IS source; citations spot-checked (not trusted blind); liveness of each helper verified by call-site grep.

Verdict categories used: **CLEAN** (legitimate substrate, AS-IS-grounded or pure plumbing) / **FINDING** (DIVERGENCE / INVENTED-RULE / STUB-GATE / MIRROR-LEAK / DUPLICATION / DEAD-CODE). No STUB-GATE and no INVENTED-RULE were found (every gate reads real state; every constant traces to AS-IS). Findings are duplication / documented-divergence / dead-code.

---

## Per-file judgment (27 files)

### 1. `Headless/Runtime/GameFlowProcessor.cs` (1239) — CLEAN substrate
The re-entrant "run to stable" loop (AS-IS `TurnStateMachine` rule-process / auto-process / attack-advance / end-turn cycle) plus the state-based deletion sweep. Every rule-shaped decision is AS-IS-grounded and citations verified:
- `StateBasedDeletionSweepAsync` / `HasStateBasedSweepWork` mirror AS-IS `DigimonLackDPProcess` + `TrashNoDPPermanentProcess` (AutoProcessing.cs:439-482) and route DP-zero deaths through the same `Destroy()`/`DestroyPermanentsClass` path (CardController.cs:3729-3872) — verified the `destroyTargetPermanents_Fixed` fix, the single `OnDeletionHashtable` StackSkillInfos pair (:3736/3749), and `willBeRemoveField` reset at :3591/3865. Batch semantics (one batch id per Destroy, one OnDeletion window per batch) match the single-StackSkillInfos AS-IS shape.
- `IsNoDpTrashablePermanent` / `HasLethalDp` mirror `IsNotHavingDP`/`IsDigimonLackDP` (AutoProcessing.cs:165-215), battle-area-only scope matches AS-IS `GetBattleAreaPermanents`.
- Verified non-issue: `HasLethalDp` returns false when `Permanent.DP > 0` (i.e. destroys DP≤0), whereas AS-IS `IsDigimonLackDP` uses `DP == 0`. These are equivalent because both the AS-IS and mirror `Permanent.DP` getters clamp a defined DP to a floor of 0 (`DCGO/.../Permanent.cs:490-496,560+`; mirror `Permanent.cs:550-553`). Both also gate on `CanBeDestroyed()` at predicate level — matches AS-IS :205.
- `EndTurnCheck` mirrors `EndGameProcess` both-lose→draw (AutoProcessing.cs:392-400) — verified.
- Documented (not hidden) divergence carried in-file as design item **R2-P2-4**: the DP-zero destroys interleave per-card with pending-deletion finalizes in one scan, whereas AS-IS runs `TrashNoDPPermanent` / `DigimonLackDP` as separate whole-board passes; the OnDeletion/leave-field windows are still opened per-batch so reactor fire-count is preserved. Tracked, disclosed.
- `ReclassifyKind` is a retired no-op (registry trigger-reader excised, producer 0) — correctly inert.

### 2. `Headless/Runtime/PlayCardAction.cs` (641) — CLEAN substrate
Legal-action generation + play orchestration. All cost/rule computation delegates to the mirror: `CardSource.GetPayingCostWithBaseCost` (cost fold), `SelectAssemblyClass.TryMatch/ValidateMaterials` (Assembly), `CardSource.CanEnterField` (field-placement restriction, cited CardSource.cs:1163-1170). Assembly flat discount `Math.Max(0, cost - reduceCost)` only for the full set matches AS-IS `GetPayingCost` (CardSource.cs:705-737). Summoning-sickness `enteredThisTurn` mirrors `EnterFieldTurnCount` (CardController.cs:1386). Option cards rejected (routed to ActivateOption). No cost logic reinvented in substrate. Deferred BeforePayCost/[On Play] choice handling is commit-once plumbing.

### 3. `Headless/Runtime/OptionActivateAction.cs` (490) — CLEAN (see Finding F1 for the color dup)
Option activation orchestration. Gate order — `IsOptionLocked` (metadata flags) → `CanNotPlayOptionScan` → `OptionColorRequirement` → cost — mirrors AS-IS `CardSource.CanNotPlayThisOption` region order (regions ①②③ then `!MatchColorRequirement`, CardSource.cs:184-245), verified against the mirror `CardSource.CanNotPlayThisOption` (CardSource.cs:358-411). Moves the used option Hand→Trash face-up. Cost delegates to mirror `GetPayingCostWithBaseCost`. The only issue is that its color gate calls the substrate `OptionColorRequirement` copy (Finding F1), not a defect of this file's flow.

### 4. `Headless/Runtime/LinkHelpers.cs` (333) — CLEAN substrate (carries documented divergence F4)
Substrate translation of the AS-IS `Permanent` link model onto off-field (`ChoiceZone.None`) storage + metadata. The mirror `Permanent.AddLinkCard`/`RemoveLinkedCard`/`LinkedMax`/`LinkedCards` (Permanent.cs:4332/3874/2834/2812) DELEGATE to this helper — legitimate substrate-translation pattern, not competing logic. AS-IS grounding verified: newest-first `Insert(0,…)`, overflow-resolved-before-attach, `LinkedMax==1` silent `[0]` removal (AS-IS Permanent.cs:1250-1256). `ResolveLinkedMax`/`ResolveLinkCost` fold via mirror `NewModelContinuousScan.FoldLinkedMax/FoldLinkCost`; the retired `linkedMaxDelta`/`linkCostDelta` legacy pre-folds are honestly documented as zero-producer bit-identical retirements. See F4 for the LinkedMax>1 gap.

### 5. `Headless/DataLoading/CardAssetJsonLoader.cs` (298) — CLEAN substrate
Generic JSON→`CardRecord` ingestion. Field-name aliases (id/cardId/…, playCost/cost, …) are lenient parsing convenience; the whole JSON object is copied verbatim into the metadata dict (case-insensitive) for the mirror to read — no card-type/color/cost game rule is decided here. The one validation rule (playCost/evoCost must be non-negative, :212-214/:223-225) is an input-integrity guard consistent with DCG non-negative costs, not a play rule. Deterministic directory ordering (OrdinalIgnoreCase then Ordinal). No rule leakage.

### 6. `Headless/State/CardInstanceState.cs` (262) — CLEAN substrate
Immutable card-instance record (suspend/flip/source-ids/opaque modifier+flag maps + fingerprint segment). `IsSuspended`/`IsFaceUp` correspond to AS-IS `CardSource` tap/`IsFlipped` (CardSource.cs:56-62) — stored, not decided. Source-id uniqueness/no-empty are container invariants mirroring `Permanent.cardSources`. No magic numbers, no rule-shaped conditions.

### 7. `Headless/State/ZoneState.cs` (225) — CLEAN substrate (2 low-sev notes)
Generic ordered card container. `DefaultVisibility` hidden set `{Library, Hand, Security, DigitamaLibrary}` matches AS-IS face-down piles (`LibraryCards`/`SecurityCards`/`DigitamaLibraryCards` face-down, hand hidden from opponent; owner-view override at `ToView`). No zone-capacity gate is faked. Low-sev: (a) `DigivolutionCards`/`LinkedCards` default to Public (under-stack; mitigated by owner-view, no single AS-IS constant behind the default); (b) `MoveCardTo` hardcodes destination bottom-insert as a generic default (placement is genuinely caller/mirror business; `InsertTop/At` exist). Neither is a rule the class enforces wrongly.

### 8. `Headless/Runtime/OverclockEffect.cs` (215) — FINDING F3 (in-flight duplication), otherwise sound
LIVE choice-controller substrate for end-of-turn Overclock (optional delete a trait/token ally → untapped player-only attack). Delegates the delete to `DeletionReplacementGate.SacrificeAsync` (same path as Decoy/Scapegoat) and the attack to `EffectDrivenAttack`; type check via mirror `Permanent.IsDigimon`. Candidate predicate `IsToken || HasTrait(requiredTrait)` matches the cited AS-IS `OverclockProcess` shape; candidates are additionally filtered by a `cannotBeDeleted` metadata flag (plausible CanBeDeleted pre-filter). See F3: this is an acknowledged duplicate of the mirror `Overclock.cs` rehousing (R2-A) that is still the live path pending R3 window-routing.

### 9. `Headless/Choices/ChoiceResult.cs` (185) — CLEAN substrate
Selection validation. Every bound (min/max/canSkip/candidates/validator) is supplied by `ChoiceRequest`, none hardcoded. `SelectionValidator` over the whole selected set faithfully models AS-IS `CanEndSelect(List<Permanent>)` (SelectPermanentEffect.cs:221-224) — citation verified accurate. Real accumulate-and-throw, no unconditional pass.

### 10. `Headless/Runtime/DeletionSourceTrash.cs` (151) — CLEAN substrate
Substrate translation of AS-IS `Permanent.DiscardEvoRoots` (Permanent.cs:106-142) + link detach; the mirror `Permanent.DiscardEvoRoots` (Permanent.cs:3922) delegates to it. Verified AS-IS grounding: sources trashed UNCONDITIONALLY (no protection check, `honorProtection:false`), overflow applied evo-THEN-link before any trash (Permanent.cs:113-114), ACE `Overflow` turn-player-first stable sort (CardController.cs:5836-5851). Legitimate delegate, not competing logic.

### 11. `Headless/State/DigivolutionStack.cs` (143) — CLEAN substrate
Ordered stack record (bottom DigiEgg → Top) with structural invariants only. `BaseDp => TopCard.BaseDp` matches AS-IS `Permanent.BaseDP` = top-card-only (Permanent.cs:193-202). Allocation-free validation rewrite spot-checked equivalent to the LINQ original it documents. Cross-file lead (out of scope): `DigivolutionStackReader` labels `StackRole.DigiEgg` positionally (bottommost) vs AS-IS kind-based `CardKind.DigiEgg` — this file embeds no such rule.

### 12. `Headless/Runtime/MatchConfig.cs` (124) — CLEAN substrate
Match setup record. The memory-range defaults `MinimumMemory=-10 / MaximumMemory=10` are VERIFIED against AS-IS memory clamp (Player.cs:1015-1022 / 1112-1119: `Memory >= 10 → 10`, `<= -10 → -10`; `MaxMemoryCost` uses ±10, :1135/1140). `InitialMemory=0` matches the AS-IS gauge start. Not invented — AS-IS-grounded. The rest is range/duplicate validation plumbing.

### 13. `Headless/Runtime/OptionColorRequirement.cs` (103) — FINDING F1 (duplication / mirror-leak)
See F1. A second, full implementation of the option color-requirement rule living in substrate, parallel to the mirror `CardSource.MatchColorRequirement`.

### 14. `Headless/Runtime/DpZeroDeletionHelpers.cs` (99) — FINDING F2 (dead code + latent divergence)
See F2. `SweepAsync` has zero live callers (only its const keys `DpZeroKey`/`DpKey`/`DeletedByEffectKey` are consumed elsewhere). The live DP-zero path is GameFlowProcessor's sweep (which does gate on `CanBeDestroyed()`).

### 15. `Headless/Runtime/AttackDeclarationCommons.cs` (88) — CLEAN substrate
Single declare-attack chokepoint for player + effect-driven attacks; delegates the AS-IS `Attack()` sequence (suspend / snapshot / beforeOnAttack hook / OnAttack+OnAllyAttack windows / gates, AttackProcess.cs:73-253) to the mirror `AttackProcess.Attack`. The sync `Declare` uses `GetAwaiter().GetResult()` over the async path — documented safe because the null-hook path never awaits an agent choice; the hook path is the awaited `DeclareAsync`. Deferred behavioral items (RD9-87 suspend-through-sink, RD9-90 Main-skill emit) disclosed. Pure routing.

### 16. `Headless/Bridge/GManagerBridge.cs` (76) — CLEAN substrate
Thin accessor facade over `EngineContext` (Get*/TryGetService). No game logic. Substrate replacement surface for AS-IS `GManager` accessors.

### 17. `Headless/Services/HeadlessEntityId.cs` (64) — CLEAN substrate
Value-type id + JSON converter. Trim/non-empty guards only. No rules.

### 18. `Headless/Coroutines/IEngineTask.cs` (62) — CLEAN substrate
Cooperative-task abstraction (status/wait/step) replacing Unity coroutines. Pure substrate contract.

### 19. `Headless/Bridge/AmbientMatchContext.cs` (57) — CLEAN substrate
`AsyncLocal<EngineContext>` replacement for the AS-IS process-global `GManager.instance`, correctly match-scoped (save/restore nesting) so concurrent matches don't cross-read. Explicitly the sanctioned substrate for the mirror `GManager.instance`. No rule content.

### 20. `Headless/DataLoading/CardDatabase.cs` (50) — CLEAN substrate
`ICardRepository` wrapper over `InMemoryCardRepository` (upsert/query/snapshot/clear). Pure plumbing.

### 21. `Headless/Choices/ChoiceOption.cs` (40) — CLEAN substrate
Trivial value object (id/label/zone) + candidate adapter; guards non-empty id/label and concrete zone (rejects `None`). No rule.

### 22. `Headless/Runtime/DeferredActivationController.cs` (39) — CLEAN substrate
Holds the single activation suspended mid-resolution across an agent choice, with flavor flags so the resume replays the same gate/consume order. Commit-once resume plumbing; no game rule.

### 23. `Headless/Effects/WindowChoicePendingException.cs` (23) — CLEAN substrate
Control-flow exception used to unwind the mirror window loop's C# stack when a choice opens (the headless suspension signal). No logic.

### 24. `Headless/Choices/ChoiceZone.cs` (19) — CLEAN substrate
Zone enum. First 10 members are 1:1 (name + ordinal) with AS-IS `SelectCardEffect.Root` (Library…LinkedCards). The 3 additions (BattleArea/BreedingArea/DigitamaLibrary) are all real AS-IS zones (`Player.GetBattleAreaPermanents`/`GetBreedingAreaPermanents`, `DigitamaLibraryCards`). No invented or missing zones.

### 25. `Headless/Services/NullLogSink.cs` (16) — CLEAN substrate
No-op `ILogSink`. Diagnostics sink, not a game gate. (Its empty bodies discard log output only — not a rule bypass.)

### 26. `Headless/Services/ZoneMoveResult.cs` (9) — CLEAN substrate
Immutable result record (request/event/source+dest card lists). Data only.

### 27. `Headless/Diagnostics/ITraceSink.cs` (6) — CLEAN substrate
Trace interface. No logic.

---

## Findings (all)

### F1 — DUPLICATION / MIRROR-LEAK — `Headless/Runtime/OptionColorRequirement.cs` (whole file)
The option color-requirement rule ("every required color must be present on some owner field permanent, unless an ignore-color effect applies") is fully implemented **twice**: here in substrate (`OptionColorRequirement.Matches`) AND as the 1:1 mirror port `CardSource.MatchColorRequirement` (`Assets/Scripts/Script/CardSource.cs:313-343`, cites AS-IS CardSource.cs:255-321). Both are LIVE on different paths:
- the headless option-play legality gate calls the **substrate** copy — `OptionActivateAction.cs:239` and mirror `CardEffectCommons.cs:4998`;
- the mirror `CardSource.CanNotPlayThisOption` calls the **mirror** copy — `CardSource.cs:406` (`!MatchColorRequirement`), and that is consumed by card effects (EX8_037.cs:157, P_223.cs:309).

Two parallel implementations of one AS-IS rule that can drift out of sync (e.g. an ignore-color or dual-card fix applied to one and not the other). Per the project's "substrate = substrate only, game logic = mirror" rule, the color-requirement rule is game logic and should have a single home (the mirror), with the play-gate delegating to it. Severity: medium (correctness-by-duplication hazard; both currently agree).

### F2 — DEAD-CODE + LATENT DIVERGENCE — `Headless/Runtime/DpZeroDeletionHelpers.cs:22` (`SweepAsync`)
`SweepAsync` (the DP-zero deletion sweep) has **zero live callers** repo-wide (verified: only the constants `DpZeroKey`/`DpKey`/`DeletedByEffectKey` are referenced, e.g. `CardEffectCommons.cs:4333`). The live DP-zero path is `GameFlowProcessor.StateBasedDeletionSweepAsync`. If this dead helper were ever re-wired it would diverge from AS-IS/the live path: its selection lacks the predicate-level `CanBeDestroyed()` immunity gate that AS-IS `IsDigimonLackDP` (AutoProcessing.cs:205) and the live `GameFlowProcessor.HasLethalDp` (GameFlowProcessor.cs:679) both apply — a Delete/Prevent-protected 0-DP Digimon would be selected. Severity: low today (unreferenced), but it is an un-guarded retirement candidate — recommend deletion or an `[Obsolete]`+baseline guard per the retirement-guard protocol rather than leaving it live-looking.

### F3 — IN-FLIGHT DUPLICATION (tracked) — `Headless/Runtime/OverclockEffect.cs` (whole file)
The Overclock end-of-turn logic (candidate select → delete → untapped player-only attack) is rehoused 1:1 in the mirror `Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Overclock.cs` (R2-A) yet **this substrate file is still the live path** (consumed by `MetadataActionProcessor.cs:665`, `MatchStateMutationSink.cs:294`, the end-of-turn window). So the rule exists in two live-adjacent places pending R3 window-routing of the mirror `ActivateClass`. This is disclosed in the file header (잔존=R3 몫). Severity: low (self-documented migration state, single live consumer), but it is a real duplication that should close when R3 lands — flagging so it is not lost.

### F4 — DOCUMENTED DIVERGENCE (tracked) — `Headless/Runtime/LinkHelpers.cs:164-181` (AddLinkCard overflow)
When a link host is already at max and `LinkedMax > 1`, AS-IS `Permanent.AddLinkCard` opens an owner SELECTION to trim the excess (`RemoveLinkedCard(null, excess)` → per-pick `ITrashLinkCards`, which emits `OnLinkCardDiscarded`), Permanent.cs:1250-1256. The substrate handles only `LinkedMax == 1` (silent `[0]` removal) inline and falls back to post-attach **auto oldest-first** enforcement for `>1`, skipping the owner choice. Disclosed as design item **MIG2-ADDLINK-SELECT**; the mirror `Permanent.RemoveLinkedCard` (CardSource choice/park) exists but is bypassed on the AddLinkCard overflow path. No witness card (every ported link host is max-1). Severity: low (no live max>1 host), but a genuine order/agency divergence for the >1 case — must close before any max>1 host is ported.

### Low-severity notes (not findings; no AS-IS rule violated)
- `ZoneState.cs` — under-stack (`DigivolutionCards`/`LinkedCards`) default-Public visibility and `MoveCardTo` bottom-insert default are arbitrary generic defaults relying on higher layers.
- `DigivolutionStackReader.cs` (out of Part-03 scope) — positional `StackRole.DigiEgg` labeling vs AS-IS kind-based `CardKind.DigiEgg`; could mislabel a directly-played (un-hatched) bottom Digimon. Referred for a separate pass.

---

## Summary
- **27/27 files read in full and judged.** 22 CLEAN (legitimate substrate: plumbing, value types, AsyncLocal/GManager substrate, or AS-IS-grounded translation that the mirror delegates to). 
- **4 findings**: F1 OptionColorRequirement duplication (medium), F2 DpZeroDeletionHelpers dead SweepAsync + latent CanBeDestroyed gap (low, unguarded retirement), F3 OverclockEffect in-flight R2-A/R3 duplication (low, tracked), F4 LinkHelpers LinkedMax>1 trim-selection gap MIG2-ADDLINK-SELECT (low, tracked, no witness).
- **No STUB-GATE** (every gate reads real state/predicates), **no INVENTED-RULE** (memory range ±10, LinkedMax default 1, DP≤0/CanBeDestroyed predicates, zone set, assembly discount — all verified against AS-IS), **no undisclosed DIVERGENCE** (the two behavioral divergences F4 and GameFlowProcessor R2-P2-4 are both documented in-source as tracked design items).
- Citations spot-checked in GameFlowProcessor, PlayCardAction/OptionActivateAction, LinkHelpers, DeletionSourceTrash, OverclockEffect, MatchConfig were **accurate** against the cited AS-IS lines.
