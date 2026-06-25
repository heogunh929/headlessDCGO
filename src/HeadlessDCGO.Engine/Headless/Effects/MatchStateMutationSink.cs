namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Production <see cref="IEffectMutationSink"/> that applies card effect mutations to the
/// authoritative runtime store (<see cref="ICardInstanceRepository"/> metadata), which the
/// block/battle/security processors read.
///
/// Phase 3.5 scope (B-02): a representative subset of mutation kinds is mapped to boolean
/// card-instance metadata flags. <c>hasBlocker</c> and <c>hasRush</c> are already consumed by
/// <c>BlockTiming</c> / <c>AttackPermanentAction</c>; the remaining flags are real state writes
/// whose downstream consumers are wired in later goals. Unknown kinds are logged and recorded
/// as unsupported rather than throwing, so resolution keeps flowing.
/// </summary>
public sealed class MatchStateMutationSink : IEffectMutationSink
{
    public const string TargetEntityIdKey = "targetEntityId";

    private static readonly IReadOnlyDictionary<string, string> KindToFlag =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GrantBlocker"] = "hasBlocker",
            ["GrantRush"] = "hasRush",
            ["ScheduleRebootUnsuspend"] = "scheduleRebootUnsuspend",
            ["PreventBattleDeletion"] = "preventBattleDeletion",
            ["SetSecurityCheck"] = "pendingSecurityCheck",
        };

    private readonly ICardInstanceRepository _repository;
    private readonly ILogSink? _log;
    private readonly List<AppliedMutation> _applied = new();
    private readonly List<EffectMutation> _unsupported = new();
    private readonly List<EffectMutation> _skipped = new();

    public MatchStateMutationSink(ICardInstanceRepository repository, ILogSink? log = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _log = log;
    }

    public int AppliedCount => _applied.Count;

    public int UnsupportedCount => _unsupported.Count;

    public int SkippedCount => _skipped.Count;

    public IReadOnlyList<AppliedMutation> Applied => _applied.ToArray();

    public IReadOnlyList<EffectMutation> Unsupported => _unsupported.ToArray();

    public IReadOnlyList<EffectMutation> Skipped => _skipped.ToArray();

    public void Apply(EffectMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        if (!KindToFlag.TryGetValue(mutation.Kind, out string? flagKey))
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Unsupported effect mutation kind '{mutation.Kind}'; no MatchState mapping.");
            return;
        }

        HeadlessEntityId targetId = ResolveTargetId(mutation);
        if (targetId.IsEmpty
            || !_repository.TryGetInstance(targetId, out CardInstanceRecord? record)
            || record is null)
        {
            _skipped.Add(mutation);
            _log?.Warn(
                $"Effect mutation '{mutation.Kind}' targeted card '{targetId.Value}' which is not in the instance repository.");
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [flagKey] = true,
        };
        _repository.Upsert(record with { Metadata = metadata });
        _applied.Add(new AppliedMutation(mutation.Kind, targetId, flagKey));
    }

    private static HeadlessEntityId ResolveTargetId(EffectMutation mutation)
    {
        if (mutation.Values.TryGetValue(TargetEntityIdKey, out object? raw))
        {
            switch (raw)
            {
                case HeadlessEntityId typed when !typed.IsEmpty:
                    return typed;
                case string text when !string.IsNullOrWhiteSpace(text):
                    return new HeadlessEntityId(text.Trim());
            }
        }

        return mutation.SourceEntityId;
    }
}

public sealed record AppliedMutation(string Kind, HeadlessEntityId TargetId, string FlagKey);
