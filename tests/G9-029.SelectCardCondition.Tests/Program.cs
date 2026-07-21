using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W2 (G9-029): the fuller SelectCardConditionClass descriptor drives the AS-IS reveal-select commons
// (CardEffectCommons.RevealDeckTopCardsAndSelect — 이연③-f re-target off the retired declarative classes).
// Reveal 3, add 1 Tamer to hand, rest to deck bottom — proving the descriptor is wired to the working
// reveal-select. Driven directly with a ScriptedChoiceProvider (the retired TfxSelectCardCond fixture is gone).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("SelectCardConditionClass reveal-select: Tamer -> hand, others -> deck bottom", SelectViaFullDescriptor),
    ("(P4) FULL RevealDeckTopCardsAndSelect commons: 2 passes over the shared pool, Custom pick recorded", FullMultiConditionFactory),
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

async Task SelectViaFullDescriptor()
{
    EngineContext context = Context();
    var host = await PlaceFixture(context, P1, "TfxHost");
    var tamer = await PlaceLibrary(context, P1, "TAMER", "Tamer");
    await PlaceLibrary(context, P1, "DIGI1", "Digimon");
    await PlaceLibrary(context, P1, "DIGI2", "Digimon");

    bool IsTamer(CardSource cs) =>
        context.CardInstanceRepository.TryGetInstance(cs.InstanceId, out CardInstanceRecord? inst) && inst is not null &&
        context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null && def.IsCardType("Tamer");

    var activate = new ActivateClass();
    activate.SetUpICardEffect("Reveal 3, add 1 Tamer to hand (SelectCardConditionClass).", _ => true, new CardSource(context, host, P1));

    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(tamer));
    // (이연③-f RE-TARGET) drive the AS-IS commons RevealDeckTopCardsAndSelect with the fuller SelectCardConditionClass
    // descriptor directly (replacing the retired SimplifiedRevealAndSelectEffect + its TfxSelectCardCond fixture).
    await CardEffectCommons.RevealDeckTopCardsAndSelect(
        revealCount: 3,
        selectCardConditions: new SelectCardConditionClass[]
        {
            new SelectCardConditionClass(
                canTargetCondition: IsTamer,
                canTargetCondition_ByPreSelecetedList: null, canEndSelectCondition: null,
                canNoSelect: true, selectCardCoroutine: null,
                message: "Select 1 Tamer card.", maxCount: 1, canEndNotMax: false,
                mode: SelectCardEffect.Mode.AddHand),
        },
        remainingCardsPlace: RemainingCardsPlace.DeckBottom,
        activateClass: activate);

    var zones = (IZoneStateReader)context.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.Hand).Contains(tamer), "selected Tamer went to hand");
    AssertEqual(2, zones.GetCards(P1, ChoiceZone.Library).Count, "the 2 others returned to the deck bottom");
}

// (이연③-f RE-TARGET) the FULL multi-condition mirror (BT10-096 shape) now drives the AS-IS commons
// CardEffectCommons.RevealDeckTopCardsAndSelect (the retired RevealMultiSelectEffect class was a mirror-invented
// duplicate of it): pass 0 mandatory Tamer -> hand; pass 1 optional Digimon -> Custom (captured by the pass's
// selectCardCoroutine, NOT moved — the card script's follow-up plays it); rest -> deck bottom. The commons
// stages every move on its own sink and flushes internally.
async Task FullMultiConditionFactory()
{
    EngineContext context = Context();
    var host = await PlaceFixture(context, P1, "TfxHost");
    var tamer = await PlaceLibrary(context, P1, "TAMER", "Tamer");
    var digi1 = await PlaceLibrary(context, P1, "DIGI1", "Digimon");
    await PlaceLibrary(context, P1, "DIGI2", "Digimon");

    bool IsType(HeadlessEntityId id, string type) =>
        context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? i) && i is not null &&
        context.CardRepository.TryGetCard(i.DefinitionId, out CardRecord? d) && d is not null && d.IsCardType(type);

    var captured = new List<HeadlessEntityId>();
    Task Capture(CardSource cs) { captured.Add(cs.InstanceId); return Task.CompletedTask; }

    var activate = new ActivateClass();
    activate.SetUpICardEffect("Reveal 3: Tamer to hand, Digimon played free.", _ => true, new CardSource(context, host, P1));
    activate.SetUpActivateClass(null, _ => Task.CompletedTask, -1, false, "Reveal 3: Tamer to hand, Digimon played free.");

    var provider = (ScriptedChoiceProvider)context.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(tamer));   // pass 0 (mandatory Tamer -> hand)
    provider.Enqueue(ChoiceResult.Select(digi1));   // pass 1 (optional Digimon -> Custom)

    await CardEffectCommons.RevealDeckTopCardsAndSelect(
        revealCount: 3,
        selectCardConditions: new SelectCardConditionClass[]
        {
            new SelectCardConditionClass(
                canTargetCondition: cs => IsType(cs.InstanceId, "Tamer"),
                canTargetCondition_ByPreSelecetedList: null, canEndSelectCondition: null,
                canNoSelect: false, selectCardCoroutine: null,
                message: "Select 1 Tamer.", maxCount: 1, canEndNotMax: false,
                mode: SelectCardEffect.Mode.AddHand),
            new SelectCardConditionClass(
                canTargetCondition: cs => IsType(cs.InstanceId, "Digimon"),
                canTargetCondition_ByPreSelecetedList: null, canEndSelectCondition: null,
                canNoSelect: true, selectCardCoroutine: Capture,
                message: "Select 1 Digimon.", maxCount: 1, canEndNotMax: false,
                mode: SelectCardEffect.Mode.Custom),
        },
        remainingCardsPlace: RemainingCardsPlace.DeckBottom,
        activateClass: activate);

    var zones = (IZoneStateReader)context.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.Hand).Contains(tamer), "pass-0 Tamer went to hand");
    AssertTrue(zones.GetCards(P1, ChoiceZone.Library).Contains(digi1), "the Custom pick is NOT moved by the flow");
    AssertEqual(1, captured.Count, "the Custom pick is recorded (via the pass selectCardCoroutine) for the card script");
    AssertEqual(digi1.Value, captured[0].Value, "recorded pick = the pass-1 selection");
    AssertEqual(2, zones.GetCards(P1, ChoiceZone.Library).Count, "custom pick + the untouched card remain in the library (rest to bottom)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 929);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceFixture(EngineContext context, HeadlessPlayerId owner, string cardNumber)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber);
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:src:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceLibrary(EngineContext context, HeadlessPlayerId owner, string tag, string cardType)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:lib:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Library));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
