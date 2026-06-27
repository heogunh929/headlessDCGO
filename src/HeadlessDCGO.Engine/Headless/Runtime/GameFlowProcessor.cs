namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
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
        bool reachedStable = false;

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
                reachedStable = true;
                break;
            }

            if (context.RuleQueryService.IsTerminal())
            {
                reachedStable = true;
                break;
            }
        }

        // GPT-#3: distinguish a genuine fixpoint (no progress / terminal) from exhausting the iteration
        // budget while still making progress — the latter signals a runaway trigger loop, not stability.
        if (!reachedStable)
        {
            context.LogSink.Warn(
                $"[GameFlowProcessor] RunToStable hit MaxIterations ({MaxIterations}) while still progressing; " +
                "possible runaway trigger loop.");
            return FlowProcessResult.MaxIterationsExceeded(progressedAny, resolvedTotal, iterations);
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
    /// (G3.5-RL-C1 + D-2) State-based action pass. Sweeps off the field into the trash any field card
    /// that is either flagged <see cref="PendingDeletionKey"/> (the uniform effect-driven deletion path)
    /// OR a Digimon whose effective DP has dropped to 0 or below (AS-IS <c>DigimonLackDPProcess</c> /
    /// <c>TrashNoDPPermanentProcess</c> / <c>CutInProcess</c>: <c>DP &lt;= 0 &amp;&amp; IsDigimon</c>).
    /// Returns true when it acts so the common loop keeps iterating until the board is stable.
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
                    bool pending = IsPendingDeletion(context, cardId);
                    bool lethalDp = !pending && HasLethalDp(context, cardId);
                    if (!pending && !lethalDp)
                    {
                        continue;
                    }

                    if (pending)
                    {
                        ClearPendingDeletion(context, cardId);
                    }

                    await context.ZoneMover.MoveAsync(
                        new ZoneMoveRequest(playerId, cardId, zone, ChoiceZone.Trash),
                        cancellationToken).ConfigureAwait(false);
                    progressed = true;
                }
            }
        }

        return progressed;
    }

    /// <summary>
    /// (D-2) A field Digimon whose effective DP (base + typed modifiers, via <see cref="DpCalculator"/>)
    /// is 0 or below is destroyed as a state-based action. Only applies when DP is actually DEFINED — a
    /// Digimon with no printed DP at all is left alone (mirrors BattleResolver's "no battle DP" guard
    /// and avoids deleting DP-less abstract fixtures).
    /// </summary>
    private static bool HasLethalDp(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null ||
            !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) ||
            definition is null ||
            !string.Equals(definition.CardType, "Digimon", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryReadInt(instance.Metadata, BattleResolver.DpKey, out int baseDp) &&
            !TryReadInt(definition.Metadata, BattleResolver.DpKey, out baseDp))
        {
            return false; // no defined DP -> not subject to the DP<=0 rule
        }

        IReadOnlyList<DpModifier> modifiers =
            instance.Metadata.TryGetValue(BattleResolver.DpModifiersKey, out object? raw) && raw is IEnumerable<DpModifier> typed
                ? typed.ToArray()
                : Array.Empty<DpModifier>();

        return DpCalculator.ComputeDp(baseDp, modifiers) <= 0;
    }

    private static bool TryReadInt(IReadOnlyDictionary<string, object?> metadata, string key, out int value)
    {
        value = 0;
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        switch (raw)
        {
            case int i: value = i; return true;
            case long l when l >= int.MinValue && l <= int.MaxValue: value = (int)l; return true;
            case double d when d % 1 == 0 && d is >= int.MinValue and <= int.MaxValue: value = (int)d; return true;
            case string s when int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int p): value = p; return true;
            default: return false;
        }
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
    /// (X-05 + D-3) Drives event-driven trigger collection then resolves the scheduler. Every pending
    /// game event is collected into one batch of triggers (across all derived timings), which is then
    /// ordered by <see cref="MandatoryEffectOrdering"/> — turn-player triggers first, then non-turn,
    /// mandatory before optional — before enqueuing, mirroring the AS-IS simultaneous-trigger rule.
    /// <para>
    /// LIMITATION (D-3/D-4): optional ("you may") triggers are currently enqueued (and so auto-resolve)
    /// AFTER the mandatory ones, rather than being surfaced as an agent choice. They are no longer
    /// dropped, but a full optional-trigger-as-agent-decision mechanism (DeferredChoiceProvider for
    /// triggers) is deferred to the Phase 4 effect work.
    /// </para>
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
            var batch = new List<TimingWindowTrigger>();
            var seen = new HashSet<HeadlessEntityId>();
            foreach (GameEvent gameEvent in pendingEvents)
            {
                if (gameEvent.Type == GameEventType.Unknown)
                {
                    continue;
                }

                foreach (TimingWindowTrigger trigger in collector.CollectAllTriggers(gameEvent))
                {
                    if (seen.Add(trigger.Request.EffectId))
                    {
                        batch.Add(trigger);
                    }
                }
            }

            collected = EnqueueOrdered(context, batch);
        }

        IReadOnlyList<EffectResult> results = await context.EffectScheduler
            .ResolveAllAsync(cancellationToken)
            .ConfigureAwait(false);

        return (results.Count(result => result.Resolved), collected);
    }

    /// <summary>
    /// (D-3) Enqueues a batch of simultaneously-collected triggers in resolution order: ordered
    /// mandatory (turn-player first), then optional, then any with an unknown controller. Returns the
    /// number enqueued.
    /// </summary>
    private static int EnqueueOrdered(EngineContext context, IReadOnlyList<TimingWindowTrigger> batch)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        HeadlessTurnState turn = context.TurnController.Current;
        HeadlessPlayerId turnPlayer = turn.TurnPlayerId ?? default;

        // No established turn player -> preserve collection order (cannot apply player priority).
        if (turnPlayer.IsEmpty)
        {
            foreach (TimingWindowTrigger trigger in batch)
            {
                context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
            }

            return batch.Count;
        }

        HeadlessPlayerId? nonTurnPlayer = null;
        foreach (HeadlessPlayerId candidate in turn.PlayerOrder)
        {
            if (!candidate.IsEmpty && candidate != turnPlayer)
            {
                nonTurnPlayer = candidate;
                break;
            }
        }

        MandatoryEffectOrderResult ordering = new MandatoryEffectOrdering().Order(batch, turnPlayer, nonTurnPlayer);
        if (!ordering.IsSuccess)
        {
            foreach (TimingWindowTrigger trigger in batch)
            {
                context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
            }

            return batch.Count;
        }

        int enqueued = 0;
        foreach (TimingWindowTrigger trigger in ordering.OrderedMandatoryTriggers)
        {
            context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
            enqueued++;
        }

        // D-3/D-4 limitation: optional triggers auto-resolve after mandatory (not yet agent-gated).
        foreach (TimingWindowTrigger trigger in ordering.DeferredOptionalTriggers)
        {
            context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
            enqueued++;
        }

        foreach (TimingWindowTrigger trigger in ordering.UnknownPlayerTriggers)
        {
            context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
            enqueued++;
        }

        return enqueued;
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

    /// <summary>(GPT-#3) The loop hit <see cref="GameFlowProcessor.MaxIterations"/> while still making
    /// progress — it did NOT reach a stable fixpoint. Signals a probable runaway trigger loop.</summary>
    MaxIterationsExceeded,
}

public sealed record FlowProcessResult(
    FlowProcessStatus Status,
    bool ProgressedAny,
    int ResolvedEffectCount,
    int Iterations)
{
    public bool PausedForChoice => Status == FlowProcessStatus.PausedForChoice;

    public bool IsStable => Status == FlowProcessStatus.Stable;

    public bool IsMaxIterationsExceeded => Status == FlowProcessStatus.MaxIterationsExceeded;

    public static FlowProcessResult Stable(bool progressedAny, int resolvedEffectCount, int iterations)
    {
        return new FlowProcessResult(FlowProcessStatus.Stable, progressedAny, resolvedEffectCount, iterations);
    }

    public static FlowProcessResult Paused(bool progressedAny, int resolvedEffectCount, int iterations)
    {
        return new FlowProcessResult(FlowProcessStatus.PausedForChoice, progressedAny, resolvedEffectCount, iterations);
    }

    public static FlowProcessResult MaxIterationsExceeded(bool progressedAny, int resolvedEffectCount, int iterations)
    {
        return new FlowProcessResult(FlowProcessStatus.MaxIterationsExceeded, progressedAny, resolvedEffectCount, iterations);
    }
}
