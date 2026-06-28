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
    private readonly DeletionReplacementTiming _deletionReplacement = new();

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

            // F-6.8: before the state-based sweep finishes any deferred deletion, open the would-be-deleted
            // replacement window for cards that carry an OPTIONAL replacement keyword, so the owner decides
            // (activate / skip) instead of it being auto-applied. Opening a choice pauses the loop.
            if (_deletionReplacement.RequestChoice(context))
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
        var deletionReplacement = new DeletionReplacementTiming();
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

                    // F-6.8: a card still awaiting its owner's would-be-deleted replacement decision is not
                    // swept yet — the deletion-replacement window resolves first (activate clears the flag,
                    // skip marks it declined so the next sweep finishes it). A BATTLE-deferred card
                    // (deletedByBattle) is finalized by BattleResolver.FinalizeDeferredAsync, never swept here.
                    if (pending && (deletionReplacement.IsPreAwaiting(context, cardId) || IsBattleDeferred(context, cardId)))
                    {
                        continue;
                    }

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

        // N-2 / D-A1: the DP<=0 deletion rule also reflects continuous DP effects (a continuous -DP that
        // drops the card to 0 deletes it), mirroring the original CanBeDestroyed-via-DP check. No-op until
        // continuous DP effects are registered.
        int staticDp = DpCalculator.ComputeDp(baseDp, modifiers);
        return ContinuousDpGate.ResolveDp(context, cardId, staticDp) <= 0;
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

    /// <summary>(F-6.8) A pending-deletion card flagged <c>deletedByBattle</c> is a deferred battle casualty
    /// finalized by <see cref="BattleResolver.FinalizeDeferredAsync"/>, not by the state-based sweep.</summary>
    private static bool IsBattleDeferred(EngineContext context, HeadlessEntityId cardId) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) && instance is not null &&
        instance.Metadata.TryGetValue(BattleResolver.DeletedByBattleKey, out object? raw) && raw is bool flag && flag;

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
    /// (X-05 + D-3 + #2) Drives event-driven trigger collection then resolves the scheduler. Pending
    /// game events are collected into one batch, each trigger's Kind reclassified from the bound
    /// effect's <see cref="Effects.CardEffectDefinition.IsOptional"/>, then ordered by
    /// <see cref="MandatoryEffectOrdering"/> (turn-player first, mandatory before optional).
    /// <para>
    /// MANDATORY triggers are enqueued and resolve immediately. OPTIONAL ("you may") triggers are NOT
    /// auto-resolved: they go to the <see cref="EngineContext.OptionalPromptQueue"/> (turn player's
    /// first) and surface as an agent ResolveChoice decision (activate-which / skip), exactly like the
    /// AS-IS optional-trigger prompt. Opening a prompt pauses the loop via the pending choice.
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
                    if (!seen.Add(trigger.Request.EffectId))
                    {
                        continue;
                    }

                    // F-4: gate once-per-turn / max-count-per-turn effects. An effect bound with a
                    // CardEffectDefinition.MaxCountPerTurn cap that has already activated its limit this
                    // turn is skipped; passing effects register a use. Effects without a cap always pass.
                    int? maxCountPerTurn = context.EffectRegistry.Find(trigger.Request.EffectId)?.Effect?.Definition.MaxCountPerTurn;
                    if (!context.OnceFlags.TryActivate(trigger.Request, maxCountPerTurn))
                    {
                        continue;
                    }

                    batch.Add(ReclassifyKind(context, trigger));
                }
            }

            collected = EnqueueOrdered(context, batch);
        }

        IReadOnlyList<EffectResult> results = await context.EffectScheduler
            .ResolveAllAsync(cancellationToken)
            .ConfigureAwait(false);

        // #2: after mandatory effects resolve, surface the next queued optional-trigger prompt to the
        // agent. Counts as progress so the loop re-iterates and pauses on the now-pending choice.
        bool openedPrompt = RequestNextOptionalPrompt(context);

        return (results.Count(result => result.Resolved), collected + (openedPrompt ? 1 : 0));
    }

    /// <summary>
    /// (#2) A trigger's mandatory/optional Kind is authoritative from the bound effect's
    /// <c>Definition.IsOptional</c> when the effect is registered with a body; otherwise the
    /// collection-time Kind (event metadata) is kept.
    /// </summary>
    private static TimingWindowTrigger ReclassifyKind(EngineContext context, TimingWindowTrigger trigger)
    {
        if (context.EffectRegistry.Find(trigger.Request.EffectId)?.Effect is { } effect)
        {
            TimingWindowTriggerKind kind = effect.Definition.IsOptional
                ? TimingWindowTriggerKind.Optional
                : TimingWindowTriggerKind.Mandatory;
            if (trigger.Kind != kind)
            {
                return new TimingWindowTrigger(trigger.Request, trigger.Mode, kind, trigger.Priority, trigger.Sequence);
            }
        }

        return trigger;
    }

    /// <summary>
    /// (D-3 + #2) Enqueues mandatory triggers (turn-player first) to resolve immediately, and routes
    /// optional triggers to the <see cref="OptionalPromptQueue"/> grouped by controller (turn player
    /// first) so they become an agent decision. Returns the number of triggers handled.
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

        int handled = 0;

        // Mandatory (turn-player first) + unknown-controller triggers resolve immediately.
        foreach (TimingWindowTrigger trigger in ordering.OrderedMandatoryTriggers)
        {
            context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
            handled++;
        }

        foreach (TimingWindowTrigger trigger in ordering.UnknownPlayerTriggers)
        {
            context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
            handled++;
        }

        // #2: optional triggers -> agent prompt, grouped by controller (turn player's prompt first).
        handled += QueueOptionalPrompts(context, ordering.DeferredOptionalTriggers, turnPlayer, nonTurnPlayer);

        return handled;
    }

    /// <summary>(#2) Enqueues one optional-trigger prompt per controller (turn player first) into the
    /// OptionalPromptQueue. Returns the number of optional triggers queued.</summary>
    private static int QueueOptionalPrompts(
        EngineContext context,
        IReadOnlyList<TimingWindowTrigger> optionalTriggers,
        HeadlessPlayerId turnPlayer,
        HeadlessPlayerId? nonTurnPlayer)
    {
        if (optionalTriggers.Count == 0)
        {
            return 0;
        }

        int queued = 0;
        foreach (HeadlessPlayerId? player in new[] { (HeadlessPlayerId?)turnPlayer, nonTurnPlayer })
        {
            if (player is not { } owner || owner.IsEmpty)
            {
                continue;
            }

            TimingWindowTrigger[] forPlayer = optionalTriggers
                .Where(trigger => trigger.Request.ControllerId == owner)
                .ToArray();
            if (forPlayer.Length == 0)
            {
                continue;
            }

            context.OptionalPromptQueue.EnqueuePrompt(forPlayer, owner);
            queued += forPlayer.Length;
        }

        return queued;
    }

    /// <summary>(#2) Opens the next queued optional-trigger prompt as a pending choice (if any and no
    /// choice is already pending). Returns true when a prompt was opened.</summary>
    private static bool RequestNextOptionalPrompt(EngineContext context)
    {
        if (!context.OptionalPromptQueue.HasPendingPrompt || context.ChoiceController.Current.IsPending)
        {
            return false;
        }

        return context.OptionalPromptQueue.RequestNextChoice(context.ChoiceController).IsSuccess;
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
