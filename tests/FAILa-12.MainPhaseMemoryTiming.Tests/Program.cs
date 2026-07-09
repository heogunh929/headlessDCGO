using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #12 (mapping remediation): the "[Start of Your Main Phase]" Tamer memory-gain effects must register at
// OnStartMainPhase, NOT OnStartTurn (which fires earlier during unsuspend/draw). AS-IS descriptions are
// "[Start of Your Main Phase]" and both known cards (BT23_081/BT23_083) return them under OnStartMainPhase.

HeadlessPlayerId P1 = new(1);
string MainPhase = HeadlessDCGO.Engine.Headless.Effects.TriggerTimings.OnStartMainPhase;
string StartTurn = HeadlessDCGO.Engine.Headless.Effects.TriggerTimings.OnStartTurn;

var ctx = EngineContext.CreateDefault(randomSeed: 912);
var cards = (CardDatabase)ctx.CardRepository;
cards.Upsert(new CardRecord(new HeadlessEntityId("T"), "T", "Tamer", new Dictionary<string, object?>(), CardType: "Tamer"));
var tamer = new HeadlessEntityId("p1:battle:T");
ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(tamer, new HeadlessEntityId("T"), P1));
var card = new CardSource(ctx, tamer, P1);

var effects = new (string Name, ICardEffect Effect)[]
{
    ("Gain1MemoryTamerOpponentDigimonEffect", CardEffectFactory.Gain1MemoryTamerOpponentDigimonEffect(card)),
    ("Gain1MemoryTamerOwnerDigimonConditionalEffect", CardEffectFactory.Gain1MemoryTamerOwnerDigimonConditionalEffect(
        "[Start of Your Main Phase] If you have a matching Digimon, gain 1 memory.", permanentCondition: null, condition: null, card)),
};

var failures = new List<string>();
foreach (var (name, effect) in effects)
{
    string timing = ((IHeadlessCardEffect)effect).Definition.Timing;
    try
    {
        AssertEqual(MainPhase, timing, $"{name} registers under OnStartMainPhase");
        AssertTrue(timing != StartTurn, $"{name} does NOT register under OnStartTurn");
        Console.WriteLine($"PASS {name} (timing={timing})");
    }
    catch (Exception ex) { failures.Add(name); Console.Error.WriteLine($"FAIL {name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{effects.Length} test(s) passed.");

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
