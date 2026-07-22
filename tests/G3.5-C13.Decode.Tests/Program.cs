// C-13 Decode — AS-IS: when THIS Digimon WOULD leave the field by an effect (NOT by battle), the controller
// MAY play one of its digivolution sources (matching a colour condition) as a new permanent for free. The card
// still leaves; DiscardEvoRoots trashes the remaining sources. (C-Del 3c-2b) The keyword fires ONLY through the
// AS-IS PRE cut-in window now (the invented DeletionReplacementGate firing-half is retired), so this suite drives
// the REAL ported <Decode> card BT19_024 ([Blue Lv.4]: play 1 Blue Lv.4 source) through the universal effect-delete
// sink's would-be-deleted window (WhenPermanentWouldBeDeleted → WhenRemoveField). Step 1 is the OptionalEffect
// ("Will you use Decode?"); step 2 the Blue-Lv.4 source sub-select. Battle deletions offer nothing (AS-IS
// !IsByBattle), asserted on the window's GetSkillInfos collection.
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Decode opens an optional would-be-deleted choice with a matching (Blue Lv.4) source", DecodeOpensPostChoice),
    ("Declining Decode plays nothing; the deletion proceeds and all sources trash", DecodeDeclineLeavesSourceUnplayed),
    ("Selecting the matching source plays it to the battle area for free", DecodePlaysChosenSourceForFree),
    ("Battle removal does not trigger Decode (AS-IS !IsByBattle)", DecodeNotOfferedOnBattleRemoval),
    ("A stack with no Blue-Lv.4 source offers no Decode choice", DecodeNotOfferedWithoutMatchingSource),
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

// The PRE (would-be-deleted) window must open (holder STILL on the field) and offer Decode (the Blue Lv.4 source
// is playable) as an OptionalEffect.
async Task DecodeOpensPostChoice()
{
    (DcgoMatch match, HeadlessEntityId holder, _, _) = await EffectDeleteDecoder();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, holder), "the holder is still on the field (PRE window, deletion deferred)");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "a would-be-deleted Decode choice is open");
    AssertTrue(ResolveActions(match, P2).Any(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal)), "decode option offered");
    AssertTrue(ResolveActions(match, P2).Any(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal)), "the optional decline is offered (you MAY)");
}

// Skipping the optional step-1 plays nothing and lets the deletion proceed: the card leaves and DiscardEvoRoots
// trashes ALL its sources (AS-IS willBeRemoveField unchanged — Decode never cancels the deletion).
async Task DecodeDeclineLeavesSourceUnplayed()
{
    (DcgoMatch match, HeadlessEntityId holder, HeadlessEntityId blueSrc, HeadlessEntityId otherSrc) = await EffectDeleteDecoder();

    LegalAction skip = ResolveActions(match, P2).Single(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(skip);
    await match.StepAsync();

    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no choice remains after declining");
    AssertFalse(InZone(match, P2, ChoiceZone.BattleArea, blueSrc), "the source did not enter the battle area");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the deletion proceeded — the holder is in the trash");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, blueSrc), "the unplayed matching source was trashed with the stack");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, otherSrc), "the unplayed non-matching source was trashed with the stack");
}

// Two-step: activate Decode, then pick the Blue Lv.4 source -> it enters the battle area for free; the non-matching
// source is NOT a candidate; the holder still leaves and its remaining source is trashed.
async Task DecodePlaysChosenSourceForFree()
{
    (DcgoMatch match, HeadlessEntityId holder, HeadlessEntityId blueSrc, HeadlessEntityId otherSrc) = await EffectDeleteDecoder();

    LegalAction activate = ResolveActions(match, P2).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // step-2 source choice opens

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "step-2 source choice is open");
    // Only the Blue Lv.4 source is a candidate; the non-matching source is filtered out by the sourceCondition.
    AssertFalse(ResolveActions(match, P2).Any(a => a.Id.Value.Contains(otherSrc.Value, StringComparison.Ordinal)),
        "the non-matching source is not a Decode candidate");

    LegalAction pick = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(blueSrc.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, blueSrc), "the chosen source is played to the battle area");
    AssertTrue(ReadFlag(match, blueSrc, "enteredThisTurn"), "the played source enters summoning-sick");
    AssertFalse(SourceIds(match, holder).Contains(blueSrc.Value), "the played source is detached from the dead card");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the deletion proceeded — the card left play");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, otherSrc), "the unplayed source was trashed with the stack");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, blueSrc), "the played source was NOT re-trashed");
}

// A battle-deleted holder must NOT offer Decode (AS-IS !IsByBattle). The retired gate is BLIND either way; the AS-IS
// window collects Decode for an EFFECT would-be-delete but NOT a BATTLE one (byBattleCause → !IsByBattle rejects).
async Task DecodeNotOfferedOnBattleRemoval()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    using var scope = AmbientMatchContext.Enter(ctx);
    HeadlessEntityId holder = HandCard(match, P2, 1);
    RetypeHolder(ctx, holder, "BT19_024");
    HeadlessEntityId blue = Source(ctx, P2, "c13bat-blue", "Blue", 4);

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.SourceIdsKey] = new[] { blue.Value } });
    CardEffectRegistrar.RegisterCard(ctx, holder, P2);

    var effectPerm = new Permanent(ctx, holder, P2) { willBeRemoveField = true };
    var effectHt = CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(new List<Permanent> { effectPerm }, cardEffect: null, battle: null);
    int effectCollected = AutoProcessing.GetSkillInfos(effectHt, EffectTiming.WhenRemoveField).Count;
    effectPerm.willBeRemoveField = false;
    AssertTrue(effectCollected >= 1, "an EFFECT would-be-delete collects Decode");

    var battlePerm = new Permanent(ctx, holder, P2) { willBeRemoveField = true };
    var battleHt = CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(new List<Permanent> { battlePerm }, byBattleCause: true);
    int battleCollected = AutoProcessing.GetSkillInfos(battleHt, EffectTiming.WhenRemoveField).Count;
    battlePerm.willBeRemoveField = false;
    AssertEqual(0, battleCollected, "battle removal offers no Decode (AS-IS !IsByBattle)");
}

// hasDecode but the only source fails the Blue-Lv.4 condition -> no playable source -> no window opens.
async Task DecodeNotOfferedWithoutMatchingSource()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    RetypeHolder(ctx, holder, "BT19_024");
    HeadlessEntityId redSrc = Source(ctx, P2, "c13no-red", "Red", 3);   // fails Blue-Lv.4

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.SourceIdsKey] = new[] { redSrc.Value } });
    CardEffectRegistrar.RegisterCard(ctx, holder, P2);

    await DeleteByEffect(match, ctx, holder);

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "holder swept to trash");
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no Decode choice without a matching source");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, redSrc), "the non-matching source was trashed with the stack");
}

// --- Shared setup --------------------------------------------------------

// A real BT19_024 with [Blue Lv.4, Red Lv.3] sources, deleted by an effect through the sink; the PRE window opens.
async Task<(DcgoMatch, HeadlessEntityId, HeadlessEntityId, HeadlessEntityId)> EffectDeleteDecoder()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = HandCard(match, P2, 1);
    RetypeHolder(ctx, holder, "BT19_024");
    HeadlessEntityId blueSrc = Source(ctx, P2, "c13-blue", "Blue", 4);
    HeadlessEntityId otherSrc = Source(ctx, P2, "c13-red", "Red", 3);

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.SourceIdsKey] = new[] { blueSrc.Value, otherSrc.Value },
    });
    CardEffectRegistrar.RegisterCard(ctx, holder, P2);

    await DeleteByEffect(match, ctx, holder);
    return (match, holder, blueSrc, otherSrc);
}

async Task DeleteByEffect(DcgoMatch match, EngineContext ctx, HeadlessEntityId cardId)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, log: null, ctx.ZoneMover, memory: null, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value }));
    await sink.FlushAsync();
    await match.StepAsync();
}

async Task<(DcgoMatch, EngineContext)> StartedMatch()
{
    // (C-Del 3c-2b) deferredChoice:true — the interactive would-be-deleted window PARKS (the default provider
    // auto-skips, never parking). CompleteResolution resets the provider after the setup phase's auto-answers.
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 73, deferredChoice: true);
    var cards = (CardDatabase)ctx.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(ctx, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);
    (ctx.ChoiceProvider as DeferredChoiceProvider)?.CompleteResolution();
    return (match, ctx);
}

// Re-point a match-dealt hand card at the ported card's number so RegisterCard dispatches its real effect class.
void RetypeHolder(EngineContext ctx, HeadlessEntityId holder, string cardNumber)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"def:{cardNumber}");
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Blue" }, ["level"] = 6, ["dp"] = 5000 }, CardType: "Digimon"));
    if (!ctx.CardInstanceRepository.TryGetInstance(holder, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"missing {holder}");
    ctx.CardInstanceRepository.Upsert(record with { DefinitionId = defId });
}

HeadlessEntityId Source(EngineContext ctx, HeadlessPlayerId owner, string tag, string colour, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"def:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { colour }, ["level"] = level, ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}-{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    return id;
}

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
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, player));

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


// --- Phase driving (pump auto-flow, F62/alpha/EXEMPLAR-T1 precedent) ------
// Drive the pump's natural Active->Draw->Breeding->Main auto-flow to the player's main wait; the OLD
// AdvancePhase step currency is retired. Breeding/Mulligan decisions are declined; observed Main arrival
// is asserted (assertion strength unchanged).
static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

static async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else await StepOnceAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}
