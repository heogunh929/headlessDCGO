# Headless substrate audit — part 7/8

Manifest: `docs/audit/manifest/hl_part_07.txt` (27 files). All files read in full; AS-IS cross-checks done against
`DCGO/Assets/Scripts/Script/{CardSource,CardController,Permanent,TurnStateMachine,SelectPermanentEffect}.cs`.

## Per-file judgment

1. **Effects/DigivolutionCostHelpers.cs** — OK, with one minor dead-path note. `ParseEvolutionCondition` is the
   single canonical parser shared with the mirror `CardSource.PrintedEvoCosts` (verified: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardSource.cs:2506-2542`
   calls `Headless.Effects.DigivolutionCostHelpers.ParseEvolutionCondition`/`ReadRequirements` directly — no
   duplicate parser). The `IgnoreRequirement.All`-only bypass of the `TargetIdentity` gate matches AS-IS
   `PrintedEvoCosts` (CardSource.cs:2467-2474: the `ignore==All` check runs *before* the `TokenMatch` check,
   independent of Color/Level). Color/Level gating order-independent AND, matches AS-IS `EvoCosts` nested-if
   (CardSource.cs:2476-2486). **Minor**: `ReadRequirementsFromMetadata`/`TryReadRequirement`/`TryReadModifier`'s
   dictionary-form legacy path (`targetCardType`, `targetDefinitionId`, `digivolutionCostModifiers`, etc.) has
   zero producers anywhere else in the engine (`grep` for these keys/`DigivolutionCostModifiersKey` outside this
   file returns nothing) — inert scaffolding, not wrong, just unexercised.

2. **Runtime/DigivolutionStackHelpers.cs** — OK. Every mutation (append/prepend/trash/return/detach) carries an
   explicit `CardController.cs:NNNN` / `Permanent.cs:NNNN` citation and the OnAddDigivolutionCards /
   OnDigivolutionCardDiscarded timing placement, protection-filter, and ACE-overflow-pass ordering read as
   faithful mirrors. No issues found.

3. **Runtime/SpecialPlayAction.cs** — OK. `TryMatchMaterials`'s backtracking assignment is a faithful (more
   efficient) equivalent of AS-IS's brute-force permutation enumeration (`CardSource.CanPlayJogress` →
   `ParameterComparer.Enumerate`, CardSource.cs:2755/2858) — same solution space, not a divergence. The
   DigiXros-only `CanEnterField` gate vs. the DIGIVOLVE-shaped kinds' bypass is explained and plausible (targets
   an empty frame vs. an existing permanent). No issues found.

4. **Runtime/EffectDrivenAttack.cs** — **finding**. `RequestChoice` always builds the `ChoiceRequest` with
   `canSkip: true` (line ~260). AS-IS `SelectPermanentEffect`'s sequential Attack-mode loop
   (SelectPermanentEffect.cs:1009-1027) supports a **mandatory** attack: `if (!_canNoSelect)
   selectAttackEffect.SetCanNotSelectNotAttack();` — a card-authored effect can force the attack (no decline).
   `EffectAttackOptions` (the per-effect options record this helper is driven by) has no equivalent
   `CanNoSelect`/mandatory flag, so the primitive can only ever express the AS-IS *optional* variant. Any current
   or future card that forces an effect-driven attack cannot be represented faithfully through this substrate —
   a real (if currently perhaps unwitnessed) narrowing of the AS-IS primitive.

5. **Runtime/DigivolutionSourceStackPort.cs** — **finding (dead/orphaned, uncited game rules)**. `grep` across
   `src/` and `tests/` shows `DigivolutionSourceStackPort`, `SourceStackMutationRequest`, `SourceStackMutation`,
   `SourceAttachPosition` are referenced **only** by this file and its own isolated test
   (`tests/G2D-004.Digivolution.source.attach.Tests/Program.cs`) — no production call site (`GameFlowProcessor`,
   any action processor, `MatchStateMutationSink`) ever constructs a `SourceStackMutationRequest`. It operates on
   the `MatchState`/`CardInstanceState.SourceIds` model, entirely disconnected from the LIVE digivolution-stack
   model (`CardInstanceRecord.Metadata["sourceIds"]`) that #2/#6 and the rest of the engine actually use. Its
   embedded game-rule decisions — "a card cannot be attached as its own source," "source and target must share
   an owner," "a token cannot receive/be a digivolution source" — carry **zero** AS-IS citation, unlike its
   sibling `DigivolutionStackHelpers.cs` which cites `CardController.cs`/`Permanent.cs` for every rule. This is
   exactly the "substrate arbitrary game-rule decision" the audit targets, compounded by being fully unwired: a
   second, uncited, never-invoked implementation of digivolution-stack mutation sitting beside the real one.

6. **Runtime/FusionDigivolveHelpers.cs** — OK. Source-merge ordering (material, material's own sources, ...,
   top's prior sources), Jogress/Xros summoning-sickness exemption, per-fusion tuck-reset, and the
   `IsJogress`/`IsDigiXros` WhenDigivolving tags all read as faithful, well-cited mirrors (`CardController.cs:1509-1512`,
   `SelectDigiXrosClass.cs:923`, `CardController.cs:1497`/`1372-1376` for the topSwap/continuity split). No issues.

7. **Runtime/HeadlessEndTurnCleanupFlow.cs** — OK. Cross-checked directly against `TurnStateMachine.cs:3170-3202`
   (EndPhase reset block): the player-scope (`UntilEachTurnEndEffects`, `UntilCalculateFixedCostEffect`) and
   permanent-scope (`UntilEachTurnEndEffects`, `UntilOwnerTurnEndEffects`, `UntilOpponentTurnEndEffects`) resets
   match 1:1 (order differs — one combined loop vs. AS-IS's several — but all operations are independent
   "reassign empty list" writes with no read-dependency between them, so the reorder is behavior-neutral). Note:
   AS-IS's same block also resets `player.DigivolveCount_ThisTurn = 0` (TurnStateMachine.cs:3181); this file does
   not reset it, but `PlayerTurnCounterController.ResetForTurn()` (outside this manifest) is the dedicated mirror
   and is invoked from `TurnFlowPump.cs:314` — a different call site, not a gap in this file specifically.

8. **Effects/TriggerTimingMap.cs** — OK. Spot-checked two derivation claims against AS-IS and both held: (a)
   `OnLoseSecurity` before `OnDiscardSecurity` — `CardController.cs:4360` (`IReduceSecurity`) runs before `:4377`
   (`StackSkillInfos(..., OnDiscardSecurity)`), matching the code-comment ordering note; (b) the
   `DeletionBatchIdKey` marker gating `OnDeletion`/`OnLeaveField` to true field→Trash *deletions* (vs. a
   top-swap trash) has 4 live producers outside this file (`MatchStateMutationSink`, `SecurityResolver`,
   `GameFlowProcessor`, `BattleResolver`), consistent with the "sink/battle/sweep/security" claim in the header
   comment. No issues found.

9. **DataLoading/BanlistLoader.cs** — OK. Deck-legality/ban-list is a metagame concept with no core AS-IS engine
   rule surface to diverge from; parsing is simple and defensive (limit/id validation, comment stripping,
   section-header skip). No issues.

10. **Runtime/MulliganCoordinator.cs** — OK. Cross-checked against `TurnStateMachine.cs:373-495`: per-player
    sequential decision before security is dealt, redraw = hand→deck-bottom, shuffle, draw (hand size), and
    security dealt from the post-mulligan deck only after every player has decided — all match. Button semantics
    (`IsSkipped` = "Keep Hand", selecting the redraw candidate = "Mulligan") match AS-IS's
    `NotSelectButtonMessage`/`EndSelectButtonMessage` pairing.

11. **Diagnostics/TraceEvent.cs** — Pure diagnostics/fingerprint infra, no game-rule surface. OK.

12. **Runtime/CardObservation.cs** — RL-agent observation projection (not a rule-engine decision point); computes
    DP through the shared `DpCalculator`, so it can't itself diverge from the real DP fold. OK.

13. **Effects/EffectResult.cs** — Infra status enum/record (Resolved/Unbound/Failed/Suspended/Skipped) with
    clear, documented AS-IS anchoring for `Skipped` (RD-10, `MultipleSkills.cs:122-126` fizzle/continue) and
    `Suspended` (W7 agent-choice pause). OK.

14. **Runtime/ContinuousFieldMembership.cs** — **finding (partial AS-IS coverage, disclosed)**. Cross-checked
    against `Permanent.EffectList_ForCard` (Permanent.cs:1497-1546). Rules ①(flipped source contributes
    nothing), ②(non-top source requires the permanent `IsDigimon`), ③(inherited-vs-top split) are faithfully
    mirrored (`GranterMembershipHolds` returns `!isInherited` for the top card and `isInherited && IsDigimon` for
    a tucked source — matches AS-IS lines 1526-1541 exactly). However AS-IS has a **4th** independent membership
    arm at line 1532 — `cardEffect.IsLinkedEffect && cardSource.IsLinked` — admitting a linked effect from
    *either* the top or a non-top source regardless of the inherited split. This helper has no equivalent arm at
    all. The class's own comment discloses the gap ("no linked-effect producer exists for these scopes... add it
    with its first witness"), so it is a tracked rather than silent omission, but per audit protocol it remains a
    live AS-IS divergence: a Linked-card-granted continuous effect reaching either of this helper's current
    consumers (`CanNotTrashFromDigivolutionCards`, `CanNotPlayOptionScan`) would be silently dropped from the
    field scan today.

15. **Runtime/DpBoostHelpers.cs** — OK. Cross-checked against `Permanent.cs:653-663` (fold position, after the
    NotIsUpDown group + LinkedDP, before the final `>=0` clamp) and `:672-686` (`AddBoost`/`RemoveBoost` upsert-
    by-id semantics) — both match exactly.

16. **Effects/EventCollectionMetadata.cs** — Infra convention codification (CSV-of-id-values flatten/parse), not
    a rule decision. Byte-identical to the ad-hoc call sites it replaces per its own doc comment; spot-checked
    against a couple of existing emit sites (`DigivolutionStackHelpers`) and the convention holds. OK.

17. **Services/ZoneMoveRequest.cs** — Pure DTO with structural validation (no same-zone move, no `Custom`
    endpoint, not both `None`). No rule content. OK.

18. **Runtime/InMemoryHeadlessMemoryController.cs** — Functionally OK (`CanPay`/`Pay`/`Add`/clamp logic is real,
    not a stub) but carries a stale literal `// TODO: Replace this clamp-only tracker with real memory handoff
    and cost handling.` header comment that no longer matches the codebase's actual maturity (the engine has
    since grown a full cost pipeline — `PlayCostHelpers`, `DigivolutionCostHelpers`, etc. — that consumes this
    controller as a real service, not a placeholder). Low-severity hygiene note, not a fidelity defect.

19. **Services/CardInstanceRecord.cs** — Plain data record with non-empty-id/owner validation and defensive
    metadata copy. No rule content. OK.

20. **Runtime/InMemoryHeadlessPlayerStatusController.cs** — Simple lose-tracking substrate; "first loss reason
    wins" is a reasonable, harmless substrate policy (AS-IS UI/log concerns don't constrain this). OK.

21. **Choices/ChoiceCandidate.cs** — DTO with defensive validation (non-empty id, concrete zone). OK.

22. **Runtime/DigivolveCommons.cs** — OK. `OnDigivolveCompletedAsync` (counter++, draw 1, OnDraw emit) is cited
    to `CardController.cs:1526-1529`/`:1948-1960` and used uniformly by every non-DigiXros digivolve completion
    site in this manifest (`SpecialPlayAction`, mirrored by `DigivolveAction` elsewhere) — consistent chokepoint.

23. **Effects/PendingEffect.cs** — Plain DTO (Request + Mode) with an enum-defined guard. No rule content. OK.

24. **Runtime/GameEventType.cs** — Internal event-category enum, infra only. OK.

25. **Services/ICardRepository.cs** — Interface only, no logic to audit. OK.

26. **Services/IHeadlessLegalActionController.cs** — Interface only. Carries a stale literal
    `// TODO: Replace with rule-generated legal action updates after AS-IS rule flow is ported.` — the same
    hygiene note as #18: the engine now has extensive rule-generated legal-action producers throughout the
    codebase (this very manifest's `SpecialPlayAction.GetLegalActions` is one), so the comment reads as leftover
    from an earlier stage rather than a live gap. No functional issue (the interface itself is a thin, correct
    contract).

27. **Services/IHeadlessLegalActionSeeder.cs** — Interface only; same stale-TODO hygiene note as #26. No
    functional issue.

## Summary of problems found

- **#5 `DigivolutionSourceStackPort.cs`** — dead/orphaned parallel implementation of digivolution source-stack
  mutation, operating on a disconnected `MatchState` model, exercised only by its own isolated test, with
  uncited (no AS-IS reference) embedded game-rule decisions. Highest-severity finding in this batch.
- **#4 `EffectDrivenAttack.cs`** — the effect-driven attack choice is unconditionally skippable; the AS-IS
  mandatory-attack variant (`_canNoSelect`/`SetCanNotSelectNotAttack`) has no substrate equivalent
  (`EffectAttackOptions` carries no such flag).
- **#14 `ContinuousFieldMembership.cs`** — missing the AS-IS `IsLinkedEffect && cardSource.IsLinked` membership
  arm from `Permanent.EffectList_ForCard`; disclosed in-code as a deferred gap, but still a live AS-IS-coverage
  gap for its two current consumers.
- **#1 `DigivolutionCostHelpers.cs`**, minor — dictionary-form legacy cost/modifier metadata keys have zero
  producers anywhere in the engine (inert, not wrong).
- **#18/#26/#27**, minor/hygiene — stale literal `TODO` comments describing components as provisional
  placeholders when the surrounding engine has since matured past that state; no functional defect, but worth a
  cleanup pass (and note these are the kind of literal-"TODO" strings the project's lint-guard convention
  otherwise avoids in engine source).

No gate-stub (`=>true`/unconditional-pass) substrates were found among the 27 files. No cases of mirror-layer
game logic leaking into these particular substrate files were found (the substrate files that touch rules — cost
helpers, digivolution stack, fusion, end-turn cleanup, trigger-timing derivation, field-membership — all route
through or cite the mirror/AS-IS layer rather than deciding rules independently, with the two exceptions above).
