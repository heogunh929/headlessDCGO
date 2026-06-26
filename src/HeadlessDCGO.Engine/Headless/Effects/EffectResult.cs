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
    Suspended
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

    // Unbound: no effect body is wired yet. Resolved=true so the queue keeps draining, but the
    // status makes the gap countable instead of masquerading as a real success.
    public static EffectResult Unbound(
        string? message = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return new EffectResult(Resolved: true, message, values, EffectResolutionStatus.Unbound);
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
