namespace HeadlessDCGO.Engine.Headless.State;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record PlayerState
{
    private IReadOnlyDictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>> _zones =
        new ReadOnlyDictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>>(
            new Dictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>>());
    private IReadOnlyDictionary<string, bool> _flags =
        new ReadOnlyDictionary<string, bool>(new Dictionary<string, bool>());

    public PlayerState(
        HeadlessPlayerId PlayerId,
        int Memory = 0,
        IReadOnlyDictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>>? Zones = null,
        IReadOnlyDictionary<string, bool>? Flags = null)
    {
        if (PlayerId.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", nameof(PlayerId));
        }

        this.PlayerId = PlayerId;
        this.Memory = Memory;
        this.Zones = Zones ?? new Dictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>>();
        this.Flags = Flags ?? new Dictionary<string, bool>();
    }

    public HeadlessPlayerId PlayerId { get; init; }

    public int Memory { get; init; }

    public IReadOnlyDictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>> Zones
    {
        get => _zones;
        init => _zones = CopyZones(value);
    }

    public IReadOnlyDictionary<string, bool> Flags
    {
        get => _flags;
        init => _flags = CopyFlags(value);
    }

    public IReadOnlyList<HeadlessEntityId> GetZone(ChoiceZone zone)
    {
        return Zones.TryGetValue(zone, out IReadOnlyList<HeadlessEntityId>? cards)
            ? cards
            : Array.Empty<HeadlessEntityId>();
    }

    public ZoneState GetZoneState(ChoiceZone zone)
    {
        return ZoneState.Create(zone, GetZone(zone));
    }

    public PlayerState SetMemory(int memory)
    {
        return this with { Memory = memory };
    }

    public PlayerState SetFlag(string key, bool value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        Dictionary<string, bool> flags = new(Flags, StringComparer.Ordinal)
        {
            [key.Trim()] = value
        };
        return this with { Flags = flags };
    }

    public PlayerState WithZone(ChoiceZone zone, IEnumerable<HeadlessEntityId> cardIds)
    {
        ValidateZone(zone);
        ArgumentNullException.ThrowIfNull(cardIds);

        Dictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>> zones = MutableZones();
        zones[zone] = Array.AsReadOnly(cardIds.ToArray());
        return this with { Zones = zones };
    }

    public PlayerState WithZone(ZoneState zoneState)
    {
        ArgumentNullException.ThrowIfNull(zoneState);
        return WithZone(zoneState.Id.Value, zoneState.CardIds);
    }

    public PlayerState RemoveFromZone(ChoiceZone zone, HeadlessEntityId cardId)
    {
        ValidateZone(zone);
        ValidateCardId(cardId);

        List<HeadlessEntityId> cards = GetZone(zone).ToList();
        if (!cards.Remove(cardId))
        {
            throw new InvalidOperationException(
                $"Card id '{cardId}' is not in player '{PlayerId}' zone '{zone}'.");
        }

        return WithZone(zone, cards);
    }

    public PlayerState AddToZone(ChoiceZone zone, HeadlessEntityId cardId)
    {
        ValidateZone(zone);
        ValidateCardId(cardId);

        Dictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>> zones = MutableZones();
        foreach (ChoiceZone existingZone in zones.Keys.ToArray())
        {
            zones[existingZone] = Array.AsReadOnly(zones[existingZone]
                .Where(id => id != cardId)
                .ToArray());
        }

        List<HeadlessEntityId> target = zones.TryGetValue(zone, out IReadOnlyList<HeadlessEntityId>? current)
            ? current.ToList()
            : new List<HeadlessEntityId>();

        if (!target.Contains(cardId))
        {
            target.Add(cardId);
        }

        zones[zone] = Array.AsReadOnly(target.ToArray());
        return this with { Zones = zones };
    }

    public PlayerStateView ToView(HeadlessPlayerId viewerId)
    {
        bool isOwner = viewerId == PlayerId;
        PlayerZoneView[] zones = Zones
            .OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
            .Select(pair =>
            {
                ZoneStateView view = ZoneState
                    .Create(pair.Key, pair.Value)
                    .ToView(isOwner);
                return new PlayerZoneView(
                    view.Id.Value,
                    view.Count,
                    view.CardIds,
                    view.IsHidden);
            })
            .ToArray();

        return new PlayerStateView(PlayerId, isOwner, Memory, zones);
    }

    private Dictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>> MutableZones()
    {
        return Zones.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<HeadlessEntityId>)Array.AsReadOnly(pair.Value.ToArray()));
    }

    private static IReadOnlyDictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>> CopyZones(
        IReadOnlyDictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>>? zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        Dictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>> copy = new();
        foreach (KeyValuePair<ChoiceZone, IReadOnlyList<HeadlessEntityId>> pair in zones)
        {
            ValidateZone(pair.Key);
            ArgumentNullException.ThrowIfNull(pair.Value);
            copy[pair.Key] = Array.AsReadOnly(pair.Value.ToArray());
        }

        return new ReadOnlyDictionary<ChoiceZone, IReadOnlyList<HeadlessEntityId>>(copy);
    }

    private static IReadOnlyDictionary<string, bool> CopyFlags(
        IReadOnlyDictionary<string, bool>? flags)
    {
        ArgumentNullException.ThrowIfNull(flags);

        Dictionary<string, bool> copy = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, bool> pair in flags)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, bool>(copy);
    }

    private static void ValidateZone(ChoiceZone zone)
    {
        if (zone is ChoiceZone.None or ChoiceZone.Custom)
        {
            throw new ArgumentException("Zone must be a concrete gameplay zone.", nameof(zone));
        }
    }

    private static void ValidateCardId(HeadlessEntityId cardId)
    {
        if (cardId.IsEmpty)
        {
            throw new ArgumentException("Card id must not be empty.", nameof(cardId));
        }
    }
}

public sealed record PlayerStateView(
    HeadlessPlayerId PlayerId,
    bool IsOwnerView,
    int Memory,
    IReadOnlyList<PlayerZoneView> Zones)
{
    public PlayerZoneView? FindZone(ChoiceZone zone)
    {
        return Zones.FirstOrDefault(snapshot => snapshot.Zone == zone);
    }
}

public sealed record PlayerZoneView(
    ChoiceZone Zone,
    int Count,
    IReadOnlyList<HeadlessEntityId> CardIds,
    bool IsHidden);
