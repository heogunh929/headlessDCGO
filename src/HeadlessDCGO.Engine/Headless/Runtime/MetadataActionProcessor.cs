namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// (4b B6, S3c-d retirement complete) RETAINED SUBSTRATE: the system/choice/zone action arms the pump's
// TurnFlowDriver delegates to (plus the unguarded scripting profile's direct applies). The OLD step-cadence
// turn driver that used to live here (AdvancePhase/EndTurn bodies + HeadlessEarlyPhaseFlow/
// HeadlessMainPhaseFlow) is physically deleted — those arms now answer Illegal, phases auto-flow in the
// TurnFlowPump, and the turn ends via the mirror PassTurn/EndTurnCheck.
public sealed class MetadataActionProcessor : IActionProcessor
{
    /// <summary>(4b B6, §3.4i (b)) AS-IS voluntary-pass gauge jump — the opponent starts with exactly 3
    /// (AutoProcessing.cs `gameContext.Memory = 3` / `= -3`). Rehomed from the retired
    /// <c>HeadlessMainPhaseFlow</c>; the live pass seat is the mirror <c>PassTurn → EndTurnProcess</c>.</summary>
    public const int DefaultMemoryPassValue = 3;

    public async Task<ActionProcessResult> ProcessAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return HeadlessActionTypes.Normalize(action.ActionType) switch
        {
            HeadlessActionTypes.NormalizedNoOp => NoOp(action),
            // (4b B6) The OLD Runtime PassAction processor is retired — the canonical pass is the pump's
            // TurnFlowDriver -> mirror PassAction -> TurnStateMachine.PassTurn -> EndTurnProcess (GR-001
            // re-aim). A Pass reaching THIS processor is a non-pump (scripting) match: illegal.
            HeadlessActionTypes.NormalizedPass => ActionProcessResult.Illegal(
                action, "Pass is pump-only: it routes through TurnFlowDriver to the mirror PassTurn.", BaseMetadata(action)),
            HeadlessActionTypes.NormalizedCheat => CheatActionGuard.Reject(action),
            HeadlessActionTypes.NormalizedPlayCard => await new PlayCardAction()
                .ProcessAsync(action, context, cancellationToken)
                .ConfigureAwait(false),
            HeadlessActionTypes.NormalizedDigivolve => await new DigivolveAction()
                .ProcessAsync(action, context, cancellationToken)
                .ConfigureAwait(false),
            HeadlessActionTypes.NormalizedSpecialPlay => await new SpecialPlayAction()
                .ProcessAsync(action, context, cancellationToken)
                .ConfigureAwait(false),
            HeadlessActionTypes.NormalizedActivateOption => await new OptionActivateAction()
                .ProcessAsync(action, context, cancellationToken)
                .ConfigureAwait(false),
            HeadlessActionTypes.NormalizedActivateMain => await new MainSkillActivateAction()
                .ProcessAsync(action, context, cancellationToken)
                .ConfigureAwait(false),
            HeadlessActionTypes.NormalizedSetTerminal => SetTerminal(action, context, isTerminal: true),
            HeadlessActionTypes.NormalizedClearTerminal => SetTerminal(action, context, isTerminal: false),
            HeadlessActionTypes.NormalizedMoveCard => await MoveCardAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedAddToHand => await AddToHandAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedAddToTrash => await AddToTrashAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedAddToSecurity => await AddToSecurityAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedMoveToDeckTop => await MoveToDeckTopAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedMoveToDeckBottom => await MoveToDeckBottomAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedDrawCards => await DrawCardsAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedAddSecurityFromLibrary => await AddSecurityFromLibraryAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedTrashSecurity => await TrashSecurityAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedHatchDigitama => await HatchDigitamaAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedMoveBreedingToBattle => await MoveBreedingToBattleAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedDeclareAttack => new AttackPermanentAction().Process(action, context),
            HeadlessActionTypes.NormalizedResolveAttack => ResolveAttack(action, context),
            HeadlessActionTypes.NormalizedClearAttack => ClearAttack(action, context),
            HeadlessActionTypes.NormalizedRequestChoice => RequestChoice(action, context),
            HeadlessActionTypes.NormalizedResolveChoice => await ResolveChoiceAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedToggleChoiceCandidate => ToggleChoiceCandidate(action, context),
            HeadlessActionTypes.NormalizedClearChoice => ClearChoice(action, context),
            HeadlessActionTypes.NormalizedShuffleDeck => await ShuffleDeckAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedEnqueueEffect => EnqueueEffect(action, context),
            // (4b B6) The OLD step cadence is physically retired: AdvancePhase/EndTurn were the invented
            // step-driver currency (phases auto-flow in the TurnFlowPump; the turn ends via Pass/EndTurnCheck).
            HeadlessActionTypes.NormalizedAdvancePhase => ActionProcessResult.Illegal(
                action, "AdvancePhase is retired: phases auto-flow (TurnFlowPump).", BaseMetadata(action)),
            HeadlessActionTypes.NormalizedEndTurn => ActionProcessResult.Illegal(
                action, "EndTurn is retired: the turn ends via Pass or the EndTurnCheck auto-flow.", BaseMetadata(action)),
            HeadlessActionTypes.NormalizedSetMemory => SetMemory(action, context),
            HeadlessActionTypes.NormalizedAddMemory => AddMemory(action, context),
            HeadlessActionTypes.NormalizedPayMemory => PayMemory(action, context),
            _ => ActionProcessResult.Illegal(
                action,
                $"Unsupported headless action type: {action.ActionType}",
                BaseMetadata(action))
        };
    }

    private static ActionProcessResult NoOp(LegalAction action)
    {
        return ActionProcessResult.Success(
            $"Processed {action.ActionType}.",
            BaseMetadata(action));
    }

    private static ActionProcessResult SetTerminal(
        LegalAction action,
        EngineContext context,
        bool isTerminal)
    {
        if (!TerminalActionPayload.TryRead(action, isTerminal, out TerminalActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error, BaseMetadata(action));
        }

        if (context.RuleQueryService is not ITerminalStateController terminalStateController)
        {
            return ActionProcessResult.Failure(
                "Rule query service does not support terminal state mutation.",
                BaseMetadata(action));
        }

        terminalStateController.SetTerminal(payload.IsTerminal);
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.IsTerminal] = payload.IsTerminal;
        metadata[HeadlessActionParameterKeys.WinnerPlayerId] = payload.WinnerPlayerId?.Value;
        metadata[HeadlessActionParameterKeys.IsDraw] = payload.IsDraw;
        metadata[HeadlessActionParameterKeys.IsSurrender] = payload.IsSurrender;
        metadata[HeadlessActionParameterKeys.Reason] = payload.Reason;

        return ActionProcessResult.Success(
            payload.IsTerminal ? "Terminal state set." : "Terminal state cleared.",
            metadata);
    }

    private static async Task<ActionProcessResult> MoveCardAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (!MoveCardActionPayload.TryRead(action, out MoveCardActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error, BaseMetadata(action));
        }

        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(action.PlayerId, payload.CardId, payload.FromZone, payload.ToZone, payload.FaceUp),
            cancellationToken).ConfigureAwait(false);

        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.CardId] = payload.CardId.Value;
        metadata[HeadlessActionParameterKeys.FromZone] = payload.FromZone.ToString();
        metadata[HeadlessActionParameterKeys.ToZone] = payload.ToZone.ToString();
        metadata[HeadlessActionParameterKeys.FaceUp] = payload.FaceUp;

        return ActionProcessResult.Success("Card moved.", metadata);
    }

    private static async Task<ActionProcessResult> AddToHandAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (!CardActionPayload.TryRead(action, out CardActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error, BaseMetadata(action));
        }

        await context.ZoneMover.AddToHandAsync(action.PlayerId, payload.CardId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ActionProcessResult.Success("Card added to hand.", MetadataWithCard(action, payload.CardId));
    }

    private static async Task<ActionProcessResult> AddToTrashAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (!CardActionPayload.TryRead(action, out CardActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error, BaseMetadata(action));
        }

        await context.ZoneMover.AddToTrashAsync(action.PlayerId, payload.CardId, cancellationToken).ConfigureAwait(false);
        return ActionProcessResult.Success("Card added to trash.", MetadataWithCard(action, payload.CardId));
    }

    private static async Task<ActionProcessResult> AddToSecurityAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (!SecurityActionPayload.TryRead(action, out SecurityActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error, BaseMetadata(action));
        }

        await context.ZoneMover.AddToSecurityAsync(
            action.PlayerId,
            payload.CardId,
            payload.FaceUp,
            addSecurityBatchId: context.NextSecurityAddBatchId(),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        Dictionary<string, object?> metadata = MetadataWithCard(action, payload.CardId);
        metadata[HeadlessActionParameterKeys.FaceUp] = payload.FaceUp;
        return ActionProcessResult.Success("Card added to security.", metadata);
    }

    private static async Task<ActionProcessResult> MoveToDeckTopAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (!CardActionPayload.TryRead(action, out CardActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error, BaseMetadata(action));
        }

        await context.ZoneMover.MoveToDeckTopAsync(action.PlayerId, payload.CardId, cancellationToken).ConfigureAwait(false);
        return ActionProcessResult.Success("Card moved to deck top.", MetadataWithCard(action, payload.CardId));
    }

    private static async Task<ActionProcessResult> MoveToDeckBottomAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (!CardActionPayload.TryRead(action, out CardActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error, BaseMetadata(action));
        }

        await context.ZoneMover.MoveToDeckBottomAsync(action.PlayerId, payload.CardId, cancellationToken).ConfigureAwait(false);
        return ActionProcessResult.Success("Card moved to deck bottom.", MetadataWithCard(action, payload.CardId));
    }

    private static async Task<ActionProcessResult> DrawCardsAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.DrawCount, out int count) || count < 0)
        {
            return ActionProcessResult.Failure(
                "DrawCards action is missing a valid non-negative draw count.",
                BaseMetadata(action));
        }

        IReadOnlyList<HeadlessEntityId> drawnCards = await context.ZoneMover
            .DrawAsync(action.PlayerId, count, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.DrawCount] = count;
        metadata[HeadlessActionParameterKeys.DrawnCardIds] = drawnCards
            .Select(cardId => cardId.Value)
            .ToArray();

        return ActionProcessResult.Success(
            $"Drew {drawnCards.Count} card(s).",
            metadata);
    }

    private static async Task<ActionProcessResult> AddSecurityFromLibraryAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.SecurityCount, out int count) || count < 0)
        {
            return ActionProcessResult.Failure(
                "AddSecurityFromLibrary action is missing a valid non-negative security count.",
                BaseMetadata(action));
        }

        bool faceUp = ReadBoolOrDefault(action.Parameters, HeadlessActionParameterKeys.FaceUp, defaultValue: false);
        IReadOnlyList<HeadlessEntityId> addedCards = await context.ZoneMover
            .AddSecurityFromLibraryAsync(action.PlayerId, count, faceUp, () => context.NextSecurityAddBatchId(), cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.SecurityCount] = count;
        metadata[HeadlessActionParameterKeys.FaceUp] = faceUp;
        metadata[HeadlessActionParameterKeys.AddedSecurityCardIds] = addedCards
            .Select(cardId => cardId.Value)
            .ToArray();

        return ActionProcessResult.Success(
            $"Added {addedCards.Count} card(s) to security.",
            metadata);
    }

    private static async Task<ActionProcessResult> TrashSecurityAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.TrashCount, out int count) || count < 0)
        {
            return ActionProcessResult.Failure(
                "TrashSecurity action is missing a valid non-negative trash count.",
                BaseMetadata(action));
        }

        bool fromTop = ReadBoolOrDefault(action.Parameters, HeadlessActionParameterKeys.FromTop, defaultValue: true);
        // (F1-M1 P1-1) one action == one IReduceSecurity == one OnLoseSecurity batch id shared across N cards.
        // (F1-Tier1) no card-effect cause is threaded on this normalized action path, so the derived
        // OnDiscardSecurity CardMoved carries no cause id and does NOT satisfy the OnDiscardSecurity CardEffect!=null
        // gate — OnLoseSecurity (player-scope, no cause gate) is unaffected.
        IReadOnlyList<HeadlessEntityId> trashedCards = await context.ZoneMover
            .TrashSecurityAsync(action.PlayerId, count, fromTop, context.NextSecurityLossBatchId(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.TrashCount] = count;
        metadata[HeadlessActionParameterKeys.FromTop] = fromTop;
        metadata[HeadlessActionParameterKeys.TrashedCardIds] = trashedCards
            .Select(cardId => cardId.Value)
            .ToArray();

        return ActionProcessResult.Success(
            $"Trashed {trashedCards.Count} security card(s).",
            metadata);
    }

    private static async Task<ActionProcessResult> HatchDigitamaAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        HeadlessEntityId? hatchedCardId = await context.ZoneMover
            .HatchDigitamaAsync(action.PlayerId, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.HatchedCardId] = hatchedCardId?.Value;

        return ActionProcessResult.Success(
            hatchedCardId.HasValue
                ? $"Hatched digitama {hatchedCardId.Value.Value}."
                : "No digitama card was available to hatch.",
            metadata);
    }

    private static async Task<ActionProcessResult> MoveBreedingToBattleAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.BreedingMoveCount, out int count) || count < 0)
        {
            return ActionProcessResult.Failure(
                "MoveBreedingToBattle action is missing a valid non-negative move count.",
                BaseMetadata(action));
        }

        IReadOnlyList<HeadlessEntityId> movedCards = await context.ZoneMover
            .MoveBreedingToBattleAsync(action.PlayerId, count, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.BreedingMoveCount] = count;
        metadata[HeadlessActionParameterKeys.FromZone] = "BreedingArea";
        metadata[HeadlessActionParameterKeys.ToZone] = "BattleArea";
        metadata[HeadlessActionParameterKeys.MovedBreedingCardIds] = movedCards
            .Select(cardId => cardId.Value)
            .ToArray();

        return ActionProcessResult.Success(
            $"Moved {movedCards.Count} breeding card(s) to battle.",
            metadata);
    }

    private static ActionProcessResult ResolveAttack(
        LegalAction action,
        EngineContext context)
    {
        string reason = ReadStringOrDefault(
            action.Parameters,
            HeadlessActionParameterKeys.Reason,
            string.Empty);
        HeadlessAttackState attack = context.AttackController.ResolveAttack(reason);
        Dictionary<string, object?> metadata = MetadataWithAttack(action, attack);
        metadata[HeadlessActionParameterKeys.Reason] = attack.Reason;
        return ActionProcessResult.Success("Attack resolved.", metadata);
    }

    private static ActionProcessResult ClearAttack(
        LegalAction action,
        EngineContext context)
    {
        HeadlessAttackState attack = context.AttackController.ClearAttack();
        return ActionProcessResult.Success("Attack cleared.", MetadataWithAttack(action, attack));
    }

    private static ActionProcessResult RequestChoice(
        LegalAction action,
        EngineContext context)
    {
        if (context.ChoiceController.Current.IsPending)
        {
            return ActionProcessResult.Failure(
                "RequestChoice action was received while another choice is pending.",
                MetadataWithChoice(action, context.ChoiceController.Current));
        }

        if (!TryReadChoiceType(action.Parameters, HeadlessActionParameterKeys.ChoiceType, out ChoiceType choiceType))
        {
            return ActionProcessResult.Failure(
                "RequestChoice action is missing a valid choice type.",
                BaseMetadata(action));
        }

        if (!TryReadChoiceZone(action.Parameters, HeadlessActionParameterKeys.ChoiceSourceZone, out ChoiceZone sourceZone))
        {
            return ActionProcessResult.Failure(
                "RequestChoice action is missing a valid source zone.",
                BaseMetadata(action));
        }

        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.ChoiceMinCount, out int minCount) || minCount < 0)
        {
            return ActionProcessResult.Failure(
                "RequestChoice action is missing a valid non-negative min count.",
                BaseMetadata(action));
        }

        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.ChoiceMaxCount, out int maxCount) || maxCount < minCount)
        {
            return ActionProcessResult.Failure(
                "RequestChoice action is missing a valid max count.",
                BaseMetadata(action));
        }

        string message = ReadStringOrDefault(
            action.Parameters,
            HeadlessActionParameterKeys.ChoiceMessage,
            string.Empty);
        bool canSkip = ReadBoolOrDefault(
            action.Parameters,
            HeadlessActionParameterKeys.ChoiceCanSkip,
            defaultValue: false);
        IReadOnlyList<HeadlessEntityId> candidateIds = ReadEntityIds(
            action.Parameters,
            HeadlessActionParameterKeys.ChoiceCandidateIds);

        ChoiceRequest request = new(
            choiceType,
            action.PlayerId,
            message,
            minCount,
            maxCount,
            canSkip,
            sourceZone,
            candidateIds
                .Select(candidateId => new ChoiceCandidate(candidateId, candidateId.Value, sourceZone, IsSelectable: true))
                .ToArray());

        HeadlessChoiceState choice = context.ChoiceController.RequestChoice(request, action.Id);
        return ActionProcessResult.Success("Choice requested.", MetadataWithChoice(action, choice));
    }

    private static async Task<ActionProcessResult> ResolveChoiceAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (context.ChoiceController.PendingRequest is null)
        {
            return ActionProcessResult.Failure(
                "ResolveChoice action was received without a pending choice.",
                BaseMetadata(action));
        }

        try
        {
            ChoiceRequest pendingRequest = context.ChoiceController.PendingRequest!;

            // G3.5-RL-A2: when the action carries the agent's selection, apply it directly so the
            // policy decides the outcome. Fall back to the choice provider only for legacy /
            // effect-driven resolution that does not carry a selection.
            ChoiceResult result = TryReadCarriedChoiceResult(action.Parameters, out ChoiceResult? carried) && carried is not null
                ? carried
                : await context.ChoiceProvider
                    .ChooseAsync(pendingRequest, cancellationToken)
                    .ConfigureAwait(false);

            // (R4 S3a, decision 3 = B) A choice opened ON the TurnFlowPump stack (await-mode: the provider/port
            // parked the pump in place) resolves by DEPOSIT: hand the parsed answer to the pump host and clear
            // the controller. NO record-replay resume runs — the window/effect body is still live on the parked
            // pump stack and consumes the answer in place when the task-runner's next step releases the gate.
            // Routing through the legacy branches below would double-drive a body that never unwound.
            if (TurnFlowPumpHost.Find(context) is { HasPendingPumpChoice: true } pumpHost)
            {
                HeadlessChoiceState pumpChoice = context.ChoiceController.ResolveChoice(result);
                context.ChoiceController.ClearChoice();
                pumpHost.DepositAnswer(result);
                Dictionary<string, object?> pumpMetadata = MetadataWithChoice(action, pumpChoice);
                pumpMetadata["pumpChoiceResolved"] = true;
                return ActionProcessResult.Success("Pump-parked choice resolved.", pumpMetadata);
            }

            // Block-timing choices must flow through BlockTiming so the blocker selection is applied
            // to the attack state (SelectBlocker); a plain ResolveChoice would clear the choice
            // without ever updating the pending attack (G3.5-005).
            if (pendingRequest.Type == ChoiceType.Blocker)
            {
                BlockTimingResult block = new BlockTiming().ResolveBlockChoice(context, result);
                if (!block.IsSuccess)
                {
                    Dictionary<string, object?> blockFailure = MetadataWithChoice(action, context.ChoiceController.Current);
                    blockFailure["error"] = block.FailureReason;
                    return ActionProcessResult.Failure("Block choice resolve failed.", blockFailure);
                }

                Dictionary<string, object?> blockMetadata = MetadataWithChoice(action, block.Choice);
                blockMetadata[HeadlessActionParameterKeys.BlockerId] = block.BlockerId?.Value;
                return ActionProcessResult.Success("Block choice resolved.", blockMetadata);
            }

            // #2: optional-trigger prompts must flow through the OptionalPromptQueue so the chosen
            // optional effect is enqueued (or skipped); a plain ResolveChoice would clear the choice
            // without activating the agent's selected optional trigger.
            // (C-Del 3c-1) an OptionalEffect choice raised INSIDE a resolving trigger window — a "will you use
            // Evade?" cut-in optional (OptionalSkill.SelectOptional → ChooseAsync), including the AS-IS "would be
            // deleted" PRE cut-in the deletion sink drains — is NOT an OptionalPromptQueue trigger prompt. Only
            // route through the queue when it actually has a pending prompt; otherwise fall through to the window
            // body-resume path below (record the answer + resume the suspended window, whose deferred provider
            // replays it), so a retired-keyword replacement is resolvable by the real agent, not only the
            // trigger-queue optionals.
            if (pendingRequest.Type == ChoiceType.OptionalEffect && context.OptionalPromptQueue.HasPendingPrompt)
            {
                Effects.OptionalPromptQueueResult optional = context.OptionalPromptQueue
                    .ResolveChoice(result, context.ChoiceController, context.EffectScheduler);
                if (!optional.IsSuccess)
                {
                    Dictionary<string, object?> optionalFailure = MetadataWithChoice(action, context.ChoiceController.Current);
                    optionalFailure["error"] = optional.FailureReason;
                    return ActionProcessResult.Failure("Optional effect choice resolve failed.", optionalFailure);
                }

                return ActionProcessResult.Success("Optional effect choice resolved.", MetadataWithChoice(action, optional.ChoiceState));
            }

            // (Stage 5, Phase 3) a trigger-window order / optional decision resumes the SUSPENDED window: record the
            // agent's answer keyed by the choice identity, resolve the pending choice, then re-drive the window's
            // continuation — the AgentWindowChoicePort replays the recorded answer at that same choice point, so the
            // loop advances past it. A further window choice re-suspends (a new choice is pending; RunToStable
            // re-pauses on the next iteration); running the stack to exhaustion clears the parked window.
            if (pendingRequest.Type == ChoiceType.WindowChoice)
            {
                // (C2 seam 2) A trigger-window ORDER / optional pick resumes the SUSPENDED mirror window. Decode the
                // agent's answer into the SkillInfo-currency key (RequestId == SkillWindowChoiceKey.Offered; each
                // selected candidate id is "cardInstanceId#ordinal"; empty selection = decline), record it on the
                // DEEPEST in-flight MultipleSkills continuation (the one whose port opened this choice), clear the
                // pending choice, then resume the suspended chain deepest-first — the port replays the recorded answer
                // at that choice point, so the loop advances. A further order/body choice re-suspends (a new pending
                // choice; RunToStable re-pauses). Runs under an ambient scope (ResumeSuspendedWindowsAsync reads
                // GManager.instance through the mirror window loop).
                using AmbientMatchContext.Scope _windowScope = AmbientMatchContext.Enter(context);
                // (C-Del 3c-1) the parked trigger window lives in EITHER pool: the MAIN pool (AutoProcessing.For)
                // for ordinary trigger windows, or the CUT-IN pool (AutoProcessing.ForCutIn) for the AS-IS "would
                // be deleted" PRE cut-in the deletion sink drains (an INTERACTIVE Evade/Barrier/… replacement
                // suspends there). ForCutIn is a SEPARATE AutoProcessing with its own MultipleSkills pool, so its
                // suspended window is invisible to For.executingMultipleSkills — resolve/resume whichever pool holds
                // the in-flight pick (only one choice is pending at a time, in one pool).
                Assets.Scripts.Script.AutoProcessing windowAutoProcessing = Assets.Scripts.Script.AutoProcessing.For(context);
                Assets.Scripts.Script.AutoProcessing cutInAutoProcessing = Assets.Scripts.Script.AutoProcessing.ForCutIn(context);
                Assets.Scripts.Script.AutoProcessing resumeTarget =
                    windowAutoProcessing.executingMultipleSkills is not null ? windowAutoProcessing : cutInAutoProcessing;
                string windowKey = context.ChoiceController.Current.RequestId?.Value ?? string.Empty;
                if (resumeTarget.executingMultipleSkills is { } deepestWindow)
                {
                    deepestWindow.Continuation.RecordAnswer(
                        new Effects.SkillWindowChoiceKey(windowKey), DecodeWindowAnswer(result));
                }

                HeadlessChoiceState windowChoice = context.ChoiceController.ResolveChoice(result);
                try
                {
                    await resumeTarget.ResumeSuspendedWindowsAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception windowEx) when (windowEx is Effects.WindowChoicePendingException or DeferredChoicePendingException)
                {
                    // The resumed window suspended again (a further order/body choice) — normal pause; the new
                    // choice is pending and a later ResolveChoice re-enters this same seam.
                }

                Dictionary<string, object?> windowMetadata = MetadataWithChoice(action, windowChoice);
                windowMetadata["windowResolved"] =
                    windowAutoProcessing.executingMultipleSkills is null && cutInAutoProcessing.executingMultipleSkills is null;
                return ActionProcessResult.Success("Window choice resolved.", windowMetadata);
            }

            // (MIG2) the rule-process link-max trim selection (AS-IS Permanent.RemoveLinkedCard(null, count)
            // -> SelectCardEffect mode Discard over LinkedCards) — each selected link card routes through
            // ITrashLinkCards, the AS-IS Mode.Discard linked-card branch (SelectCardEffect.cs:715-724); a
            // plain ResolveChoice would clear the choice without trashing anything.
            if (context.ChoiceController.Current.RequestId?.Value is { } linkTrimRequestId &&
                linkTrimRequestId.StartsWith(Assets.Scripts.Script.AutoProcessing.LinkTrimRequestIdPrefix, StringComparison.Ordinal))
            {
                var linkHostId = new HeadlessEntityId(
                    linkTrimRequestId[Assets.Scripts.Script.AutoProcessing.LinkTrimRequestIdPrefix.Length..]);
                HeadlessChoiceState linkTrimChoice = context.ChoiceController.ResolveChoice(result);

                // (MIG2) ITrashLinkCards.TrashLinkCards reads GManager.instance.autoProcessing (the OnLinkCardDiscarded
                // StackSkillInfos), and the resume below likewise runs ported effect code — both need the ambient match
                // scope. In production ProcessAsync is reached inside the pump's AmbientMatchContext scope; when
                // ResolveChoice is driven directly (a unit re-drive) no ambient is set, so self-scope the whole
                // trim-resolution (trash side-effect + resume). Nested Enter is a save/restore no-op in production.
                using (AmbientMatchContext.Scope _linkTrimScope = AmbientMatchContext.Enter(context))
                {
                    if (context.CardInstanceRepository.TryGetInstance(linkHostId, out CardInstanceRecord? linkHost) && linkHost is not null)
                    {
                        var hostPermanent = new Assets.Scripts.Script.CardEffectCommons.Permanent(context, linkHostId, linkHost.OwnerId);
                        foreach (HeadlessEntityId selectedId in result.SelectedIds)
                        {
                            HeadlessPlayerId linkOwner = context.CardInstanceRepository.TryGetInstance(selectedId, out CardInstanceRecord? link) && link is not null
                                ? link.OwnerId
                                : linkHost.OwnerId;
                            await new Assets.Scripts.Script.ITrashLinkCards(
                                hostPermanent,
                                new List<Assets.Scripts.Script.CardEffectCommons.CardSource>
                                {
                                    new(context, selectedId, linkOwner),
                                },
                                causeEffectSourceId: null).TrashLinkCards(cancellationToken).ConfigureAwait(false);
                        }
                    }

                    // (C2 seam 3) the trim is a between-picks (F3) rule pass that may have parked mid-window: apply the
                    // side-effect above (ITrashLinkCards) first, then resume the suspended mirror window deepest-first —
                    // the same resume the WindowChoice path uses (there is no order answer to record; the pass head
                    // re-runs and re-evaluates the now-trimmed board).
                    try
                    {
                        await Assets.Scripts.Script.AutoProcessing.For(context)
                            .ResumeSuspendedWindowsAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception linkTrimEx) when (linkTrimEx is Effects.WindowChoicePendingException or DeferredChoicePendingException)
                    {
                        // Re-suspended — normal pause (see the WindowChoice seam).
                    }
                }

                return ActionProcessResult.Success("Link-trim choice resolved.", MetadataWithChoice(action, linkTrimChoice));
            }

            // (C-Atk RETIRED) the ChoiceType.AttackTarget (Raid) and ChoiceType.AllianceTarget (Alliance)
            // dispatch branches are gone: the RaidAttackSwitch / AllianceAttackBoost gates no longer OPEN those
            // choices (their counter-head RequestChoice firing-half was de-wired), so these choice types are now
            // unreachable. Raid/Alliance resolve inside the OnAllyAttack window (RaidProcess / AllianceProcess use
            // the ChoiceProvider, not the ChoiceController). These were the ONLY openers of either choice type.

            // S1 (C-20 Vortex / C-16 Overclock): the optional effect-driven attack target choice flows through
            // EffectDrivenAttack so the attack is declared on the chosen target; a plain ResolveChoice would not.
            if (pendingRequest.Type == ChoiceType.EffectAttack)
            {
                if (!EffectDrivenAttack.ResolveChoice(context, result))
                {
                    Dictionary<string, object?> attackFailure = MetadataWithChoice(action, context.ChoiceController.Current);
                    attackFailure["error"] = "Effect-driven attack resolve failed.";
                    return ActionProcessResult.Failure("Effect-driven attack resolve failed.", attackFailure);
                }

                return ActionProcessResult.Success("Effect-driven attack resolved.", MetadataWithChoice(action, context.ChoiceController.Current));
            }

            // C-16 Overclock: the optional delete-a-trait-ally choice flows through OverclockEffect so the
            // ally is deleted and the untapped player attack is offered; a plain ResolveChoice would not.
            if (pendingRequest.Type == ChoiceType.OverclockTarget)
            {
                if (!await OverclockEffect.ResolveChoice(context, result).ConfigureAwait(false))
                {
                    Dictionary<string, object?> overclockFailure = MetadataWithChoice(action, context.ChoiceController.Current);
                    overclockFailure["error"] = "Overclock resolve failed.";
                    return ActionProcessResult.Failure("Overclock resolve failed.", overclockFailure);
                }

                return ActionProcessResult.Success("Overclock resolved.", MetadataWithChoice(action, context.ChoiceController.Current));
            }

            // B-7: the reveal-and-select choice flows through RevealAndSelect so the selected/remaining
            // revealed cards are routed to their destinations; a plain ResolveChoice would not move them.
            if (pendingRequest.Type == ChoiceType.RevealSelect)
            {
                if (!await RevealAndSelect.ResolveChoice(context, result).ConfigureAwait(false))
                {
                    Dictionary<string, object?> revealFailure = MetadataWithChoice(action, context.ChoiceController.Current);
                    revealFailure["error"] = "Reveal-and-select resolve failed.";
                    return ActionProcessResult.Failure("Reveal-and-select resolve failed.", revealFailure);
                }

                return ActionProcessResult.Success("Reveal-and-select resolved.", MetadataWithChoice(action, context.ChoiceController.Current));
            }

            // F-6.8: a would-be-deleted replacement decision flows through DeletionReplacementTiming so the
            // chosen replacement's cost+save is applied (clearing the deletion) or the card is marked
            // declined; a plain ResolveChoice would clear the choice without acting on the deferred deletion.
            if (pendingRequest.Type == ChoiceType.DeletionReplacement)
            {
                DeletionReplacementResolveResult replacement = await new DeletionReplacementTiming().ResolveChoice(context, result).ConfigureAwait(false);
                if (!replacement.IsSuccess)
                {
                    Dictionary<string, object?> replacementFailure = MetadataWithChoice(action, context.ChoiceController.Current);
                    replacementFailure["error"] = replacement.FailureReason;
                    return ActionProcessResult.Failure("Deletion-replacement resolve failed.", replacementFailure);
                }

                Dictionary<string, object?> replacementMetadata = MetadataWithChoice(action, context.ChoiceController.Current);
                replacementMetadata["deletionReplacementCard"] = replacement.CardId.Value;
                replacementMetadata["deletionReplacementOption"] = replacement.Option;
                replacementMetadata["deletionReplacementActivated"] = replacement.WasActivated;
                return ActionProcessResult.Success("Deletion-replacement resolved.", replacementMetadata);
            }

            // N-5: opening-hand mulligan decisions flow through the MulliganCoordinator so the redraw
            // (and, after the last decision, the deferred security deal) are applied; a plain
            // ResolveChoice would clear the choice without performing the mulligan / security steps.
            if (pendingRequest.Type == ChoiceType.Mulligan)
            {
                MulliganResolveResult mulligan = await context.MulliganCoordinator
                    .ResolveAsync(context.ZoneMover, context.ChoiceController, result, cancellationToken)
                    .ConfigureAwait(false);
                if (!mulligan.IsSuccess)
                {
                    Dictionary<string, object?> mulliganFailure = MetadataWithChoice(action, context.ChoiceController.Current);
                    mulliganFailure["error"] = mulligan.FailureReason;
                    return ActionProcessResult.Failure("Mulligan resolve failed.", mulliganFailure);
                }

                Dictionary<string, object?> mulliganMetadata = MetadataWithChoice(action, context.ChoiceController.Current);
                mulliganMetadata["mulliganPlayerId"] = mulligan.Player.Value;
                mulliganMetadata["mulliganRedrew"] = mulligan.Redrew;
                return ActionProcessResult.Success("Mulligan decision resolved.", mulliganMetadata);
            }

            HeadlessChoiceState choice = context.ChoiceController.ResolveChoice(result);

            // G11-002: if this resolved a suspended activation's pending choice, resume the activation —
            // re-resolve the effect (the DeferredChoiceProvider replays this answer) WITHOUT re-running the
            // originating action, so the cost is not paid again. A further choice re-suspends it.
            if (context.DeferredActivations.Pending is { } pendingActivation)
            {
                bool beforePayCost = pendingActivation.Timing == Assets.Scripts.Script.CardEffectCommons.EffectTiming.BeforePayCost;
                // (B.O.4 #1) a resumed BeforePayCost activation is a suspended PLAY (only PlayCardAction defers this
                // timing in v1) — restore the Play root so the card's root-gated [BeforePayCost] effect re-appears.
                if (beforePayCost)
                {
                    context.CurrentPayCostRoot = Headless.Bridge.PayCostRoot.Play;
                }

                try
                {
                    await Assets.Scripts.Script.CardEffectCommons.ActivatedEffectResolver.ResolveAsync(
                        context, pendingActivation.CardId, pendingActivation.PlayerId, pendingActivation.Timing, cancellationToken,
                        drivingEvent: pendingActivation.DrivingEvent,
                        declarative: pendingActivation.Declarative, windowDispatched: pendingActivation.WindowDispatched)
                        .ConfigureAwait(false);
                    // Clear BEFORE the brick-2b continuation: finishing the play may itself open a deferred
                    // [All Turns] reactivation, which re-suspends a FRESH activation we must not clobber.
                    context.DeferredActivations.Clear();
                }
                catch (DeferredChoicePendingException resumeEx)
                {
                    Dictionary<string, object?> resumePending = MetadataWithChoice(action, context.ChoiceController.Current);
                    resumePending["pendingChoice"] = true;
                    resumePending["pendingChoiceMessage"] = resumeEx.Message;
                    return ActionProcessResult.Success("Choice resolved; activation awaiting further choice.", resumePending);
                }
                finally
                {
                    if (beforePayCost)
                    {
                        context.CurrentPayCostRoot = Headless.Bridge.PayCostRoot.None;
                    }
                }

                // (brick 2b) A suspended PLAY committed nothing before its BeforePayCost choice — now that the
                // pre-payment reduction has resolved, FINISH the play (pay the reduced cost + move + register +
                // [All Turns] reactivation). Every other timing already committed its originating action before
                // suspending, so re-resolving the effect alone is enough for those.
                if (beforePayCost)
                {
                    return await PlayCardAction.CompleteDeferredPlayAsync(
                        context, pendingActivation.CardId, pendingActivation.PlayerId, cancellationToken)
                        .ConfigureAwait(false);
                }

                // (Stage 5, 3b-iii) if this activation was a WINDOW body-suspend (SuspendedExternally), the parked
                // trigger window now continues — the just-finished body was one pick; re-drive to resolve the
                // REMAINING stack (its own cut-ins are drained as the next pick resolves). A further suspend re-parks
                // the window (pausing the loop again); running to exhaustion clears it.
                // (C2 seam 4) if this activation was a WINDOW body-suspend, the parked mirror window continues — the
                // just-finished body was one pick; resume the suspended chain to resolve the REMAINING stack (its own
                // cut-ins drain as the next pick resolves). A further suspend re-parks the window.
                using (AmbientMatchContext.Scope _resumeScope = AmbientMatchContext.Enter(context))
                {
                    try
                    {
                        await Assets.Scripts.Script.AutoProcessing.For(context)
                            .ResumeSuspendedWindowsAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception resumeEx2) when (resumeEx2 is Effects.WindowChoicePendingException or DeferredChoicePendingException)
                    {
                        // Re-suspended — normal pause (see the WindowChoice seam).
                    }
                }

                return ActionProcessResult.Success("Choice resolved; activation resumed.", MetadataWithChoice(action, choice));
            }

            // (C2 seam 4b) A mirror-window BODY pick that opened a NON-window choice (SelectCard / count / a cut-in
            // "will you use Evade?" optional / …) suspends via the MultipleSkills continuation's in-flight pick, NOT
            // DeferredActivations. Its answer was just recorded by ResolveChoice above; resume the suspended window
            // so the in-flight body REPLAYS it (through the ResolveWithinCycle deferred-choice provider) and the
            // pass finishes. No-op when no window is parked.
            // (C-Del 3c-1) the parked window may live in the CUT-IN pool (AutoProcessing.ForCutIn) — the AS-IS
            // "would be deleted" PRE cut-in the deletion sink drains — which is a SEPARATE AutoProcessing whose
            // suspended window For.executingMultipleSkills cannot see. Resume whichever pool holds it.
            Assets.Scripts.Script.AutoProcessing bodyMain = Assets.Scripts.Script.AutoProcessing.For(context);
            Assets.Scripts.Script.AutoProcessing bodyCutIn = Assets.Scripts.Script.AutoProcessing.ForCutIn(context);
            Assets.Scripts.Script.AutoProcessing? bodyResumeTarget =
                bodyMain.executingMultipleSkills is not null ? bodyMain
                : bodyCutIn.executingMultipleSkills is not null ? bodyCutIn
                : null;
            if (bodyResumeTarget is not null)
            {
                using AmbientMatchContext.Scope _bodyResumeScope = AmbientMatchContext.Enter(context);
                try
                {
                    await bodyResumeTarget.ResumeSuspendedWindowsAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception bodyEx) when (bodyEx is Effects.WindowChoicePendingException or DeferredChoicePendingException)
                {
                    // Re-suspended — normal pause (see the WindowChoice seam).
                }

                return ActionProcessResult.Success("Choice resolved; window body resumed.", MetadataWithChoice(action, choice));
            }

            return ActionProcessResult.Success("Choice resolved.", MetadataWithChoice(action, choice));
        }
        catch (InvalidOperationException ex)
        {
            Dictionary<string, object?> metadata = MetadataWithChoice(action, context.ChoiceController.Current);
            metadata["error"] = ex.Message;
            return ActionProcessResult.Failure("Choice resolve failed.", metadata);
        }
    }

    /// <summary>(B5-2, 설계 §B5.5) One AS-IS selection tap: apply a ToggleChoiceCandidate action to the pending
    /// choice's partial-selection scratchpad (<see cref="IHeadlessChoiceController.ToggleCandidate"/> — AS-IS
    /// OnClickHandCard :269-322 Contains→Remove / Add / replace-last). Pure controller-state transition: no game
    /// state mutates, no effect resumes, and on a pump match the parked pump stays parked (only the Confirm lane's
    /// ResolveChoice deposits an answer) — so this same seat serves both the pump and legacy surfaces (the
    /// TurnFlowDriver's default arm delegates here). Per-pick gates (PartialPickGate) are NOT re-evaluated here:
    /// the dispatcher's toggle lanes filter them at enumeration time and the A1 legality boundary enforces table
    /// membership for the agent path, mirroring AS-IS registering click handlers only on selectable cards.</summary>
    private static ActionProcessResult ToggleChoiceCandidate(
        LegalAction action,
        EngineContext context)
    {
        if (!context.ChoiceController.Current.IsPending)
        {
            return ActionProcessResult.Failure(
                "ToggleChoiceCandidate action was received without a pending choice.",
                BaseMetadata(action));
        }

        if (!HeadlessActionPayloadReader.TryReadEntityId(
                action, HeadlessActionParameterKeys.ChoiceCandidateId, out HeadlessEntityId candidateId, out string? idError))
        {
            return ActionProcessResult.Failure(
                idError ?? "ToggleChoiceCandidate action is missing a candidate id.",
                BaseMetadata(action));
        }

        try
        {
            HeadlessChoiceState choice = context.ChoiceController.ToggleCandidate(candidateId);
            Dictionary<string, object?> metadata = MetadataWithChoice(action, choice);
            metadata[HeadlessActionParameterKeys.ChoiceCandidateId] = candidateId.Value;
            metadata[HeadlessActionParameterKeys.ChoicePendingSelectedIds] = choice.PendingSelectedIds
                .Select(pendingId => pendingId.Value)
                .ToArray();
            return ActionProcessResult.Success("Choice candidate toggled.", metadata);
        }
        catch (ArgumentException ex)
        {
            Dictionary<string, object?> failure = MetadataWithChoice(action, context.ChoiceController.Current);
            failure["error"] = ex.Message;
            return ActionProcessResult.Failure("Choice candidate toggle failed.", failure);
        }
    }

    private static ActionProcessResult ClearChoice(
        LegalAction action,
        EngineContext context)
    {
        HeadlessChoiceState choice = context.ChoiceController.ClearChoice();
        return ActionProcessResult.Success("Choice cleared.", MetadataWithChoice(action, choice));
    }

    /// <summary>(C2 seam 2) Decode a mirror-window ORDER pick from the agent's <see cref="ChoiceResult"/> into the
    /// SkillInfo-currency <see cref="Effects.SkillWindowAnswer"/>. The <see cref="AgentSkillWindowChoicePort"/>
    /// offers candidates whose id is "cardInstanceId#ordinal"; an empty selection is a decline (the AS-IS -1 skip
    /// sentinel).</summary>
    private static Effects.SkillWindowAnswer DecodeWindowAnswer(ChoiceResult result)
    {
        if (result.SelectedIds is not { Count: > 0 } selected)
        {
            return Effects.SkillWindowAnswer.Decline;
        }

        string token = selected[0].Value;
        int hash = token.LastIndexOf('#');
        if (hash <= 0 || !int.TryParse(token[(hash + 1)..], out int ordinal))
        {
            return Effects.SkillWindowAnswer.Decline;
        }

        return Effects.SkillWindowAnswer.Pick(new HeadlessEntityId(token[..hash]), ordinal);
    }

    // G3.5-RL-A2: read an agent-supplied selection from a ResolveChoice action.
    // Returns false when no selection parameter is present (legacy provider-driven path).
    private static bool TryReadCarriedChoiceResult(
        IReadOnlyDictionary<string, object?> parameters,
        out ChoiceResult? result)
    {
        bool hasSkip = parameters.ContainsKey(HeadlessActionParameterKeys.ChoiceSkipped);
        bool hasCount = parameters.ContainsKey(HeadlessActionParameterKeys.ChoiceSelectedCount);
        bool hasIds = parameters.ContainsKey(HeadlessActionParameterKeys.ChoiceSelectedIds);

        if (!hasSkip && !hasCount && !hasIds)
        {
            result = null;
            return false;
        }

        if (hasSkip && ReadBoolOrDefault(parameters, HeadlessActionParameterKeys.ChoiceSkipped, defaultValue: false))
        {
            result = ChoiceResult.Skip();
            return true;
        }

        if (hasCount && TryReadInt(parameters, HeadlessActionParameterKeys.ChoiceSelectedCount, out int selectedCount))
        {
            result = ChoiceResult.SelectCount(selectedCount);
            return true;
        }

        result = ChoiceResult.Select(ReadEntityIds(parameters, HeadlessActionParameterKeys.ChoiceSelectedIds));
        return true;
    }

    private static async Task<ActionProcessResult> ShuffleDeckAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        await context.ZoneMover.ShuffleAsync(action.PlayerId, cancellationToken).ConfigureAwait(false);
        return ActionProcessResult.Success("Deck shuffled.", BaseMetadata(action));
    }

    private static ActionProcessResult EnqueueEffect(
        LegalAction action,
        EngineContext context)
    {
        if (!EffectActionPayload.TryRead(action, out EffectActionPayload? payload, out string? error))
        {
            return ActionProcessResult.Failure(error, BaseMetadata(action));
        }

        EffectContext effectContext = new(
            action.PlayerId,
            payload.SourceEntityId,
            new Dictionary<string, object?>(action.Parameters));

        context.EffectScheduler.Enqueue(new EffectRequest(
            payload.EffectId,
            action.PlayerId,
            payload.Timing,
            effectContext));

        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.EffectId] = payload.EffectId.Value;
        metadata[HeadlessActionParameterKeys.Timing] = payload.Timing;
        metadata[HeadlessActionParameterKeys.SourceEntityId] = payload.SourceEntityId.Value;

        return ActionProcessResult.Success("Effect enqueued.", metadata);
    }

    private static ActionProcessResult SetMemory(
        LegalAction action,
        EngineContext context)
    {
        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.Memory, out int memory))
        {
            return ActionProcessResult.Failure(
                "SetMemory action is missing a valid memory value.",
                BaseMetadata(action));
        }

        // (4b B6) Substrate poke only: the OLD driver's inline memory-pass evaluation
        // (HeadlessMainPhaseFlow.EvaluateAfterMemoryMutation) is retired — the pump runs the real AS-IS
        // EndTurnCheck at every pump point, so a mutation that crosses the threshold auto-ends the turn there.
        HeadlessMemoryState state = context.MemoryController.Set(memory);
        Dictionary<string, object?> metadata = MetadataWithMemory(action, state);
        return ActionProcessResult.Success(
            $"Memory set to {state.Current}.",
            metadata);
    }

    private static ActionProcessResult AddMemory(
        LegalAction action,
        EngineContext context)
    {
        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.MemoryAmount, out int amount))
        {
            return ActionProcessResult.Failure(
                "AddMemory action is missing a valid memory amount.",
                BaseMetadata(action));
        }

        // (4b B6) Substrate poke only — see SetMemory: the inline memory-pass evaluation is retired.
        HeadlessMemoryState state = context.MemoryController.Add(amount);
        Dictionary<string, object?> metadata = MetadataWithMemory(action, state);
        metadata[HeadlessActionParameterKeys.MemoryAmount] = amount;
        return ActionProcessResult.Success(
            $"Memory changed by {amount} to {state.Current}.",
            metadata);
    }

    private static ActionProcessResult PayMemory(
        LegalAction action,
        EngineContext context)
    {
        if (!TryReadInt(action.Parameters, HeadlessActionParameterKeys.MemoryCost, out int cost))
        {
            return ActionProcessResult.Failure(
                "PayMemory action is missing a valid memory cost.",
                BaseMetadata(action));
        }

        if (!context.MemoryController.CanPay(cost))
        {
            Dictionary<string, object?> failureMetadata = MetadataWithMemory(
                action,
                context.MemoryController.Current);
            failureMetadata[HeadlessActionParameterKeys.MemoryCost] = cost;
            return ActionProcessResult.Failure(
                $"Cannot pay memory cost {cost}.",
                failureMetadata);
        }

        // (4b B6) Substrate poke only — see SetMemory: the inline memory-pass evaluation is retired.
        HeadlessMemoryState state = context.MemoryController.Pay(cost);
        Dictionary<string, object?> metadata = MetadataWithMemory(action, state);
        metadata[HeadlessActionParameterKeys.MemoryCost] = cost;
        return ActionProcessResult.Success(
            $"Paid memory cost {cost}; memory is {state.Current}.",
            metadata);
    }

    private static Dictionary<string, object?> BaseMetadata(LegalAction action)
    {
        return new Dictionary<string, object?>
        {
            [HeadlessActionParameterKeys.ActionId] = action.Id.Value,
            [HeadlessActionParameterKeys.PlayerId] = action.PlayerId.Value,
            [HeadlessActionParameterKeys.ActionType] = action.ActionType
        };
    }

    private static Dictionary<string, object?> MetadataWithCard(
        LegalAction action,
        HeadlessEntityId cardId)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.CardId] = cardId.Value;
        return metadata;
    }

    private static Dictionary<string, object?> MetadataWithMemory(
        LegalAction action,
        HeadlessMemoryState state)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.Memory] = state.Current;
        metadata[HeadlessActionParameterKeys.MemoryMinimum] = state.Minimum;
        metadata[HeadlessActionParameterKeys.MemoryMaximum] = state.Maximum;
        return metadata;
    }

    private static void AddEndTurnCleanupMetadata(
        Dictionary<string, object?> metadata,
        EndTurnCleanupResult result)
    {
        metadata[HeadlessActionParameterKeys.EndTurnCleanupApplied] = result.Applied;
        metadata[HeadlessActionParameterKeys.EndTurnCleanupReason] = result.Reason;
        metadata[HeadlessActionParameterKeys.EndTurnCleanupCardIds] = result.CleanedCardIds.ToArray();
        metadata[HeadlessActionParameterKeys.EndTurnCleanupRemovedKeys] = result.RemovedKeys.ToArray();
        metadata[HeadlessActionParameterKeys.EndTurnCleanupRemovedKeyCount] = result.RemovedKeys.Count;
        metadata[HeadlessActionParameterKeys.EndTurnCleanupResetAttackCount] = result.ResetAttackCount;
    }

    private static Dictionary<string, object?> MetadataWithAttack(
        LegalAction action,
        HeadlessAttackState state)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.AttackCount] = state.AttackCount;
        metadata[HeadlessActionParameterKeys.AttackerId] = state.AttackerId?.Value;
        metadata[HeadlessActionParameterKeys.DefendingPlayerId] = state.DefendingPlayerId?.Value;
        metadata[HeadlessActionParameterKeys.AttackTargetId] = state.TargetId?.Value;
        metadata[HeadlessActionParameterKeys.BlockerId] = state.BlockerId?.Value;
        metadata[HeadlessActionParameterKeys.AttackBlocked] = state.IsBlocked;
        metadata[HeadlessActionParameterKeys.IsDirectAttack] = state.IsDirectAttack;
        metadata[HeadlessActionParameterKeys.AttackPending] = state.IsPending;
        metadata[HeadlessActionParameterKeys.AttackResolved] = state.IsResolved;
        return metadata;
    }

    private static Dictionary<string, object?> MetadataWithChoice(
        LegalAction action,
        HeadlessChoiceState state)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.ChoiceRequestId] = state.RequestId?.Value;
        metadata[HeadlessActionParameterKeys.ChoiceType] = state.Type.ToString();
        metadata[HeadlessActionParameterKeys.ChoiceMessage] = state.Message;
        metadata[HeadlessActionParameterKeys.ChoiceMinCount] = state.MinCount;
        metadata[HeadlessActionParameterKeys.ChoiceMaxCount] = state.MaxCount;
        metadata[HeadlessActionParameterKeys.ChoiceCanSkip] = state.CanSkip;
        metadata[HeadlessActionParameterKeys.ChoiceSourceZone] = state.SourceZone.ToString();
        metadata[HeadlessActionParameterKeys.ChoicePending] = state.IsPending;
        metadata[HeadlessActionParameterKeys.ChoiceResolved] = state.IsResolved;
        metadata[HeadlessActionParameterKeys.ChoiceSkipped] = state.IsSkipped;
        metadata[HeadlessActionParameterKeys.ChoiceSelectedCount] = state.SelectedCount;
        metadata[HeadlessActionParameterKeys.ChoiceSelectedIds] = state.SelectedIds
            .Select(selectedId => selectedId.Value)
            .ToArray();
        return metadata;
    }

    private static bool TryReadInt(
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        out int value)
    {
        if (!parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = default;
            return false;
        }

        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }

        if (rawValue is long longValue &&
            longValue >= int.MinValue &&
            longValue <= int.MaxValue)
        {
            value = (int)longValue;
            return true;
        }

        if (rawValue is string stringValue && int.TryParse(stringValue, out int parsedValue))
        {
            value = parsedValue;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryReadChoiceType(
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        out ChoiceType value)
    {
        if (!parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = default;
            return false;
        }

        if (rawValue is ChoiceType choiceType)
        {
            value = choiceType;
            return true;
        }

        if (rawValue is string stringValue && Enum.TryParse(stringValue, ignoreCase: true, out ChoiceType parsedValue))
        {
            value = parsedValue;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryReadChoiceZone(
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        out ChoiceZone value)
    {
        if (!parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            value = default;
            return false;
        }

        if (rawValue is ChoiceZone choiceZone)
        {
            value = choiceZone;
            return true;
        }

        if (rawValue is string stringValue && Enum.TryParse(stringValue, ignoreCase: true, out ChoiceZone parsedValue))
        {
            value = parsedValue;
            return true;
        }

        value = default;
        return false;
    }

    private static IReadOnlyList<HeadlessEntityId> ReadEntityIds(
        IReadOnlyDictionary<string, object?> parameters,
        string key)
    {
        if (!parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        if (rawValue is IEnumerable<HeadlessEntityId> entityIds)
        {
            return entityIds.ToArray();
        }

        if (rawValue is IEnumerable<string> stringIds)
        {
            return stringIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => new HeadlessEntityId(id))
                .ToArray();
        }

        if (rawValue is string stringValue && !string.IsNullOrWhiteSpace(stringValue))
        {
            return stringValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(id => new HeadlessEntityId(id))
                .ToArray();
        }

        return Array.Empty<HeadlessEntityId>();
    }

    private static bool ReadBoolOrDefault(
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        bool defaultValue)
    {
        if (!parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return defaultValue;
        }

        if (rawValue is bool boolValue)
        {
            return boolValue;
        }

        if (rawValue is string stringValue && bool.TryParse(stringValue, out bool parsedValue))
        {
            return parsedValue;
        }

        return defaultValue;
    }

    private static string ReadStringOrDefault(
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        string defaultValue)
    {
        if (!parameters.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return defaultValue;
        }

        return rawValue is string stringValue
            ? stringValue
            : defaultValue;
    }
}
