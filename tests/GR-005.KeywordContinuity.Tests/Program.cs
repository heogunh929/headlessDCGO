using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GR-005: a self-static keyword (<Blocker>/<Jamming>/<Piercing>) is registered as an EffectRegistry binding
// when its card enters play, NOT as a per-instance metadata flag. ContinuousKeywordGate derives keyword
// presence from the registry (the working pull pattern used by DP / Security Attack modifiers), so the
// keyword consumers see a ported self-static. Before this, BlockTiming/BattleResolver/SecurityResolver read
// a metadata flag the self-static never set, so the keyword was inert in live play.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Action Body)[]
{
    ("ContinuousKeywordGate derives Blocker/Jamming/Piercing from a registered self-static binding", GateDerivesRegisteredKeywords),
    ("A registered <Blocker> with NO metadata flag is a live block candidate (the bridge)", RegisteredBlockerIsLiveCandidate),
    ("B-group keywords (Reboot/Rush/Blitz/Retaliation) are derived by the gate (preemptive seal)", GateDerivesBGroupKeywords),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}\n{ex}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

void GateDerivesRegisteredKeywords()
{
    EngineContext context = Context();
    var kw = PlaceDigimon(context, P2, "KW", dp: 3000);
    var plain = PlaceDigimon(context, P2, "PLAIN", dp: 3000);
    Register(context, new TfxKeywords(), "TfxKeywords", kw, P2);

    foreach (var keyword in new[] { ContinuousKeywordGate.Blocker, ContinuousKeywordGate.Jamming, ContinuousKeywordGate.Piercing })
    {
        AssertTrue(ContinuousKeywordGate.HasKeyword(context, kw, keyword), $"registered self-static <{keyword}> is derived from the registry");
        AssertTrue(!ContinuousKeywordGate.HasKeyword(context, plain, keyword), $"a card with no keyword binding is not a <{keyword}>");
    }

    // The bridge is specifically NOT the metadata flag — confirm the instance carries no hasBlocker flag.
    context.CardInstanceRepository.TryGetInstance(kw, out CardInstanceRecord? inst);
    AssertTrue(inst is not null && !(inst!.Metadata.TryGetValue("hasBlocker", out var v) && v is true),
        "the keyword works without any hasBlocker metadata flag set");
}

void RegisteredBlockerIsLiveCandidate()
{
    EngineContext context = Context();
    var attacker = PlaceDigimon(context, P1, "ATK", dp: 4000);
    var blocker = PlaceDigimon(context, P2, "BLK", dp: 3000);
    Register(context, new TfxKeywords(), "TfxKeywords", blocker, P2); // registers <Blocker> (+ Jamming/Piercing)

    context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    var candidates = new BlockTiming().GetBlockerCandidates(context);
    AssertTrue(candidates.Any(c => c.BlockerId == blocker),
        "the registered <Blocker> is a live block candidate (would be empty before GR-005)");
}

void GateDerivesBGroupKeywords()
{
    // No card ports Reboot/Rush/Blitz/Retaliation as a self-static yet, so register the keyword batches
    // directly (the same bindings a future self-static would create) and confirm the gate derives them —
    // the consumers (EarlyPhaseFlow/AttackPermanentAction/BattleResolver) now OR this gate, so such a card
    // will work without re-touching those sites.
    EngineContext context = Context();
    var card = PlaceDigimon(context, P2, "BG", dp: 3000);
    var plain = PlaceDigimon(context, P2, "BGPLAIN", dp: 3000);
    var effectContext = new EffectContext(P2, card);
    KeywordBaseBatch1Factory.RegisterBaseBatch1(context.EffectRegistry, card, P2, effectContext);
    KeywordBaseBatch2Factory.RegisterBaseBatch2(context.EffectRegistry, card, P2, effectContext);

    foreach (var keyword in new[] { ContinuousKeywordGate.Reboot, ContinuousKeywordGate.Rush, ContinuousKeywordGate.Blitz, ContinuousKeywordGate.Retaliation })
    {
        AssertTrue(ContinuousKeywordGate.HasKeyword(context, card, keyword), $"registered <{keyword}> is derived by the gate (B-group seal)");
        AssertTrue(!ContinuousKeywordGate.HasKeyword(context, plain, keyword), $"a plain card is not a <{keyword}>");
    }
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

HeadlessEntityId PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, int dp)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

void Register(EngineContext context, CEntity_Effect effect, string number, HeadlessEntityId source, HeadlessPlayerId owner) =>
    CardEffectRegistrar.RegisterOnEnterPlay(context, effect, number, new CardSource(context, source, owner, owner));

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
