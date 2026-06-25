namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public enum TargetCandidateKind
{
    Card = 0,
    Permanent = 1,
    Player = 2,
}

public enum TargetOwnerScope
{
    Any = 0,
    SourcePlayer = 1,
    Opponent = 2,
}

public enum TargetVisibilityScope
{
    PublicOnly = 0,
    ControllerPrivate = 1,
    IncludeHidden = 2,
}

public enum TargetSuspensionScope
{
    Any = 0,
    SuspendedOnly = 1,
    UnsuspendedOnly = 2,
}

public sealed record TargetFilterRequest
{
    public TargetFilterRequest(
        MatchState matchState,
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> cardDefinitions,
        HeadlessPlayerId sourcePlayerId,
        HeadlessPlayerId viewerId,
        IReadOnlyList<TargetCandidateKind>? candidateKinds = null,
        IReadOnlyList<ChoiceZone>? zones = null,
        TargetOwnerScope ownerScope = TargetOwnerScope.Any,
        TargetVisibilityScope visibilityScope = TargetVisibilityScope.PublicOnly,
        TargetSuspensionScope suspensionScope = TargetSuspensionScope.Any,
        IReadOnlyList<CardRequirement>? cardRequirements = null,
        IReadOnlyList<string>? requiredFlags = null,
        IReadOnlyList<string>? excludedFlags = null)
    {
        ArgumentNullException.ThrowIfNull(matchState);
        ArgumentNullException.ThrowIfNull(cardDefinitions);
        if (sourcePlayerId.IsEmpty)
        {
            throw new ArgumentException("Source player id must not be empty.", nameof(sourcePlayerId));
        }

        if (viewerId.IsEmpty)
        {
            throw new ArgumentException("Viewer id must not be empty.", nameof(viewerId));
        }

        if (!Enum.IsDefined(ownerScope))
        {
            throw new ArgumentOutOfRangeException(nameof(ownerScope), "Owner scope must be known.");
        }

        if (!Enum.IsDefined(visibilityScope))
        {
            throw new ArgumentOutOfRangeException(nameof(visibilityScope), "Visibility scope must be known.");
        }

        if (!Enum.IsDefined(suspensionScope))
        {
            throw new ArgumentOutOfRangeException(nameof(suspensionScope), "Suspension scope must be known.");
        }

        MatchState = matchState;
        CardDefinitions = CopyDefinitions(cardDefinitions);
        SourcePlayerId = sourcePlayerId;
        ViewerId = viewerId;
        CandidateKinds = CopyKinds(candidateKinds);
        Zones = CopyZones(zones);
        OwnerScope = ownerScope;
        VisibilityScope = visibilityScope;
        SuspensionScope = suspensionScope;
        CardRequirements = Array.AsReadOnly((cardRequirements ?? Array.Empty<CardRequirement>()).ToArray());
        RequiredFlags = NormalizeFlags(requiredFlags);
        ExcludedFlags = NormalizeFlags(excludedFlags);
    }

    public MatchState MatchState { get; }

    public IReadOnlyDictionary<HeadlessEntityId, CardRecord> CardDefinitions { get; }

    public HeadlessPlayerId SourcePlayerId { get; }

    public HeadlessPlayerId ViewerId { get; }

    public IReadOnlyList<TargetCandidateKind> CandidateKinds { get; }

    public IReadOnlyList<ChoiceZone> Zones { get; }

    public TargetOwnerScope OwnerScope { get; }

    public TargetVisibilityScope VisibilityScope { get; }

    public TargetSuspensionScope SuspensionScope { get; }

    public IReadOnlyList<CardRequirement> CardRequirements { get; }

    public IReadOnlyList<string> RequiredFlags { get; }

    public IReadOnlyList<string> ExcludedFlags { get; }

    private static IReadOnlyDictionary<HeadlessEntityId, CardRecord> CopyDefinitions(
        IReadOnlyDictionary<HeadlessEntityId, CardRecord> definitions)
    {
        var copy = new Dictionary<HeadlessEntityId, CardRecord>();
        foreach (KeyValuePair<HeadlessEntityId, CardRecord> pair in definitions)
        {
            if (pair.Key.IsEmpty)
            {
                throw new ArgumentException("Definition id must not be empty.", nameof(definitions));
            }

            ArgumentNullException.ThrowIfNull(pair.Value);
            if (pair.Key != pair.Value.Id)
            {
                throw new ArgumentException("Definition dictionary key must match card record id.", nameof(definitions));
            }

            copy[pair.Key] = pair.Value;
        }

        return new ReadOnlyDictionary<HeadlessEntityId, CardRecord>(copy);
    }

    private static IReadOnlyList<TargetCandidateKind> CopyKinds(IReadOnlyList<TargetCandidateKind>? candidateKinds)
    {
        TargetCandidateKind[] kinds = (candidateKinds ?? new[] { TargetCandidateKind.Card })
            .Distinct()
            .OrderBy(kind => kind)
            .ToArray();
        if (kinds.Length == 0 || kinds.Any(kind => !Enum.IsDefined(kind)))
        {
            throw new ArgumentException("At least one known target candidate kind is required.", nameof(candidateKinds));
        }

        return Array.AsReadOnly(kinds);
    }

    private static IReadOnlyList<ChoiceZone> CopyZones(IReadOnlyList<ChoiceZone>? zones)
    {
        ChoiceZone[] copy = (zones ?? Array.Empty<ChoiceZone>())
            .Distinct()
            .OrderBy(zone => zone.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (copy.Any(zone => !Enum.IsDefined(zone)))
        {
            throw new ArgumentException("Target zones must be known.", nameof(zones));
        }

        return Array.AsReadOnly(copy);
    }

    private static IReadOnlyList<string> NormalizeFlags(IReadOnlyList<string>? flags)
    {
        return Array.AsReadOnly((flags ?? Array.Empty<string>())
            .Select(flag => string.IsNullOrWhiteSpace(flag) ? string.Empty : flag.Trim())
            .Where(flag => flag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(flag => flag, StringComparer.Ordinal)
            .ToArray());
    }
}

public sealed record TargetCandidate
{
    public TargetCandidate(
        TargetCandidateKind kind,
        HeadlessEntityId? cardId,
        HeadlessPlayerId? playerId,
        HeadlessPlayerId ownerId,
        ChoiceZone? zone,
        HeadlessEntityId? definitionId,
        bool isFaceUp,
        bool isSuspended)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Target candidate kind must be known.");
        }

        if ((kind is TargetCandidateKind.Card or TargetCandidateKind.Permanent) &&
            (!cardId.HasValue || cardId.Value.IsEmpty))
        {
            throw new ArgumentException("Card and permanent target candidates require a card id.", nameof(cardId));
        }

        if (kind == TargetCandidateKind.Player && (!playerId.HasValue || playerId.Value.IsEmpty))
        {
            throw new ArgumentException("Player target candidates require a player id.", nameof(playerId));
        }

        if (ownerId.IsEmpty)
        {
            throw new ArgumentException("Target owner id must not be empty.", nameof(ownerId));
        }

        Kind = kind;
        CardId = cardId;
        PlayerId = playerId;
        OwnerId = ownerId;
        Zone = zone;
        DefinitionId = definitionId;
        IsFaceUp = isFaceUp;
        IsSuspended = isSuspended;
    }

    public TargetCandidateKind Kind { get; }

    public HeadlessEntityId? CardId { get; }

    public HeadlessPlayerId? PlayerId { get; }

    public HeadlessPlayerId OwnerId { get; }

    public ChoiceZone? Zone { get; }

    public HeadlessEntityId? DefinitionId { get; }

    public bool IsFaceUp { get; }

    public bool IsSuspended { get; }

    public string StableId => Kind == TargetCandidateKind.Player
        ? $"player:{PlayerId?.Value.ToString() ?? string.Empty}"
        : $"card:{CardId?.Value ?? string.Empty}";
}

public sealed record TargetFilterResult
{
    private TargetFilterResult(
        bool isSuccess,
        string reason,
        IReadOnlyList<TargetCandidate> candidates,
        IReadOnlyDictionary<string, object?> values)
    {
        IsSuccess = isSuccess;
        Reason = reason;
        Candidates = Array.AsReadOnly(candidates.ToArray());
        Values = CopyValues(values);
    }

    public bool IsSuccess { get; }

    public string Reason { get; }

    public IReadOnlyList<TargetCandidate> Candidates { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public static TargetFilterResult Success(
        IReadOnlyList<TargetCandidate> candidates,
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new TargetFilterResult(true, reason, candidates, values);
    }

    public static TargetFilterResult Failure(
        string reason,
        IReadOnlyDictionary<string, object?> values)
    {
        return new TargetFilterResult(false, reason, Array.Empty<TargetCandidate>(), values);
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(IReadOnlyDictionary<string, object?> values)
    {
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}

public static class TargetFilterHelpers
{
    public static TargetFilterResult Evaluate(TargetFilterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.MatchState.Players.Any(player => player.PlayerId == request.SourcePlayerId))
        {
            return TargetFilterResult.Failure(
                $"Source player '{request.SourcePlayerId}' was not found.",
                BaseValues(request));
        }

        if (!request.MatchState.Players.Any(player => player.PlayerId == request.ViewerId))
        {
            return TargetFilterResult.Failure(
                $"Viewer player '{request.ViewerId}' was not found.",
                BaseValues(request));
        }

        TargetCandidate[] candidates = EnumerateCandidates(request)
            .Where(candidate => MatchesOwnerScope(candidate.OwnerId, request))
            .Where(candidate => MatchesVisibility(candidate, request))
            .Where(candidate => MatchesSuspension(candidate, request))
            .Where(candidate => MatchesFlags(candidate, request))
            .Where(candidate => MatchesRequirements(candidate, request))
            .OrderBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.OwnerId.Value)
            .ThenBy(candidate => candidate.Zone?.ToString() ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.StableId, StringComparer.Ordinal)
            .ToArray();

        Dictionary<string, object?> values = BaseValues(request);
        values["candidateCount"] = candidates.Length;
        values["candidateIds"] = candidates.Select(candidate => candidate.StableId).ToArray();

        return TargetFilterResult.Success(candidates, "Target candidates resolved.", values);
    }

    public static TargetFilterResult Cards(TargetFilterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Evaluate(new TargetFilterRequest(
            request.MatchState,
            request.CardDefinitions,
            request.SourcePlayerId,
            request.ViewerId,
            new[] { TargetCandidateKind.Card },
            request.Zones,
            request.OwnerScope,
            request.VisibilityScope,
            request.SuspensionScope,
            request.CardRequirements,
            request.RequiredFlags,
            request.ExcludedFlags));
    }

    public static TargetFilterResult Permanents(TargetFilterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Evaluate(new TargetFilterRequest(
            request.MatchState,
            request.CardDefinitions,
            request.SourcePlayerId,
            request.ViewerId,
            new[] { TargetCandidateKind.Permanent },
            request.Zones.Count == 0 ? PermanentZones() : request.Zones,
            request.OwnerScope,
            request.VisibilityScope,
            request.SuspensionScope,
            request.CardRequirements,
            request.RequiredFlags,
            request.ExcludedFlags));
    }

    public static TargetFilterResult Players(TargetFilterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Evaluate(new TargetFilterRequest(
            request.MatchState,
            request.CardDefinitions,
            request.SourcePlayerId,
            request.ViewerId,
            new[] { TargetCandidateKind.Player },
            Array.Empty<ChoiceZone>(),
            request.OwnerScope,
            TargetVisibilityScope.IncludeHidden,
            TargetSuspensionScope.Any,
            Array.Empty<CardRequirement>(),
            Array.Empty<string>(),
            Array.Empty<string>()));
    }

    private static IEnumerable<TargetCandidate> EnumerateCandidates(TargetFilterRequest request)
    {
        if (request.CandidateKinds.Contains(TargetCandidateKind.Player))
        {
            foreach (PlayerState player in request.MatchState.Players)
            {
                yield return new TargetCandidate(
                    TargetCandidateKind.Player,
                    cardId: null,
                    player.PlayerId,
                    player.PlayerId,
                    zone: null,
                    definitionId: null,
                    isFaceUp: true,
                    isSuspended: false);
            }
        }

        bool includeCards = request.CandidateKinds.Contains(TargetCandidateKind.Card);
        bool includePermanents = request.CandidateKinds.Contains(TargetCandidateKind.Permanent);
        if (!includeCards && !includePermanents)
        {
            yield break;
        }

        ChoiceZone[] requestedZones = request.Zones.Count == 0
            ? AllConcreteZones(request.MatchState)
            : request.Zones.ToArray();

        foreach (PlayerState player in request.MatchState.Players)
        {
            foreach (ChoiceZone zone in requestedZones)
            {
                if (zone is ChoiceZone.None or ChoiceZone.Custom)
                {
                    continue;
                }

                bool isPermanentZone = IsPermanentZone(zone);
                foreach (HeadlessEntityId cardId in player.GetZone(zone))
                {
                    if (!request.MatchState.CardInstances.TryGetValue(cardId, out CardInstanceState? instance))
                    {
                        continue;
                    }

                    if (instance.OwnerId != player.PlayerId)
                    {
                        continue;
                    }

                    if (includeCards)
                    {
                        yield return CreateCandidate(TargetCandidateKind.Card, instance, zone);
                    }

                    if (includePermanents && isPermanentZone)
                    {
                        yield return CreateCandidate(TargetCandidateKind.Permanent, instance, zone);
                    }
                }
            }
        }
    }

    private static TargetCandidate CreateCandidate(
        TargetCandidateKind kind,
        CardInstanceState instance,
        ChoiceZone zone)
    {
        return new TargetCandidate(
            kind,
            instance.InstanceId,
            playerId: null,
            instance.OwnerId,
            zone,
            instance.DefinitionId,
            instance.IsFaceUp,
            instance.IsSuspended);
    }

    private static bool MatchesOwnerScope(HeadlessPlayerId ownerId, TargetFilterRequest request)
    {
        return request.OwnerScope switch
        {
            TargetOwnerScope.Any => true,
            TargetOwnerScope.SourcePlayer => ownerId == request.SourcePlayerId,
            TargetOwnerScope.Opponent => ownerId != request.SourcePlayerId,
            _ => false,
        };
    }

    private static bool MatchesVisibility(TargetCandidate candidate, TargetFilterRequest request)
    {
        if (candidate.Kind == TargetCandidateKind.Player || candidate.IsFaceUp)
        {
            return true;
        }

        return request.VisibilityScope switch
        {
            TargetVisibilityScope.PublicOnly => false,
            TargetVisibilityScope.ControllerPrivate => candidate.OwnerId == request.ViewerId,
            TargetVisibilityScope.IncludeHidden => true,
            _ => false,
        };
    }

    private static bool MatchesSuspension(TargetCandidate candidate, TargetFilterRequest request)
    {
        if (candidate.Kind == TargetCandidateKind.Player)
        {
            return true;
        }

        return request.SuspensionScope switch
        {
            TargetSuspensionScope.Any => true,
            TargetSuspensionScope.SuspendedOnly => candidate.IsSuspended,
            TargetSuspensionScope.UnsuspendedOnly => !candidate.IsSuspended,
            _ => false,
        };
    }

    private static bool MatchesFlags(TargetCandidate candidate, TargetFilterRequest request)
    {
        if (candidate.Kind == TargetCandidateKind.Player)
        {
            PlayerState? player = request.MatchState.Players.FirstOrDefault(player => player.PlayerId == candidate.OwnerId);
            return player is not null &&
                request.RequiredFlags.All(flag => player.Flags.TryGetValue(flag, out bool value) && value) &&
                request.ExcludedFlags.All(flag => !player.Flags.TryGetValue(flag, out bool value) || !value);
        }

        if (!candidate.CardId.HasValue ||
            !request.MatchState.CardInstances.TryGetValue(candidate.CardId.Value, out CardInstanceState? instance))
        {
            return false;
        }

        return request.RequiredFlags.All(flag => instance.Flags.TryGetValue(flag, out bool value) && value) &&
            request.ExcludedFlags.All(flag => !instance.Flags.TryGetValue(flag, out bool value) || !value);
    }

    private static bool MatchesRequirements(TargetCandidate candidate, TargetFilterRequest request)
    {
        if (request.CardRequirements.Count == 0 || candidate.Kind == TargetCandidateKind.Player)
        {
            return true;
        }

        if (!candidate.CardId.HasValue)
        {
            return false;
        }

        CardRequirementResult result = CardRequirementHelpers.Evaluate(new CardRequirementRequest(
            request.MatchState,
            candidate.CardId.Value,
            request.CardDefinitions,
            request.CardRequirements));
        return result.IsMatch;
    }

    private static Dictionary<string, object?> BaseValues(TargetFilterRequest request)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sourcePlayerId"] = request.SourcePlayerId.Value,
            ["viewerId"] = request.ViewerId.Value,
            ["candidateKinds"] = request.CandidateKinds.Select(kind => kind.ToString()).ToArray(),
            ["zones"] = request.Zones.Select(zone => zone.ToString()).ToArray(),
            ["ownerScope"] = request.OwnerScope.ToString(),
            ["visibilityScope"] = request.VisibilityScope.ToString(),
            ["suspensionScope"] = request.SuspensionScope.ToString(),
            ["cardRequirementCount"] = request.CardRequirements.Count,
            ["requiredFlags"] = request.RequiredFlags.ToArray(),
            ["excludedFlags"] = request.ExcludedFlags.ToArray(),
        };
    }

    private static ChoiceZone[] AllConcreteZones(MatchState state)
    {
        return state.Players
            .SelectMany(player => player.Zones.Keys)
            .Where(zone => zone is not ChoiceZone.None and not ChoiceZone.Custom)
            .Distinct()
            .OrderBy(zone => zone.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ChoiceZone> PermanentZones()
    {
        return new[] { ChoiceZone.BattleArea, ChoiceZone.BreedingArea };
    }

    private static bool IsPermanentZone(ChoiceZone zone)
    {
        return zone is ChoiceZone.BattleArea or ChoiceZone.BreedingArea;
    }
}
