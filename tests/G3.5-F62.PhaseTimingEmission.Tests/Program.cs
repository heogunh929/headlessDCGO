using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-6.2: phase-boundary timing windows. The engine now opens OnStartMainPhase when an advance enters
// the main phase (the original's verified emit point) and OnEndMainPhase / OnEndAttackPhase on the
// Main→(next) transition. Verified end-to-end: an effect bound to each timing fires through the common
// loop at the right phase transition and stays dormant otherwise.
//
// (harness triage, R3-C2b-2 window cutover) "OnStartMainPhase effect fires" is retargeted to a live
// CardSource-bound ActivateClass probe: SkillWindowSupply RDW-07 CLOSED this timing (AS-IS null-payload,
// the port's TriggerEventEmitter already stamps the explicit timing key so the live mirror stacks + drains
// it) — the BEHAVIOR is re-expressible, the original registry-only RecordingFakeEffect just can no longer be
// collected (AutoProcessingTriggerCollector has zero live callers post-cutover). OnEndMainPhase /
// OnEndAttackPhase stay on the OLD registry harness UNCHANGED and remain red — RETIREMENT CANDIDATES, not
// engine gaps: both are AS-IS DEAD timings (declared in the AS-IS EffectTiming enum but NEVER stacked/gated
// there and reacted to by ZERO cards — see the F1-DEAD block in Script/CardEffectCommons/EffectTiming.cs and
// TriggerTimings.cs:79-84 "not actively fired there; headless opens them"). The port's PassAction.cs:28-29
// emit was bridge-era invention; the live mirror window rightly never opens these (SkillWindowSupply has no
// case — consistent with AS-IS never stacking them). The subtests' expectation ("fires on leaving main") is
// not derivable from AS-IS, so it cannot be retargeted without inventing behavior; left red for coordinator
// disposition.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Phase timing constants are defined", () => Pure(ConstantsDefined)),
    ("OnStartMainPhase effect fires when the main phase is entered", StartMainPhaseFires),
    ("OnEndMainPhase effect fires on leaving the main phase, not on entry", EndMainPhaseFires),
    ("OnEndAttackPhase effect fires on leaving the main phase", EndAttackPhaseFires),
    ("An OnEndMainPhase effect does NOT fire merely on entering main", EndMainPhaseDormantOnEntry),
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

// --- Tests ---------------------------------------------------------------

void ConstantsDefined()
{
    foreach (string t in new[] { TriggerTimings.OnStartMainPhase, TriggerTimings.OnEndMainPhase, TriggerTimings.OnEndAttackPhase })
    {
        AssertTrue(!string.IsNullOrWhiteSpace(t), "timing constant non-empty");
    }
}

async Task StartMainPhaseFires()
{
    (DcgoMatch match, TurnBoundaryProbe effect) = await CreateLiveMatchAsync(EffectTiming.OnStartMainPhase);
    await AdvanceToMainAsync(match, P1);
    AssertEqual(1, effect.ResolveCalls, "OnStartMainPhase effect fired once on main-phase entry");
}

async Task EndMainPhaseFires()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync(TriggerTimings.OnEndMainPhase);
    await AdvanceToMainAsync(match, P1);
    AssertEqual(0, effect.ResolveCalls, "OnEndMainPhase effect does not fire on entry");
    await LeaveMainAsync(match, P1);
    AssertEqual(1, effect.ResolveCalls, "OnEndMainPhase effect fired on leaving the main phase");
}

async Task EndAttackPhaseFires()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync(TriggerTimings.OnEndAttackPhase);
    await AdvanceToMainAsync(match, P1);
    await LeaveMainAsync(match, P1);
    AssertEqual(1, effect.ResolveCalls, "OnEndAttackPhase effect fired on leaving the main phase");
}

async Task EndMainPhaseDormantOnEntry()
{
    (DcgoMatch match, RecordingFakeEffect effect) = await CreateMatchAsync(TriggerTimings.OnEndMainPhase);
    await AdvanceToMainAsync(match, P1);
    AssertEqual(0, effect.ResolveCalls, "OnEndMainPhase stays dormant when only entering main");
}

// --- Phase driving -------------------------------------------------------

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    for (int attempt = 0; attempt < 10 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = SingleLegalAction(match, player, HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advanced to main");
}

async Task LeaveMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    // From the main phase the phase-advancing legal action is Pass (Main→End). Apply it so the
    // Main→(next) transition runs through AdvancePhaseAsync and opens the end-of-main windows.
    LegalAction leave = match.GetLegalActions(player)
        .First(a => a.ActionType is HeadlessActionTypes.Pass or HeadlessActionTypes.AdvancePhase);
    await match.ApplyActionAsync(leave);
    await match.StepAsync();
    AssertTrue(match.GetObservation().Turn.Phase != HeadlessPhase.Main, "left the main phase");
}

LegalAction SingleLegalAction(DcgoMatch match, HeadlessPlayerId player, string actionType)
{
    LegalAction[] actions = match.GetLegalActions(player).Where(a => a.ActionType == actionType).ToArray();
    AssertTrue(actions.Length >= 1, $"{actionType} available");
    return actions[0];
}

// --- Harness (mirrors G3.5-W1b) ------------------------------------------

async Task<(DcgoMatch Match, RecordingFakeEffect Effect)> CreateMatchAsync(string timing)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));

    var effect = new RecordingFakeEffect("fx", "src", timing);
    context.EffectRegistry.Register(new EffectBinding(CreateRequest("fx", "src", timing), effect: effect));
    return (match, effect);
}

// (harness triage) Live variant for the RDW-07-CLOSED OnStartMainPhase timing: a real CardSource-bound
// ActivateClass probe, collected via the mirror SkillInfo scan (same shape as TfxOnLeaveFieldCounter).
async Task<(DcgoMatch Match, TurnBoundaryProbe Effect)> CreateLiveMatchAsync(EffectTiming timing)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));

    var fxDef = new HeadlessEntityId("DEF:FX");
    cards.Upsert(new CardRecord(fxDef, fxDef.Value, "FX", new Dictionary<string, object?>(), CardType: "Digimon"));
    var fxId = new HeadlessEntityId("1:battle:FX");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(fxId, fxDef, P1, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, fxId, ChoiceZone.None, ChoiceZone.BattleArea));

    var effect = new TurnBoundaryProbe(timing);
    CardEffectRegistrar.RegisterOnEnterPlay(context, effect, "FX", new CardSource(context, fxId, P1));
    return (match, effect);
}

static EffectRequest CreateRequest(string effectId, string sourceId, string timing)
{
    var player = new HeadlessPlayerId(1);
    return new EffectRequest(
        new HeadlessEntityId(effectId), player, timing,
        new EffectContext(player, player, new HeadlessEntityId(sourceId), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));
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
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
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

// (harness triage) TEST-LOCAL live replacement for RecordingFakeEffect, used only for the RDW-07-CLOSED
// OnStartMainPhase subtest: an uncapped ActivateClass reactor bound to a given AS-IS null-payload
// turn-boundary EffectTiming, collected by the live SkillInfo scan (same shape as TfxOnLeaveFieldCounter /
// G3.5-W1b's TurnBoundaryProbe).
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
