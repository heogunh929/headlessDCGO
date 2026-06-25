namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;

public sealed record EffectResult
{
    public EffectResult(
        bool Resolved,
        string? Message = null,
        IReadOnlyDictionary<string, object?>? Values = null)
    {
        this.Resolved = Resolved;
        this.Message = string.IsNullOrWhiteSpace(Message)
            ? null
            : Message.Trim();
        this.Values = CopyValues(Values);
    }

    public bool Resolved { get; }

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
