namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Single drainable source of <see cref="GameEvent"/>s for auto-processing trigger collection
/// (issue X-05). Mirrors Unity AS-IS <c>AutoProcessing.StackSkillInfos</c>, which inspects game
/// state changes to stack timing skills: the common loop (<see cref="GameFlowProcessor"/>) drains
/// this queue every auto-process pass and feeds each event to
/// <see cref="HeadlessDCGO.Engine.Headless.Effects.AutoProcessingTriggerCollector"/>.
///
/// Two population paths:
/// <list type="bullet">
/// <item>Direct <see cref="Publish"/> for components (and tests) that synthesize trigger events.</item>
/// <item><see cref="SyncFrom"/> bridges an append-only event log (e.g. <see cref="IZoneMover.Events"/>)
/// so card movements automatically open trigger windows without each producer wiring the queue.</item>
/// </list>
/// </summary>
public sealed class GameEventQueue : IHeadlessMatchStateResettable
{
    private readonly Queue<GameEvent> _pending = new();

    /// <summary>
    /// High-water mark (count) of events already consumed from the most recent <see cref="SyncFrom"/>
    /// source. The source is append-only within a match, so a count cursor is sufficient and is reset
    /// together with the queue.
    /// </summary>
    private int _syncCursor;

    public int PendingCount => _pending.Count;

    public void Publish(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        _pending.Enqueue(gameEvent);
    }

    /// <summary>
    /// Appends events produced by an append-only source (such as <see cref="IZoneMover.Events"/>)
    /// that have not been consumed yet. Idempotent across repeated passes within a match: only events
    /// beyond the internal cursor are enqueued, so the same move is never collected twice.
    /// </summary>
    public int SyncFrom(IReadOnlyList<GameEvent> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        int appended = 0;
        for (int index = _syncCursor; index < source.Count; index++)
        {
            GameEvent gameEvent = source[index];
            if (gameEvent is null)
            {
                continue;
            }

            _pending.Enqueue(gameEvent);
            appended++;
        }

        if (source.Count > _syncCursor)
        {
            _syncCursor = source.Count;
        }

        return appended;
    }

    /// <summary>Removes and returns every pending event in publish order.</summary>
    public IReadOnlyList<GameEvent> DrainPending()
    {
        if (_pending.Count == 0)
        {
            return Array.Empty<GameEvent>();
        }

        var drained = new GameEvent[_pending.Count];
        for (int index = 0; index < drained.Length; index++)
        {
            drained[index] = _pending.Dequeue();
        }

        return drained;
    }

    public void ResetMatchState()
    {
        _pending.Clear();
        _syncCursor = 0;
    }
}
