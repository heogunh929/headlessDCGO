namespace HeadlessDCGO.Engine.Headless.State;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record CardInstanceState
{
    private IReadOnlyList<HeadlessEntityId> _sourceIds = Array.Empty<HeadlessEntityId>();
    private IReadOnlyDictionary<string, object?> _modifiers = ReadOnlyDictionary<string, object?>.Empty;
    private IReadOnlyDictionary<string, bool> _flags = ReadOnlyDictionary<string, bool>.Empty;

    public CardInstanceState(
        HeadlessEntityId InstanceId,
        HeadlessEntityId DefinitionId,
        HeadlessPlayerId OwnerId,
        bool IsSuspended = false,
        bool IsFaceUp = false,
        IReadOnlyList<HeadlessEntityId>? SourceIds = null,
        IReadOnlyDictionary<string, object?>? Modifiers = null,
        IReadOnlyDictionary<string, bool>? Flags = null)
    {
        if (InstanceId.IsEmpty)
        {
            throw new ArgumentException("Card instance id must not be empty.", nameof(InstanceId));
        }

        if (DefinitionId.IsEmpty)
        {
            throw new ArgumentException("Card definition id must not be empty.", nameof(DefinitionId));
        }

        if (OwnerId.IsEmpty)
        {
            throw new ArgumentException("Owner id must not be empty.", nameof(OwnerId));
        }

        this.InstanceId = InstanceId;
        this.DefinitionId = DefinitionId;
        this.OwnerId = OwnerId;
        this.IsSuspended = IsSuspended;
        this.IsFaceUp = IsFaceUp;
        this.SourceIds = SourceIds ?? Array.Empty<HeadlessEntityId>();
        this.Modifiers = Modifiers ?? new Dictionary<string, object?>();
        this.Flags = Flags ?? new Dictionary<string, bool>();
    }

    public HeadlessEntityId InstanceId { get; init; }

    public HeadlessEntityId DefinitionId { get; init; }

    public HeadlessPlayerId OwnerId { get; init; }

    public bool IsSuspended { get; init; }

    public bool IsFaceUp { get; init; }

    public IReadOnlyList<HeadlessEntityId> SourceIds
    {
        get => _sourceIds;
        init => _sourceIds = CopySourceIds(value);
    }

    public IReadOnlyDictionary<string, object?> Modifiers
    {
        get => _modifiers;
        init => _modifiers = CopyModifiers(value);
    }

    public IReadOnlyDictionary<string, bool> Flags
    {
        get => _flags;
        init => _flags = CopyFlags(value);
    }

    public CardInstanceState Suspend()
    {
        return this with { IsSuspended = true };
    }

    public CardInstanceState Unsuspend()
    {
        return this with { IsSuspended = false };
    }

    public CardInstanceState Reveal()
    {
        return this with { IsFaceUp = true };
    }

    public CardInstanceState Hide()
    {
        return this with { IsFaceUp = false };
    }

    public CardInstanceState AttachSource(HeadlessEntityId sourceId)
    {
        if (sourceId.IsEmpty)
        {
            throw new ArgumentException("Source id must not be empty.", nameof(sourceId));
        }

        return SourceIds.Contains(sourceId)
            ? this
            : this with { SourceIds = SourceIds.Concat(new[] { sourceId }).ToArray() };
    }

    public CardInstanceState DetachSource(HeadlessEntityId sourceId)
    {
        if (sourceId.IsEmpty)
        {
            throw new ArgumentException("Source id must not be empty.", nameof(sourceId));
        }

        List<HeadlessEntityId> sources = SourceIds.ToList();
        if (!sources.Remove(sourceId))
        {
            throw new InvalidOperationException($"Source id '{sourceId}' is not attached to card '{InstanceId}'.");
        }

        return this with { SourceIds = sources };
    }

    public CardInstanceState ClearSources()
    {
        return SourceIds.Count == 0
            ? this
            : this with { SourceIds = Array.Empty<HeadlessEntityId>() };
    }

    public CardInstanceState AddModifier(string key, object? value)
    {
        string normalizedKey = NormalizeStateKey(key, nameof(key));

        Dictionary<string, object?> modifiers = new(Modifiers, StringComparer.Ordinal)
        {
            [normalizedKey] = value
        };
        return this with { Modifiers = modifiers };
    }

    public CardInstanceState RemoveModifier(string key)
    {
        string normalizedKey = NormalizeStateKey(key, nameof(key));
        Dictionary<string, object?> modifiers = new(Modifiers, StringComparer.Ordinal);
        if (!modifiers.Remove(normalizedKey))
        {
            throw new InvalidOperationException($"Modifier '{normalizedKey}' is not set on card '{InstanceId}'.");
        }

        return this with { Modifiers = modifiers };
    }

    public CardInstanceState SetFlag(string key, bool value)
    {
        string normalizedKey = NormalizeStateKey(key, nameof(key));
        Dictionary<string, bool> flags = new(Flags, StringComparer.Ordinal)
        {
            [normalizedKey] = value
        };
        return this with { Flags = flags };
    }

    public CardInstanceState ClearFlag(string key)
    {
        string normalizedKey = NormalizeStateKey(key, nameof(key));
        Dictionary<string, bool> flags = new(Flags, StringComparer.Ordinal);
        if (!flags.Remove(normalizedKey))
        {
            throw new InvalidOperationException($"Flag '{normalizedKey}' is not set on card '{InstanceId}'.");
        }

        return this with { Flags = flags };
    }

    public bool HasFlag(string key)
    {
        string normalizedKey = NormalizeStateKey(key, nameof(key));
        return Flags.TryGetValue(normalizedKey, out bool value) && value;
    }

    public string FingerprintSegment()
    {
        string sources = string.Join(",", SourceIds.Select(id => id.Value));
        string modifiers = string.Join(
            ",",
            Modifiers
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={FormatModifierValue(pair.Value)}"));
        string flags = string.Join(
            ",",
            Flags
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value}"));

        return string.Join(
            "|",
            $"id={InstanceId.Value}",
            $"def={DefinitionId.Value}",
            $"owner={OwnerId.Value}",
            $"suspended={IsSuspended}",
            $"faceUp={IsFaceUp}",
            $"sources={sources}",
            $"modifiers={modifiers}",
            $"flags={flags}");
    }

    private static IReadOnlyList<HeadlessEntityId> CopySourceIds(IReadOnlyList<HeadlessEntityId>? sourceIds)
    {
        ArgumentNullException.ThrowIfNull(sourceIds);

        HeadlessEntityId[] snapshot = sourceIds.ToArray();
        if (snapshot.Any(id => id.IsEmpty))
        {
            throw new ArgumentException("Source ids must not contain empty ids.", nameof(sourceIds));
        }

        if (snapshot.Distinct().Count() != snapshot.Length)
        {
            throw new InvalidOperationException("Source ids must be unique.");
        }

        return Array.AsReadOnly(snapshot);
    }

    private static IReadOnlyDictionary<string, object?> CopyModifiers(
        IReadOnlyDictionary<string, object?>? modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        Dictionary<string, object?> copy = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in modifiers)
        {
            copy[NormalizeStateKey(pair.Key, nameof(modifiers))] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }

    private static IReadOnlyDictionary<string, bool> CopyFlags(IReadOnlyDictionary<string, bool>? flags)
    {
        ArgumentNullException.ThrowIfNull(flags);

        Dictionary<string, bool> copy = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, bool> pair in flags)
        {
            copy[NormalizeStateKey(pair.Key, nameof(flags))] = pair.Value;
        }

        return new ReadOnlyDictionary<string, bool>(copy);
    }

    private static string NormalizeStateKey(string key, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key, parameterName);
        return key.Trim();
    }

    private static string FormatModifierValue(object? value)
    {
        return value?.ToString() ?? "<null>";
    }
}
