using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
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
// collected (AutoProcessingTriggerCollector has zero live callers post-cutover).
//
// RE-TARGETED (4b B2-c1): main-phase arrival is now driven by DcgoMatch.CreatePumpDriven's natural auto-flow
// (EXEMPLAR-T1/α precedent) — the OLD AdvancePhase step currency is RETIRED. The live OnStartMainPhase probe
// fires through the pump's real main-entry window; the OnEndMainPhase-dormant-on-entry negative guard is
// preserved on the retained EffectRegistry substrate (never enters the leaving-main transition).
//
// RETIRED (4b B2-c1, invented verification target = deletion-bound): the two subtests "OnEndMainPhase fires on
// leaving main" / "OnEndAttackPhase fires on leaving main" are DELETED. Their verification target — the port's
// PassAction.cs:28-29 dead-timing emit — is bridge-era INVENTION: OnEndMainPhase / OnEndAttackPhase are AS-IS
// DEAD timings (declared in the AS-IS EffectTiming enum but NEVER stacked/gated there and reacted to by ZERO
// cards — F1-DEAD block in Script/CardEffectCommons/EffectTiming.cs, TriggerTimings.cs:79-84). The live mirror
// window (SkillWindowSupply) rightly never opens them. "Fires on leaving main" is not derivable from AS-IS, so
// it cannot be retargeted without inventing behavior — per §2.1-P-B "step concept is an invention → retire,
// record reason", these assertions are retired rather than forced green (F62's own prior header flagged them
// as retirement candidates for coordinator disposition; this batch disposes: RETIRE).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Phase timing constants are defined", () => Pure(ConstantsDefined)),
    ("OnStartMainPhase effect fires when the main phase is entered", StartMainPhaseFires),
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

async Task EndMainPhaseDormantOnEntry()
{
    // (④ harness rewire) The registry-bound RecordingFakeEffect substrate is deleted. Re-express the SAME
    // negative guard on the live path: a real CardSource-bound probe keyed to OnEndMainPhase must NOT fire
    // when only ENTERING main (the SkillWindowSupply window for OnEndMainPhase — an AS-IS dead timing — never
    // opens on main entry), so ResolveCalls stays 0.
    (DcgoMatch match, TurnBoundaryProbe effect) = await CreateLiveMatchAsync(EffectTiming.OnEndMainPhase);
    await AdvanceToMainAsync(match, P1);
    AssertEqual(0, effect.ResolveCalls, "OnEndMainPhase stays dormant when only entering main");
}

// --- Phase driving (pump auto-flow, α/EXEMPLAR-T1 precedent) --------------

// Drive the pump's natural Active→Draw→Breeding→Main auto-flow to P1's main wait — no OLD AdvancePhase
// step currency. Breeding/Mulligan decisions are declined so main is reached with the OnStartMainPhase
// window already drained (any live probe bound to that timing has fired exactly once).
static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, player));
    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advanced to main");
}

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
            $"pump drive did not reach the expected state — phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
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

// --- Harness (mirrors G3.5-W1b) ------------------------------------------

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

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
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

// (harness triage) TEST-LOCAL live probe, used for the RDW-07-CLOSED
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
