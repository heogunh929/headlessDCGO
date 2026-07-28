namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

public sealed record MatchConfig
{
    private IReadOnlyList<HeadlessPlayerId> _playerIds = Array.Empty<HeadlessPlayerId>();
    private int _initialMemory;
    private int _minimumMemory = -10;
    private int _maximumMemory = 10;

    public IReadOnlyList<HeadlessPlayerId> PlayerIds
    {
        get => _playerIds;
        init => _playerIds = CopyPlayerIds(value);
    }

    public int RandomSeed { get; init; }

    public bool UseDeterministicChoices { get; init; } = true;

    public MatchSetupConfig? Setup { get; init; }

    public int InitialMemory
    {
        get => _initialMemory;
        init
        {
            if (value < _minimumMemory || value > _maximumMemory)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "InitialMemory must be inside the configured memory range.");
            }

            _initialMemory = value;
        }
    }

    public int MinimumMemory
    {
        get => _minimumMemory;
        init
        {
            if (value > _maximumMemory)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MinimumMemory must be less than or equal to MaximumMemory.");
            }

            if (_initialMemory < value)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MinimumMemory must not exceed InitialMemory.");
            }

            _minimumMemory = value;
        }
    }

    public int MaximumMemory
    {
        get => _maximumMemory;
        init
        {
            if (value < _minimumMemory)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MaximumMemory must be greater than or equal to MinimumMemory.");
            }

            if (_initialMemory > value)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "MaximumMemory must not be less than InitialMemory.");
            }

            _maximumMemory = value;
        }
    }

    public static MatchConfig Create(
        IEnumerable<HeadlessPlayerId> playerIds,
        int randomSeed = 0,
        bool useDeterministicChoices = true,
        int initialMemory = 0,
        int minimumMemory = -10,
        int maximumMemory = 10,
        MatchSetupConfig? setup = null)
    {
        return new MatchConfig
        {
            PlayerIds = CopyPlayerIds(playerIds),
            RandomSeed = randomSeed,
            UseDeterministicChoices = useDeterministicChoices,
            InitialMemory = initialMemory,
            MinimumMemory = minimumMemory,
            MaximumMemory = maximumMemory,
            Setup = setup
        }.Validate();
    }

    public MatchConfig Validate()
    {
        if (MinimumMemory > MaximumMemory)
        {
            throw new InvalidOperationException("MinimumMemory must be less than or equal to MaximumMemory.");
        }

        if (InitialMemory < MinimumMemory || InitialMemory > MaximumMemory)
        {
            throw new InvalidOperationException("InitialMemory must be inside the configured memory range.");
        }

        if (PlayerIds.Count != PlayerIds.Distinct().Count())
        {
            throw new InvalidOperationException("PlayerIds must not contain duplicate values.");
        }

        Setup?.Validate(PlayerIds);

        return this;
    }

    private static IReadOnlyList<HeadlessPlayerId> CopyPlayerIds(IEnumerable<HeadlessPlayerId>? playerIds)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        return playerIds.ToArray();
    }
}

/// <summary>
/// (SUBSTRATE — the Photon-lobby substitution) The per-match DECK DATA + turn-order policy the platform supplies
/// to the engine. AS-IS reads exactly this from the network layer: <c>CardObjectController.DeckRecipie(player)</c>
/// (CardObjectController.cs:130-139) pulls the deck code out of
/// <c>player.CustomProperties[ContinuousController.DeckDataPropertyKey]</c> into a <c>DeckData</c>, and the
/// first-player pick is read from <c>PhotonNetwork.CurrentRoom.CustomProperties[DataBase.FirstPlayerKey]</c>
/// (TurnStateMachine.cs:262-283). Headless has no Photon room, so this record IS that platform read — the
/// sanctioned substitution, per the AS-IS-mirror decision that <c>Headless/</c> holds substrate only.
///
/// PURE DATA: it carries deck definition-id lists, the shuffle flags and the first-player policy, and nothing
/// else. The former orchestration flags (<c>InitialHandSize</c> / <c>InitialSecuritySize</c> / <c>EnableMulligan</c>)
/// died with the retired MatchSetupFlow — the opening hand, the mulligan and the security deal are AS-IS
/// <c>TurnStateMachine.StartGame</c> (:369-501) steps now, with the AS-IS hard-coded counts of 5 and 5, so there
/// is no setup-level knob to steer them (and no deal-suppression hack to keep them from double-dealing).
/// </summary>
public sealed record MatchSetupConfig
{
    private IReadOnlyList<PlayerDeckSetup> _playerDecks = Array.Empty<PlayerDeckSetup>();

    /// <summary>AS-IS <c>DeckRecipie</c>/<c>DigitamaDeckRecipie</c> per seat — the deck the platform handed in.</summary>
    public IReadOnlyList<PlayerDeckSetup> PlayerDecks
    {
        get => _playerDecks;
        init => _playerDecks = CopyDecks(value);
    }

    /// <summary>AS-IS the room custom property <c>DataBase.FirstPlayerKey</c> (TurnStateMachine.cs:262-283): when
    /// the platform names the first player it overrides the :256 random pick; null = let :256 decide.</summary>
    public HeadlessPlayerId? FirstPlayerId { get; init; }

    // AS-IS shuffles both decks at game start (CardObjectController.DeckRecipie /
    // DigitamaDeckRecipie return RandomUtility.ShuffledDeckCards(...) for EVERY deck source, :143/:229 etc.).
    // Default true to match; deterministic scenario / unit setups opt out with shuffleDecks:false. The shuffle is
    // seeded (GameRandomSource), so a fixed seed stays reproducible.
    public bool ShuffleDecks { get; init; } = true;

    public bool ShuffleDigitamaDecks { get; init; } = true;

    public static MatchSetupConfig Create(
        IEnumerable<PlayerDeckSetup> playerDecks,
        HeadlessPlayerId? firstPlayerId = null,
        bool shuffleDecks = true,
        bool shuffleDigitamaDecks = true)
    {
        return new MatchSetupConfig
        {
            PlayerDecks = CopyDecks(playerDecks),
            FirstPlayerId = firstPlayerId,
            ShuffleDecks = shuffleDecks,
            ShuffleDigitamaDecks = shuffleDigitamaDecks
        };
    }

    /// <summary>The AS-IS opening draw (TurnStateMachine.cs:371 <c>new DrawClass(player, 5, null)</c>) plus the
    /// AS-IS security deal (:499 <c>new IAddSecurityFromLibrary(player, 5)</c>) — the minimum a main deck must
    /// hold for StartGame to complete. Hard-coded in AS-IS, so hard-coded here.</summary>
    private const int StartGameMainDeckDraw = 5 + 5;

    public MatchSetupConfig Validate(IReadOnlyList<HeadlessPlayerId> playerIds)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        HeadlessPlayerId[] players = playerIds.ToArray();
        if (players.Length != 2)
        {
            throw new InvalidOperationException("Match setup requires exactly two players.");
        }

        if (players.Any(playerId => playerId.IsEmpty))
        {
            throw new InvalidOperationException("Match setup player ids must not contain empty values.");
        }

        if (players.Distinct().Count() != players.Length)
        {
            throw new InvalidOperationException("Match setup player ids must be unique.");
        }

        if (FirstPlayerId.HasValue && !players.Contains(FirstPlayerId.Value))
        {
            throw new InvalidOperationException("FirstPlayerId must be included in PlayerIds.");
        }

        if (PlayerDecks.Count != players.Length)
        {
            throw new InvalidOperationException("Match setup requires one deck setup per player.");
        }

        HeadlessPlayerId[] deckPlayerIds = PlayerDecks.Select(deck => deck.PlayerId).ToArray();
        if (deckPlayerIds.Distinct().Count() != deckPlayerIds.Length)
        {
            throw new InvalidOperationException("Match setup deck player ids must be unique.");
        }

        foreach (HeadlessPlayerId playerId in players)
        {
            PlayerDeckSetup deck = PlayerDecks.FirstOrDefault(candidate => candidate.PlayerId == playerId)
                ?? throw new InvalidOperationException($"Missing setup deck for player '{playerId}'.");

            if (deck.MainDeckDefinitionIds.Count < StartGameMainDeckDraw)
            {
                throw new InvalidOperationException(
                    $"Player '{playerId}' main deck has {deck.MainDeckDefinitionIds.Count} cards; StartGame requires at least {StartGameMainDeckDraw}.");
            }
        }

        return this;
    }

    private static IReadOnlyList<PlayerDeckSetup> CopyDecks(IEnumerable<PlayerDeckSetup>? playerDecks)
    {
        ArgumentNullException.ThrowIfNull(playerDecks);
        PlayerDeckSetup[] decks = playerDecks.ToArray();
        if (decks.Any(deck => deck is null))
        {
            throw new ArgumentException("PlayerDecks must not contain null entries.", nameof(playerDecks));
        }

        return Array.AsReadOnly(decks);
    }
}

/// <summary>(SUBSTRATE) One seat's deck as the platform handed it in — the headless stand-in for the
/// <c>DeckData</c> the AS-IS <c>DeckRecipie</c> decodes out of a Photon custom property. Definition ids, in deck
/// order; the mirror <c>CardObjectController.CreatePlayerDecks</c> shuffles and instantiates them.</summary>
public sealed record PlayerDeckSetup
{
    private IReadOnlyList<HeadlessEntityId> _mainDeckDefinitionIds = Array.Empty<HeadlessEntityId>();
    private IReadOnlyList<HeadlessEntityId> _digitamaDeckDefinitionIds = Array.Empty<HeadlessEntityId>();

    public PlayerDeckSetup(
        HeadlessPlayerId PlayerId,
        IReadOnlyList<HeadlessEntityId> MainDeckDefinitionIds,
        IReadOnlyList<HeadlessEntityId>? DigitamaDeckDefinitionIds = null)
    {
        if (PlayerId.IsEmpty)
        {
            throw new ArgumentException("Player id must not be empty.", nameof(PlayerId));
        }

        this.PlayerId = PlayerId;
        this.MainDeckDefinitionIds = MainDeckDefinitionIds;
        this.DigitamaDeckDefinitionIds = DigitamaDeckDefinitionIds ?? Array.Empty<HeadlessEntityId>();
    }

    public HeadlessPlayerId PlayerId { get; init; }

    public IReadOnlyList<HeadlessEntityId> MainDeckDefinitionIds
    {
        get => _mainDeckDefinitionIds;
        init => _mainDeckDefinitionIds = CopyCardIds(value, nameof(MainDeckDefinitionIds));
    }

    public IReadOnlyList<HeadlessEntityId> DigitamaDeckDefinitionIds
    {
        get => _digitamaDeckDefinitionIds;
        init => _digitamaDeckDefinitionIds = CopyCardIds(value, nameof(DigitamaDeckDefinitionIds));
    }

    public static PlayerDeckSetup FromDeckList(
        HeadlessPlayerId playerId,
        DeckList deckList)
    {
        ArgumentNullException.ThrowIfNull(deckList);
        return new PlayerDeckSetup(
            playerId,
            ExpandEntries(deckList.MainDeck),
            ExpandEntries(deckList.DigitamaDeck));
    }

    private static IReadOnlyList<HeadlessEntityId> ExpandEntries(IReadOnlyList<DeckListEntry> entries)
    {
        return entries
            .SelectMany(entry => Enumerable.Repeat(entry.CardId, entry.Count))
            .ToArray();
    }

    private static IReadOnlyList<HeadlessEntityId> CopyCardIds(
        IEnumerable<HeadlessEntityId>? cardIds,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(cardIds, parameterName);
        HeadlessEntityId[] snapshot = cardIds.ToArray();
        if (snapshot.Any(cardId => cardId.IsEmpty))
        {
            throw new ArgumentException("Deck card ids must not contain empty values.", parameterName);
        }

        return Array.AsReadOnly(snapshot);
    }
}
