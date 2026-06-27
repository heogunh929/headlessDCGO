namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class MatchSetupFlow
{
    public async Task<MatchSetupResult> ApplyAsync(
        EngineContext context,
        IReadOnlyList<HeadlessPlayerId> playerIds,
        MatchSetupConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(playerIds);
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();

        MatchSetupConfig setup = config.Validate(playerIds);
        HeadlessPlayerId firstPlayerId = ResolveFirstPlayer(context.RandomSource, playerIds, setup);
        HeadlessPlayerId setupTurnPlayerId = ResolvePreSwitchTurnPlayer(playerIds, firstPlayerId);
        IReadOnlyList<HeadlessPlayerId> setupOrder = BuildSetupOrder(playerIds, firstPlayerId);
        Dictionary<HeadlessPlayerId, PlayerDeckSetup> decksByPlayer =
            setup.PlayerDecks.ToDictionary(deck => deck.PlayerId);

        List<PlayerSetupResult> playerResults = new();
        foreach (HeadlessPlayerId playerId in setupOrder)
        {
            PlayerDeckSetup deck = decksByPlayer[playerId];
            IReadOnlyList<HeadlessEntityId> mainDeck = PrepareDeck(deck.MainDeckDefinitionIds, setup.ShuffleDecks, context.RandomSource);
            IReadOnlyList<HeadlessEntityId> digitamaDeck = PrepareDeck(deck.DigitamaDeckDefinitionIds, setup.ShuffleDigitamaDecks, context.RandomSource);

            await SeedDeckAsync(
                context,
                playerId,
                mainDeck,
                ChoiceZone.Library,
                "main",
                cancellationToken).ConfigureAwait(false);

            await SeedDeckAsync(
                context,
                playerId,
                digitamaDeck,
                ChoiceZone.DigitamaLibrary,
                "digitama",
                cancellationToken).ConfigureAwait(false);

            IReadOnlyList<HeadlessEntityId> hand = await context.ZoneMover
                .DrawAsync(playerId, setup.InitialHandSize, cancellationToken)
                .ConfigureAwait(false);

            // N-5: when mulligan is enabled, security is dealt only AFTER every player's mulligan
            // decision (from the post-mulligan deck), so defer it to the MulliganCoordinator here.
            IReadOnlyList<HeadlessEntityId> security = setup.EnableMulligan
                ? Array.Empty<HeadlessEntityId>()
                : await context.ZoneMover
                    .AddSecurityFromLibraryAsync(playerId, setup.InitialSecuritySize, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

            playerResults.Add(new PlayerSetupResult(
                playerId,
                mainDeck.Count,
                digitamaDeck.Count,
                hand.Count,
                security.Count,
                GetZoneCount(context, playerId, ChoiceZone.Library),
                GetZoneCount(context, playerId, ChoiceZone.DigitamaLibrary),
                hand,
                security));
        }

        // N-5: open the first player's mulligan decision. Subsequent players' decisions and the deferred
        // security deal are driven by the MulliganCoordinator as each choice resolves.
        if (setup.EnableMulligan)
        {
            context.MulliganCoordinator.Begin(
                context.ChoiceController,
                setupOrder,
                setup.InitialHandSize,
                setup.InitialSecuritySize);
        }

        return new MatchSetupResult(
            firstPlayerId,
            setupTurnPlayerId,
            setupOrder,
            playerResults.ToArray());
    }

    private static async Task SeedDeckAsync(
        EngineContext context,
        HeadlessPlayerId playerId,
        IReadOnlyList<HeadlessEntityId> definitionIds,
        ChoiceZone zone,
        string deckKind,
        CancellationToken cancellationToken)
    {
        for (int index = 0; index < definitionIds.Count; index++)
        {
            HeadlessEntityId definitionId = definitionIds[index];
            HeadlessEntityId instanceId = new(
                $"p{playerId.Value}:{deckKind}:{index + 1:D3}:{definitionId.Value}");
            context.CardInstanceRepository.Upsert(new CardInstanceRecord(
                instanceId,
                definitionId,
                playerId,
                Metadata: new Dictionary<string, object?>
                {
                    ["setupDeck"] = deckKind,
                    ["setupIndex"] = index
                }));

            if (zone == ChoiceZone.DigitamaLibrary)
            {
                await context.ZoneMover
                    .MoveAsync(new ZoneMoveRequest(playerId, instanceId, ChoiceZone.None, ChoiceZone.DigitamaLibrary), cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await context.ZoneMover
                    .MoveToDeckBottomAsync(playerId, instanceId, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static IReadOnlyList<HeadlessEntityId> PrepareDeck(
        IReadOnlyList<HeadlessEntityId> source,
        bool shuffle,
        IRandomSource randomSource)
    {
        HeadlessEntityId[] cards = source.ToArray();
        if (shuffle)
        {
            randomSource.Shuffle(cards);
        }

        return cards;
    }

    private static HeadlessPlayerId ResolveFirstPlayer(
        IRandomSource randomSource,
        IReadOnlyList<HeadlessPlayerId> playerIds,
        MatchSetupConfig setup)
    {
        if (setup.FirstPlayerId.HasValue)
        {
            return setup.FirstPlayerId.Value;
        }

        int setupTurnPlayerIndex = randomSource.NextInt(0, playerIds.Count);
        HeadlessPlayerId setupTurnPlayer = playerIds[setupTurnPlayerIndex];
        return ResolvePreSwitchTurnPlayer(playerIds, setupTurnPlayer);
    }

    private static HeadlessPlayerId ResolvePreSwitchTurnPlayer(
        IReadOnlyList<HeadlessPlayerId> playerIds,
        HeadlessPlayerId firstPlayerId)
    {
        return playerIds.First(playerId => playerId != firstPlayerId);
    }

    private static IReadOnlyList<HeadlessPlayerId> BuildSetupOrder(
        IReadOnlyList<HeadlessPlayerId> playerIds,
        HeadlessPlayerId firstPlayerId)
    {
        return new[] { firstPlayerId }
            .Concat(playerIds.Where(playerId => playerId != firstPlayerId))
            .ToArray();
    }

    private static int GetZoneCount(
        EngineContext context,
        HeadlessPlayerId playerId,
        ChoiceZone zone)
    {
        return context.ZoneMover is IZoneStateReader zoneReader
            ? zoneReader.GetCards(playerId, zone).Count
            : 0;
    }
}

public sealed record MatchSetupConfig
{
    private IReadOnlyList<PlayerDeckSetup> _playerDecks = Array.Empty<PlayerDeckSetup>();
    private int _initialHandSize = 5;
    private int _initialSecuritySize = 5;

    public IReadOnlyList<PlayerDeckSetup> PlayerDecks
    {
        get => _playerDecks;
        init => _playerDecks = CopyDecks(value);
    }

    public HeadlessPlayerId? FirstPlayerId { get; init; }

    public int InitialHandSize
    {
        get => _initialHandSize;
        init => _initialHandSize = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "InitialHandSize must not be negative.");
    }

    public int InitialSecuritySize
    {
        get => _initialSecuritySize;
        init => _initialSecuritySize = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "InitialSecuritySize must not be negative.");
    }

    // N-4: the original shuffles both decks at game start (CardObjectController.CreatePlayerDecks uses
    // RandomUtility.ShuffledDeckCards for every deck). Default true to match; deterministic scenario /
    // unit setups opt out with shuffleDecks:false. Shuffle is seeded, so a fixed seed stays reproducible.
    public bool ShuffleDecks { get; init; } = true;

    public bool ShuffleDigitamaDecks { get; init; } = true;

    // N-5: when true, each player makes an opening-hand mulligan decision (keep/redraw) before security
    // is dealt, mirroring the original. Default false so existing deterministic setups (which advance
    // straight from Setup) are unaffected; the RL / faithful-game path opts in.
    public bool EnableMulligan { get; init; }

    public static MatchSetupConfig Create(
        IEnumerable<PlayerDeckSetup> playerDecks,
        HeadlessPlayerId? firstPlayerId = null,
        int initialHandSize = 5,
        int initialSecuritySize = 5,
        bool shuffleDecks = true,
        bool shuffleDigitamaDecks = true,
        bool enableMulligan = false)
    {
        return new MatchSetupConfig
        {
            PlayerDecks = CopyDecks(playerDecks),
            FirstPlayerId = firstPlayerId,
            InitialHandSize = initialHandSize,
            InitialSecuritySize = initialSecuritySize,
            ShuffleDecks = shuffleDecks,
            ShuffleDigitamaDecks = shuffleDigitamaDecks,
            EnableMulligan = enableMulligan
        };
    }

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

            int requiredMainDeckCount = InitialHandSize + InitialSecuritySize;
            if (deck.MainDeckDefinitionIds.Count < requiredMainDeckCount)
            {
                throw new InvalidOperationException(
                    $"Player '{playerId}' main deck has {deck.MainDeckDefinitionIds.Count} cards; setup requires at least {requiredMainDeckCount}.");
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

public sealed record MatchSetupResult(
    HeadlessPlayerId FirstPlayerId,
    HeadlessPlayerId SetupTurnPlayerId,
    IReadOnlyList<HeadlessPlayerId> SetupOrder,
    IReadOnlyList<PlayerSetupResult> Players);

public sealed record PlayerSetupResult(
    HeadlessPlayerId PlayerId,
    int MainDeckCount,
    int DigitamaDeckCount,
    int HandCount,
    int SecurityCount,
    int RemainingLibraryCount,
    int RemainingDigitamaLibraryCount,
    IReadOnlyList<HeadlessEntityId> HandCards,
    IReadOnlyList<HeadlessEntityId> SecurityCards);
