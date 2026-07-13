using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-b (mapping remediation): InvertSAttackClass was DEAD — the invert-security-attack delta was accumulated
// but never applied to the final value. AS-IS Permanent.InvertSecutiryValue (clamped [-1,1]) FLIPS the direction
// of every security-attack change: +N ⇄ -N (ChangeSAttackClass.GetSAttack).

const int Base = 3;

var tests = new (string Name, Func<bool> Body)[]
{
    // No invert: a +2 change raises 3 -> 5; a -2 change lowers 3 -> 1.
    ("no invert: +2 change raises to 5", () => Resolve(delta: +2, invert: 0) == 5),
    ("no invert: -2 change lowers to 1", () => Resolve(delta: -2, invert: 0) == 1),
    // invert = +1: an INCREASE is flipped to the equal decrease (+2 becomes -2): 3 -> 1.
    ("invert +1: the +2 increase is flipped to a decrease (5 -> 1)", () => Resolve(delta: +2, invert: +1) == 1),
    // invert = -1: a DECREASE is flipped to the equal increase (-2 becomes +2): 3 -> 5.
    ("invert -1: the -2 decrease is flipped to an increase (1 -> 5)", () => Resolve(delta: -2, invert: -1) == 5),
    // invert only bites in its own direction: invert +1 leaves a decrease alone.
    ("invert +1 does NOT touch a decrease (-2 stays 1)", () => Resolve(delta: -2, invert: +1) == 1),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { if (t.Body()) Console.WriteLine($"PASS {t.Name}"); else { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}"); } }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

int Resolve(int delta, int invert)
{
    HeadlessPlayerId P1 = new(1);
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 918);
    ctx.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId("C"), "C", "C",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId("p1:C");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("C"), P1));
    var card = new CardSource(ctx, id, P1);

    ctx.EffectRegistry.Register(new ContinuousSelfModifierEffect(card, ModifierHelpers.SecurityAttackDeltaKey, delta, false, null).ToBinding($"sa:{id.Value}"));
    if (invert != 0)
    {
        // MIGRATION-NOTE (P7 test-fix): InvertSAttackClass (Assets/Scripts/Script/CardEffects/
        // InvertSAttackClass.cs) is a new-model kind-class with no ToBinding/EffectRegistry bridge (stage-B
        // RED, docs/audit/rebuild_p6_stageA_notes.md). ContinuousModifierGate.ResolveSecurityAttack reads only
        // the substrate EffectRegistry bindings, not this kind-class's IInvertSAttackEffect interface. The
        // engine's stage-B live is-scan (CardSource.EffectList -> CEntity_Effect.GetCardEffects) IS the path for
        // a real ported card, but it is unreachable from test code for a SYNTHETIC card: CardEffectDispatch
        // resolves a card's CEntity_Effect subclass by reflecting over the ENGINE assembly only, keyed by card
        // number, so this test's synthetic "C" card has no ported class to attach the grant to. There is thus no
        // buildable way from test code alone to make this factory's grant observable. Assertions below are
        // UNCHANGED and EXPECTED TO FAIL until a test-facing effect-injection hook exists — tracked, not silently weakened.
        CardEffectFactory.InvertSAttackStaticEffect(
            permanentCondition: null, changeValue: invert, isInheritedEffect: false, card, condition: null);
    }

    return ContinuousModifierGate.ResolveSecurityAttack(ctx, id, Base);
}
