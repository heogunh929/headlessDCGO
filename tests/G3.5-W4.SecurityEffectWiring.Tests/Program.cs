using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-W4: security effect wiring. A revealed [Security] card's own effect (bound to OnSecurityCheck)
// fires through the common loop, scoped to that card — effects bound to OnSecurityCheck on OTHER cards
// stay dormant. SecurityResolver emits the scoped timing window (W1-2 + W4 scoping); the collector
// resolves only the subject's effect.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
const string OnSecurityCheck = TriggerTimings.OnSecurityCheck;

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId SecurityOneId = new("p2:main:006:P2-M06");
HeadlessEntityId SecurityTwoId = new("p2:main:007:P2-M07");
HeadlessEntityId SecurityThreeId = new("p2:main:008:P2-M08");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Scoped OnSecurityCheck fires only the subject card's effect", ScopedFiresSubjectOnly),
    ("A different card's OnSecurityCheck effect stays dormant", OtherCardStaysDormant),
    ("Unscoped timing window (no subject) still fires all bound effects", UnscopedFiresAll),
    ("SecurityResolver emits an OnSecurityCheck window scoped to the revealed card", ResolverEmitsScopedWindow),
    ("End to end: revealed security card's effect fires, the next one does not", EndToEndSecurityEffectFires),
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

// --- Collector-level scoping --------------------------------------------

async Task ScopedFiresSubjectOnly()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 91);
    RecordingFakeEffect onCardA = Register(context, "fxA", "cardA", OnSecurityCheck);
    RecordingFakeEffect onCardB = Register(context, "fxB", "cardB", OnSecurityCheck);

    TriggerEventEmitter.Emit(context.GameEventQueue, OnSecurityCheck, actor: P2, subject: new HeadlessEntityId("cardA"));
    await DrainCollectResolveAsync(context);

    AssertEqual(1, onCardA.ResolveCalls, "subject card's effect fired");
    AssertEqual(0, onCardB.ResolveCalls, "other card's effect did NOT fire (scoped)");
}

async Task OtherCardStaysDormant()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 92);
    RecordingFakeEffect onCardB = Register(context, "fxB", "cardB", OnSecurityCheck);

    // Reveal cardA — there is no effect bound to cardA, and cardB's effect must not fire either.
    TriggerEventEmitter.Emit(context.GameEventQueue, OnSecurityCheck, actor: P2, subject: new HeadlessEntityId("cardA"));
    await DrainCollectResolveAsync(context);

    AssertEqual(0, onCardB.ResolveCalls, "unrelated security card's effect stayed dormant");
}

async Task UnscopedFiresAll()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 93);
    RecordingFakeEffect onCardA = Register(context, "fxA", "cardA", TriggerTimings.OnEndTurn);
    RecordingFakeEffect onCardB = Register(context, "fxB", "cardB", TriggerTimings.OnEndTurn);

    // No subject -> global timing window: every effect bound to the timing fires (turn boundaries).
    TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnEndTurn, actor: P1);
    await DrainCollectResolveAsync(context);

    AssertEqual(1, onCardA.ResolveCalls, "card A end-turn effect fired");
    AssertEqual(1, onCardB.ResolveCalls, "card B end-turn effect fired");
}

// --- SecurityResolver integration ---------------------------------------

async Task ResolverEmitsScopedWindow()
{
    // (수리-2 re-aim) The invented EffectRegistry binding of a RecordingFakeEffect is replaced by the current-model
    // canon: a card-registered live OnSecurityCheck ActivateClass (TfxOnSecurityCheckDraw) — the surface the
    // SecurityResolver's window actually reads via AutoProcessing.GetSkillInfos (SecurityResolver.cs:436). The
    // reactor is a battle-area Digimon whose gate scopes to ITS OWNER's checked security (the EX5_053 checked-card
    // predicate). Firing is witnessed by an observable draw (library shrinks), scoping by a same-effect reactor on
    // the OTHER player whose owner's security is never checked staying dormant.
    // (P8) the window resolves SYNCHRONOUSLY inside the check loop — assert the effect fired by the time
    // ResolveAsync returns, with no drain, and the scoping held.
    DcgoMatch match = await CreateConfiguredMatchAsync(strike: 1, securityCount: 3);
    EngineContext context = match.Context;
    RegisterReactor(context, TargetId, Opponent);   // P2 reactor: fires when P2's security is checked
    RegisterReactor(context, AttackerId, Player);    // P1 reactor: dormant (P1's security is not checked)
    int p2LibraryBefore = LibraryCount(context, Opponent);
    int p1LibraryBefore = LibraryCount(context, Player);
    DeclareDirectAttack(match);

    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(context);
    AssertTrue(result.IsSuccess, "security resolved");

    AssertEqual(p2LibraryBefore - 1, LibraryCount(context, Opponent),
        "the security-owner's reactor fired inline (drew 1 through the OnSecurityCheck window)");
    AssertEqual(p1LibraryBefore, LibraryCount(context, Player),
        "the other player's reactor stayed dormant (checked-card owner scoping)");
}

async Task EndToEndSecurityEffectFires()
{
    // (수리-2 re-aim) live OnSecurityCheck reactor (TfxOnSecurityCheckDraw) via CardEffectRegistrar, witnessed by a
    // draw. strike 1 reveals ONE security card, so the reactor fires exactly once; the other player's reactor
    // (whose owner's security is never checked) does not fire.
    DcgoMatch match = await CreateConfiguredMatchAsync(strike: 1, securityCount: 2);
    EngineContext context = match.Context;

    RegisterReactor(context, TargetId, Opponent);   // fires once (one revealed card)
    RegisterReactor(context, AttackerId, Player);    // dormant
    int p2LibraryBefore = LibraryCount(context, Opponent);
    int p1LibraryBefore = LibraryCount(context, Player);

    DeclareDirectAttack(match);
    await new SecurityResolver().ResolveAsync(context);
    await DrainCollectResolveAsync(context);

    AssertEqual(p2LibraryBefore - 1, LibraryCount(context, Opponent), "the reactor fired once (one revealed card)");
    AssertEqual(p1LibraryBefore, LibraryCount(context, Player), "the other player's reactor did not fire");
}

// --- Common-loop emulation (mirrors GameFlowProcessor.AutoProcessAsync) --

async Task DrainCollectResolveAsync(EngineContext context)
{
    context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
    var collector = new AutoProcessingTriggerCollector(context.EffectRegistry);
    foreach (GameEvent gameEvent in context.GameEventQueue.DrainPending())
    {
        if (gameEvent.Type == GameEventType.Unknown)
        {
            continue;
        }

        collector.CollectAndEnqueueAll(gameEvent, context.EffectScheduler);
    }

    await context.EffectScheduler.ResolveAllAsync();
}

// (수리-2 re-aim) Register a live OnSecurityCheck reactor on a battle-area Digimon: retype it to the
// TfxOnSecurityCheckDraw fixture def and register through CardEffectRegistrar, so its ActivateClass surfaces in
// AutoProcessing.GetSkillInfos — the surface the SecurityResolver window reads (NOT the EffectRegistry binding).
void RegisterReactor(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("def:TfxOnSecurityCheckDraw");
    cards.Upsert(new CardRecord(defId, "TfxOnSecurityCheckDraw", "TfxOnSecurityCheckDraw",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4 }, CardType: "Digimon"));
    if (context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null)
    {
        context.CardInstanceRepository.Upsert(record with { DefinitionId = defId });
    }

    HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectRegistrar.RegisterCard(context, cardId, owner);
}

int LibraryCount(EngineContext context, HeadlessPlayerId player) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.Library).Count;

RecordingFakeEffect Register(EngineContext context, string effectId, string sourceId, string timing)
{
    var effect = new RecordingFakeEffect(effectId, sourceId, timing);
    context.EffectRegistry.Register(new EffectBinding(CreateRequest(effectId, sourceId, timing), effect: effect));
    return effect;
}

static EffectRequest CreateRequest(string effectId, string sourceId, string timing)
{
    var player = new HeadlessPlayerId(2);
    return new EffectRequest(
        new HeadlessEntityId(effectId), player, timing,
        new EffectContext(player, player, new HeadlessEntityId(sourceId), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

// --- Match harness (trimmed from G2G-004) --------------------------------

void DeclareDirectAttack(DcgoMatch match)
{
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, targetId: null, isDirectAttack: true);
}

async Task<DcgoMatch> CreateConfiguredMatchAsync(int strike, int securityCount)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") },
        firstPlayerId: Player,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 74, setup: setup));
    await AdvanceToMainAsync(match, Player);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    HeadlessEntityId[] securityCards = { SecurityOneId, SecurityTwoId, SecurityThreeId };
    for (int index = 0; index < securityCount; index++)
    {
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, securityCards[index], ChoiceZone.None, ChoiceZone.Security));
    }

    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false, [SecurityResolver.StrikeKey] = strike });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true });

    // Drain the events produced by setup so the test only observes events from the security check.
    context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
    context.GameEventQueue.DrainPending();
    return match;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction[] actions = match.GetLegalActions(playerId)
            .Where(action => action.ActionType == HeadlessActionTypes.AdvancePhase)
            .ToArray();
        AssertEqual(1, actions.Length, "advance phase count");
        await match.ApplyActionAsync(actions[0]);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

// --- Assertions ----------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

internal sealed class RecordingFakeEffect : IHeadlessCardEffect
{
    public RecordingFakeEffect(string effectId, string sourceId, string timing)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(effectId), new HeadlessEntityId(sourceId), name: effectId, timing: timing);
    }

    public CardEffectDefinition Definition { get; }

    public int ResolveCalls { get; private set; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        ResolveCalls++;
        return ValueTask.FromResult(EffectResult.Success("fake resolved"));
    }
}
