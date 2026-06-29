using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST7.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Phase 1 vertical slice: ported card ST7_10 (1:1 mirror of the original).
//   [All Turns] <Security Attack +1>   -> continuous self modifier folded by ContinuousModifierGate
//   [When Attacking] <Piercing>        -> keyword grant reusing KeywordBaseBatch1 wiring
// Drives the real porting recipe end-to-end: CardEffectRegistrar.RegisterOnEnterPlay materialises the
// card's bindings into the EffectRegistry, then the live gates resolve them.

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Card = new("p1:battle:ST7_10");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Security Attack +1 is folded in by the continuous gate", () => Pure(SecurityAttackPlusOne)),
    ("Piercing keyword is registered and queryable", () => Pure(PiercingRegistered)),
    ("Both ST7_10 effects register (1 continuous + 1 keyword)", () => Pure(BothEffectsRegister)),
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

void SecurityAttackPlusOne()
{
    EngineContext context = Board();
    Register(context);
    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, Card, baseSecurityAttack: 1), "base 1 + 1 = 2 security attack");
    AssertEqual(3, ContinuousModifierGate.ResolveSecurityAttack(context, Card, baseSecurityAttack: 2), "base 2 + 1 = 3 security attack");
}

void PiercingRegistered()
{
    EngineContext context = Board();
    Register(context);
    int pierce = context.EffectRegistry.GetKeywordEffects("Piercing").Count;
    AssertTrue(pierce >= 1, $"a Piercing keyword binding is registered (found {pierce})");
}

void BothEffectsRegister()
{
    EngineContext context = Board();
    IReadOnlyList<EffectBinding> registered = CardEffectRegistrar.RegisterOnEnterPlay(context, new ST7_10(), "ST7_10", Source(context));
    AssertEqual(2, registered.Count, "ST7_10 registers exactly two effects");
}

// --- Helpers -------------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("ST7_10"), "ST7_10", "MetalGreymon", new Dictionary<string, object?>(), CardType: "Digimon"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("ST7_10"), P1));
    return context;
}

CardSource Source(EngineContext context) => new(context, Card, P1);

void Register(EngineContext context) =>
    CardEffectRegistrar.RegisterOnEnterPlay(context, new ST7_10(), "ST7_10", Source(context));

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
