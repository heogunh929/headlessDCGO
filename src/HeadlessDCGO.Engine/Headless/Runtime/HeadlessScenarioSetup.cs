namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

// TODO: Replace with real deck/player setup once AS-IS game initialization is ported.
public sealed record HeadlessScenarioSetup(
    IReadOnlyList<HeadlessZoneSeed> ZoneSeeds,
    IReadOnlyList<LegalAction> LegalActions,
    IReadOnlyList<HeadlessDeckSeed>? DeckSeeds = null)
{
    public static HeadlessScenarioSetup Empty { get; } = new(
        ZoneSeeds: Array.Empty<HeadlessZoneSeed>(),
        LegalActions: Array.Empty<LegalAction>(),
        DeckSeeds: Array.Empty<HeadlessDeckSeed>());
}

public sealed record HeadlessZoneSeed(
    HeadlessPlayerId PlayerId,
    HeadlessEntityId CardId,
    ChoiceZone Zone,
    bool FaceUp = false,
    HeadlessEntityId? DefinitionId = null);

public sealed record HeadlessDeckSeed(
    HeadlessPlayerId PlayerId,
    DeckList DeckList);

public sealed class HeadlessScenarioSetupApplier
{
    public async Task ApplyAsync(
        HeadlessRlEnvironment environment,
        HeadlessScenarioSetup setup,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(setup);

        foreach (HeadlessDeckSeed deckSeed in setup.DeckSeeds ?? Array.Empty<HeadlessDeckSeed>())
        {
            foreach (HeadlessZoneSeed zoneSeed in ExpandDeckSeed(deckSeed))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ApplyZoneSeedAsync(environment, zoneSeed, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (HeadlessZoneSeed seed in setup.ZoneSeeds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ApplyZoneSeedAsync(environment, seed, cancellationToken).ConfigureAwait(false);
        }

        if (environment.Match.Context.RuleQueryService is IHeadlessLegalActionSeeder legalActionSeeder)
        {
            legalActionSeeder.SetLegalActions(setup.LegalActions);
        }
    }

    private static async Task ApplyZoneSeedAsync(
        HeadlessRlEnvironment environment,
        HeadlessZoneSeed seed,
        CancellationToken cancellationToken)
    {
        environment.Match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(
            seed.CardId,
            seed.DefinitionId ?? seed.CardId,
            seed.PlayerId));

        await environment.Match.Context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(
                seed.PlayerId,
                seed.CardId,
                ChoiceZone.None,
                seed.Zone,
                seed.FaceUp),
            cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<HeadlessZoneSeed> ExpandDeckSeed(HeadlessDeckSeed deckSeed)
    {
        List<HeadlessZoneSeed> seeds = new();

        AddDeckSectionSeeds(
            seeds,
            deckSeed.PlayerId,
            deckSeed.DeckList.MainDeck,
            ChoiceZone.Library,
            "main");
        AddDeckSectionSeeds(
            seeds,
            deckSeed.PlayerId,
            deckSeed.DeckList.DigitamaDeck,
            ChoiceZone.DigitamaLibrary,
            "digitama");

        return seeds;
    }

    private static void AddDeckSectionSeeds(
        List<HeadlessZoneSeed> seeds,
        HeadlessPlayerId playerId,
        IReadOnlyList<DeckListEntry> entries,
        ChoiceZone zone,
        string sectionName)
    {
        int entryIndex = 0;

        foreach (DeckListEntry entry in entries)
        {
            entryIndex++;

            for (int copyIndex = 1; copyIndex <= entry.Count; copyIndex++)
            {
                seeds.Add(new HeadlessZoneSeed(
                    playerId,
                    CreateDeckInstanceId(playerId, sectionName, entryIndex, entry.CardId, copyIndex),
                    zone,
                    DefinitionId: entry.CardId));
            }
        }
    }

    private static HeadlessEntityId CreateDeckInstanceId(
        HeadlessPlayerId playerId,
        string sectionName,
        int entryIndex,
        HeadlessEntityId cardId,
        int copyIndex)
    {
        return new HeadlessEntityId(
            $"deck:{playerId.Value}:{sectionName}:{entryIndex}:{cardId.Value}:{copyIndex}");
    }
}
