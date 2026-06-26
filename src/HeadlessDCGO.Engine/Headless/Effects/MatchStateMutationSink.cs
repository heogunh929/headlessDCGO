namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Globalization;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// Production <see cref="IEffectMutationSink"/> that applies card effect mutations to the
/// authoritative runtime store (<see cref="ICardInstanceRepository"/> metadata), which the
/// block/battle/security processors and the observation encoder read.
///
/// W2 vocabulary (synchronous, card-instance metadata):
/// <list type="bullet">
/// <item>Keyword grants → boolean flags (Blocker/Rush/Reboot/PreventBattleDeletion/SecurityCheck/
/// Blitz/Retaliation/ArmorPurge).</item>
/// <item><see cref="AddDpModifierKind"/> → appends a typed <see cref="DpModifier"/> to the target's
/// <c>dpModifiers</c> list (read by <c>BattleResolver</c> and <c>CardObservationView</c>).</item>
/// <item><see cref="SuspendKind"/> / <see cref="UnsuspendKind"/> → the <c>isSuspended</c> flag.</item>
/// <item><see cref="SetFlagKind"/> / <see cref="ClearFlagKind"/> → an arbitrary named flag (for
/// restrictions, once-per-turn markers, custom state).</item>
/// </list>
/// Async vocabulary (zone moves, draw, memory) is W2-follow: those need a deferred flush with the
/// engine context and are not yet handled here — they are recorded as unsupported.
/// </summary>
public sealed class MatchStateMutationSink : IEffectMutationSink
{
    public const string TargetEntityIdKey = "targetEntityId";
    public const string DpModifiersKey = "dpModifiers";
    public const string SuspendedFlagKey = "isSuspended";

    // Mutation kinds (the effect→state vocabulary contract for Phase 4 card porting).
    public const string AddDpModifierKind = "AddDpModifier";
    public const string SuspendKind = "Suspend";
    public const string UnsuspendKind = "Unsuspend";
    public const string SetFlagKind = "SetFlag";
    public const string ClearFlagKind = "ClearFlag";

    // W2-follow: async / controller-backed kinds (applied on flush or via the memory controller).
    public const string TrashCardKind = "TrashCard";
    public const string ReturnToHandKind = "ReturnToHand";
    public const string ReturnToDeckTopKind = "ReturnToDeckTop";
    public const string ReturnToDeckBottomKind = "ReturnToDeckBottom";
    public const string AddToSecurityKind = "AddToSecurity";
    public const string DrawCardsKind = "DrawCards";
    public const string AddMemoryKind = "AddMemory";
    public const string SetMemoryKind = "SetMemory";

    // Value keys.
    public const string DpValueKey = "value";
    public const string DpAbsoluteKey = "absolute";
    public const string DpActivatedOrderKey = "activatedOrder";
    public const string FlagKeyKey = "flagKey";
    public const string PlayerIdKey = "playerId";
    public const string CountKey = "count";
    public const string FaceUpKey = "faceUp";
    public const string AmountKey = "amount";

    private static readonly IReadOnlyDictionary<string, string> KindToFlag =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GrantBlocker"] = "hasBlocker",
            ["GrantRush"] = "hasRush",
            ["ScheduleRebootUnsuspend"] = "scheduleRebootUnsuspend",
            ["PreventBattleDeletion"] = "preventBattleDeletion",
            ["SetSecurityCheck"] = "pendingSecurityCheck",
            // W2: previously-dropped keyword kinds now write a flag (consumers wired per keyword).
            ["RequestBlitzAttack"] = "hasBlitz",
            ["DeleteRetaliationTarget"] = "hasRetaliation",
            ["ApplyArmorPurge"] = "hasArmorPurge",
        };

    private readonly ICardInstanceRepository _repository;
    private readonly IZoneMover? _zoneMover;
    private readonly IHeadlessMemoryController? _memory;
    private readonly ILogSink? _log;
    private readonly List<AppliedMutation> _applied = new();
    private readonly List<EffectMutation> _unsupported = new();
    private readonly List<EffectMutation> _skipped = new();
    private readonly List<Func<CancellationToken, Task>> _pendingAsync = new();

    public MatchStateMutationSink(
        ICardInstanceRepository repository,
        ILogSink? log = null,
        IZoneMover? zoneMover = null,
        IHeadlessMemoryController? memory = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _log = log;
        _zoneMover = zoneMover;
        _memory = memory;
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

        // Unknown kinds are reported as unsupported BEFORE the target is checked, so an effect that
        // emits a mutation this sink does not understand is surfaced regardless of its target.
        if (!IsKnownKind(mutation.Kind))
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Unsupported effect mutation kind '{mutation.Kind}'; no MatchState mapping.");
            return;
        }

        // Player-scoped / global mutations (no specific card target).
        switch (mutation.Kind)
        {
            case AddMemoryKind:
                ApplyMemory(mutation, isSet: false);
                return;
            case SetMemoryKind:
                ApplyMemory(mutation, isSet: true);
                return;
            case DrawCardsKind:
                ApplyDraw(mutation);
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

        if (KindToFlag.TryGetValue(mutation.Kind, out string? flagKey))
        {
            WriteMetadata(record, targetId, mutation.Kind, flagKey, true);
            return;
        }

        switch (mutation.Kind)
        {
            case AddDpModifierKind:
                ApplyDpModifier(mutation, record, targetId);
                break;
            case SuspendKind:
                WriteMetadata(record, targetId, mutation.Kind, SuspendedFlagKey, true);
                break;
            case UnsuspendKind:
                WriteMetadata(record, targetId, mutation.Kind, SuspendedFlagKey, false);
                break;
            case SetFlagKind:
                ApplyNamedFlag(mutation, record, targetId, value: true);
                break;
            case ClearFlagKind:
                ApplyNamedFlag(mutation, record, targetId, value: false);
                break;
            case TrashCardKind:
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.AddToTrashAsync(owner, id, ct));
                break;
            case ReturnToHandKind:
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.AddToHandAsync(owner, id, ct));
                break;
            case ReturnToDeckTopKind:
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.MoveToDeckTopAsync(owner, id, ct));
                break;
            case ReturnToDeckBottomKind:
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.MoveToDeckBottomAsync(owner, id, ct));
                break;
            case AddToSecurityKind:
                bool faceUp = ReadBool(mutation.Values, FaceUpKey);
                ApplyZoneMove(mutation, record, targetId, (zm, owner, id, ct) => zm.AddToSecurityAsync(owner, id, faceUp, ct));
                break;
            default:
                _unsupported.Add(mutation);
                _log?.Warn($"Unsupported effect mutation kind '{mutation.Kind}'; no MatchState mapping.");
                break;
        }
    }

    /// <summary>Applies pending asynchronous zone moves / draws deferred by <see cref="Apply"/>.</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingAsync.Count == 0)
        {
            return;
        }

        Func<CancellationToken, Task>[] operations = _pendingAsync.ToArray();
        _pendingAsync.Clear();
        foreach (Func<CancellationToken, Task> operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await operation(cancellationToken).ConfigureAwait(false);
        }
    }

    private void ApplyMemory(EffectMutation mutation, bool isSet)
    {
        if (_memory is null)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a memory controller; none is wired.");
            return;
        }

        int amount = ReadInt(mutation.Values, AmountKey) ?? 0;
        if (isSet)
        {
            _memory.Set(amount);
        }
        else
        {
            _memory.Add(amount);
        }

        _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "memory"));
    }

    private void ApplyDraw(EffectMutation mutation)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId player = ReadPlayer(mutation.Values, PlayerIdKey);
        if (player.IsEmpty)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{PlayerIdKey}' value.");
            return;
        }

        int count = ReadInt(mutation.Values, CountKey) ?? 1;
        _pendingAsync.Add(ct => zoneMover.DrawAsync(player, count, ct));
        _applied.Add(new AppliedMutation(mutation.Kind, mutation.SourceEntityId, "draw"));
    }

    private void ApplyZoneMove(
        EffectMutation mutation,
        CardInstanceRecord record,
        HeadlessEntityId targetId,
        Func<IZoneMover, HeadlessPlayerId, HeadlessEntityId, CancellationToken, Task> move)
    {
        if (_zoneMover is not { } zoneMover)
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' requires a zone mover; none is wired.");
            return;
        }

        HeadlessPlayerId owner = record.OwnerId;
        _pendingAsync.Add(ct => move(zoneMover, owner, targetId, ct));
        _applied.Add(new AppliedMutation(mutation.Kind, targetId, "pendingMove"));
    }

    private void ApplyDpModifier(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId targetId)
    {
        int value = ReadInt(mutation.Values, DpValueKey) ?? 0;
        bool absolute = ReadBool(mutation.Values, DpAbsoluteKey);
        long order = ReadLong(mutation.Values, DpActivatedOrderKey) ?? 0;
        string source = mutation.SourceEntityId.Value;

        DpModifier modifier = absolute
            ? DpModifier.Absolute(value, order, source)
            : DpModifier.Relative(value, order, source);

        DpModifier[] existing = record.Metadata.TryGetValue(DpModifiersKey, out object? raw) &&
            raw is IEnumerable<DpModifier> mods
            ? mods.ToArray()
            : Array.Empty<DpModifier>();

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [DpModifiersKey] = existing.Append(modifier).ToArray(),
        };
        _repository.Upsert(record with { Metadata = metadata });
        _applied.Add(new AppliedMutation(mutation.Kind, targetId, DpModifiersKey));
    }

    private void ApplyNamedFlag(EffectMutation mutation, CardInstanceRecord record, HeadlessEntityId targetId, bool value)
    {
        string? key = ReadString(mutation.Values, FlagKeyKey);
        if (string.IsNullOrWhiteSpace(key))
        {
            _unsupported.Add(mutation);
            _log?.Warn($"Mutation '{mutation.Kind}' is missing a '{FlagKeyKey}' value.");
            return;
        }

        WriteMetadata(record, targetId, mutation.Kind, key.Trim(), value);
    }

    private void WriteMetadata(CardInstanceRecord record, HeadlessEntityId targetId, string kind, string key, object? value)
    {
        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [key] = value,
        };
        _repository.Upsert(record with { Metadata = metadata });
        _applied.Add(new AppliedMutation(kind, targetId, key));
    }

    private static bool IsKnownKind(string kind)
    {
        return KindToFlag.ContainsKey(kind)
            || kind is AddDpModifierKind or SuspendKind or UnsuspendKind or SetFlagKind or ClearFlagKind
            || kind is TrashCardKind or ReturnToHandKind or ReturnToDeckTopKind or ReturnToDeckBottomKind
                or AddToSecurityKind or DrawCardsKind or AddMemoryKind or SetMemoryKind;
    }

    private static HeadlessPlayerId ReadPlayer(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return default;
        }

        return raw switch
        {
            HeadlessPlayerId p => p,
            int i => new HeadlessPlayerId(i),
            long l when l >= int.MinValue && l <= int.MaxValue => new HeadlessPlayerId((int)l),
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) => new HeadlessPlayerId(p),
            _ => default,
        };
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

    private static int? ReadInt(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            int i => i,
            long l when l >= int.MinValue && l <= int.MaxValue => (int)l,
            double d when d % 1 == 0 && d is >= int.MinValue and <= int.MaxValue => (int)d,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) => p,
            _ => null,
        };
    }

    private static long? ReadLong(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            long l => l,
            int i => i,
            string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long p) => p,
            _ => null,
        };
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out object? raw) && raw is bool b && b;
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out object? raw) && raw is string s ? s : null;
    }
}

public sealed record AppliedMutation(string Kind, HeadlessEntityId TargetId, string FlagKey);
