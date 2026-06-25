namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record SkillInfo
{
    public SkillInfo(
        CardEffectDefinition definition,
        EffectRequest request,
        EffectResolutionMode mode = EffectResolutionMode.MainStack,
        int priority = 0,
        long sequence = 0,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "SkillInfo mode must be a known value.");
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "SkillInfo sequence must not be negative.");
        }

        if (request.EffectId != definition.EffectId)
        {
            throw new ArgumentException("SkillInfo request effect id must match the effect definition.", nameof(request));
        }

        if (request.Context.SourceEntityId != definition.SourceEntityId)
        {
            throw new ArgumentException("SkillInfo request source entity id must match the effect definition.", nameof(request));
        }

        if (!string.Equals(request.Timing, definition.Timing, StringComparison.Ordinal))
        {
            throw new ArgumentException("SkillInfo request timing must match the effect definition timing.", nameof(request));
        }

        Definition = definition;
        Request = request;
        Mode = mode;
        Priority = priority;
        Sequence = sequence;
        Metadata = CopyMetadata(metadata);
    }

    public CardEffectDefinition Definition { get; }

    public EffectRequest Request { get; }

    public EffectResolutionMode Mode { get; }

    public int Priority { get; }

    public long Sequence { get; }

    public IReadOnlyDictionary<string, object?> Metadata { get; }

    public HeadlessEntityId EffectId => Definition.EffectId;

    public HeadlessEntityId SourceEntityId => Definition.SourceEntityId;

    public HeadlessPlayerId ControllerId => Request.ControllerId;

    public string Timing => Definition.Timing;

    public EffectContext Context => Request.Context;

    public bool IsOptional => Definition.IsOptional;

    public bool IsBackgroundProcess => Definition.IsBackgroundProcess;

    public int? MaxCountPerTurn => Definition.MaxCountPerTurn;

    public string? Hash => Definition.Hash;

    public PendingEffect ToPendingEffect()
    {
        return new PendingEffect(Request, Mode);
    }

    public EffectBinding ToBinding(
        IReadOnlyList<string>? keywords = null,
        EffectQueryRole queryRoles = EffectQueryRole.None,
        IReadOnlyList<string>? queryScopes = null)
    {
        return new EffectBinding(Request, keywords, queryRoles, queryScopes);
    }

    public SkillInfo WithMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        return new SkillInfo(Definition, Request, Mode, Priority, Sequence, metadata);
    }

    public static SkillInfo FromEffect(
        IHeadlessCardEffect effect,
        HeadlessPlayerId controllerId,
        EffectContext context,
        EffectResolutionMode? mode = null,
        int priority = 0,
        long sequence = 0,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(context);

        EffectResolutionMode resolvedMode = mode
            ?? (effect.Definition.IsBackgroundProcess
                ? EffectResolutionMode.Background
                : EffectResolutionMode.MainStack);

        var request = new EffectRequest(
            effect.Definition.EffectId,
            controllerId,
            effect.Definition.Timing,
            context);

        return new SkillInfo(
            effect.Definition,
            request,
            resolvedMode,
            priority,
            sequence,
            metadata);
    }

    private static IReadOnlyDictionary<string, object?> CopyMetadata(
        IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata is null)
        {
            return new ReadOnlyDictionary<string, object?>(
                new Dictionary<string, object?>(StringComparer.Ordinal));
        }

        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in metadata)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("SkillInfo metadata keys must not be null or whitespace.", nameof(metadata));
            }

            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}
