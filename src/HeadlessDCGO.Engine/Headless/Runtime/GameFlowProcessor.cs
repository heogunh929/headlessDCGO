namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// Encapsulates the AS-IS common processing loop (Unity <c>TurnStateMachine</c>'s repeated
/// rule-process / auto-process / attack-advance / end-turn cycle) as a headless, re-entrant
/// step. The loop runs until no sub-step makes progress (stable state) or a choice becomes
/// pending, in which case it pauses so the next <c>HeadlessGameLoop</c> step can resume it.
///
/// Phase 3.5 scope: auto-processing collects triggered effects from pending game events
/// (<see cref="AutoProcessingTriggerCollector"/> driven by <see cref="GameEventQueue"/>, G3.5-006)
/// and resolves the scheduler (<see cref="EffectScheduler"/>, G3.5-001/002); the attack pipeline
/// (<see cref="AttackPipeline"/>, G3.5-005) is active. Rule processing and end-turn evaluation are
/// hooks that are filled in by G3.5-007 / G3.5-008.
/// </summary>
public sealed class GameFlowProcessor
{
    public const int MaxIterations = 256;

    private readonly AttackPipeline _attackPipeline;

    public GameFlowProcessor(AttackPipeline? attackPipeline = null)
    {
        _attackPipeline = attackPipeline ?? new AttackPipeline();
    }

    public async Task<FlowProcessResult> RunToStableAsync(
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        bool progressedAny = false;
        int resolvedTotal = 0;
        int iterations = 0;

        while (iterations < MaxIterations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.ChoiceController.Current.IsPending)
            {
                return FlowProcessResult.Paused(progressedAny, resolvedTotal, iterations);
            }

            iterations++;
            bool progressed = false;

            // 1) Rule processing (state-based actions): cleanup of cards flagged for deletion
            //    (G3.5-RL-C1). Deck-out loss is marked at draw time and consolidated by EndTurnCheck.
            progressed |= await RuleProcessAsync(context, cancellationToken).ConfigureAwait(false);

            // 2) Auto-processing: collect triggers from pending game events (G3.5-006) and resolve
            //    the effect scheduler (G3.5-001/002).
            (int resolved, int collected) = await AutoProcessAsync(context, cancellationToken).ConfigureAwait(false);
            if (resolved > 0 || collected > 0)
            {
                progressed = true;
                resolvedTotal += resolved;
            }

            // 3) Attack pipeline single-step advance (G3.5-005): block → battle/security → end attack.
            if (context.AttackController.Current.Phase != AttackPhase.None)
            {
                AttackAdvanceResult attackResult = await _attackPipeline
                    .AdvanceAsync(context, cancellationToken)
                    .ConfigureAwait(false);
                progressed |= attackResult.Progressed;
            }

            // 4) End-turn / win-loss evaluation (G3.5-008): PlayerRuleAdapter terminal verdict → match result.
            progressed |= EndTurnCheck(context);

            progressedAny |= progressed;

            if (!progressed)
            {
                break;
            }

            if (context.RuleQueryService.IsTerminal())
            {
                break;
            }
        }

        return FlowProcessResult.Stable(progressedAny, resolvedTotal, iterations);
    }

    /// <summary>Marks a card instance for state-based deletion. Effects set this flag; the rule
    /// process sweeps flagged cards off the field into the trash.</summary>
    public const string PendingDeletionKey = "pendingDeletion";

    private static readonly ChoiceZone[] FieldZones =
    {
        ChoiceZone.BattleArea,
        ChoiceZone.BreedingArea
    };

    /// <summary>
    /// (G3.5-RL-C1) State-based action pass. Sweeps cards flagged <see cref="PendingDeletionKey"/>
    /// that still occupy a field zone into the trash and clears the flag — the uniform deletion path
    /// for ported effects (the AS-IS rule timing's deletion cleanup). Returns true when it acts so
    /// the common loop keeps iterating until the board is stable.
    /// </summary>
    private static async Task<bool> RuleProcessAsync(
        EngineContext context,
        CancellationToken cancellationToken)
    {
        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return false;
        }

        bool progressed = false;
        foreach (HeadlessPlayerId playerId in context.TurnController.Current.PlayerOrder)
        {
            if (playerId.IsEmpty)
            {
                continue;
            }

            foreach (ChoiceZone zone in FieldZones)
            {
                foreach (HeadlessEntityId cardId in zoneReader.GetCards(playerId, zone).ToArray())
                {
                    if (!IsPendingDeletion(context, cardId))
                    {
                        continue;
                    }

                    ClearPendingDeletion(context, cardId);
                    await context.ZoneMover.MoveAsync(
                        new ZoneMoveRequest(playerId, cardId, zone, ChoiceZone.Trash),
                        cancellationToken).ConfigureAwait(false);
                    progressed = true;
                }
            }
        }

        return progressed;
    }

    private static bool IsPendingDeletion(EngineContext context, HeadlessEntityId cardId)
    {
        return context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) &&
            instance is not null &&
            instance.Metadata.TryGetValue(PendingDeletionKey, out object? raw) &&
            raw is bool flag &&
            flag;
    }

    private static void ClearPendingDeletion(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return;
        }

        Dictionary<string, object?> metadata = new(instance.Metadata, StringComparer.Ordinal)
        {
            [PendingDeletionKey] = false
        };
        context.CardInstanceRepository.Upsert(instance with { Metadata = metadata });
    }

    /// <summary>
    /// (X-05) Drives event-driven trigger collection then resolves the scheduler. Newly produced
    /// zone events are bridged into the <see cref="GameEventQueue"/>; every pending game event is fed
    /// to the <see cref="AutoProcessingTriggerCollector"/> which enqueues matching triggered effects;
    /// finally the scheduler is drained. Returns the number of effects actually resolved and the number
    /// of triggered effects collected so the loop can detect progress even when collection and
    /// resolution happen in separate passes.
    /// </summary>
    private static async Task<(int Resolved, int Collected)> AutoProcessAsync(
        EngineContext context,
        CancellationToken cancellationToken)
    {
        context.GameEventQueue.SyncFrom(context.ZoneMover.Events);

        int collected = 0;
        IReadOnlyList<GameEvent> pendingEvents = context.GameEventQueue.DrainPending();
        if (pendingEvents.Count > 0)
        {
            var collector = new AutoProcessingTriggerCollector(context.EffectRegistry);
            foreach (GameEvent gameEvent in pendingEvents)
            {
                if (gameEvent.Type == GameEventType.Unknown)
                {
                    continue;
                }

                TriggerCollectionResult collection = collector.CollectAndEnqueue(gameEvent, context.EffectScheduler);
                if (collection.IsSuccess)
                {
                    collected += collection.EnqueuedCount;
                }
            }
        }

        IReadOnlyList<EffectResult> results = await context.EffectScheduler
            .ResolveAllAsync(cancellationToken)
            .ConfigureAwait(false);

        return (results.Count(result => result.Resolved), collected);
    }

    /// <summary>
    /// (X-02) Consolidated win-loss evaluation, mirroring Unity AS-IS <c>AutoProcessing</c> reading
    /// <c>Player.IsLose</c>. The <see cref="TerminalEvaluator"/> builds a <see cref="PlayerRuleAdapter"/>
    /// from the active player order plus runtime lose flags; on a terminal verdict the winner/reason is
    /// pushed into the terminal state so <c>DcgoMatch.GetResult()</c> and the <c>GameEnded</c> event carry it.
    /// </summary>
    private static bool EndTurnCheck(EngineContext context)
    {
        if (context.RuleQueryService.IsTerminal())
        {
            return false;
        }

        PlayerTerminalCheck? check = TerminalEvaluator.Evaluate(context);
        if (check is null || !check.IsTerminal)
        {
            return false;
        }

        if (context.RuleQueryService is ITerminalOutcomeSink outcomeSink)
        {
            outcomeSink.SetTerminalOutcome(check.WinnerPlayerId, isDraw: false, check.Message);
        }
        else if (context.RuleQueryService is ITerminalStateController terminalController)
        {
            terminalController.SetTerminal(true);
        }

        context.LogSink.Info(
            $"[EndTurnCheck] Terminal: {check.Reason} winner={check.WinnerPlayerId?.Value} loser={check.LosingPlayerId?.Value}.");
        return true;
    }
}

public enum FlowProcessStatus
{
    Stable,
    PausedForChoice,
}

public sealed record FlowProcessResult(
    FlowProcessStatus Status,
    bool ProgressedAny,
    int ResolvedEffectCount,
    int Iterations)
{
    public bool PausedForChoice => Status == FlowProcessStatus.PausedForChoice;

    public bool IsStable => Status == FlowProcessStatus.Stable;

    public static FlowProcessResult Stable(bool progressedAny, int resolvedEffectCount, int iterations)
    {
        return new FlowProcessResult(FlowProcessStatus.Stable, progressedAny, resolvedEffectCount, iterations);
    }

    public static FlowProcessResult Paused(bool progressedAny, int resolvedEffectCount, int iterations)
    {
        return new FlowProcessResult(FlowProcessStatus.PausedForChoice, progressedAny, resolvedEffectCount, iterations);
    }
}
