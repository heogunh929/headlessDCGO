using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-A3: blocking suspends the blocker (DCG rule) and opens the OnBlockAnyone timing window scoped
// to the blocker — mirroring AS-IS AttackProcess.SwitchDefender (SuspendPermanentsClass.Tap() +
// StackSkillInfos(OnBlockAnyone)). Previously SelectBlocker only rewrote the attack target.

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId InitialTargetId = new("p2:main:001:P2-M01");
HeadlessEntityId BlockerId = new("p2:main:002:P2-M02");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Blocking suspends the blocker", BlockingSuspendsBlocker),
    ("Blocking emits an OnBlock window scoped to the blocker", BlockingEmitsScopedOnBlock),
    ("Skipping the block does NOT suspend or emit OnBlock", SkippingDoesNotSuspendOrEmit),
    // (RC-6) OnBlockEffectFires removed — it registered a RecordingFakeEffect into the EffectRegistry and asserted
    // the invented AutoProcessingTriggerCollector re-collected/fired it (the excised registry trigger-reader
    // surface). The window emit + scoping (BlockingEmitsScopedOnBlock, via GameEventQueue) is retained.
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

// --- Tests ---------------------------------------------------------------

async Task BlockingSuspendsBlocker()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await DeclareDirectAttackAsync(match);
    using var _probe = AmbientMatchContext.Enter(match.Context);
    var timing = new BlockTiming();
    timing.RequestBlockChoice(match.Context);

    AssertFalse(IsSuspended(match, BlockerId), "blocker starts unsuspended");
    BlockTimingResult result = timing.ResolveBlockChoice(match.Context, ChoiceResult.Select(BlockerId));

    AssertTrue(result.IsSuccess, "resolve success");
    AssertTrue(IsSuspended(match, BlockerId), "blocker is suspended after blocking");
    AssertTrue(match.Context.AttackController.Current.IsBlocked, "attack is blocked");
}

async Task BlockingEmitsScopedOnBlock()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await DeclareDirectAttackAsync(match);
    using var _probe = AmbientMatchContext.Enter(match.Context);
    var timing = new BlockTiming();
    timing.RequestBlockChoice(match.Context);

    timing.ResolveBlockChoice(match.Context, ChoiceResult.Select(BlockerId));

    GameEvent window = match.Context.GameEventQueue.DrainPending()
        .Single(e => string.Equals(e.Cause, TriggerTimings.OnBlock, StringComparison.Ordinal));

    // (F1-Tier2 OnBlockAnyone fidelity) AS-IS OnBlockAnyone reactors all gate on the ATTACKER (CanTriggerOnAttack —
    // "[When this Digimon is blocked]"); the emit subject/actor are the attacker / attacking player, and the window is
    // scoped to the attacker. (This test previously asserted the blocker, encoding the old subject=blocker bug.)
    AssertEqual(AttackerId, window.Subject, "OnBlock window subject is the ATTACKER");
    AssertEqual(Player, window.Actor, "OnBlock actor is the attacking player");
    AssertTrue(
        window.Metadata.TryGetValue(AutoProcessingTriggerCollector.SourceEntityIdKey, out object? scoped)
            && scoped is HeadlessEntityId id && id == AttackerId,
        "OnBlock window is scoped to the attacker");
}

async Task SkippingDoesNotSuspendOrEmit()
{
    DcgoMatch match = await CreateConfiguredMatchAsync();
    await DeclareDirectAttackAsync(match);
    using var _probe = AmbientMatchContext.Enter(match.Context);
    var timing = new BlockTiming();
    timing.RequestBlockChoice(match.Context);

    timing.ResolveBlockChoice(match.Context, ChoiceResult.Skip());

    AssertFalse(IsSuspended(match, BlockerId), "skipped block leaves blocker unsuspended");
    AssertFalse(
        match.Context.GameEventQueue.DrainPending().Any(e => string.Equals(e.Cause, TriggerTimings.OnBlock, StringComparison.Ordinal)),
        "no OnBlock window emitted on skip");
}

// --- Harness (trimmed from G2G-002) --------------------------------------

Task DeclareDirectAttackAsync(DcgoMatch match)
{
    match.Context.AttackController.DeclareAttack(Player, AttackerId, Opponent, targetId: null, isDirectAttack: true);
    return Task.CompletedTask;
}

bool IsSuspended(DcgoMatch match, HeadlessEntityId cardId)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing instance '{cardId}'.");
    }

    return record.Metadata.TryGetValue("isSuspended", out object? raw) && raw is bool flag && flag;
}

static EffectRequest CreateRequest(string effectId, string sourceId, string timing)
{
    var player = new HeadlessPlayerId(2);
    return new EffectRequest(
        new HeadlessEntityId(effectId), player, timing,
        new EffectContext(player, player, new HeadlessEntityId(sourceId), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));
}

async Task<DcgoMatch> CreateConfiguredMatchAsync()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId($"P1-M{index:D2}"), $"P1-M{index:D2}", $"P1 Digimon {index}",
            new Dictionary<string, object?>(), CardType: "Digimon"));
        cards.Upsert(new CardRecord(new HeadlessEntityId($"P2-M{index:D2}"), $"P2-M{index:D2}", $"P2 Digimon {index}",
            new Dictionary<string, object?>(), CardType: "Digimon"));
    }

    // (4b A′-1) OLD default ctor (MetadataActionProcessor step cadence) → TurnFlowPump. Setup is normalized
    // for pump ownership (hand 0 / security 0 / mulligan off) at InitializeAsync, so the synthetic board is
    // staged From=None (RemoveFromAllZones→BattleArea) — robust to the deck-resident (not Hand) start.
    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(Player, "P1"), BuildDeck(Opponent, "P2") }, firstPlayerId: Player, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 71, setup: setup));
    await AdvanceToMainAsync(match, Player);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.None, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, InitialTargetId, ChoiceZone.None, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, BlockerId, ChoiceZone.None, ChoiceZone.BattleArea));

    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false });
    SetMetadata(match, InitialTargetId, new Dictionary<string, object?> { ["isSuspended"] = true, [BlockTiming.HasBlockerKey] = true });
    SetMetadata(match, BlockerId, new Dictionary<string, object?> { ["isSuspended"] = false, [BlockTiming.HasBlockerKey] = true });

    // Setup zone moves emitted CardMoved events; drain them so tests only observe the block window.
    context.GameEventQueue.SyncFrom(context.ZoneMover.Events);
    context.GameEventQueue.DrainPending();
    return match;
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// (4b A′-1) pump drive-to-main preamble (EXEMPLAR DriveUntil(AtMainWaitOf) idiom). The OLD AdvancePhase
// step-cadence action retired; the TurnFlowPump auto-flows Active→Draw→Breeding→Main and pauses at the
// turn player's Main wait. Breeding/mulligan pauses are declined (skip). The OLD "advance phase count"
// step-currency invariant is retired (§2.1 P-A); the "advance to main" reach is preserved by the loop-cap
// throw + the terminal AtMainWaitOf assertion.
static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    await StepOnceAsync(match);
    for (var attempt = 0; attempt < 96 && !AtMainWaitOf(match, playerId); attempt++)
    {
        if (match.HasPendingChoice()) await ResolveSkipAsync(match);
        else await StepOnceAsync(match);
    }

    AssertTrue(AtMainWaitOf(match, playerId), "advance to main");
}

static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static async Task ResolveSkipAsync(DcgoMatch match)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    LegalAction resolve = match.GetLegalActions(chooser)
        .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
            && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal))
        ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice)
        ?? throw new InvalidOperationException("no ResolveChoice lane for pending pump pause");
    await match.ApplyActionAsync(resolve);
    await match.StepAsync();
    await match.StepAsync();
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

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
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
