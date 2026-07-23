# AS-IS↔TO-BE Match Check — Part 04

Manifest: `docs/audit/manifest/both_part_04.txt`
AS-IS root: `DCGO/Assets/Scripts/`　TO-BE root: `src/HeadlessDCGO.Engine/Assets/Scripts/`

## 1. Script/CardEffectFactory.cs — 정상 (PASS)

AS-IS 1530 lines / TO-BE 2418 lines (main partial file only; both sides also have a `Script/CardEffectFactory/`
subdirectory of further partial files, out of this file's literal scope but cross-checked below).

**Method-level census**: extracted all 41 `public static` top-level members from the AS-IS main file
(`SetMemoryTo3TamerEffect`, `Gain1MemoryTamerOpponentDigimonEffect`, `Gain1MemoryTamerOwnerDigimonConditionalEffect`,
`PlaySelfTamerSecurityEffect`, `PlayMindLinkTamerFromDigivolutionCards`, `PlaySelfDigimonAfterBattleSecurityEffect`,
`Gain2MemoryOptionDelayEffect`, `PlaceSelfDelayOptionSecurityEffect`, `ActivateMainOptionSecurityEffect`,
`ReplaceBottomSecurityWithFaceUpOptionMainEffect`, `ReplaceTopSecurityWithFaceUpOptionMainEffect`,
`ReplaceBottomSecurityWithFaceUpOptionEffect`, `ReplaceTopSecurityWithFaceUpOptionEffect`, `UseRequirements`,
`GetJogressConditionClass`, `DigiXrosEffectFromNames`, `ActivateClassesForSharedEffects`, `ActivateClass`
(boilerplate), `WhenMovingClass`, `OnPlayClass`, `WhenDigivolvingClass`, `WhenAttackingClass`, `OnDeletionClass`,
`WhenLinkingClass`, `SecurityClass`, `EndOfAttackClass`, `CounterClass`, `TurnTimingClass`,
`StartOfYourTurnClass`, `StartOfYourMainPhaseClass`, `EndOfYourTurnClass`, `YourTurnClass`,
`StartOfOpponentsTurnClass`, `StartOfYourOpponentsMainPhaseClass`, `EndOfYourOpponentsTurnClass`,
`OpponentsTurnClass`, `EndOfAllTurnsClass`, `AllTurnsClass`, `EoTLose3Memory`, `PlaceToSecurityEffect`,
`AddDetailClass`) — **all 41 present in TO-BE**, each carrying an AS-IS-line-cited comment. Body-level diff of
the boilerplate timing-builder family (`ActivateClass`/`WhenMovingClass`/`OnPlayClass`/`WhenDigivolvingClass`/
`WhenAttackingClass`) is verbatim except the mechanical `IEnumerator`→`Task`/`StartCoroutine`→`await`
substrate translation. Spot-checked full-body fidelity on `Gain1MemoryTamerOpponentDigimonEffect`,
`Gain2MemoryOptionDelayEffect`, `ReplaceBottomSecurityWithFaceUpOptionMainEffect/Effect`,
`PlayMindLinkTamerFromDigivolutionCards`, `ActivateMainOptionSecurityEffect`, `PlaceSelfDelayOptionSecurityEffect`,
and the large `PlaySelfDigimonAfterBattleSecurityEffect` (nested triple-grant body, AS-IS :285-464) — all
verbatim line-for-line with only substrate (coroutine/UI-yield) translations, each documented in-line.

**Symbols absent from the AS-IS main file but present in TO-BE's main file** (`ChangeSelfSAttackStaticEffect`,
`ChangeSelfDPStaticEffect`, `PierceSelfEffect`, `BlockerSelfStaticEffect`, etc.) were checked and confirmed to
live in the AS-IS `Script/CardEffectFactory/` subdirectory (partial-class siblings, not this file) — every
"moved to CardEffectFactory/X.cs" comment was spot-verified to point at a real existing TO-BE file
(`ChangeSAttack.cs`, `ChangeDP.cs`, `CanNotDigivolve.cs`, `CanNotAttack.cs`, `KeyWordEffects/Pierce.cs`,
`KeyWordEffects/Blocker.cs` all exist).

**New-model additions with no AS-IS `CardEffectFactory.cs` namesake** (`CanNotReduceCostStaticEffect`,
`CanNotAddSecurityStaticEffect`, `CanNotAddMemoryStaticEffect`, `DontHaveDPStaticEffect`,
`CannotIgnoreDigivolutionConditionStaticEffect`, `DigiXrosEffect`/`DigiXrosWithExtraMaterialsEffect`/
`BurstDigivolveEffect`/`JogressEffect`/`JogressEffectFromNames`/`AddJogressLevelsEffect`, etc.) — checked their
backing types/interfaces against real AS-IS source: `ICannotAddSecurityEffect`/`Player.CanAddSecurity`,
`ICannotReduceCostEffect`/`Player.CanReduceCost`, `ICannotAddMemoryEffect`/`Player.CanAddMemory` all exist
verbatim in AS-IS `Player.cs` (see §2); `IAddJogressLevelsEffect`/`AddJogressLevelsClass` exist in AS-IS
`CardEffectInterfaces.cs`/`CardEffects/AddJogressLevelsClass.cs`. These are legitimate NEW-MODEL kind-class
factories consolidating AS-IS mechanisms that were previously expressed only as ad-hoc inline patterns per
card (not a prior shared `CardEffectFactory` helper) — grounded in real AS-IS interfaces, not fabricated. One
observation (not a hard finding): `BurstDigivolveEffect`/`JogressEffectFromNames`/`JogressEffect` register into
a `SpecialPlayRecipeRegistry` — a registry-shaped construct — which is worth a second look against this
project's registry-teardown convention, but is outside this file's literal AS-IS anchor (no `CardEffectFactory.cs`
line to check it against) so is flagged as an observation only.

**Minor, non-fidelity-breaking observation**: in the ported `PlaySelfDigimonAfterBattleSecurityEffect`
(deleteDigimon branch), AS-IS `CanUseCondition2` calls
`IsPermanentExistsOnOwnerBattleArea(playedDigimon, playedDigimon.TopCard)` (CardEffectFactory.cs:390) while
TO-BE calls `IsPermanentExistsOnOwnerBattleArea(playedDigimon, card)` (line 1126). Traced
`IsOwnerPermanent(permanent, card)` (AS-IS `GameContextDeterminarion.cs:388`) — it only compares
`.Owner`, and `card`'s owner is match-invariant, so `card.Owner == playedDigimon.TopCard.Owner` holds in
every reachable state (no ownership-transfer effect exists in this codebase). Functionally equivalent; not
flagged as a bug.

## 2. Script/Player.cs — 정상 (PASS)

AS-IS 1675 lines / TO-BE 1006 lines. The AS-IS↔TO-BE size gap is fully accounted for by Unity/UI substrate
that has no game-logic content: `Start()`, `Update()`, `OnClickFrame`, all `Image`/`Text`/`Transform`/
`GameObject`/`EventTrigger`/`SpriteRenderer`/`DOTween` fields and their setters (`SetPlayerUI`,
`SetHandCountText`, `SetLibraryCountText`, `SetDigitamaLibraryCountText`, `ShowTrash`, `ShuffleAnimation`,
`AlignHand`, `SetOriginalPlayMat`, hatch-object UI, frame-select visuals), confirmed to have no card-effect
callers by disjoint-symbol grep.

Traced every AS-IS Player member that looked like real game logic but is absent from the mirror `Player` class,
to confirm it is genuinely relocated rather than dropped:
- `IsLose` getter (AS-IS AutoProcessing.cs:324/390/392, CutInProcess.cs:16) → TO-BE `IHeadlessPlayerStatusController.IsLose`
  (read directly by `TerminalEvaluator`/`AutoProcessing.EndGameProcess`); `SetLose()` itself IS ported
  (`Context.PlayerStatusController.MarkLose`).
- `DigivolveCount_ThisTurn` (AS-IS BT1_007.cs / CardController.cs:1528 / TurnStateMachine.cs:3181) →
  TO-BE `PlayerTurnCounterController.DigivolveCountKey` + `CardEffectCommons.DigivolveCountThisTurn(card)`,
  incremented in `DigivolveCommons.cs`, reset in `TurnFlowPump.cs:313`.
- `QueuePlayerSelection`/`DequeuePlayerSelection`/`HasPlayerSelection` (AS-IS selection-queue idiom used
  pervasively across `SelectHandEffect.cs`, `SelectCardEffect.cs`, `SelectCountEffect.cs`, `OptionalSkill.cs`,
  `SelectPermanentEffect.cs`, `SelectAttackEffect.cs`, `SelectDigiXrosClass.cs`, `TurnStateMachine.cs`,
  `MultipleSkills.cs`, `DNADigivolveEffects.cs`, `UserSelectionManager.cs`) → deliberately replaced by the
  Headless.Choices architecture; confirmed via explicit AS-IS-line-citing comments in the mirror
  `TurnStateMachine.cs`/`SelectHandEffect.cs` ("ChoiceType.BreedingDecision choice-pause replacing the :788
  WaitWhile(HasPlayerSelection) poll") — a documented architectural substitution, not a silent drop.
- `KeyCard` — grepped every AS-IS use; all real usages are `DeckData.KeyCard` (deck-building/UI), never
  `Player.KeyCard` from card-effect code. Dead in AS-IS card logic; correctly omitted.
- `FieldCardFrame.FacingFrame` — defined in AS-IS but has zero call sites anywhere in AS-IS (grep-verified).
  Dead code; correctly omitted from the mirror `FieldCardFrame`.

Verified the memory-sign-convention math the class relies on: AS-IS `MaxMemoryCost` (`Player.cs:1127-1146`,
seat-absolute `PlayerID==0 ? |10−Memory| : |−10−Memory|`) reduces algebraically to TO-BE's
`Math.Abs(MemoryForPlayer + 10)` once AS-IS `MemoryForPlayer` (`PlayerID==0 ⇒ −Memory`) is substituted — derived
by hand, confirmed correct for both branches. Verified the turn-player-relative sign convention TO-BE's
`AddMemory`/`SetFixedMemory`/`MemoryForPlayer` depend on is real, live substrate (not an invented assumption) by
reading `AceOverflowGate.MemoryDelta` (`Headless/Runtime/AceOverflowGate.cs:44-56`), which documents and
implements the identical "positive turn-player-relative" convention independently.

**One documentation-only inconsistency** (not a matching/fidelity bug): the class's top-of-file XML summary
(`Player.cs:14-21`) still states `CanAddMemory`/`CanReduceCost`/`IsEmptyFrame`/`IsBattleAreaFrame` are
"deliberately NOT stubbed" (an old MIG5-era SCOPE note) — all four are in fact now implemented further down
in the same file (`CanAddMemory` at :769, `CanReduceCost` at :522, `IsEmptyFrame`/`IsBattleAreaFrame` on
`FieldCardFrame` at :995/:1001), each citing its own later, superseding design item. Stale top comment only;
the actual code is complete.

## 3. Script/AutoProcessing.cs — 정상 (PASS)

AS-IS 1106 lines / TO-BE 1618 lines. Read the TO-BE file in full (not just the rule-processing half implied by
its own header note). Every top-level AS-IS symbol is present and body-verified verbatim modulo the standard
substrate translations (`MonoBehaviourPunCallbacks` component → `EngineContext`-scoped service via
`AutoProcessing.For`/`ForCutIn`; `IEnumerator`/`StartCoroutine` → `async Task`/`await`; UI-only yields
`ShowCardEffect`/`PlaySE`/`WaitForSeconds`/`ShrinkSecurityDigimonDisplay` internals stripped per convention):
`IsRuleProcessing`, all seven rule predicates (`IsNotDigimonInBreeding` … `IsPermanentFaceDown`), `RuleProcess`/
`DoRuleProcess`, all eight per-rule processes (`EndGameProcess`, `TrashNonDigimonPermanentProcess`,
`TrashNoDPPermanentProcess`, `DigimonLackDPProcess`, `BattleWithoutDigimon`, `DigimonLackLinkConditionProcess`,
`DigimonLackLinkMaxCountProcess`, `CardFaceDownProcess`), the pool (`multipleSkills`/`availableMultipleSkills`/
`executingMultipleSkills`), `PutStackedSkill`, `GetSkillInfos`, `ActivateBackgroundEffects`, `StackSkillInfos`,
`GetSkillInfosOfCards`, `ActivateBackgroundEffectsOfCards`, `StackSkillInfosOfCards`, `TriggeredSkillProcess`,
`skillInfos_used`, `HasExecutedSameEffect`, `EndTurnCheck`, `TurnEndMinMemory`, `EndTurnProcess`,
`ShrinkSecurityDigimonDisplay`/`HasAwaitingActivateEffects`, `ActivateEffectProcess`, `IsCutInEffectHasUsed`
(still verbatim-stubbed `false`, matching AS-IS's own dead/commented-out real check), `IsCutInEffectUsedMaxCount`,
`AddCutinEffect`. Every method carries an AS-IS-line-cited comment and, where behavior necessarily differs
(e.g., an 8th `DoRuleProcess` check for `GameFlowProcessor.HasStateBasedSweepWork`, a no-progress-break guard,
choice-pause parking on the link-max trim), the divergence is explicitly called out as a substrate/park
necessity rather than silently introduced.

One AS-IS asymmetry correctly preserved rather than "fixed": `CardFaceDownProcess`/`TO-BE CardFaceDownProcess`
has no `Count >= 1` guard before its `foreach` (unlike the other three trash stages) — matches AS-IS
`AutoProcessing.cs:541-564` exactly (TO-BE comment explicitly flags "note the original has NO Count>=1 guard
here").

**One documentation-only inconsistency** (not a bug): the file's own top header (lines 45-51) and a later
inline banner (lines 787-796, 802-811) describe the trigger-stack half as having "landed elsewhere" / being a
"P6 stage A" re-addition — read literally this implies `PutStackedSkill`/`GetSkillInfos`/`StackSkillInfos`/etc.
might not be in this file, but they are, in full, later in the same file. The commentary is a layered history
of the port (SCOPE note superseded by later landings) rather than a description of a gap; no functional
absence was found.

## Summary

3/3 files: high-fidelity AS-IS↔TO-BE matches. No missing symbols, no signature drift, no silently-dropped
logic found in any of the three files. Two purely cosmetic doc-staleness items noted (Player.cs top summary,
AutoProcessing.cs header banners — both describe superseded intermediate states, not current gaps). One
functionally-inert lexical substitution noted in CardEffectFactory.cs (`playedDigimon.TopCard` vs `card` as the
second arg to `IsPermanentExistsOnOwnerBattleArea`, proven equivalent by tracing the owner-only comparison).
One architecture observation (not a finding) on `SpecialPlayRecipeRegistry`-backed new factory methods in
CardEffectFactory.cs, flagged for whoever owns the registry-teardown effort to weigh in on, since it falls
outside this file's own AS-IS anchor.
