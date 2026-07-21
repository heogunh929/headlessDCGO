namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (F-4) Match-scoped holder for once-per-turn / max-count-per-turn effect activation. The data layer
/// is <see cref="OnceFlagHelpers"/> (immutable <see cref="OnceFlagState"/>); this is the mutable holder
/// the trigger loop consults so an effect bound with <c>CardEffectDefinition.MaxCountPerTurn</c> does not
/// activate more than its cap allows in a turn. Mirrors the original CardController use-count tracking
/// (<c>isOverMaxCountPerTurn</c> + <c>InitUseCountThisTurn</c>).
/// </summary>
public sealed class OnceFlagController : IHeadlessMatchStateResettable
{
    private OnceFlagState _state = OnceFlagState.Empty;

    // (B-1 rework) Resolution-cycle TRANSACTION for the uniform activated path. AS-IS registers a use BEFORE the
    // body runs (register-before-body: TurnStateMachine.cs:1183-1186 / ICardEffect.cs:1119-1121 /
    // AutoProcessing.cs:902...). The headless resume model re-invokes the WHOLE resolution from the top after a
    // suspend, replaying recorded answers (DeferredChoiceProvider) — so a use consumed before the body must be
    // REPLAYED on the re-run, not double-registered, and must not be read back as capped-out by the re-run's own
    // gate (that mis-read was the original B-1 bug; flipping to consume-AFTER-body "fixed" it but inverted the
    // AS-IS order and broke the multi-effect suspend case, where an earlier effect's committed consume outlived
    // its discarded sink mutations). The transaction mirrors the window path's InFlightPick idempotent-replay:
    //   - Consume STAGES a use in _pending (per key) instead of committing;
    //   - a re-invocation resets the per-key _replayCursor; a Consume whose cursor lags the staged count is a
    //     REPLAY of an earlier consume (cursor advances, nothing staged);
    //   - CanActivate reads state + cursor while an invocation is executing (the gate sees exactly what it saw
    //     the first time) and state + pending while SUSPENDED (an outside observer — legal actions, other gates —
    //     sees the cap consumed, matching the AS-IS mid-body view);
    //   - the owning invocation COMMITS all staged uses on completion (before the sink flush emits events) or
    //     keeps them staged across a suspend; a non-choice failure aborts the stage (nothing consumed).
    private readonly Dictionary<string, StagedUse> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _replayCursor = new(StringComparer.Ordinal);
    private bool _cycleOpen;
    private bool _cycleSuspended;
    private bool _invocationActive;

    // (R6-Da'-6, D1=A) The mutation-replay journal that used to live here (BeginMutationApply / RecordFreshMutation
    // / _mutationJournal) MOVED to CEntityUseCycle (CEntity_EffectController.cs) — it is model-INDEPENDENT shared
    // substrate (every resolution path consumes it, not just the uniform-cap path) so it belongs on the surviving
    // cycle. The two cycles run in exact lockstep, so the move is behaviour-identical. This holder now owns ONLY the
    // uniform-cap partition (CanActivate/Consume/Refund) + its resolution-cycle transaction, which remain live while
    // uniform ActivatedEffect production is non-zero (retired at corpus deletion / R6-Db).

    private sealed class StagedUse
    {
        public StagedUse(OnceFlagKey key, int maxCount)
        {
            Key = key;
            MaxCount = maxCount;
        }

        public OnceFlagKey Key { get; }

        public int MaxCount { get; }

        public int Count { get; set; }
    }

    public OnceFlagState State => _state;

    /// <summary>Open (or resume) the uniform-resolution transaction. Returns <c>true</c> when this invocation
    /// OWNS the cycle — a fresh open, or the re-invocation resuming a suspended cycle (per-key replay cursors are
    /// reset so staged consumes replay in order, like the answer cursor in
    /// <see cref="Runtime.DeferredChoiceProvider.BeginResolution"/>). Returns <c>false</c> for a nested
    /// resolution inside an already-executing cycle (it shares the open transaction).</summary>
    public bool BeginUniformCycle()
    {
        if (!_cycleOpen)
        {
            _cycleOpen = true;
            _cycleSuspended = false;
            _pending.Clear();
            _replayCursor.Clear();
            _invocationActive = true;
            return true;
        }

        if (_cycleSuspended)
        {
            _cycleSuspended = false;
            _replayCursor.Clear();
            _invocationActive = true;
            return true;
        }

        return false;
    }

    /// <summary>Mark the owning invocation suspended (an agent choice is pending). Staged consumes stay staged;
    /// outside observers now read them as consumed (<see cref="CanActivate"/>'s suspended view).</summary>
    public void SuspendUniformCycle(bool owner)
    {
        if (!owner)
        {
            return;
        }

        _cycleSuspended = true;
        _invocationActive = false;
    }

    /// <summary>Commit every staged use to the real per-turn counts and close the transaction. Call before the
    /// sink flush so any window the flushed events open reads committed caps.</summary>
    public void CompleteUniformCycle(bool owner)
    {
        if (!owner)
        {
            return;
        }

        foreach (StagedUse staged in _pending.Values)
        {
            for (int i = 0; i < staged.Count; i++)
            {
                OnceFlagResult registered = OnceFlagHelpers.RegisterUse(_state, staged.Key, staged.MaxCount);
                if (registered.IsSuccess)
                {
                    _state = registered.State;
                }
            }
        }

        CloseCycle();
    }

    /// <summary>Abandon the transaction WITHOUT committing (a non-choice failure aborted the resolution — nothing
    /// was applied, so nothing is consumed).</summary>
    public void AbortUniformCycle(bool owner)
    {
        if (!owner)
        {
            return;
        }

        CloseCycle();
    }

    private void CloseCycle()
    {
        _pending.Clear();
        _replayCursor.Clear();
        _cycleOpen = false;
        _cycleSuspended = false;
        _invocationActive = false;
    }

    /// <summary>Uses of <paramref name="key"/> this open cycle that the current view should count on top of the
    /// committed state: the replay cursor while executing (what the gate saw at this point of the original run),
    /// the full staged count while suspended or for outside observers.</summary>
    private int CycleExtra(string key)
    {
        if (!_cycleOpen)
        {
            return 0;
        }

        if (_invocationActive)
        {
            return _replayCursor.TryGetValue(key, out int cursor) ? cursor : 0;
        }

        return _pending.TryGetValue(key, out StagedUse? staged) ? staged.Count : 0;
    }

    /// <summary>Reset the per-turn use counts for a new turn (original <c>InitUseCountThisTurn</c>).</summary>
    public void ResetForTurn(long turnSequence, HeadlessPlayerId? turnPlayerId)
    {
        OnceFlagResult result = OnceFlagHelpers.ResetTurn(_state, turnSequence < 0 ? 0 : turnSequence, turnPlayerId);
        if (result.IsSuccess)
        {
            _state = result.State;
        }
    }

    /// <summary>(B-3 / P1-6) Reset the per-turn use counts of a SINGLE card (all its effects) — AS-IS
    /// <c>CardSource.Init()</c>, run when the card gets a fresh play context (played / replayed / de-digivolved /
    /// re-stacked). Leaves every other card's counts intact.</summary>
    public void ResetForCard(HeadlessPlayerId owner, HeadlessEntityId cardInstanceId)
    {
        _state = OnceFlagHelpers.ResetForCard(_state, owner, cardInstanceId);
    }

    /// <summary>
    /// Gate one activation of <paramref name="request"/>. An effect with no per-turn cap
    /// (<paramref name="maxCountPerTurn"/> is null) always passes. When capped, returns <c>false</c> if
    /// the cap is already reached this turn; otherwise registers the use and returns <c>true</c>.
    /// </summary>
    public bool TryActivate(EffectRequest request, int? maxCountPerTurn)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CanActivate(request, maxCountPerTurn))
        {
            return false;
        }

        Consume(request, maxCountPerTurn);
        return true;
    }

    /// <summary>(RD-12/13) Whether the effect is still under its per-turn cap — WITHOUT consuming a use. Used to
    /// gate an OPTIONAL effect's yes/no prompt (don't offer a capped-out effect) so the actual use is registered
    /// only at execution (after the player accepts), mirroring AS-IS RegisterUseEffectThisTurn firing in the
    /// effect's OnProcess callback, not at collection.</summary>
    public bool CanActivate(EffectRequest request, int? maxCountPerTurn)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (maxCountPerTurn is not int max)
        {
            return true;
        }

        OnceFlagKey key = OnceFlagHelpers.ForRequest(request);
        return _state.GetUseCount(key) + CycleExtra(key.Value) < max;
    }

    /// <summary>(RD-12, B-1 rework) Register one use of the effect's per-turn cap (the AS-IS
    /// <c>RegisterUseEffectThisTurn</c> step) — BEFORE the body, mirroring AS-IS register-before-body. Inside an
    /// open uniform cycle the use is STAGED (transaction, replay-aware); outside a cycle (the window path's
    /// commit) it commits directly, as before.</summary>
    public void Consume(EffectRequest request, int? maxCountPerTurn)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (maxCountPerTurn is not int max)
        {
            return;
        }

        OnceFlagKey key = OnceFlagHelpers.ForRequest(request);
        if (!_cycleOpen || !_invocationActive)
        {
            OnceFlagResult registered = OnceFlagHelpers.RegisterUse(_state, key, max);
            if (registered.IsSuccess)
            {
                _state = registered.State;
            }

            return;
        }

        int cursor = _replayCursor.TryGetValue(key.Value, out int c) ? c : 0;
        int staged = _pending.TryGetValue(key.Value, out StagedUse? use) ? use.Count : 0;
        if (cursor < staged)
        {
            // Replay of a consume the suspended run already staged — advance the cursor, stage nothing.
            _replayCursor[key.Value] = cursor + 1;
            return;
        }

        if (use is null)
        {
            use = new StagedUse(key, max);
            _pending[key.Value] = use;
        }

        use.Count++;
        _replayCursor[key.Value] = cursor + 1;
    }

    /// <summary>(B-4 rework) AS-IS <c>RemoveUse()</c> — refund ONE registered use. PER-CARD OPT-IN (an AS-IS card
    /// body's explicit <c>if (!executed) RemoveUse()</c>, ~38 cards), never a default: an unmarked effect keeps
    /// its use consumed even when the body does nothing. Cycle-aware: a refund inside the open cycle removes the
    /// most recent staged use for the key (so a suspended run's consume+refund pair replays to the same net).</summary>
    public void Refund(EffectRequest request, int? maxCountPerTurn)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (maxCountPerTurn is null)
        {
            return;
        }

        OnceFlagKey key = OnceFlagHelpers.ForRequest(request);
        if (_cycleOpen && _invocationActive && _pending.TryGetValue(key.Value, out StagedUse? use) && use.Count > 0)
        {
            use.Count--;
            if (_replayCursor.TryGetValue(key.Value, out int cursor) && cursor > use.Count)
            {
                _replayCursor[key.Value] = use.Count;
            }

            return;
        }

        OnceFlagResult removed = OnceFlagHelpers.RemoveUse(_state, key);
        if (removed.IsSuccess)
        {
            _state = removed.State;
        }
    }

    public void ResetMatchState()
    {
        _state = OnceFlagState.Empty;
        CloseCycle();
    }
}
