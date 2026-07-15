// C-Del 3c-1 INTERACTIVE promote-to-defer substrate witness. The AS-IS PRE cut-in "would be deleted" window
// (opened by the sink, 3b) can PAUSE on an interactive "will you use it?" replacement. The sink's inline-drain
// model cannot resume mid-flush, so it PROMOTES the batch to a deferred deletion (flags every field member
// pendingDeletion + PreWindowPromoted) and FlushAsync SWALLOWS the pause; the parked ForCutIn window carries the
// choice; once the agent resolves it (MetadataActionProcessor resumes the ForCutIn pool — the resume extension
// under test) the resumed body sets willBeRemoveField, and the GameFlowProcessor sweep finalizes the batch on it
// (survivor spared / casualty trashed) in ONE batch-unit OnDeletion window (RD-C2-DEFERRED-DELETE-BATCH resolved).
//
// GATE-INVISIBLE fixture (TfxWouldBeDeletedInteractive): a non-keyword-named, isOptional new-model ActivateClass
// with no EffectRegistry binding, so HasPreOption is FALSE → DeferAll=false → the sink opens the PRE cut-in and the
// promote-at-pause substrate is the sole firing path (no live replacement gate involved — that is the keyword
// retirement witnesses).

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var P1 = new HeadlessPlayerId(1);
var P2 = new HeadlessPlayerId(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("PAUSE+PROMOTE: an interactive PRE replacement pauses; the sink promotes the batch to pendingDeletion + swallows (card still on field)", PausePromotes),
    ("SURVIVE: resolving 'yes' resumes the ForCutIn window (body clears willBeRemoveField); the sweep spares the survivor", InteractiveSurvives),
    ("DECLINE: resolving 'no' leaves willBeRemoveField set; the sweep trashes the card", InteractiveDeclines),
    ("BATCH-UNIT: [interactive-survivor, plain-casualty] in ONE Destroy — survivor spared, casualty trashed, ONE OnDeletion window", BatchSurvivorCasualty),
    ("CONTROL: a plain deleted card (no PRE effect) is trashed with no pause (false-green guard)", ControlTrashed),
    ("REAL AGENT: a MetadataActionProcessor ResolveChoice (OptionalEffect → ForCutIn window resume) spares the survivor", ProcessorResolvesSurvivor),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ============================================================ TESTS

async Task PausePromotes()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, "TfxWouldBeDeletedInteractive");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndSwallow(context, card);

    AssertTrue(context.ChoiceController.Current.IsPending, "the interactive PRE cut-in surfaced 'will you use it?'");
    AssertTrue(ReadFlag(context, card, GameFlowProcessor.PendingDeletionKey), "the batch was promoted to pendingDeletion");
    AssertTrue(ReadFlag(context, card, MatchStateMutationSink.PreWindowPromotedKey), "the promote marker is set");
    AssertTrue(ReadFlag(context, card, "willBeRemoveField"), "willBeRemoveField still set (the choice is not resolved yet)");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, card), "the promoted card is NOT trashed while the choice pends");
}

async Task InteractiveSurvives()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, "TfxWouldBeDeletedInteractive");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndSwallow(context, card);
    ChoiceRequest optional = context.ChoiceController.PendingRequest!;

    // Resolve YES via the ESTABLISHED window-optional pattern (C-Atk-Raid / C-EoT2): record the answer + resume
    // the OWNING pool (the PRE cut-in drains through ForCutIn), the deferred provider replays it on the re-drive.
    await ResolveWindowOptional(context, ChoiceResult.Select(optional.Candidates[0].Id));
    AssertFalse(context.ChoiceController.Current.IsPending, "the resumed ForCutIn window fully resolved (no pending choice)");
    AssertFalse(ReadFlag(context, card, "willBeRemoveField"), "the resumed replacement body cleared willBeRemoveField (survives)");

    await new GameFlowProcessor().RunToStableAsync(context);

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, card), "the interactive survivor was spared on the battle area");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, card), "the survivor is NOT in the trash");
    AssertFalse(ReadFlag(context, card, GameFlowProcessor.PendingDeletionKey), "the deferral was cleared for the survivor");
    AssertFalse(ReadFlag(context, card, "willBeRemoveField"), "no stale willBeRemoveField on the spared survivor");
}

async Task InteractiveDeclines()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, "TfxWouldBeDeletedInteractive");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndSwallow(context, card);

    // Resolve NO (decline the optional): the replacement body never runs (willBeRemoveField stays set through the
    // cut-in), so the card DIES — the state-based sweep (which the cut-in window's own rule-processing drives)
    // finalizes it.
    await ResolveWindowOptional(context, ChoiceResult.Skip());
    await new GameFlowProcessor().RunToStableAsync(context);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, card), "the declined card was trashed by the sweep");
    AssertFalse(InZone(context, P1, ChoiceZone.BattleArea, card), "the declined card left the battle area");
    AssertFalse(ReadFlag(context, card, "willBeRemoveField"), "willBeRemoveField was reset before the trash (no stale marker)");
}

async Task BatchSurvivorCasualty()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var survivor = await Place(context, P1, "TfxWouldBeDeletedInteractive", instance: "1:battle:Surv");
    var casualty = await Place(context, P1, "PLAIN", instance: "1:battle:Cas");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, survivor, P1);
    CardEffectRegistrar.RegisterCard(context, casualty, P1);

    // ONE Destroy over both. The interactive survivor pauses -> the WHOLE batch promotes (both pendingDeletion).
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Delete(survivor));
    sink.Apply(Delete(casualty));
    await sink.FlushAsync();   // swallowed (promote path)

    AssertTrue(ReadFlag(context, survivor, GameFlowProcessor.PendingDeletionKey), "the survivor member is promoted");
    AssertTrue(ReadFlag(context, casualty, GameFlowProcessor.PendingDeletionKey), "the casualty member is promoted with it (batch-atomic)");
    AssertTrue(context.ChoiceController.Current.IsPending, "the survivor's interactive choice pends");

    // Resolve YES for the survivor.
    ChoiceRequest optional = context.ChoiceController.PendingRequest!;
    await ResolveWindowOptional(context, ChoiceResult.Select(optional.Candidates[0].Id));
    AssertFalse(context.ChoiceController.Current.IsPending, "the batch cut-in fully resolved");

    await new GameFlowProcessor().RunToStableAsync(context);

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, survivor), "the batch survivor was spared");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, casualty), "the batch casualty was trashed");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, survivor), "the survivor is not in the trash");
    AssertFalse(context.ChoiceController.Current.IsPending, "no leftover choice — one Destroy, one batch-unit window");
}

async Task ControlTrashed()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, "PLAIN");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndSwallow(context, card);
    AssertFalse(context.ChoiceController.Current.IsPending, "a plain card opens no interactive replacement (false-green guard)");
    await new GameFlowProcessor().RunToStableAsync(context);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, card), "the plain deleted card is trashed");
    AssertFalse(InZone(context, P1, ChoiceZone.BattleArea, card), "the plain card left the battle area");
}

async Task ProcessorResolvesSurvivor()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, "TfxWouldBeDeletedInteractive");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndSwallow(context, card);
    ChoiceRequest optional = context.ChoiceController.PendingRequest!;

    // Resolve YES through the REAL agent path: a ResolveChoice action routed through MetadataActionProcessor
    // (OptionalEffect with an empty trigger queue → the cut-in window body-resume, ForCutIn pool). This proves a
    // retired-keyword replacement is resolvable by an actual agent, not only the direct test-level resume.
    var processor = new MetadataActionProcessor();
    ActionProcessResult resolved = await processor.ProcessAsync(
        HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(optional.Candidates[0].Id)), context);
    AssertTrue(resolved.IsSuccess, $"processor ResolveChoice succeeded ({resolved.Message})");
    AssertFalse(context.ChoiceController.Current.IsPending, "the ForCutIn window fully resolved via the processor");
    AssertFalse(ReadFlag(context, card, "willBeRemoveField"), "the resumed body cleared willBeRemoveField (survives)");

    await new GameFlowProcessor().RunToStableAsync(context);
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, card), "the survivor was spared via the real-agent resolution");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, card), "the survivor is not in the trash");
}

// ============================================================ HARNESS

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 23, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry, context: context);

async Task DeleteAndSwallow(EngineContext context, HeadlessEntityId card)
{
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Delete(card));
    await sink.FlushAsync();   // the promote path swallows the interactive pause inside FlushAsync
}

// Resolve the parked PRE cut-in optional (ForCutIn pool): record the answer + resume the cut-in pool, the
// deferred provider replays it. Same shape as the established C-Atk-Raid / C-EoT2 window-optional resolution.
async Task ResolveWindowOptional(EngineContext context, ChoiceResult answer)
{
    context.ChoiceController.ResolveChoice(answer);
    try { await AutoProcessing.ForCutIn(context).ResumeSuspendedWindowsAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* re-parked */ }
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string num, string? instance = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 };
    cards.Upsert(new CardRecord(defId, num, num, defMeta, CardType: "Digimon"));
    var id = new HeadlessEntityId(instance ?? $"{owner.Value}:battle:{num}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false };
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

EffectMutation Delete(HeadlessEntityId cardId) =>
    new(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("2:battle:FOE"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value });

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(EngineContext context, HeadlessEntityId cardId, string key) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
