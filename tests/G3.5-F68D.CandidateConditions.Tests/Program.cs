// (#3 porting-readiness) The deletion-replacement candidate enumerators were hardcoded GENERIC (any
// eligible owner battle-area card). AS-IS keyword effects take a per-card Func<Permanent,bool>
// permanentCondition (e.g. "only red allies", "only a Tamer"); when null the set is generic. This suite
// verifies the headless seam that restores that: an optional Func<CardInstanceRecord,bool> on the Gate
// enumerators (null = generic, unchanged) plus IDeletionReplacementCandidateConditions resolved through
// EngineContext, so DeletionReplacementTiming offers/sub-selects only condition-passing candidates.
// No card ports a conditional keyword yet, so the default resolver keeps behaviour identical — this is
// pre-wiring so card porting needs no engine refactor.
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
const string RedKey = "isRed";   // a stand-in card-specific attribute the condition filters on

var tests = new (string Name, Func<Task> Body)[]
{
    ("Gate Scapegoat: a condition filters the sacrifice candidates", GateScapegoatConditionFilters),
    ("Gate Scapegoat: no condition keeps the generic candidate set", GateScapegoatGenericWithoutCondition),
    ("Gate Decoy: a condition filters the redirect candidates", GateDecoyConditionFilters),
    ("Gate single-pick Scapegoat skips a condition-failing first ally", GateSinglePickRespectsCondition),
    ("Resolver: the default returns null (generic)", DefaultResolverReturnsNull),
    ("Resolver: the delegate returns the supplied predicate", DelegateResolverReturnsPredicate),
    ("EngineContext: a registered resolver is retrievable by interface", EngineContextRegisterAndResolve),
    ("Integration: a registered resolver filters the offered sub-selection", IntegrationResolverFiltersSubSelection),
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

// --- Gate-level candidate enumeration ------------------------------------

async Task GateScapegoatConditionFilters()
{
    HeadlessEntityId holder = new("P2-Holder");
    HeadlessEntityId redAlly = new("P2-AllyRed");
    HeadlessEntityId plainAlly = new("P2-AllyPlain");
    EngineContext context = await FieldSetup(
        (holder, P2, new[] { (DeletionReplacementGate.HasScapegoatKey, true) }),
        (redAlly, P2, new[] { (RedKey, true) }),
        (plainAlly, P2, Array.Empty<(string, bool)>()));
    var zones = (IZoneStateReader)context.ZoneMover;

    IReadOnlyList<HeadlessEntityId> filtered = DeletionReplacementGate.FindScapegoatSacrificeCandidates(
        context.CardInstanceRepository, zones, Instance(context, holder), IsRed);

    AssertEqual(1, filtered.Count, "only the condition-passing ally is a candidate");
    AssertTrue(filtered.Contains(redAlly), "the red ally qualifies");
    AssertFalse(filtered.Contains(plainAlly), "the plain ally is filtered out");
}

async Task GateScapegoatGenericWithoutCondition()
{
    HeadlessEntityId holder = new("P2-Holder");
    HeadlessEntityId redAlly = new("P2-AllyRed");
    HeadlessEntityId plainAlly = new("P2-AllyPlain");
    EngineContext context = await FieldSetup(
        (holder, P2, new[] { (DeletionReplacementGate.HasScapegoatKey, true) }),
        (redAlly, P2, new[] { (RedKey, true) }),
        (plainAlly, P2, Array.Empty<(string, bool)>()));
    var zones = (IZoneStateReader)context.ZoneMover;

    IReadOnlyList<HeadlessEntityId> generic = DeletionReplacementGate.FindScapegoatSacrificeCandidates(
        context.CardInstanceRepository, zones, Instance(context, holder));

    AssertEqual(2, generic.Count, "both allies are candidates when no condition is supplied");
}

async Task GateDecoyConditionFilters()
{
    HeadlessEntityId target = new("P2-Target");
    HeadlessEntityId redDecoy = new("P2-DecoyRed");
    HeadlessEntityId plainDecoy = new("P2-DecoyPlain");
    EngineContext context = await FieldSetup(
        (target, P2, Array.Empty<(string, bool)>()),
        (redDecoy, P2, new[] { (DeletionReplacementGate.HasDecoyKey, true), (RedKey, true) }),
        (plainDecoy, P2, new[] { (DeletionReplacementGate.HasDecoyKey, true) }));
    var zones = (IZoneStateReader)context.ZoneMover;

    IReadOnlyList<HeadlessEntityId> filtered = DeletionReplacementGate.FindDecoyRedirectCandidates(
        context.CardInstanceRepository, zones, Instance(context, target), IsRed);
    IReadOnlyList<HeadlessEntityId> generic = DeletionReplacementGate.FindDecoyRedirectCandidates(
        context.CardInstanceRepository, zones, Instance(context, target));

    AssertEqual(1, filtered.Count, "only the red Decoy qualifies under the condition");
    AssertTrue(filtered.Contains(redDecoy), "red Decoy qualifies");
    AssertEqual(2, generic.Count, "both Decoys qualify generically");
}

async Task GateSinglePickRespectsCondition()
{
    // The plain ally sorts first; the single-pick variant must skip it and return the red one.
    HeadlessEntityId holder = new("P2-Holder");
    HeadlessEntityId plainAlly = new("P2-AllyA-plain");
    HeadlessEntityId redAlly = new("P2-AllyB-red");
    EngineContext context = await FieldSetup(
        (holder, P2, new[] { (DeletionReplacementGate.HasScapegoatKey, true) }),
        (plainAlly, P2, Array.Empty<(string, bool)>()),
        (redAlly, P2, new[] { (RedKey, true) }));
    var zones = (IZoneStateReader)context.ZoneMover;

    HeadlessEntityId? pick = DeletionReplacementGate.FindScapegoatSacrifice(
        context.CardInstanceRepository, zones, Instance(context, holder), IsRed);

    AssertEqual(redAlly, pick ?? default, "single-pick skips the condition-failing ally");
}

// --- Resolver + EngineContext seam ---------------------------------------

Task DefaultResolverReturnsNull()
{
    var holder = new CardInstanceRecord(new HeadlessEntityId("h"), new HeadlessEntityId("def"), P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal));
    Func<CardInstanceRecord, bool>? predicate =
        NoDeletionReplacementCandidateConditions.Instance.Resolve(holder, DeletionReplacementTiming.ScapegoatOption);
    AssertTrue(predicate is null, "the default resolver imposes no condition");
    return Task.CompletedTask;
}

Task DelegateResolverReturnsPredicate()
{
    var holder = new CardInstanceRecord(new HeadlessEntityId("h"), new HeadlessEntityId("def"), P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal));
    var resolver = new DelegateDeletionReplacementCandidateConditions((h, option) =>
        option == DeletionReplacementTiming.ScapegoatOption ? IsRed : null);

    Func<CardInstanceRecord, bool>? scapegoat = resolver.Resolve(holder, DeletionReplacementTiming.ScapegoatOption);
    Func<CardInstanceRecord, bool>? decoy = resolver.Resolve(holder, DeletionReplacementTiming.DecoyOption);

    AssertTrue(scapegoat is not null, "the delegate supplies a predicate for the scapegoat option");
    AssertTrue(decoy is null, "the delegate imposes no condition for the decoy option");
    AssertTrue(scapegoat!(Red()), "the predicate accepts a red candidate");
    AssertFalse(scapegoat!(Plain()), "the predicate rejects a non-red candidate");
    return Task.CompletedTask;
}

Task EngineContextRegisterAndResolve()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 1);
    IDeletionReplacementCandidateConditions resolver =
        new DelegateDeletionReplacementCandidateConditions((h, option) => IsRed);
    context.RegisterService(resolver);

    AssertTrue(context.TryGetService(out IDeletionReplacementCandidateConditions? found) && found is not null,
        "the resolver resolves by interface");
    AssertTrue(ReferenceEquals(resolver, found), "the same resolver instance is returned");
    return Task.CompletedTask;
}

// --- Full-match integration: the offered sub-selection is filtered --------

async Task IntegrationResolverFiltersSubSelection()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);

    HeadlessEntityId holder = HandCard(match, P2, 1);
    HeadlessEntityId allyAllowed = HandCard(match, P2, 2);
    HeadlessEntityId allyBlocked = HandCard(match, P2, 3);
    foreach (HeadlessEntityId id in new[] { holder, allyAllowed, allyBlocked })
    {
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.Hand, ChoiceZone.BattleArea));
    }

    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.HasScapegoatKey] = true });

    // Register a card-specific condition: only allyAllowed may be sacrificed for Scapegoat.
    context.RegisterService<IDeletionReplacementCandidateConditions>(
        new DelegateDeletionReplacementCandidateConditions((h, option) =>
            option == DeletionReplacementTiming.ScapegoatOption ? (r => r.InstanceId == allyAllowed) : null));

    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = holder.Value }));
    await sink.FlushAsync();
    await match.StepAsync();   // step-1 window opens (Scapegoat offered: allyAllowed passes the condition)

    LegalAction activate = ResolveActions(match, P2).Single(a =>
        a.Id.Value.Contains("#scapegoat", StringComparison.Ordinal) &&
        !a.Id.Value.Contains(allyAllowed.Value, StringComparison.Ordinal) &&
        !a.Id.Value.Contains(allyBlocked.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // step-2 sub-selection opens, filtered by the resolver

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "step-2 target choice is open");
    string[] targetActions = ResolveActions(match, P2).Select(a => a.Id.Value).ToArray();
    AssertTrue(targetActions.Any(v => v.Contains(allyAllowed.Value, StringComparison.Ordinal)),
        "the allowed ally is an offered sacrifice target");
    AssertFalse(targetActions.Any(v => v.Contains(allyBlocked.Value, StringComparison.Ordinal)),
        "the condition-blocked ally is NOT offered");
}

// --- Helpers -------------------------------------------------------------

bool IsRed(CardInstanceRecord record) =>
    record.Metadata.TryGetValue(RedKey, out object? raw) && raw is bool b && b;

CardInstanceRecord Red() =>
    new(new HeadlessEntityId("red"), new HeadlessEntityId("def"), P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { [RedKey] = true });

CardInstanceRecord Plain() =>
    new(new HeadlessEntityId("plain"), new HeadlessEntityId("def"), P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal));

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) && record is not null
        ? record
        : throw new InvalidOperationException($"Missing instance '{id.Value}'.");

async Task<EngineContext> FieldSetup(params (HeadlessEntityId Id, HeadlessPlayerId Owner, (string Key, bool Value)[] Flags)[] cards)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 12);
    foreach ((HeadlessEntityId id, HeadlessPlayerId owner, (string Key, bool Value)[] flags) in cards)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach ((string key, bool value) in flags)
        {
            metadata[key] = value;
        }

        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("def"), owner, Metadata: metadata));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    }

    return context;
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

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon");

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
