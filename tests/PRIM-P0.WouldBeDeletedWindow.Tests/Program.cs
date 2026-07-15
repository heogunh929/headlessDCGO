// PRIM-P0-timing batch 4: a card-registered WhenPermanentWouldBeDeleted effect surfaces in the existing
// deletion-replacement window as a generic PRE option. Deferring, the window, survival (ClearDeletion) and the
// sweep-finish-on-decline are all pre-existing machinery (DeletionReplacementTiming); this exercises the new
// bridge: (1) the deletion of a card with such an effect DEFERS, (2) activating the custom option RUNS the
// effect body AND cancels the deletion, (3) declining lets the deletion finish. See
// docs/porting/when_permanent_would_be_deleted_design.md.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("a registered WhenPermanentWouldBeDeleted effect DEFERS the deletion and opens the window", DeferOpensWindow),
    ("activating the custom option runs the effect (memory gained) AND the card survives", ActivateSurvivesAndRunsEffect),
    ("declining lets the deletion finish (card trashed)", DeclineGetsDeleted),
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

async Task DeferOpensWindow()
{
    (DcgoMatch match, HeadlessEntityId card) = await SetupAndDelete();

    AssertTrue(ReadFlag(match, card, GameFlowProcessor.PendingDeletionKey), "deletion deferred (pendingDeletion)");
    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, card), "card not yet trashed");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "a replacement choice is open");
    AssertEqual(ChoiceType.DeletionReplacement, match.Context.ChoiceController.PendingRequest!.Type, "choice type");
    AssertEqual(P1, match.Context.ChoiceController.PendingRequest!.PlayerId, "owner decides");
}

async Task ActivateSurvivesAndRunsEffect()
{
    (DcgoMatch match, HeadlessEntityId card) = await SetupAndDelete();
    match.Context.MemoryController.Set(0);

    LegalAction activate = ResolveActions(match, P1).Single(a => a.Id.Value.Contains("#customwouldbedeleted", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P1, ChoiceZone.BattleArea, card), "the custom option cancels the deletion (survives)");
    AssertFalse(InZone(match, P1, ChoiceZone.Trash, card), "card not trashed");
    AssertFalse(ReadFlag(match, card, GameFlowProcessor.PendingDeletionKey), "pendingDeletion cleared");
    AssertEqual(1, match.Context.MemoryController.Current.Current, "the card's own would-be-deleted effect ran (gained 1 memory)");
}

async Task DeclineGetsDeleted()
{
    (DcgoMatch match, HeadlessEntityId card) = await SetupAndDelete();

    LegalAction decline = ResolveActions(match, P1).Single(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(decline);
    await match.StepAsync();

    AssertTrue(InZone(match, P1, ChoiceZone.Trash, card), "declining lets the deletion finish");
    AssertFalse(InZone(match, P1, ChoiceZone.BattleArea, card), "card left the field");
}

// --- Harness -------------------------------------------------------------

// The would-be-deleted card is owned by P1 (the turn player) so the probe's TriggeredGainMemoryEffect turn-gate
// passes when it runs — letting the test observe that the effect body actually executed.
async Task<(DcgoMatch Match, HeadlessEntityId Card)> SetupAndDelete()
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

    HeadlessEntityId card = HandCard(match, P1, 1);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, card, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, card, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.IsSuspendedKey] = false });

    // Register a WhenPermanentWouldBeDeleted effect on the card (a would-be-deleted survival effect).
    CardEffectRegistrar.RegisterOnEnterPlay(context, new WouldBeDeletedProbe(), "PROBE", new CardSource(context, card, P1));

    // An effect deletes the card -> deferred (the new custom PRE option makes HasPreOption defer it), then the
    // loop opens the replacement window.
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.Value }));
    await sink.FlushAsync();
    await match.StepAsync();   // RunToStable opens the deletion-replacement choice
    return (match, card);
}

IEnumerable<LegalAction> ResolveActions(DcgoMatch match, HeadlessPlayerId player) =>
    match.GetLegalActions(player).Where(a => a.ActionType == HeadlessActionTypes.ResolveChoice);

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(playerId).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

HeadlessEntityId HandCard(DcgoMatch match, HeadlessPlayerId player, int index)
{
    HeadlessEntityId[] hand = ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(player, ChoiceZone.Hand).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
    return hand[index - 1];
}

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null &&
    r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

static CardRecord Digimon(string id) => new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

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

// A minimal would-be-deleted effect: on WhenPermanentWouldBeDeleted it returns a (non-optional) gain-memory
// effect. Activating the window option enqueues this, so its execution is observable via the memory gain.
public sealed class WouldBeDeletedProbe : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            effects.Add(new LocalGainMemoryProbe(card, EffectTiming.WhenPermanentWouldBeDeleted, amount: 1, "[probe] would-be-deleted: gain 1 memory."));
        }
        return effects;
    }
}

// (R3-C2b-2) The engine's invented old-model TriggeredGainMemoryEffect was deleted. The WhenPermanentWouldBeDeleted
// custom-option mechanism (DeletionReplacementTiming) runs the card's effect through the OLD-model
// binding->EffectScheduler->CardEffectSchedulerResolver path (context.EffectScheduler.Enqueue(binding.Request)),
// which is a separate still-live subsystem (NOT the R3 trigger window). This test-local old-model gain-memory
// probe (an ICardEffect+IHeadlessCardEffect with ToBinding, emitting an AddMemory mutation gated on the owner's
// turn) preserves the exact observable — the effect body ran => +1 memory — without keeping the deleted engine
// primitive alive.
public sealed class LocalGainMemoryProbe : ICardEffect, IHeadlessCardEffect
{
    public LocalGainMemoryProbe(CardSource card, EffectTiming timing, int amount, string description)
    {
        Card = card;
        Amount = amount;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:gainmemoryprobe:{trigger}"), card.InstanceId, description, trigger, isOptional: false);
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();
        if (Card.Context.TurnController.Current.TurnPlayerId != Card.Owner)
        {
            return ValueTask.FromResult(EffectResult.Success("Not owner's turn; no change."));
        }

        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind, Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount }));
        return ValueTask.FromResult(EffectResult.Success($"Gain {Amount} memory."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var ctx = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, ctx),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}
