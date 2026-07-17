namespace HeadlessDCGO.Engine.Headless.Runtime;

public static class HeadlessActionParameterKeys
{
    public const string ActionId = "actionId";
    public const string PlayerId = "playerId";
    public const string ActionType = "actionType";
    public const string CheatType = "cheatType";
    public const string CardId = "cardId";
    public const string SkillIndex = "skillIndex";
    public const string DrawCount = "drawCount";
    public const string DrawnCardIds = "drawnCardIds";
    public const string SecurityCount = "securityCount";
    public const string AddedSecurityCardIds = "addedSecurityCardIds";
    public const string TrashCount = "trashCount";
    public const string FromTop = "fromTop";
    public const string TrashedCardIds = "trashedCardIds";
    public const string HatchedCardId = "hatchedCardId";
    public const string BreedingMoveCount = "breedingMoveCount";
    public const string MovedBreedingCardIds = "movedBreedingCardIds";
    public const string AttackerId = "attackerId";
    public const string DefendingPlayerId = "defendingPlayerId";
    public const string AttackTargetId = "attackTargetId";
    public const string BlockerId = "blockerId";
    public const string AttackBlocked = "attackBlocked";
    public const string TargetCardId = "targetCardId";
    public const string IsDirectAttack = "isDirectAttack";
    public const string AttackCount = "attackCount";
    public const string AttackPending = "attackPending";
    public const string AttackResolved = "attackResolved";
    public const string ChoiceRequestId = "choiceRequestId";
    public const string ChoiceType = "choiceType";
    public const string ChoiceMessage = "choiceMessage";
    public const string ChoiceMinCount = "choiceMinCount";
    public const string ChoiceMaxCount = "choiceMaxCount";
    public const string ChoiceCanSkip = "choiceCanSkip";
    public const string ChoiceSourceZone = "choiceSourceZone";
    public const string ChoiceCandidateIds = "choiceCandidateIds";
    // (B5-2, 설계 §B5.5) The single candidate a ToggleChoiceCandidate action taps (AS-IS OnClick* card).
    public const string ChoiceCandidateId = "choiceCandidateId";
    // (B5-2) The pending partial selection after a toggle, surfaced in the toggle's result metadata.
    public const string ChoicePendingSelectedIds = "choicePendingSelectedIds";
    public const string ChoiceSelectedIds = "choiceSelectedIds";
    public const string ChoiceSelectedCount = "choiceSelectedCount";
    public const string ChoiceSkipped = "choiceSkipped";
    public const string ChoicePending = "choicePending";
    public const string ChoiceResolved = "choiceResolved";
    // (B5-1, 설계 §B5.6) Marks a no-select demotion of an unsatisfiable FORCED batch choice (Hand/Card
    // surfaces: AS-IS bounded search fails -> SetTargetHandCards(null) -> _noSelect, SelectHandEffect.cs
    // :504-570/:595-608). Defined here so the demotion is a counted fuzzing-harvest channel (D6) from its
    // first wiring; no producer until the dispatcher demotion lands in B5-2.
    public const string UnsatisfiableForcedChoice = "unsatisfiableForcedChoice";
    public const string FromZone = "fromZone";
    public const string ToZone = "toZone";
    public const string FaceUp = "faceUp";
    public const string EffectId = "effectId";
    public const string Timing = "timing";
    public const string SourceEntityId = "sourceEntityId";
    public const string IsTerminal = "isTerminal";
    public const string WinnerPlayerId = "winnerPlayerId";
    public const string IsDraw = "isDraw";
    public const string IsSurrender = "isSurrender";
    public const string Reason = "reason";
    public const string TurnNumber = "turnNumber";
    public const string Phase = "phase";
    // (R4 S2) the substrate step-position sub-cursor accompanying Phase — see TurnStepCursor. Preserves the
    // former 9-value phase distinction (Setup/Unsuspend/MemoryPass) that folded into (phase, cursor) pairs.
    public const string StepCursor = "stepCursor";
    public const string PreviousStepCursor = "previousStepCursor";
    public const string PreviousPhase = "previousPhase";
    public const string TurnPlayerId = "turnPlayerId";
    public const string NonTurnPlayerId = "nonTurnPlayerId";
    public const string IsFirstTurn = "isFirstTurn";
    public const string PhaseOperations = "phaseOperations";
    public const string DrawSkipped = "drawSkipped";
    public const string DeckOut = "deckOut";
    public const string UnsuspendedCardIds = "unsuspendedCardIds";
    public const string BreedingAction = "breedingAction";
    public const string Memory = "memory";
    public const string MemoryAmount = "memoryAmount";
    public const string MemoryCost = "memoryCost";
    public const string MemoryMinimum = "memoryMinimum";
    public const string MemoryMaximum = "memoryMaximum";
    public const string PreviousMemory = "previousMemory";
    public const string MainPhaseEntered = "mainPhaseEntered";
    public const string MemoryPassTriggered = "memoryPassTriggered";
    public const string MemoryPassCompleted = "memoryPassCompleted";
    public const string MemoryPassReason = "memoryPassReason";
    public const string MemoryPassThreshold = "memoryPassThreshold";
    public const string PassedMemory = "passedMemory";
    public const string EndTurnCleanupApplied = "endTurnCleanupApplied";
    public const string EndTurnCleanupReason = "endTurnCleanupReason";
    public const string EndTurnCleanupCardIds = "endTurnCleanupCardIds";
    public const string EndTurnCleanupRemovedKeys = "endTurnCleanupRemovedKeys";
    public const string EndTurnCleanupRemovedKeyCount = "endTurnCleanupRemovedKeyCount";
    public const string EndTurnCleanupResetAttackCount = "endTurnCleanupResetAttackCount";
}
