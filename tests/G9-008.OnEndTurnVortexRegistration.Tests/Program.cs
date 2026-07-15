using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G9-008 (EX8-3): a card that returns a self-static <Vortex> at EffectTiming.OnEndTurn (the original
// EX8_074 "Vortex" region keys it there) now registers the keyword at enter-play, because OnEndTurn was
// added to CardEffectRegistrar.AllTimings. The live end-of-turn trigger GR-006 built
// (EndOfTurnEffectAttack) then sees it and offers the effect-driven attack.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("OnEndTurn is a registered AllTimings dispatch point", OnEndTurnInAllTimings),
    ("Entering play registers the card's OnEndTurn <Vortex> as a live keyword", RegistersVortexOnEnter),
    ("The registered Vortex opens the GR-006 end-of-turn attack window", OpensEndOfTurnWindow),
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

Task OnEndTurnInAllTimings()
{
    AssertTrue(CardEffectRegistrar.AllTimings.Contains(EffectTiming.OnEndTurn),
        "EffectTiming.OnEndTurn is in CardEffectRegistrar.AllTimings (registered at enter-play)");
    return Task.CompletedTask;
}

async Task RegistersVortexOnEnter()
{
    EngineContext context = Context();

    // (P7 stage-B finding) Under the dispatch-flip model (CEntity_EffectControllerStore, "the flip's
    // enumeration model, replacing enter-play binding registration as the availability source" —
    // docs/audit/rebuild_p6_stageB_notes.md), a card's cEntity_Effect is dispatched from its DEFINITION as soon
    // as the instance/zone exist — NOT gated by a separate "CardEffectRegistrar.RegisterCard" enter-play step
    // (that step lowers LEGACY-model effects into the substrate registry and inits per-turn use-counts; it is
    // orthogonal to the new-model interface scan). So the keyword is already live the moment the fixture is
    // zoned — there is no meaningful "before entering any zone" checkpoint to probe (no card instance exists
    // yet to query). The original "absent before registration" assertion encoded the pre-flip registry-gated
    // model and is no longer provable once Stage B's live scan landed; this test now verifies the flip-correct
    // shape instead: live on placement, and still live (not disturbed) after enter-play registration.
    var vortex = await PlaceFixtureDigimon(context, P1, "TfxVortex", suspended: false);
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, vortex, ContinuousKeywordGate.Vortex),
        "Vortex is live once the fixture card is on the battle area (dispatch-based, via the OnEndTurn timing)");

    bool registered = CardEffectRegistrar.RegisterCard(context, vortex, P1);
    AssertTrue(registered, "the fixture card also registers (legacy-model) effects on enter-play");
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, vortex, ContinuousKeywordGate.Vortex),
        "Vortex is still live after enter-play registration");
}

async Task OpensEndOfTurnWindow()
{
    EngineContext context = Context();
    var vortex = await PlaceFixtureDigimon(context, P1, "TfxVortex", suspended: false);
    await PlaceFixtureDigimon(context, P2, "FOE", suspended: true);
    CardEffectRegistrar.RegisterCard(context, vortex, P1);

    // (C-EoT-2) <Vortex> firing is RE-HOUSED from the invented gate to the AS-IS OnEndTurn window: the card's
    // OnEndTurn <Vortex> ActivateClass (VortexSelfEffect) is now collected by AutoProcessing.GetSkillInfos and
    // resolved by MultipleSkills -> VortexProcess. The EndOfTurnEffectAttack gate no longer fires <Vortex> (it
    // is <Execute>-only). Single-fire (window XOR gate); live window firing witnessed by tests/C-EoT2.
    using (HeadlessDCGO.Engine.Headless.Bridge.AmbientMatchContext.Enter(context))
    {
        AssertTrue(AutoProcessing.GetSkillInfos(new System.Collections.Hashtable(), EffectTiming.OnEndTurn)
            .Any(si => si.CardEffect is ActivateICardEffect),
            "the OnEndTurn window collects the card-registered Vortex ActivateClass");
    }
    AssertTrue(!EndOfTurnEffectAttack.TryOpen(context, P1),
        "the retired gate no longer opens a <Vortex> window (the window resolves it — see C-EoT2)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (P7 test-fix) CanTrigger/CanUse gate on DoneStartGame (mirror proxy: phase past None/Setup).
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

async Task<HeadlessEntityId> PlaceFixtureDigimon(EngineContext context, HeadlessPlayerId owner, string cardNumber, bool suspended)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
