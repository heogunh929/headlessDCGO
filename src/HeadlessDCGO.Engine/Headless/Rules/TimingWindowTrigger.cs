namespace HeadlessDCGO.Engine.Headless.Rules;

using HeadlessDCGO.Engine.Headless.Effects;

public sealed record TimingWindowTrigger
{
    public TimingWindowTrigger(
        EffectRequest request,
        EffectResolutionMode mode,
        TimingWindowTriggerKind kind,
        int priority,
        long sequence)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Effect resolution mode must be a known value.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Timing window trigger kind must be a known value.");
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Timing window trigger sequence must not be negative.");
        }

        Request = request;
        Mode = mode;
        Kind = kind;
        Priority = priority;
        Sequence = sequence;
    }

    public EffectRequest Request { get; init; }

    public EffectResolutionMode Mode { get; init; }

    public TimingWindowTriggerKind Kind { get; init; }

    public int Priority { get; init; }

    public long Sequence { get; init; }

    /// <summary>(D-1 order) The DELETE-BATCH id of the deletion (<c>DestroyPermanentsClass.Destroy()</c>) that
    /// drives this trigger — set ONLY on deletion-derived triggers (OnDestroyedAnyone / OnLeaveFieldAnyone) by the
    /// window wiring, from the driving CardMoved event's <c>MatchStateMutationSink.DeletionBatchIdKey</c>. NULL for
    /// every other trigger (non-deletion) and for an unstamped deletion (sentinel batch 0). The window loop uses it
    /// to process CROSS-batch deletion triggers in ASCENDING batch order — one batch fully resolves before the next
    /// becomes active — so two INDEPENDENT delete-processes that happen to co-drain in one seed do NOT open a
    /// spurious cross-batch order choice (AS-IS resolves each <c>Destroy()</c> in its own StackSkillInfos window).
    /// Same-batch (equal id) triggers keep the intra-window order choice.</summary>
    public long? BatchId { get; init; }
}

public enum TimingWindowTriggerKind
{
    Mandatory = 0,
    Optional = 1,
}
