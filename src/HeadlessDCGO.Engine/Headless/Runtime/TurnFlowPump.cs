// (R4 S3a — docs/audit/r4_tsm_s1_design_2026-07-16.md "S3 실행 설계", decision 3 = B)
// ============================================================================================================
// The AS-IS-cadence turn-flow driver: a single long-running async PUMP that executes the mirror
// TurnStateMachine phase bodies CONTINUOUSLY (StartGame → {Active → Draw → Breeding → Main → End → flip} loop),
// exactly like the AS-IS coroutine chain, pausing ONLY at the already-externalized interactive stops
// (mulligan choice, in-body window/effect choices, the breeding decision, the main-phase action wait).
//
// SUSPENSION MODEL — the await-gate (TurnFlowGate): the async equivalent of the AS-IS
// `yield return new WaitUntil(...)`. A park returns control synchronously to the host (TurnFlowPumpTask);
// when an agent action satisfies the park condition the gate completes its TaskCompletionSource INLINE and the
// pump resumes IN PLACE — the C# async continuation preserves the body's stack frame across the park, which is
// the exact analogue of a parked Unity coroutine frame. This is why the pump needs NO re-entrancy, NO body
// slicing and NO record-replay: decision 1 (non-reentrant 1:1 bodies) and decision 3 (AS-IS cadence) compose.
//
// While a pump segment is executing (TurnFlowPumpHost.IsPumpExecuting), the choice pipeline's two suspension
// points switch from the THROW contract (WindowChoicePendingException / DeferredChoicePendingException — the
// stack-unwind model built for the RunToStable drive) to AWAIT-mode: the port/provider opens the choice, marks
// it pump-owned, and awaits the gate; MetadataActionProcessor.ResolveChoiceAsync deposits the agent's answer on
// the host instead of driving the record-replay resume. Windows and effect bodies therefore resolve their
// choices IN PLACE on the pump stack — the AS-IS in-coroutine wait, with zero body re-runs.
//
// INSTALLATION (S3a): injectable only — TurnFlowPumpHost.Install(context) enqueues the pump task on the
// existing Context.TaskRunner, which HeadlessGameLoop.StepAsync already steps once per agent step. Matches
// without Install are byte-for-byte unaffected (the host service is simply absent). The default (OLD) driver
// stays live until the S3c cutover approval.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Coroutines;
using HeadlessDCGO.Engine.Headless.Effects;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>The pump's await-gate — the async equivalent of the AS-IS <c>WaitUntil</c>. Single park slot
/// (the pump is one logical coroutine; AS-IS never waits on two conditions at once).</summary>
public sealed class TurnFlowGate
{
    private sealed record Park(Func<bool> Condition, TaskCompletionSource Source);

    private Park? _park;

    public bool IsParked => _park is not null;

    /// <summary>The parked condition, exposed for the host's <see cref="EngineWaitCondition"/>.</summary>
    public Func<bool>? ParkCondition => _park?.Condition;

    /// <summary>AS-IS <c>yield return new WaitUntil(condition)</c>: continue immediately when already
    /// satisfied, otherwise park until the host releases.</summary>
    public Task WaitUntilAsync(Func<bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        if (condition())
        {
            return Task.CompletedTask;
        }

        if (_park is not null)
        {
            throw new InvalidOperationException(
                "TurnFlowGate already holds a park — the pump is a single coroutine and cannot wait twice.");
        }

        Park park = new(condition, new TaskCompletionSource());
        _park = park;
        return park.Source.Task;
    }

    /// <summary>Release the park when its condition holds. The TaskCompletionSource completes INLINE, so the
    /// pump advances synchronously on the caller's stack until it completes or parks again.</summary>
    public bool TryRelease()
    {
        if (_park is { } park && park.Condition())
        {
            _park = null;
            park.Source.SetResult();
            return true;
        }

        return false;
    }
}

/// <summary>Per-match pump state (EngineContext service): the gate, the executing marker the choice pipeline
/// branches on, and the pump-owned choice deposit slot the ResolveChoice action fills.</summary>
public sealed class TurnFlowPumpHost
{
    private ChoiceResult? _depositedAnswer;

    public TurnFlowGate Gate { get; } = new();

    /// <summary>True while a pump segment is running (set by <see cref="TurnFlowPumpTask.StepAsync"/> around
    /// the start/release call). The choice pipeline (DeferredChoiceProvider / AgentSkillWindowChoicePort) uses
    /// this to pick AWAIT-mode over the throw contract; a RunToStable-driven window in the SAME match sees
    /// false and keeps the unwind model.</summary>
    public bool IsPumpExecuting { get; internal set; }

    /// <summary>True when the currently-pending agent choice was opened ON the pump stack — its resolution
    /// must be DEPOSITED here (not driven through the record-replay resume machinery).</summary>
    public bool HasPendingPumpChoice { get; private set; }

    public bool HasDepositedAnswer => _depositedAnswer is not null;

    public void MarkPumpChoice()
    {
        HasPendingPumpChoice = true;
        _depositedAnswer = null;
    }

    /// <summary>Called by the ResolveChoice action seam: hand the parsed agent answer to the parked pump.</summary>
    public void DepositAnswer(ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!HasPendingPumpChoice)
        {
            throw new InvalidOperationException("No pump-owned choice is pending.");
        }

        HasPendingPumpChoice = false;
        _depositedAnswer = result;
    }

    public ChoiceResult TakeDepositedAnswer()
    {
        ChoiceResult answer = _depositedAnswer
            ?? throw new InvalidOperationException("No deposited pump-choice answer to take.");
        _depositedAnswer = null;
        return answer;
    }

    /// <summary>Install the pump into a match: register the host service and enqueue the pump task on the
    /// existing <see cref="EngineContext.TaskRunner"/> (stepped once per HeadlessGameLoop step). Idempotent.</summary>
    public static TurnFlowPumpHost Install(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TryGetService(out TurnFlowPumpHost? existing) && existing is not null)
        {
            return existing;
        }

        return Reinstall(context);
    }

    /// <summary>(R4 S3c-d1) Fresh install for a match (re)initialization: HeadlessGameLoop.Reset cleared
    /// the TaskRunner (any previous pump task died with it), so a stale host must NOT be reused — register
    /// a NEW host (service registration overwrites) and enqueue a NEW pump task. First-time install
    /// degenerates to <see cref="Install"/>.</summary>
    public static TurnFlowPumpHost Reinstall(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        TurnFlowPumpHost host = new();
        context.RegisterService(host);
        context.TaskRunner.Enqueue(new TurnFlowPumpTask(context, host));
        return host;
    }

    /// <summary>The host when installed on this match, else null (= OLD-driver match, zero behavior change).</summary>
    public static TurnFlowPumpHost? Find(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.TryGetService(out TurnFlowPumpHost? host) ? host : null;
    }

    /// <summary>The host of the AMBIENT match when a pump segment is currently executing — the discriminator
    /// the choice pipeline uses (ambient-scoped because the provider/port do not hold an EngineContext).</summary>
    public static TurnFlowPumpHost? FindExecuting()
    {
        return AmbientMatchContext.Current is { } context
            && Find(context) is { IsPumpExecuting: true } host
            ? host
            : null;
    }
}

/// <summary>The <see cref="IEngineTask"/> hosting the pump on the existing task-runner pump
/// (HeadlessGameLoop.StepAsync line "await Context.TaskRunner.StepAsync"). While parked, the runner skips the
/// task via <see cref="CurrentWait"/>; when the park condition holds, the next step releases the gate and the
/// pump advances inline to its next park (or completion).</summary>
public sealed class TurnFlowPumpTask : IEngineTask
{
    private readonly EngineContext _context;
    private readonly TurnFlowPumpHost _host;
    private Task? _running;

    public TurnFlowPumpTask(EngineContext context, TurnFlowPumpHost host)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public EngineTaskStatus Status { get; private set; } = EngineTaskStatus.Pending;

    public EngineWaitCondition? CurrentWait { get; private set; }

    public Exception? Error { get; private set; }

    public async Task<EngineTaskStepResult> StepAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Every pump segment runs under the ambient match scope (GManager.instance resolution + the
        // FindExecuting discriminator). AsyncLocal flows into the pump's captured continuations, so a segment
        // resumed by TryRelease inherits the scope of the awaiting frame.
        using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(_context);
        _host.IsPumpExecuting = true;
        try
        {
            if (_running is null)
            {
                _running = TurnFlowPump.RunAsync(_context, _host, cancellationToken);
            }
            else
            {
                _host.Gate.TryRelease();
            }
        }
        finally
        {
            _host.IsPumpExecuting = false;
        }

        if (_running.IsCompleted)
        {
            if (_running.Exception is { } aggregate)
            {
                Error = aggregate.InnerException ?? aggregate;
                Status = EngineTaskStatus.Faulted;
                return EngineTaskStepResult.Faulted(Error);
            }

            Status = EngineTaskStatus.Completed;
            return EngineTaskStepResult.Completed();
        }

        if (_host.Gate.ParkCondition is { } condition)
        {
            Status = EngineTaskStatus.Waiting;
            EngineWaitCondition wait = EngineWaitCondition.Until(condition);
            CurrentWait = wait;
            return EngineTaskStepResult.Waiting(wait);
        }

        // The engine's async surface completes synchronously (the determinism the shadow harness proves);
        // a pump that neither completed nor parked awaited something foreign — fail fast, never race.
        throw new InvalidOperationException(
            "TurnFlowPump neither completed nor parked within a step — a non-synchronous await escaped the gate model.");
    }
}

/// <summary>The pump body — the substrate seat of the AS-IS TurnStateMachine driver chain (decision 3 = B).
/// Calls the six mirror phase bodies in the AS-IS order; owns the substrate turn-boundary block (the
/// coordinate-translation work the AS-IS in-body writes imply on the headless substrate).</summary>
public static class TurnFlowPump
{
    public static async Task RunAsync(
        EngineContext context,
        TurnFlowPumpHost host,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(host);

        Cec.TurnStateMachine turnStateMachine = Cec.TurnStateMachine.For(context);
        Cec.GameContext gameContext = turnStateMachine.gameContext;

        // AS-IS StartGame (:341-504): opening hands (non-turn player first) + mulligan begin; the coordinator
        // applies redraws and deals security as each player's choice resolves (the externalized :374-501).
        await turnStateMachine.StartGameAsync(cancellationToken).ConfigureAwait(false);

        // The mulligan decisions resolve via ResolveChoice agent actions -> MulliganCoordinator.ResolveAsync
        // (the existing seam, NOT a pump-owned choice). Park until the whole sequence (including the deferred
        // security deal) completes. AS-IS :503 DoneStartGame=true maps to the first ActivePhase entry below
        // (mirror DoneStartGame = phase past None; nothing rule-relevant sits between :503 and :554).
        await host.Gate.WaitUntilAsync(() =>
            !context.MulliganCoordinator.IsActive && !context.ChoiceController.Current.IsPending)
            .ConfigureAwait(false);

        // AS-IS turn loop: the phase-body chain. Each body self-guards on TurnPhase==End (the AS-IS in-body
        // EndTurnCheck guards), so the chain calls them in order exactly like the original driver.
        while (!turnStateMachine.endGame && !context.RuleQueryService.IsTerminal())
        {
            await turnStateMachine.ActivePhaseAsync(cancellationToken).ConfigureAwait(false);
            if (Ended(turnStateMachine, context)) break;

            await turnStateMachine.DrawPhaseAsync(cancellationToken).ConfigureAwait(false);
            if (Ended(turnStateMachine, context)) break;

            await turnStateMachine.BreedingPhaseAsync(cancellationToken).ConfigureAwait(false);
            if (Ended(turnStateMachine, context)) break;

            await turnStateMachine.MainPhaseAsync(cancellationToken).ConfigureAwait(false);
            if (Ended(turnStateMachine, context)) break;

            await turnStateMachine.EndPhaseAsync(cancellationToken).ConfigureAwait(false);

            // ==== substrate turn boundary (the AS-IS loop-back into ActivePhase with :550 TurnCount++) ====
            // Order mirrors the verified OLD flip block (MetadataActionProcessor.EndTurnAsync): cleanup ran
            // inside EndPhaseAsync (HeadlessEndTurnCleanupFlow, ending player's frame), then the flip, then the
            // new-turn resets.
            HeadlessTurnState newTurn = context.TurnController.EndTurn();
            // (리뷰3 P2-⑦ 착지) AS-IS :550 TurnCount++ side effect: every `EnterFieldTurnCount == TurnCount`
            // comparison (summoning sickness / "entered play this turn" reads) EXPIRES the instant the turn
            // number advances — for EVERY player's permanents at once. The mirror carrier is the boolean
            // `enteredThisTurn` instance flag (Permanent.EnterFieldTurnCount's write surface), which nothing
            // expired on the pump path (the OLD driver's seat is HeadlessEarlyPhaseFlow's per-turn-player
            // clear), so a PLAYED digimon stayed summoning-sick forever — the security-win shadow witness
            // surfaced it (no pump-side attack was ever possible from the real play path).
            ExpireEnteredThisTurnFlags(context, newTurn);
            // F-4: once-per-turn use counts (legacy metadata-key model) reset for the NEW turn.
            context.OnceFlags.ResetForTurn(newTurn.TurnNumber, newTurn.TurnPlayerId);
            // AS-IS :3181 player.DigivolveCount_ThisTurn = 0 — substrate PlayerTurnCounterController owner.
            context.PlayerTurnCounters.ResetForTurn();
            // LA-3: the [All Turns] once-per-turn reactivation guard resets at the boundary.
            OnPlayReactivation.ClearAll(context);
            // F-1.7: leftover "until cost is calculated" one-shot modifiers expire at the boundary.
            EffectDurationExpiry.ExpireFixedCostCalc(context.EffectRegistry);
            // COORDINATE TRANSLATION: the mirror memory gauge is turn-player-relative (AS-IS is seat-absolute,
            // which needs no re-sign at the flip). Re-sign for the incoming player — the exact translation of
            // the flip is unconditional negation Set(-m): |m| is only equivalent for m<=0 and would flip the
            // sign wrong when the turn ends with m>0 (TurnEndMinMemory<0 effects). ADAPTATION (review3 P2-1).
            context.MemoryController.Set(-context.MemoryController.Current.Current);
        }
    }

    private static bool Ended(Cec.TurnStateMachine turnStateMachine, EngineContext context)
    {
        return turnStateMachine.endGame || context.RuleQueryService.IsTerminal();
    }

    /// <summary>(리뷰3 P2-⑦ 착지) The substrate translation of the AS-IS <c>TurnCount++</c> expiry (:550):
    /// with the turn number advanced, no card "entered the field this turn" any more — clear the boolean
    /// <c>enteredThisTurn</c> carrier on every player's field-zone cards (battle + breeding; the flag is only
    /// ever stamped on field entry, and a re-play re-stamps it). ALL players at once, matching the AS-IS
    /// comparison semantics (not only the incoming turn player).</summary>
    private static void ExpireEnteredThisTurnFlags(EngineContext context, HeadlessTurnState newTurn)
    {
        if (context.ZoneMover is not Services.IZoneStateReader zones)
        {
            return;
        }

        foreach (Services.HeadlessPlayerId playerId in newTurn.PlayerOrder)
        {
            foreach (ChoiceZone zone in new[] { ChoiceZone.BattleArea, ChoiceZone.BreedingArea })
            {
                foreach (Services.HeadlessEntityId cardId in zones.GetCards(playerId, zone))
                {
                    if (!context.CardInstanceRepository.TryGetInstance(cardId, out Services.CardInstanceRecord? record)
                        || record is null
                        || !(record.Metadata.TryGetValue(MatchStateMutationSink.EnteredThisTurnKey, out object? raw) && raw is true))
                    {
                        continue;
                    }

                    var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                    {
                        [MatchStateMutationSink.EnteredThisTurnKey] = false,
                    };
                    context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
                }
            }
        }
    }
}
