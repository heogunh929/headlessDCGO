// C-13 Decode — AS-IS: when THIS Digimon leaves the field by an effect (NOT by battle), the controller
// MAY play one of its digivolution sources (matching a colour condition; default any Digimon) as a new
// permanent for free. Ported 1:1 onto the F-6.8 POST deletion-replacement window (sibling of Ascension/
// Save/ArmorPurge): sources stay in ChoiceZone.None referenced by the trashed card's sourceIds, so the
// "play a source for free" reads cleanly post-deletion. Optionality + the which-source sub-selection are
// agent choices (rules-faithful). Engine: DeletionReplacementTiming DecodeOption + Gate.TryDecodePlaySourceAsync;
// grant GrantDecode -> hasDecode.
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId DigimonDef = new("def:decode-digimon");
HeadlessEntityId NonDigimonDef = new("def:decode-tamer");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Decode opens an optional post-removal choice with a source candidate", DecodeOpensPostChoice),
    ("Declining Decode leaves the source unplayed in None", DecodeDeclineLeavesSourceUnplayed),
    ("Selecting a source plays it to the battle area for free", DecodePlaysChosenSourceForFree),
    ("Battle removal does not trigger Decode", DecodeNotOfferedOnBattleRemoval),
    ("A non-Digimon-only stack offers no Decode choice", DecodeNotOfferedWithoutDigimonSource),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// Effect-deletes a hasDecode holder with a Digimon source + a non-Digimon source. The POST window must
// open and offer Decode (the Digimon source is playable).
async Task DecodeOpensPostChoice()
{
    (DcgoMatch match, HeadlessEntityId holder, HeadlessEntityId digimonSrc, _) = await EffectDeleteDecoder();

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the holder is in the trash");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "a post-removal Decode choice is open");
    AssertEqual(ChoiceType.DeletionReplacement, match.Context.ChoiceController.PendingRequest!.Type, "choice type");
    AssertTrue(ResolveActions(match, P2).Any(a => a.Id.Value.Contains("#decode", StringComparison.Ordinal)), "decode option offered");
    AssertFalse(InZone(match, P2, ChoiceZone.BattleArea, digimonSrc), "the source is still off-field at decision time");
}

// Skipping the optional step-1 leaves the source where it was.
async Task DecodeDeclineLeavesSourceUnplayed()
{
    (DcgoMatch match, HeadlessEntityId holder, HeadlessEntityId digimonSrc, _) = await EffectDeleteDecoder();

    LegalAction skip = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(skip);
    await match.StepAsync();

    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no choice remains after declining");
    AssertFalse(InZone(match, P2, ChoiceZone.BattleArea, digimonSrc), "the source did not enter the battle area");
    AssertTrue(SourceIds(match, holder).Contains(digimonSrc.Value), "the source stays attached to the dead card (not played)");
    AssertFalse(ReadFlag(match, holder, DeletionReplacementGate.DecodedKey), "no decoded marker when declined");
}

// Two-step: activate Decode, then pick the Digimon source -> it enters the battle area for free.
async Task DecodePlaysChosenSourceForFree()
{
    (DcgoMatch match, HeadlessEntityId holder, HeadlessEntityId digimonSrc, HeadlessEntityId otherSrc) = await EffectDeleteDecoder();

    LegalAction activate = ResolveActions(match, P2).Single(a =>
        a.Id.Value.Contains("#decode", StringComparison.Ordinal) &&
        !a.Id.Value.Contains(digimonSrc.Value, StringComparison.Ordinal) &&
        !a.Id.Value.Contains(otherSrc.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // step-2 source choice opens

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "step-2 source choice is open");
    // Only the Digimon source is a candidate; the non-Digimon source is filtered out.
    AssertFalse(ResolveActions(match, P2).Any(a => a.Id.Value.Contains(otherSrc.Value, StringComparison.Ordinal)),
        "the non-Digimon source is not a Decode candidate");

    LegalAction pick = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(digimonSrc.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, digimonSrc), "the chosen source is played to the battle area");
    AssertTrue(ReadFlag(match, digimonSrc, "enteredThisTurn"), "the played source enters summoning-sick");
    AssertFalse(SourceIds(match, holder).Contains(digimonSrc.Value), "the played source is detached from the dead card");
    AssertTrue(ReadFlag(match, holder, DeletionReplacementGate.DecodedKey), "decoded marker stamped (single use)");
}

// A battle-deleted holder must NOT offer Decode (AS-IS !IsByBattle).
async Task DecodeNotOfferedOnBattleRemoval()
{
    EngineContext context = await NewMatchContext();
    DcgoMatch match = await StartedMatch(context);

    HeadlessEntityId holder = HandCard(match, P2, 1);
    HeadlessEntityId src = new("P2-DecBattleSrc");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(src, DigimonDef, P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    // Simulate a completed battle deletion: holder flagged deletedByBattle and moved to the trash.
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasDecodeKey] = true,
        [DeletionReplacementGate.DeletedByBattleKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = new[] { src.Value },
    });
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.BattleArea, ChoiceZone.Trash, FaceUp: true));
    await match.StepAsync();

    AssertFalse(ResolveActions(match, P2).Any(a => a.Id.Value.Contains("#decode", StringComparison.Ordinal)),
        "battle removal offers no Decode option");
}

// hasDecode but the only source is a non-Digimon -> no playable source -> no choice.
async Task DecodeNotOfferedWithoutDigimonSource()
{
    EngineContext context = await NewMatchContext();
    DcgoMatch match = await StartedMatch(context);

    HeadlessEntityId holder = HandCard(match, P2, 1);
    HeadlessEntityId tamerSrc = new("P2-DecTamerSrc");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(tamerSrc, NonDigimonDef, P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasDecodeKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = new[] { tamerSrc.Value },
    });

    await DeleteByEffect(match, context, holder);

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "holder swept to trash");
    AssertFalse(ResolveActions(match, P2).Any(a => a.Id.Value.Contains("#decode", StringComparison.Ordinal)),
        "no Decode option without a Digimon source");
}

// --- Shared setup --------------------------------------------------------

// Plays a hasDecode holder with [Digimon source, non-Digimon source], deletes it by effect, and steps so
// the POST window opens. Returns the holder + both source ids.
async Task<(DcgoMatch, HeadlessEntityId, HeadlessEntityId, HeadlessEntityId)> EffectDeleteDecoder()
{
    EngineContext context = await NewMatchContext();
    DcgoMatch match = await StartedMatch(context);

    HeadlessEntityId holder = HandCard(match, P2, 1);
    HeadlessEntityId digimonSrc = new("P2-DecDigimonSrc");
    HeadlessEntityId otherSrc = new("P2-DecOtherSrc");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(digimonSrc, DigimonDef, P2));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(otherSrc, NonDigimonDef, P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasDecodeKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = new[] { digimonSrc.Value, otherSrc.Value },
    });

    await DeleteByEffect(match, context, holder);
    return (match, holder, digimonSrc, otherSrc);
}

async Task DeleteByEffect(DcgoMatch match, EngineContext context, HeadlessEntityId cardId)
{
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value }));
    await sink.FlushAsync();
    await match.StepAsync();
}

async Task<EngineContext> NewMatchContext()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    cards.Upsert(new CardRecord(DigimonDef, "DEC-DIGI", "Decode source (Digimon)", new Dictionary<string, object?>(), CardType: "Digimon"));
    cards.Upsert(new CardRecord(NonDigimonDef, "DEC-TAMER", "Decode source (Tamer)", new Dictionary<string, object?>(), CardType: "Tamer"));
    return context;
}

async Task<DcgoMatch> StartedMatch(EngineContext context)
{
    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);
    return match;
}

// --- Helpers (mirrors G3.5-F68) -----------------------------------------

IEnumerable<LegalAction> ResolveActions(DcgoMatch match, HeadlessPlayerId player) =>
    match.GetLegalActions(player).Where(a => a.ActionType == HeadlessActionTypes.ResolveChoice);

HeadlessEntityId HandCard(DcgoMatch match, HeadlessPlayerId player, int index)
{
    HeadlessEntityId[] hand = ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(player, ChoiceZone.Hand).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
    if (hand.Length < index) throw new InvalidOperationException($"hand short: {hand.Length} < {index}");
    return hand[index - 1];
}

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    for (var attempt = 0; attempt < 10 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(player).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing {cardId}.");
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

string[] SourceIds(DcgoMatch match, HeadlessEntityId cardId) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> ids
        ? ids.ToArray() : Array.Empty<string>();

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
