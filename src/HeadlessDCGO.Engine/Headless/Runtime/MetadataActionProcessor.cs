namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
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
            // (S3c cutover) The OLD Runtime play processors are retired — the canonical play is the pump's
            // TurnFlowDriver -> mirror MainPhaseAction -> TurnStateMachine MainPhase -> PlayCardClass.PlayCard()
            // (the AS-IS same-path). Their GetLegalActions enumeration side stays live (HeadlessLegalActionDispatcher);
            // only ProcessAsync is retired. A play reaching THIS processor is a non-pump (scripting) match: illegal.
            HeadlessActionTypes.NormalizedPlayCard => ActionProcessResult.Illegal(
                action, "PlayCard is pump-only: it routes through TurnFlowDriver to the mirror PlayCardClass.PlayCard().", BaseMetadata(action)),
            HeadlessActionTypes.NormalizedDigivolve => ActionProcessResult.Illegal(
                action, "Digivolve is pump-only: it routes through TurnFlowDriver to the mirror PlayCardClass.PlayCard().", BaseMetadata(action)),
            HeadlessActionTypes.NormalizedSpecialPlay => ActionProcessResult.Illegal(
                action, "SpecialPlay is pump-only: a DigiXros/Assembly play is the ordinary PlayCard entry (mirror SelectDigiXros/SelectAssembly).", BaseMetadata(action)),
            HeadlessActionTypes.NormalizedActivateOption => ActionProcessResult.Illegal(
                action, "ActivateOption is pump-only: it routes through TurnFlowDriver to the mirror PlayCardClass.PlayCard() option-play.", BaseMetadata(action)),
            HeadlessActionTypes.NormalizedActivateMain => ActionProcessResult.Illegal(
                action, "ActivateMain is pump-only: it routes through TurnFlowDriver to the mirror main-skill activation.", BaseMetadata(action)),
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
            // (ATTACK cluster teardown) The OLD Runtime attack-declaration processor is retired — the canonical
            // declaration is the pump's TurnFlowDriver -> mirror AttackPermanentAction packet -> TurnStateMachine
            // main-phase Execute -> AttackProcess.Attack (the AS-IS same-path). Its GetLegalActions enumeration
            // side stays live (HeadlessLegalActionDispatcher.AttackLegalActions); only Process is retired. A
            // DeclareAttack reaching THIS processor is a non-pump (scripting) match: illegal, exactly like the
            // sibling play arms above.
            HeadlessActionTypes.NormalizedDeclareAttack => ActionProcessResult.Illegal(
                action, "DeclareAttack is pump-only: it routes through TurnFlowDriver to the mirror AttackProcess.Attack.", BaseMetadata(action)),
            HeadlessActionTypes.NormalizedResolveAttack => ResolveAttack(action, context),
            HeadlessActionTypes.NormalizedClearAttack => ClearAttack(action, context),
            HeadlessActionTypes.NormalizedRequestChoice => RequestChoice(action, context),
            HeadlessActionTypes.NormalizedResolveChoice => await ResolveChoiceAsync(action, context, cancellationToken).ConfigureAwait(false),
            HeadlessActionTypes.NormalizedToggleChoiceCandidate => ToggleChoiceCandidate(action, context),
            HeadlessActionTypes.NormalizedClearChoice => ClearChoice(action, context),
            HeadlessActionTypes.NormalizedShuffleDeck => await ShuffleDeckAsync(action, context, cancellationToken).ConfigureAwait(false),
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

            // (BlockTiming retirement) The ChoiceType.Blocker resolve branch is RETIRED with its opener. AS-IS
            // performs the blocker SELECTION inside AttackProcess.BlockTiming() (:322-405) via a SelectPermanentEffect,
            // and the mirror does the same (AttackProcess.BlockTiming) — that select is an ordinary effect-body
            // choice that parks the live pump stack and comes back through the pump-deposit path above. Nothing
            // opens ChoiceType.Blocker any more, so the externalised park/resume seam has no reachable state.

            // (window inline re-migration) An OptionalEffect prompt — the AS-IS "Will you use ~?" yes/no
            // (OptionalSkill.SelectOptional → ChoiceProvider.ChooseAsync, mirror OptionalSkill.cs:82) — is raised
            // INLINE inside the resolving skill body, which parks the pump in place; it is resolved by the
            // pump-deposit path above (DepositAnswer hands the yes/no straight back to the live SelectOptional
            // stack). The OptionalPromptQueue OLD-model trigger queue is RETIRED — AS-IS has no such queue.

            // (window inline re-migration) The trigger-window ORDER / optional pick is resolved by the pump-deposit
            // path above (a WindowChoice opened by MultipleSkills.ChooseOrderIndexAsync -> ChoiceProvider.ChooseAsync
            // parks the pump in place; DepositAnswer hands the answer straight back to the window's live C# stack).
            // The externalised record/resume/replay branch (SkillWindowChoiceKey + Continuation.RecordAnswer +
            // ResumeSuspendedWindowsAsync) is RETIRED — AS-IS has no continuation, and the gate is the re-entry seam.

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

                    // (window inline re-migration) The ITrashLinkCards side-effect above stands; the window-resume
                    // tail is RETIRED — a window parked mid-trim holds its C# stack on the pump gate and resumes in
                    // place when the trim choice is deposited (no externalised ResumeSuspendedWindowsAsync re-drive).
                }

                return ActionProcessResult.Success("Link-trim choice resolved.", MetadataWithChoice(action, linkTrimChoice));
            }

            // (C-Atk RETIRED) the ChoiceType.AttackTarget (Raid) and ChoiceType.AllianceTarget (Alliance)
            // dispatch branches are gone: the RaidAttackSwitch / AllianceAttackBoost gates no longer OPEN those
            // choices (their counter-head RequestChoice firing-half was de-wired), so these choice types are now
            // unreachable. Raid/Alliance resolve inside the OnAllyAttack window (RaidProcess / AllianceProcess use
            // the ChoiceProvider, not the ChoiceController). These were the ONLY openers of either choice type.

            // (EffectDrivenAttack / OverclockEffect / RevealAndSelect retirement) The ChoiceType.EffectAttack,
            // ChoiceType.OverclockTarget and ChoiceType.RevealSelect resolve branches are RETIRED with their
            // openers — a whole-tree scan finds ZERO remaining producers of any of the three choice types. Each
            // AS-IS counterpart raises its select INLINE in the effect body and is resolved by the pump-deposit
            // path above:
            //   * effect-driven attack  -> the AS-IS `SelectAttackEffect.Activate()` port (SelectAttackEffect.cs),
            //     which Vortex / Overclock / Execute / Blitz and SelectPermanentEffect's Mode.Attack all use;
            //   * Overclock             -> AS-IS CardEffectCommons.OverclockProcess (KeyWordEffects/Overclock.cs),
            //     whose delete-a-trait-ally step is a SelectPermanentEffect;
            //   * reveal-and-select     -> AS-IS RevealLibrary.RevealDeckTopCardsAndSelect (CardEffectCommons/
            //     RevealLibrary.cs), whose pick is a SelectCardEffect.

            // (DeletionReplacementTiming retirement) The ChoiceType.DeletionReplacement resolve branch is
            // RETIRED with its opener (AutoProcessing's RuleProcess gate): no path opens that choice type any
            // more. AS-IS resolves a would-be-deleted replacement INLINE inside the deletion path, and the
            // mirror does the same in DestroyPermanentsClass.Destroy() — the PRE cut-in window's own choices
            // are ordinary effect-body choices that park the live pump stack and come back through the generic
            // ResolveChoice below.

            // (mulligan inline re-migration) The opening-hand keep/redraw decision is resolved by the pump-deposit
            // path above — AS-IS StartGame (:374-494) runs the mulligan INLINE on the game-start coroutine, and the
            // mirror TurnStateMachine.StartGameAsync does the same via ChoiceProvider.ChooseAsync, which parks the
            // pump in place; DepositAnswer hands the answer straight back to that live stack, which then applies
            // the redraw (hand -> deck bottom, shuffle, draw 5) and, after the last player, deals security
            // (:496-501). The externalised MulliganCoordinator branch (Begin + ResolveAsync + a coordinator-owned
            // deferred security deal) is RETIRED — AS-IS has no coordinator, and the gate is the re-entry seam.

            HeadlessChoiceState choice = context.ChoiceController.ResolveChoice(result);

            // (ActivatedEffectResolver retirement) The G11-002 "resume a SUSPENDED activation by re-resolving
            // the effect" block is RETIRED with its producer. The only path that ever called
            // DeferredActivations.Suspend was MainSkillActivateAction.ProcessAsync — itself an OLD
            // action-processor path superseded by the pump (ActivateMain now routes TurnFlowDriver ->
            // ActivatePermanentAction / ActivateCardAction -> the mirror TurnStateMachine declaration branch ->
            // AutoProcessing.ActivateEffectProcess), so the pending slot has no writer. Like the mulligan and
            // window body-choice cases below, a pump-side activation that parks on a body choice holds its C#
            // stack on the pump gate and consumes the deposited answer IN PLACE — nothing to re-drive, and no
            // out-of-band re-resolve (which is what needed the deleted resolver's replay semantics).

            // (window inline re-migration) The C2 seam-4b mirror-window BODY-resume path (record on the
            // MultipleSkills continuation + ResumeSuspendedWindowsAsync deepest-first) is RETIRED: a window body
            // pick suspends INLINE on the pump gate, so its C# stack survives the agent turn and consumes the
            // deposited answer in place — nothing to re-drive.
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

    // (window inline re-migration) DecodeWindowAnswer is RETIRED: the window order pick is deposited straight to
    // the parked window's live C# stack (pump gate), so there is no SkillWindowAnswer to decode/record/replay.

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

    // (EndTurnCleanupResult retirement) AddEndTurnCleanupMetadata — the private helper that projected the
    // externalised end-turn-cleanup flow's result DTO onto an action's metadata — is RETIRED with the DTO.
    // It had 0 callers: the end-of-turn bucket reset is AS-IS TurnStateMachine.EndPhase work (now inline in
    // the mirror EndPhaseAsync) and reports nothing back through the action layer.

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
