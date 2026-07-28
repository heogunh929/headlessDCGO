namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (W6-S) The generalisation of P6's <c>sacrificeAwaiting</c> parking: an AS-IS
/// <c>DeletePeremanentAndProcessAccordingToResult</c> continuation must run only after EVERY target's
/// deletion has SETTLED — and a target with a would-be-deleted replacement (Evade …) settles only after its
/// agent choice, across a game-loop pause. The continuation (a Func — never serialised) parks here as a
/// context service; <see cref="SettleAsync"/> runs in the rule/auto-processing loop (next to
/// <c>SettleAwaitingSacrifices</c>) and fires it once all targets are settled: DESTROYED = the card left
/// the battle area (AS-IS <c>DestroyedPermanents</c> membership), SPARED = it is still there with no
/// pending deletion.
/// </summary>
public sealed class DeletionOutcomeWatcher
{
    private sealed record Watch(
        IReadOnlyList<HeadlessEntityId> Targets,
        Func<IReadOnlyList<HeadlessEntityId>, IReadOnlyList<HeadlessEntityId>, Task> OnSettled);

    private readonly List<Watch> _watches = new();

    public int Count => _watches.Count;

    /// <summary>(C-Del 3c-2b) Whether the card is a TARGET of any parked watch. The state-based sweep must
    /// finalize watch targets FIRST (their settling is what fires the parked continuation), while HOLDING every
    /// other pending deletion until the watch settles — AS-IS runs the nested
    /// <c>DeletePeremanentAndProcessAccordingToResult</c> (and its success/failure process, e.g. Scapegoat's
    /// "spare the holder when the sacrifice died") INLINE inside the enclosing cut-in body, i.e. BEFORE the
    /// enclosing Destroy() fixes its survivors. Without the hold, the sweep trashes the enclosing holder in the
    /// same pass as the sacrifice, before the parked successProcess can clear its <c>willBeRemoveField</c>.</summary>
    public bool IsWatchTarget(HeadlessEntityId cardId)
    {
        foreach (Watch watch in _watches)
        {
            if (watch.Targets.Contains(cardId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Park a continuation until every target settles. <paramref name="onSettled"/> receives
    /// (destroyed, spared).</summary>
    public void Register(
        IReadOnlyList<HeadlessEntityId> targets,
        Func<IReadOnlyList<HeadlessEntityId>, IReadOnlyList<HeadlessEntityId>, Task> onSettled)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(onSettled);
        _watches.Add(new Watch(targets.ToArray(), onSettled));
    }

    /// <summary>Fire every watch whose targets have ALL settled. Returns true when any fired.</summary>
    public async Task<bool> SettleAsync(EngineContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_watches.Count == 0 || context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        bool fired = false;
        for (int index = _watches.Count - 1; index >= 0; index--)
        {
            Watch watch = _watches[index];
            var destroyed = new List<HeadlessEntityId>();
            var spared = new List<HeadlessEntityId>();
            bool allSettled = true;
            foreach (HeadlessEntityId target in watch.Targets)
            {
                switch (Classify(context, zones, target))
                {
                    case Outcome.Destroyed:
                        destroyed.Add(target);
                        break;
                    case Outcome.Spared:
                        spared.Add(target);
                        break;
                    default:
                        allSettled = false;
                        break;
                }

                if (!allSettled)
                {
                    break;
                }
            }

            if (!allSettled)
            {
                continue;
            }

            _watches.RemoveAt(index);
            await watch.OnSettled(destroyed, spared).ConfigureAwait(false);
            fired = true;
            cancellationToken.ThrowIfCancellationRequested();
        }

        return fired;
    }

    private enum Outcome
    {
        Pending,
        Destroyed,
        Spared,
    }

    private static Outcome Classify(EngineContext context, IZoneStateReader zones, HeadlessEntityId target)
    {
        if (!context.CardInstanceRepository.TryGetInstance(target, out CardInstanceRecord? record) || record is null)
        {
            return Outcome.Destroyed;   // instance gone = it left the field
        }

        // (GAMEFLOW re-migration) the pending-deletion flag constant is re-homed onto the mirror `Permanent`
        // (its AS-IS home is the Permanent deletion bookkeeping); string value "pendingDeletion" unchanged.
        if (record.Metadata.TryGetValue(
                Assets.Scripts.Script.CardEffectCommons.Permanent.PendingDeletionMetadataKey, out object? pending)
            && pending is true)
        {
            return Outcome.Pending;     // its would-be-deleted window has not resolved yet
        }

        // AS-IS success = DestroyedPermanents membership (it actually LEFT the field); a card still on the
        // battle area with no pending deletion was spared (replacement fired / deletion prevented).
        return zones.GetCards(record.OwnerId, ChoiceZone.BattleArea).Contains(target)
            ? Outcome.Spared
            : Outcome.Destroyed;
    }
}
