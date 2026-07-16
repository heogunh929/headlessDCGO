using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-d: DontHaveDP (AS-IS IDontHaveDPEffect / Permanent.HasDP==false) makes Permanent.DP return the -1 no-DP
// sentinel, overriding the base DP AND every DP modifier (AS-IS Permanent.DP returns -1 outright when !HasDP).
//
// (R3-W3a retarget) This test previously asserted the RETIRED registry path (LegacyBindingBridge lowering of the
// old-model restriction + a registry DpDeltaKey modifier) — both halves went dead when R1-a rehoused Permanent.DP
// to the live EffectList interface scan, leaving the test red-on-a-dead-path. It now asserts the AS-IS truth
// path: the flipped DontHaveDPStaticEffect returns the new-model DontHaveDPClass (IDontHaveDPEffect) and the DP
// modifier is a new-model ChangeDPClass, both surfaced through the card's live cEntity_Effect seam and read by
// Permanent.HasDP / GetDP (the FAILb-01 idiom).

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
    // CanTrigger/CanUse gate on DoneStartGame (mirror proxy: phase past None/Setup); the DP-change effect
    // live-gates on IsExistOnBattleAreaDigimon, so the card must be a battle-area Digimon.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId("C"), "C", "C",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = Base }, CardType: "Digimon"));
    var id = new HeadlessEntityId("p1:C");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("C"), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    var card = new CardSource(ctx, id, P1);

    // GManager.instance reads inside CanUse/IsDisabled resolve the match through this ambient scope.
    using var _scope = AmbientMatchContext.Enter(ctx);

    var effects = new List<ICardEffect>();
    if (dpDelta != 0)
    {
        effects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(dpDelta, false, card, null));
    }

    if (dontHaveDp)
    {
        effects.Add(CardEffectFactory.DontHaveDPStaticEffect(permanentCondition: null, isInheritedEffect: false, card, condition: null));
    }

    card.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effects);

    return new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, id).DP;
}

sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly List<ICardEffect> _effects;
    public TestCardEntityEffect(List<ICardEffect> effects) { _effects = effects; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) =>
        timing == EffectTiming.None ? _effects : new List<ICardEffect>();
}
