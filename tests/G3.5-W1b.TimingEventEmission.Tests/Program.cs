using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-W1 (part 2): the engine now EMITS timing-window events at action points that produce no zone
// move — turn boundaries, digivolution, draw, security checks — so effects bound to OnStartTurn /
// OnEndTurn / WhenDigivolving / OnDraw / OnSecurityCheck fire through the common loop.
//
// (harness triage, R3-C2b-2 window cutover) The e2e subtests originally registered a synthetic non-card
// RecordingFakeEffect directly on the EffectRegistry, collected by AutoProcessingTriggerCollector — that
// collector has ZERO live callers post-cutover (GameFlowProcessor.AutoProcessAsync now drives the mirror
// SkillInfo trigger stack; see window_skillinfo_cutover_design_2026-07-14.md), so a raw registry binding is
// never discovered anymore. OnStartTurn/OnEndTurn are AS-IS null-payload windows that the live supply layer
// DOES handle (SkillWindowSupply RDW-07 CLOSED: the port's turn emits already carry the explicit timing
// TriggerEventEmitter stamps, so TriggerTimingMap derives them and GameFlowProcessor.AutoProcessAsync
// stacks+drains them through the live MultipleSkills window) — the BEHAVIOR is re-expressible, so this file
// re-attaches the SAME assertions (fires exactly once on the boundary; stays dormant for an unrelated
// timing) via the live mechanism: a real CardSource-bound ActivateClass (CEntity_Effect.CardEffects), the
// same shape as TfxOnLeaveFieldCounter / TfxWhenLinkedCounter, instead of a registry-only fake.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Emitter publishes an event that derives exactly the given timing", () => Pure(EmitterPublishesTiming)),
    ("Canonical timing constants are defined", () => Pure(ConstantsDefined)),
    ("OnEndTurn effect fires when a turn ends", EndTurnFiresOnEndTurn),
    ("OnStartTurn effect fires on the next turn", EndTurnFiresOnStartTurn),
    ("A turn-boundary effect bound to an unrelated timing does NOT fire", UnrelatedTimingDoesNotFire),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Emitter -------------------------------------------------------------

void EmitterPublishesTiming()
{
    var queue = new GameEventQueue();
    TriggerEventEmitter.Emit(queue, TriggerTimings.WhenDigivolving, actor: P1, subject: new HeadlessEntityId("c1"));

    IReadOnlyList<GameEvent> drained = queue.DrainPending();
    AssertEqual(1, drained.Count, "one event published");

    GameEvent e = drained[0];
    AssertEqual(P1, e.Actor, "structured actor");
    AssertEqual(TriggerTimings.WhenDigivolving, e.Cause, "cause = timing");

    var timings = TriggerTimingMap.Derive(e);
    AssertEqual(1, timings.Count, "derives exactly the emitted timing");
    AssertEqual(TriggerTimings.WhenDigivolving, timings[0], "timing value");
}

void ConstantsDefined()
{
    foreach (string t in new[]
    {
        TriggerTimings.OnStartTurn, TriggerTimings.OnEndTurn, TriggerTimings.WhenDigivolving,
        TriggerTimings.OnDraw, TriggerTimings.OnSecurityCheck,
    })
    {
        AssertTrue(!string.IsNullOrWhiteSpace(t), "timing constant non-empty");
    }
}

// --- E2E -----------------------------------------------------------------

// (4b B6 re-pin) The OLD EndTurn ACTION seam is retired: the pump's real turn boundary is driven by the
// legal Pass lane (AS-IS PassTurn -> EndTurnProcess fires the OnEndTurn window pre-flip, AutoProcessing
// :1511; the next ActivePhase fires OnStartTurn, TurnStateMachine :199). Counts are asserted as DELTAS
// across the boundary because the pump's own first-turn start ALSO legitimately opens OnStartTurn.

async Task EndTurnFiresOnEndTurn()
{
    (DcgoMatch match, TurnBoundaryProbe effect) = await CreateMatchAsync(EffectTiming.OnEndTurn);
    int before = effect.ResolveCalls;
    AssertEqual(0, before, "OnEndTurn effect dormant before any turn end");
    await PassToNextMainAsync(match);
    AssertEqual(before + 1, effect.ResolveCalls, "OnEndTurn effect fired exactly once when the turn ended");
}

async Task EndTurnFiresOnStartTurn()
{
    (DcgoMatch match, TurnBoundaryProbe effect) = await CreateMatchAsync(EffectTiming.OnStartTurn);
    int before = effect.ResolveCalls; // the pump's own first-turn start may already have opened OnStartTurn
    await PassToNextMainAsync(match);
    AssertEqual(before + 1, effect.ResolveCalls, "OnStartTurn effect fired exactly once when the next turn started");
}

async Task UnrelatedTimingDoesNotFire()
{
    (DcgoMatch match, TurnBoundaryProbe effect) = await CreateMatchAsync(EffectTiming.WhenDigivolving);
    await PassToNextMainAsync(match);
    AssertEqual(0, effect.ResolveCalls, "an effect bound to an unrelated timing does not fire on end turn");
}

async Task PassToNextMainAsync(DcgoMatch match)
{
    LegalAction pass;
    using (AmbientMatchContext.Enter(match.Context))
    {
        pass = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.Pass);
    }
    using (AmbientMatchContext.Enter(match.Context))
    {
        await match.ApplyActionAsync(pass);
        await match.StepAsync();
        await match.StepAsync();
    }
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
}

// --- Harness -------------------------------------------------------------

async Task<(DcgoMatch Match, TurnBoundaryProbe Effect)> CreateMatchAsync(EffectTiming timing)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new HeadlessDCGO.Engine.Headless.Diagnostics.EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));

    var fxDef = new HeadlessEntityId("DEF:FX");
    cards.Upsert(new CardRecord(fxDef, fxDef.Value, "FX", new Dictionary<string, object?>(), CardType: "Digimon"));
    var fxId = new HeadlessEntityId("1:battle:FX");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(fxId, fxDef, P1, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(
        P1, fxId, HeadlessDCGO.Engine.Headless.Choices.ChoiceZone.None, HeadlessDCGO.Engine.Headless.Choices.ChoiceZone.BattleArea));

    var effect = new TurnBoundaryProbe(timing);
    CardEffectRegistrar.RegisterOnEnterPlay(context, effect, "FX", new CardSource(context, fxId, P1));

    // (4b B6) drive the pump's natural auto-flow to P1's main wait (F62/EXEMPLAR precedent) — the probe is
    // registered BEFORE the drive, so a first-turn OnStartTurn opening is a legitimate pre-boundary count.
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
    return (match, effect);
}

// --- Phase driving (pump auto-flow, F62/EXEMPLAR-T1 precedent, 4b B6) -----
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
            bool decline = match.Context.ChoiceController.PendingRequest!.Type
                is HeadlessDCGO.Engine.Headless.Choices.ChoiceType.BreedingDecision
                or HeadlessDCGO.Engine.Headless.Choices.ChoiceType.Mulligan;
            await ResolvePendingDriveAsync(match, skip: decline);
        }
        else await StepOnceDriveAsync(match);
    }
    if (!condition(match))
    {
        var t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingDriveAsync(DcgoMatch match, bool skip)
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

static async Task StepOnceDriveAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

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

// (harness triage) TEST-LOCAL live replacement for the registry-only RecordingFakeEffect: an uncapped
// ActivateClass reactor (CEntity_Effect.CardEffects) bound to a given AS-IS null-payload turn-boundary
// EffectTiming, collected by the live SkillInfo scan exactly like TfxOnLeaveFieldCounter / TfxWhenLinkedCounter.
public sealed class TurnBoundaryProbe : CEntity_Effect
{
    private readonly EffectTiming _fireOn;
    public int ResolveCalls { get; private set; }

    public TurnBoundaryProbe(EffectTiming fireOn) => _fireOn = fireOn;

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == _fireOn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("probe", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, $"[probe] fires on {_fireOn}.");
            effects.Add(activateClass);

            bool CanUseCondition(System.Collections.Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

            bool CanActivateCondition(System.Collections.Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

            Task ActivateCoroutine(System.Collections.Hashtable _hashtable)
            {
                ResolveCalls++;
                return Task.CompletedTask;
            }
        }

        return effects;
    }
}
