namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class MetadataActionProcessor : IActionProcessor
{
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
            HeadlessActionTypes.NormalizedPass => new PassAction().Process(action, context),
            HeadlessActionTypes.NormalizedCheat => CheatActionGuard.Reject(action),
            HeadlessActionTypes.NormalizedPlayCard => await new PlayCardAction()
                .ProcessAsync(action, context, cancellationToken)
                .ConfigureAwait(false),
            HeadlessActionTypes.NormalizedDigivolve => await new DigivolveAction()
                .ProcessAsync(action, context, cancellationToken)
                .ConfigureAwait(false),
            HeadlessActionTypes.NormalizedActivateOption => await new OptionActivateAction()
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
            HeadlessActionTypes.NormalizedClearChoice => ClearChoice(action, context),
            HeadlessActionTypes.NormalizedShuffleDeck => await ShuffleDeckAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedEnqueueEffect => EnqueueEffect(action, context),
            HeadlessActionTypes.NormalizedAdvancePhase => await AdvancePhaseAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedEndTurn => EndTurn(action, context),
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

        await context.ZoneMover.AddToHandAsync(action.PlayerId, payload.CardId, cancellationToken).ConfigureAwait(false);
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
            .DrawAsync(action.PlayerId, count, cancellationToken)
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
            .AddSecurityFromLibraryAsync(action.PlayerId, count, faceUp, cancellationToken)
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
        IReadOnlyList<HeadlessEntityId> trashedCards = await context.ZoneMover
            .TrashSecurityAsync(action.PlayerId, count, fromTop, cancellationToken)
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
            if (pendingRequest.Type == ChoiceType.OptionalEffect)
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
            return ActionProcessResult.Success("Choice resolved.", MetadataWithChoice(action, choice));
        }
        catch (InvalidOperationException ex)
        {
            Dictionary<string, object?> metadata = MetadataWithChoice(action, context.ChoiceController.Current);
            metadata["error"] = ex.Message;
            return ActionProcessResult.Failure("Choice resolve failed.", metadata);
        }
    }

    private static ActionProcessResult ClearChoice(
        LegalAction action,
        EngineContext context)
    {
        HeadlessChoiceState choice = context.ChoiceController.ClearChoice();
        return ActionProcessResult.Success("Choice cleared.", MetadataWithChoice(action, choice));
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

    private static async Task<ActionProcessResult> AdvancePhaseAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken)
    {
        PhaseTransitionResult transition;
        try
        {
            transition = await new HeadlessEarlyPhaseFlow()
                .AdvanceAsync(context, action, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return ActionProcessResult.Illegal(action, ex.Message, BaseMetadata(action));
        }

        MainPhaseMemoryResult mainPhase;
        try
        {
            mainPhase = new HeadlessMainPhaseFlow()
                .EvaluateMainPhaseEntry(context, action, transition);
        }
        catch (InvalidOperationException ex)
        {
            return ActionProcessResult.Illegal(action, ex.Message, BaseMetadata(action));
        }

        Dictionary<string, object?> metadata = MetadataWithPhaseTransition(action, transition);
        AddMainPhaseMetadata(metadata, mainPhase);

        // F-6.2: open the start-of-main-phase window when this advance entered the main phase (the
        // original fires EffectTiming.OnStartMainPhase here). Global — each bound effect self-gates.
        if (mainPhase.MainPhaseEntered)
        {
            TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnStartMainPhase, actor: mainPhase.CurrentTurn.TurnPlayerId);
        }

        return ActionProcessResult.Success(
            $"Phase advanced to {mainPhase.CurrentTurn.Phase}.",
            metadata);
    }

    private static ActionProcessResult EndTurn(
        LegalAction action,
        EngineContext context)
    {
        HeadlessTurnState previousTurn = context.TurnController.Current;
        HeadlessMemoryState previousMemory = context.MemoryController.Current;
        EndTurnCleanupResult cleanup = new HeadlessEndTurnCleanupFlow()
            .Cleanup(context, previousTurn);
        HeadlessTurnState turn = context.TurnController.EndTurn();
        MainPhaseMemoryResult mainPhase = new HeadlessMainPhaseFlow()
            .CompleteMemoryPassTurn(context, previousTurn, turn, previousMemory);
        Dictionary<string, object?> metadata = MetadataWithTurn(action, turn);
        AddMainPhaseMetadata(metadata, mainPhase);
        AddEndTurnCleanupMetadata(metadata, cleanup);

        // W1: open the turn-boundary timing windows so [End of Turn] / [Start of Turn] effects fire.
        if (previousTurn.TurnPlayerId is HeadlessPlayerId endingPlayer)
        {
            TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnEndTurn, actor: endingPlayer);
        }

        // F-4: a new turn begins — reset the once-per-turn use counts (original InitUseCountThisTurn).
        context.OnceFlags.ResetForTurn(turn.TurnNumber, turn.TurnPlayerId);

        if (turn.TurnPlayerId is HeadlessPlayerId startingPlayer)
        {
            TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnStartTurn, actor: startingPlayer);
        }

        return ActionProcessResult.Success(
            $"Turn advanced to player {turn.TurnPlayerId?.Value.ToString() ?? "<none>"} {turn.Phase}.",
            metadata);
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

        HeadlessMemoryState previous = context.MemoryController.Current;
        HeadlessMemoryState state = context.MemoryController.Set(memory);
        MainPhaseMemoryResult mainPhase = new HeadlessMainPhaseFlow()
            .EvaluateAfterMemoryMutation(context, action, previous, state, "SetMemory");
        Dictionary<string, object?> metadata = MetadataWithMemory(action, state);
        AddMainPhaseMetadata(metadata, mainPhase);
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

        HeadlessMemoryState previous = context.MemoryController.Current;
        HeadlessMemoryState state = context.MemoryController.Add(amount);
        Dictionary<string, object?> metadata = MetadataWithMemory(action, state);
        metadata[HeadlessActionParameterKeys.MemoryAmount] = amount;
        MainPhaseMemoryResult mainPhase = new HeadlessMainPhaseFlow()
            .EvaluateAfterMemoryMutation(context, action, previous, state, "AddMemory");
        AddMainPhaseMetadata(metadata, mainPhase);
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

        HeadlessMemoryState previous = context.MemoryController.Current;
        HeadlessMemoryState state = context.MemoryController.Pay(cost);
        Dictionary<string, object?> metadata = MetadataWithMemory(action, state);
        metadata[HeadlessActionParameterKeys.MemoryCost] = cost;
        MainPhaseMemoryResult mainPhase = new HeadlessMainPhaseFlow()
            .EvaluateAfterMemoryMutation(context, action, previous, state, "MemoryThreshold");
        AddMainPhaseMetadata(metadata, mainPhase);
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

    private static Dictionary<string, object?> MetadataWithTurn(
        LegalAction action,
        HeadlessTurnState turn)
    {
        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.TurnNumber] = turn.TurnNumber;
        metadata[HeadlessActionParameterKeys.Phase] = turn.Phase.ToString();
        metadata[HeadlessActionParameterKeys.TurnPlayerId] = turn.TurnPlayerId?.Value;
        metadata[HeadlessActionParameterKeys.NonTurnPlayerId] = turn.NonTurnPlayerId?.Value;
        metadata[HeadlessActionParameterKeys.IsFirstTurn] = turn.IsFirstTurn;
        return metadata;
    }

    private static Dictionary<string, object?> MetadataWithPhaseTransition(
        LegalAction action,
        PhaseTransitionResult transition)
    {
        Dictionary<string, object?> metadata = MetadataWithTurn(action, transition.Current);
        metadata[HeadlessActionParameterKeys.PreviousPhase] = transition.Previous.Phase.ToString();
        metadata[HeadlessActionParameterKeys.PhaseOperations] = transition.Operations.ToArray();
        metadata[HeadlessActionParameterKeys.DrawnCardIds] = transition.DrawnCardIds.Select(id => id.Value).ToArray();
        metadata[HeadlessActionParameterKeys.DrawSkipped] = transition.DrawSkipped;
        metadata[HeadlessActionParameterKeys.DeckOut] = transition.DeckOut;
        metadata[HeadlessActionParameterKeys.UnsuspendedCardIds] = transition.UnsuspendedCardIds.Select(id => id.Value).ToArray();
        metadata[HeadlessActionParameterKeys.BreedingAction] = transition.BreedingAction;
        metadata[HeadlessActionParameterKeys.HatchedCardId] = transition.HatchedCardId?.Value;
        metadata[HeadlessActionParameterKeys.MovedBreedingCardIds] = transition.MovedBreedingCardIds.Select(id => id.Value).ToArray();
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

    private static void AddMainPhaseMetadata(
        Dictionary<string, object?> metadata,
        MainPhaseMemoryResult result)
    {
        metadata[HeadlessActionParameterKeys.Phase] = result.CurrentTurn.Phase.ToString();
        metadata[HeadlessActionParameterKeys.TurnNumber] = result.CurrentTurn.TurnNumber;
        metadata[HeadlessActionParameterKeys.TurnPlayerId] = result.CurrentTurn.TurnPlayerId?.Value;
        metadata[HeadlessActionParameterKeys.NonTurnPlayerId] = result.CurrentTurn.NonTurnPlayerId?.Value;
        metadata[HeadlessActionParameterKeys.IsFirstTurn] = result.CurrentTurn.IsFirstTurn;
        metadata[HeadlessActionParameterKeys.PreviousMemory] = result.PreviousMemory.Current;
        metadata[HeadlessActionParameterKeys.Memory] = result.CurrentMemory.Current;
        metadata[HeadlessActionParameterKeys.MemoryMinimum] = result.CurrentMemory.Minimum;
        metadata[HeadlessActionParameterKeys.MemoryMaximum] = result.CurrentMemory.Maximum;
        metadata[HeadlessActionParameterKeys.MainPhaseEntered] = result.MainPhaseEntered;
        metadata[HeadlessActionParameterKeys.MemoryPassTriggered] = result.MemoryPassTriggered;
        metadata[HeadlessActionParameterKeys.MemoryPassCompleted] = result.MemoryPassCompleted;
        metadata[HeadlessActionParameterKeys.MemoryPassReason] = result.Reason;
        metadata[HeadlessActionParameterKeys.MemoryPassThreshold] = result.MemoryPassThreshold;
        metadata[HeadlessActionParameterKeys.PassedMemory] = result.MemoryPassTriggered
            ? Math.Abs(result.CurrentMemory.Current)
            : null;
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
