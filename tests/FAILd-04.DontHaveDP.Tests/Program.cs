using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-d: DontHaveDP (AS-IS IDontHaveDPEffect / Permanent.HasDP==false) was MISSING. DontHaveDPStaticEffect now
// makes ContinuousDpGate.ResolveDp return the -1 no-DP sentinel, overriding the base DP AND every DP modifier
// (AS-IS Permanent.DP returns -1 outright when !HasDP).

const int Base = 3000;

var tests = new (string Name, Func<bool> Body)[]
{
    ("no effect: DP resolves to base", () => Resolve(dpDelta: 0, dontHaveDp: false) == Base),
    ("a +2000 DP modifier applies normally", () => Resolve(dpDelta: 2000, dontHaveDp: false) == 5000),
    ("DontHaveDP overrides the base to the no-DP sentinel (-1)", () => Resolve(dpDelta: 0, dontHaveDp: true) == -1),
    ("DontHaveDP overrides EVEN a +2000 DP modifier (-1, not 5000)", () => Resolve(dpDelta: 2000, dontHaveDp: true) == -1),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { if (t.Body()) Console.WriteLine($"PASS {t.Name}"); else { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}"); } }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

int Resolve(int dpDelta, bool dontHaveDp)
{
    HeadlessPlayerId P1 = new(1);
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 924);
    ctx.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId("C"), "C", "C",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = Base }, CardType: "Digimon"));
    var id = new HeadlessEntityId("p1:C");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("C"), P1));
    var card = new CardSource(ctx, id, P1);

    if (dpDelta != 0)
    {
        ctx.EffectRegistry.Register(new ContinuousSelfModifierEffect(card, ModifierHelpers.DpDeltaKey, dpDelta, false, null).ToBinding($"dp:{id.Value}"));
    }

    if (dontHaveDp)
    {
        ctx.EffectRegistry.Register(CardEffectFactory.DontHaveDPStaticEffect(
            permanentCondition: null, isInheritedEffect: false, card, condition: null).ToBinding($"nodp:{id.Value}"));
    }

    return ContinuousDpGate.ResolveDp(ctx, id, Base);
}
