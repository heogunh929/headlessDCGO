// C-Del 3c-2b-pre witnesses.
//
// TASK 1 (gate-invisible substrate) — the window/sink free-play path for a digivolution source. AS-IS
// PlayPermanentClass.PlayPermanent (CardController.cs:1361-1363) runs RemoveFromAllArea (detach from the host
// stack + off-field withdrawal) BEFORE CreateNewPermanent places the card in the battle area. A source sits
// off-field (ChoiceZone.None), tracked only by the host's sourceIds metadata; the fixed ApplyPlayCard now moves
// None->BattleArea AND detaches the source from the host's sourceIds, so the deletion finalize
// (DeletionSourceTrash.TrashEvoSourcesAsync) does NOT re-trash the just-played source. Before the fix the
// DigivolutionCards from-zone move THREW (empty zone) and the host stack still listed the source.
//
// TASK 2 — DcgoMatch.ApplyActionAsync drives an INTERACTIVE ForCutIn "would be deleted" replacement (the
// TfxWouldBeDeletedInteractive fixture) end to end: the sink parks the ForCutIn window on the OptionalEffect
// pause; the high-level loop surfaces the choice (GetLegalActions), and applying a ResolveChoice resumes the
// parked ForCutIn window (MetadataActionProcessor body-resume seam) + the GameFlowProcessor sweep finalizes.
// This is the same substrate the C-Del-3C1-Substrate suite drives directly under an explicit ambient scope;
// this suite proves the DcgoMatch loop establishes the ambient scope so the resume runs.

using HeadlessDCGO.Engine.Assets.Scripts.Script;
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
    ("(1) window free-play detaches the source from the host stack; the deletion finalize does NOT re-trash it", WindowFreePlayDetachesAndSurvivesFinalize),
    ("(1-control) with NO free-play, the deletion finalize trashes every source (false-green guard)", NoPlayTrashesAllSources),
    ("(2-survive) DcgoMatch drives an interactive ForCutIn replacement: resolve YES resumes the window and spares the survivor", DcgoMatchInteractiveForCutInSurvives),
    ("(2-decline) DcgoMatch drives an interactive ForCutIn replacement: resolve NO leaves the deletion to finish", DcgoMatchInteractiveForCutInDeclines),
    ("(3) RD-3C2BP-01: TWO distinct-keyword window-form PRE survivals in ONE Destroy -> BOTH survive", TwoSimultaneousReplacementsBothSurvive),
    ("(3-control) one survival effect + one plain casualty in ONE Destroy -> survivor spared, casualty trashed", MixedBatchSurvivorAndCasualty),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ============================================================ TASK 1

async Task WindowFreePlayDetachesAndSurvivesFinalize()
{
    EngineContext ctx = BareContext();
    (HeadlessEntityId host, HeadlessEntityId srcA, HeadlessEntityId srcB) = HostWithSources(ctx);

    // Play srcA out of the host stack for free — the window/sink path (root: DigivolutionCards).
    await PlaySourceForFree(ctx, host, srcA);

    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, srcA), "the played source is on the battle area");
    AssertEqual("SRC-B", string.Join(",", HostSources(ctx, host)), "the host stack no longer lists the played source (only srcB remains)");

    // The deletion finalize trashes the remainder — it must NOT re-trash the just-played source.
    await DeletionSourceTrash.TrashEvoSourcesAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host);

    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, srcA), "the played source survived the deletion finalize (not re-trashed)");
    AssertFalse(InZone(ctx, P1, ChoiceZone.Trash, srcA), "the played source is NOT in the trash");
    AssertTrue(InZone(ctx, P1, ChoiceZone.Trash, srcB), "the remaining source WAS trashed by the finalize");
}

async Task NoPlayTrashesAllSources()
{
    EngineContext ctx = BareContext();
    (HeadlessEntityId host, HeadlessEntityId srcA, HeadlessEntityId srcB) = HostWithSources(ctx);

    await DeletionSourceTrash.TrashEvoSourcesAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host);

    AssertTrue(InZone(ctx, P1, ChoiceZone.Trash, srcA), "with no free-play, srcA is trashed");
    AssertTrue(InZone(ctx, P1, ChoiceZone.Trash, srcB), "with no free-play, srcB is trashed");
}

(HeadlessEntityId Host, HeadlessEntityId SrcA, HeadlessEntityId SrcB) HostWithSources(EngineContext ctx)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId("HOSTDEF");
    cards.Upsert(new CardRecord(def, "HOST", "host", new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000 }, CardType: "Digimon"));
    var host = new HeadlessEntityId("HOST-1");
    var srcA = new HeadlessEntityId("SRC-A");
    var srcB = new HeadlessEntityId("SRC-B");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(host, def, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["sourceIds"] = new[] { srcA.Value, srcB.Value } }));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(srcA, def, P1));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(srcB, def, P1));
    // host on the battle area; sources at None (as natural digivolution leaves them).
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return (host, srcA, srcB);
}

async Task PlaySourceForFree(EngineContext ctx, HeadlessEntityId host, HeadlessEntityId source)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, log: null, ctx.ZoneMover, memory: null, ctx.EffectRegistry, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.PlayCardKind, host,
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = source.Value,
            [MatchStateMutationSink.FromZoneKey] = ChoiceZone.DigivolutionCards,
        }));
    await sink.FlushAsync();
}

// ============================================================ TASK 2

async Task DcgoMatchInteractiveForCutInSurvives()
{
    (DcgoMatch match, HeadlessEntityId card) = await SetupInteractiveDelete();

    // The high-level loop surfaces the OptionalEffect choice; picking the candidate = "yes, prevent it".
    LegalAction activate = InteractiveChoiceActions(match, P1)
        .Single(a => !a.Id.Value.Contains(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertFalse(match.Context.ChoiceController.Current.IsPending, "the ForCutIn window fully resolved (no pending choice)");
    AssertTrue(InZone(match.Context, P1, ChoiceZone.BattleArea, card), "the survivor is spared (still on the battle area)");
    AssertFalse(InZone(match.Context, P1, ChoiceZone.Trash, card), "the survivor is NOT in the trash");
    AssertFalse(ReadFlag(match.Context, card, GameFlowProcessor.PendingDeletionKey), "the deferral was cleared for the survivor");
}

async Task DcgoMatchInteractiveForCutInDeclines()
{
    (DcgoMatch match, HeadlessEntityId card) = await SetupInteractiveDelete();

    LegalAction skip = InteractiveChoiceActions(match, P1)
        .Single(a => a.Id.Value.Contains(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(skip);
    await match.StepAsync();

    AssertFalse(match.Context.ChoiceController.Current.IsPending, "the window resolved (declined)");
    AssertTrue(InZone(match.Context, P1, ChoiceZone.Trash, card), "declining lets the deletion finish (card trashed)");
    AssertFalse(InZone(match.Context, P1, ChoiceZone.BattleArea, card), "the declined card left the battle area");
}

IEnumerable<LegalAction> InteractiveChoiceActions(DcgoMatch match, HeadlessPlayerId player) =>
    match.GetLegalActions(player).Where(a => a.ActionType == HeadlessActionTypes.ResolveChoice);

async Task<(DcgoMatch Match, HeadlessEntityId Card)> SetupInteractiveDelete()
{
    // deferredChoice:true — the parked window's deferred provider replays the agent's answer on resume.
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73, deferredChoice: true);
    var cards = (CardDatabase)context.CardRepository;
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

    var card = new HeadlessEntityId("1:battle:Tfx");
    // Harness reset: the DeferredChoiceProvider accumulated replay answers while the match set up + advanced to
    // Main; clear them so the fixture's optional actually PARKS (an in-loop resolution brackets this itself).
    (context.ChoiceProvider as DeferredChoiceProvider)?.CompleteResolution();

    // Place the gate-invisible interactive fixture + the deleting opponent, register the fixture's printed
    // effects, and run the delete — all under an ambient scope, exactly as a real in-loop deletion runs
    // (GameFlowProcessor.RunToStableAsync enters it; RegisterCard + the PRE cut-in window both need GManager).
    using (AmbientMatchContext.Enter(context))
    {
        var defId = new HeadlessEntityId("TfxWouldBeDeletedInteractive");
        // CardNumber MUST be the fixture class name — CardEffectDispatch.TryCreateForCard dispatches on it.
        cards.Upsert(new CardRecord(defId, "TfxWouldBeDeletedInteractive", "Tfx", new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(card, defId, P1,
            Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, card, ChoiceZone.None, ChoiceZone.BattleArea));
        CardEffectRegistrar.RegisterCard(context, card, P1);

        var foeDef = new HeadlessEntityId("FOE");
        cards.Upsert(new CardRecord(foeDef, "FOE", "FOE", new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
        var foe = new HeadlessEntityId("2:battle:FOE");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(foe, foeDef, P2,
            Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000 }));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, foe, ChoiceZone.None, ChoiceZone.BattleArea));

        // An effect deletes the card -> the sink opens the PRE cut-in, PAUSES on the interactive optional, and
        // PROMOTES the batch to a deferred deletion (FlushAsync swallows the pause; the ForCutIn window is parked).
        var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry, context: context);
        sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, foe,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.Value }));
        await sink.FlushAsync();
    }

    // A no-op loop step must NOT lose the parked window (the loop preserves it for the agent to resolve).
    await match.StepAsync();
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "the interactive ForCutIn window is parked with a pending choice");
    return (match, card);
}

// ============================================================ TASK 3 (RD-3C2BP-01)

// Two DISTINCT window-form PRE survival keywords (TfxWouldBeDeleted + TfxWouldBeDeletedB) on two cards in ONE
// Destroy. AS-IS cut-in semantics: the PRE window resolves BOTH replacement bodies (each clears its own
// willBeRemoveField) BEFORE destroyTargetPermanents_Fixed is computed, so BOTH survive. Before the fix the
// state-based sweep re-entered mid-drain (DigimonLackDPProcess) and trashed the second card before its body ran.
// Distinct EffectNames rule out a same-EffectName collapse artifact — this is a genuine batch/ordering fix.
async Task TwoSimultaneousReplacementsBothSurvive()
{
    (EngineContext ctx, HeadlessEntityId c1, HeadlessEntityId c2) =
        await DeleteTwoInOneBatch("TfxWouldBeDeleted", "TfxWouldBeDeletedB");

    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, c1), "the first replacement survivor is spared");
    AssertFalse(InZone(ctx, P1, ChoiceZone.Trash, c1), "the first survivor is not trashed");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, c2), "the SECOND replacement survivor is spared (RD-3C2BP-01 fixed)");
    AssertFalse(InZone(ctx, P1, ChoiceZone.Trash, c2), "the second survivor is NOT trashed");
}

async Task MixedBatchSurvivorAndCasualty()
{
    // c1 has a survival effect, c2 is a plain Digimon (no PRE effect): the survivor is spared, the casualty dies.
    (EngineContext ctx, HeadlessEntityId c1, HeadlessEntityId c2) =
        await DeleteTwoInOneBatch("TfxWouldBeDeleted", "PLAINCASUALTY");

    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, c1), "the survivor is spared");
    AssertTrue(InZone(ctx, P1, ChoiceZone.Trash, c2), "the plain casualty is trashed (the hold does not over-spare)");
    AssertFalse(InZone(ctx, P1, ChoiceZone.BattleArea, c2), "the casualty left the battle area");
}

async Task<(EngineContext Ctx, HeadlessEntityId C1, HeadlessEntityId C2)> DeleteTwoInOneBatch(string class1, string class2)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 23, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    using var scope = AmbientMatchContext.Enter(ctx);

    HeadlessEntityId c1 = PlaceFixture(ctx, P1, class1, "1:battle:A");
    HeadlessEntityId c2 = PlaceFixture(ctx, P1, class2, "1:battle:B");
    HeadlessEntityId foe = PlaceFixture(ctx, P2, "FOE", "2:battle:FOE", register: false);

    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, log: null, ctx.ZoneMover, memory: null, ctx.EffectRegistry, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, foe,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = c1.Value }));
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, foe,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = c2.Value }));
    await sink.FlushAsync();

    // Resolve any PRE-window order choice through the real agent path (MultipleSkills order-choice for 2 effects).
    var processor = new MetadataActionProcessor();
    int guard = 0;
    while (ctx.ChoiceController.Current.IsPending && guard++ < 20)
    {
        ChoiceRequest req = ctx.ChoiceController.PendingRequest!;
        ChoiceCandidate? sel = req.SelectableCandidates.FirstOrDefault();
        ChoiceResult ans = sel is not null ? ChoiceResult.Select(sel.Id) : ChoiceResult.Skip();
        await processor.ProcessAsync(HeadlessActionFactory.ResolveChoice(P1, ans), ctx);
    }

    await new GameFlowProcessor().RunToStableAsync(ctx);
    return (ctx, c1, c2);
}

HeadlessEntityId PlaceFixture(EngineContext ctx, HeadlessPlayerId owner, string classNum, string instance, bool register = true)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(classNum);
    if (!ctx.CardRepository.TryGetCard(defId, out _))
        cards.Upsert(new CardRecord(defId, classNum, classNum, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId(instance);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    if (register) CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

// ============================================================ HARNESS

EngineContext BareContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 11);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
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

bool InZone(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(EngineContext ctx, HeadlessEntityId cardId, string key) =>
    ctx.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

string[] HostSources(EngineContext ctx, HeadlessEntityId host) =>
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> ids
        ? ids.ToArray() : Array.Empty<string>();

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
