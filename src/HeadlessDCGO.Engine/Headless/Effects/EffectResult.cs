namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;

/// <summary>
/// Distinguishes how an effect resolution ended (G3.5-RL-B3 / fixes P0-7). Previously an unbound
/// (skeleton) effect was reported as a plain success with an "unresolved" metadata flag, making
/// coverage gaps invisible. <see cref="Unbound"/> keeps the queue draining but is now observable.
/// </summary>
public enum EffectResolutionStatus
{
    Resolved,
    Unbound,
    Failed,
    Suspended,

    /// <summary>(RD-10) The effect's resolution-time gate (CanResolve) failed — it FIZZLES. AS-IS
    /// (MultipleSkills.cs:122-126) skips such an effect and CONTINUES the window; unlike <see cref="Failed"/>
    /// (a real resolver error, which parks the queue for diagnostics) the scheduler DEQUEUES a skipped effect
    /// and keeps draining, so a fizzle never wedges the effects behind it.</summary>
    Skipped
}

public sealed record EffectResult
{
    public EffectResult(
        bool Resolved,
        string? Message = null,
        IReadOnlyDictionary<string, object?>? Values = null,
        EffectResolutionStatus? Status = null)
    {
        this.Resolved = Resolved;
        this.Status = Status ?? (Resolved ? EffectResolutionStatus.Resolved : EffectResolutionStatus.Failed);
        this.Message = string.IsNullOrWhiteSpace(Message)
            ? null
            : Message.Trim();
        this.Values = CopyValues(Values);
    }

    public bool Resolved { get; }

    public EffectResolutionStatus Status { get; }

    public bool IsUnbound => Status == EffectResolutionStatus.Unbound;

    public bool IsSuspended => Status == EffectResolutionStatus.Suspended;

    /// <summary>(RD-10) The effect fizzled at its resolution-time gate — dequeue and continue (AS-IS skip).</summary>
    public bool IsSkipped => Status == EffectResolutionStatus.Skipped;

    public string? Message { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static EffectResult Success(
        string? message = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new EffectResult(Resolved: true, message, values);
    }

    public static EffectResult Failure(
        string? message = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new EffectResult(Resolved: false, message, values);
    }

    // (RD-10) Skipped: the effect's resolution-time gate (CanResolve) failed — it fizzles. Resolved=false so the
    // scheduler does not count it as resolved, but the Skipped status tells the scheduler to DEQUEUE it and keep
    // draining (AS-IS MultipleSkills.cs:122-126 skip/continue), so it never wedges the effects queued behind it.
    public static EffectResult Skipped(
        string? message = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new EffectResult(Resolved: false, message, values, EffectResolutionStatus.Skipped);
    }

    // Unbound: no effect body is wired yet. Resolved=true so the queue keeps draining, but the
    // status makes the gap countable instead of masquerading as a real success.
    public static EffectResult Unbound(
        string? message = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new EffectResult(Resolved: true, message, values, EffectResolutionStatus.Unbound);
    }

    // Suspended (W7): the effect paused waiting for an agent-driven choice. Resolved=false so the
    // scheduler leaves it at the queue head (peek-not-dequeue) and re-runs it once the agent answers.
    public static EffectResult Suspended(
        string? message = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new EffectResult(Resolved: false, message, values, EffectResolutionStatus.Suspended);
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(
        IReadOnlyDictionary<string, object?>? values)
    {
        if (values is null)
        {
            return new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Effect result value keys must not be null or whitespace.", nameof(values));
            }

            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}
