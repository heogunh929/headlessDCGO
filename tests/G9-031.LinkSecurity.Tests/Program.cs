using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W2 (G9-031): the last three W2 primitives:
//  - LinkEffect -> LinkSelfEffect: choose a host + attach this card as a link card (LinkHelpers).
//  - PlaceSelfDelayOptionSecurityEffect / PlaySelfDigimonAfterBattleSecurityEffect -> play this card from
//    security to the battle area (reuse PlayThisCardToBattleEffect).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("LinkEffect: attach this card to a chosen host, pay the link cost", LinkAttaches),
    ("PlaceSelfDelayOptionSecurityEffect: plays this card from security to battle", PlaceDelayOption),
    ("PlaySelfDigimonAfterBattleSecurityEffect: plays this Digimon from security to battle", PlayAfterBattle),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task LinkAttaches()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    // (G-Link 배치 2 flip, RD-P6C2-7 해소) The invented LinkSelfEffect era staged synthetic defs with a
    // "linkCost" metadata key; the AS-IS 1:1 flip sources the cost from the card's DECLARED LinkCondition
    // (AddSelfLinkConditionStaticEffect -> linkCondition.cost), so the fixture now stages the REAL ported
    // K:Link card EX10_029 (link cost 2, Appmon host condition) and an Appmon-trait host. The assertions
    // below are UNCHANGED (attach + 5 -> 3 memory).
    // The AS-IS registration + body read GManager.instance.* (effect enumeration walks the disabled-tree scan,
    // SelectPermanentEffect component, cut-in AutoProcessing) — enter the ambient match scope BEFORE staging,
    // exactly like the engine registers cards inside a match scope (the PlaceDelayOption case below).
    using var _ = HeadlessDCGO.Engine.Headless.Bridge.AmbientMatchContext.Enter(context);
    HeadlessDCGO.Engine.Headless.DataLoading.CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    var host = await Place(context, P1, "HOST", ChoiceZone.BattleArea, traits: new[] { "Appmon" });
    HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectRegistrar.RegisterCard(context, host, P1);
    var linkCard = await PlaceReal(context, P1, "EX10_029", ChoiceZone.Hand);

    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(host));
    var effect = (HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass)CardEffectFactory.LinkEffect(new CardSource(context, linkCard, P1));
    await effect.Activate(new System.Collections.Hashtable());

    var linked = LinkHelpers.ReadLinkedCardIds(
        context.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? h) && h is not null ? h.Metadata : new Dictionary<string, object?>());
    AssertTrue(linked.Contains(linkCard), "the link card is now attached to the host");
    AssertEqual(3, context.MemoryController.Current.Current, "link cost 2 paid: 5 -> 3");
}

async Task PlaceDelayOption()
{
    EngineContext context = Context();
    // A [Security] effect resolves while its card is in the executing area (AS-IS IsExistOnExecutingArea /
    // PlaceDelayOptionCards default root = Execution), the transient position a revealed security card occupies
    // during a security check — not the Security stack itself.
    var card = await Place(context, P1, "OPT", ChoiceZone.Execution, linkCost: 0);
    // (item-4 cast-seat alignment) CardEffectFactory.PlaceSelfDelayOptionSecurityEffect was re-ported to the
    // AS-IS Script/CardEffectFactory shape and now returns an ActivateClass (there is no PlayThisCardToBattleEffect
    // type any more). Drive it the AS-IS ActivateICardEffect way — ActivateClass.Activate(Hashtable) — under an
    // ambient match scope (the body reads GManager.instance.* like AS-IS). Its ported ActivateCoroutine plays the
    // card cost-free onto the owner's battle area (PlaceDelayOptionCards).
    using var _ = HeadlessDCGO.Engine.Headless.Bridge.AmbientMatchContext.Enter(context);
    var effect = (HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass)
        CardEffectFactory.PlaceSelfDelayOptionSecurityEffect(new CardSource(context, card, P1));
    await effect.Activate(new System.Collections.Hashtable());
    AssertTrue(InBattle(context, card), "the card was placed into the battle area from security");
}

async Task PlayAfterBattle()
{
    EngineContext context = Context();
    var card = await Place(context, P1, "DIG", ChoiceZone.Execution, linkCost: 0);
    // (item-4 cast-seat alignment + honest STOP downgrade) CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect
    // was re-ported to the AS-IS Script/CardEffectFactory shape and now returns an ActivateClass (the mirror-invented
    // PlaySelfAtEndOfBattleSecurityEffect type — with its .Apply(sink) OnEndBattle registration — was DELETED). The
    // old test cast to that defunct type, an InvalidCastException CRASH. Driving the factory the AS-IS way
    // (ActivateClass.Activate) reaches the factory's HONEST STOP: the AS-IS end-of-battle deferred play nests the
    // Player.UntilEndBattleEffects / Permanent.UntilOpponentTurnEndEffects mutable grant buckets (unlanded
    // player/permanent-grant store, P6A-PLAYER-EFFECTLIST) + a DestroyPermanentsClass batch-delete with no mirror —
    // the full RD-P6C3-B2 fold, out of this batch's scope. So the crash is DOWNGRADED to the explicit
    // NotSupportedException STOP (design item RD-P6C3-B2). Assertions below are KEPT (unreached until RD-P6C3-B2
    // lands) — tracked, not silently weakened, exactly like the LinkEffect (RD-P6C2-7) case above.
    using var _ = HeadlessDCGO.Engine.Headless.Bridge.AmbientMatchContext.Enter(context);
    var effect = (HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass)
        CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect(new CardSource(context, card, P1));
    await effect.Activate(new System.Collections.Hashtable());
    AssertTrue(!InBattle(context, card), "the Digimon is NOT played immediately (deferred to end of battle)");
    AssertTrue(((HeadlessDCGO.Engine.Headless.Effects.EffectRegistry)context.EffectRegistry)
        .GetEffects(card, HeadlessDCGO.Engine.Headless.Effects.TriggerTimings.OnEndBattle).Count > 0,
        "an OnEndBattle trigger was registered to play the Digimon after the battle");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 931);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (G-Link 배치 2) AS-IS effects gate on a STARTED game (DoneStartGame / a live phase) — the a03072ee
    // G3.5-D1L fixture convention: stamp the Main phase so CanUse/CanTrigger scans evaluate as in-match.
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag, ChoiceZone zone, int linkCost = 0, string[]? traits = null)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4, ["linkCost"] = linkCost };
    if (traits is { Length: > 0 })
    {
        defMeta["traits"] = traits;
    }

    cards.Upsert(new CardRecord(defId, tag, tag, defMeta, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

// (G-Link 배치 2) 실카드 인스턴스 스테이징 — cards.json def(id = 카드번호)를 그대로 사용(EXEMPLAR Stage 관례).
async Task<HeadlessEntityId> PlaceReal(EngineContext context, HeadlessPlayerId owner, string cardNumber, ChoiceZone zone)
{
    var defId = new HeadlessEntityId(cardNumber);
    if (!context.CardRepository.TryGetCard(defId, out CardRecord? def) || def is null)
    {
        throw new InvalidOperationException($"definition {cardNumber} not found in the loaded card database");
    }

    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectRegistrar.RegisterCard(context, id, owner);
    return id;
}

bool InBattle(EngineContext context, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.BattleArea).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
