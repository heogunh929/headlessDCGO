using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (R3-W3a narrow flip) Witnesses for the flipped continuous factory method DontHaveDPStaticEffect — it now
// returns the AS-IS NEW-MODEL kind-class (DontHaveDPClass : IDontHaveDPEffect, no ToBinding), consumed by the
// LIVE R1-a interface scan Permanent.HasDP (the old DontHaveDpKey registry binding had ZERO readers — dead).
//
// Union instrumentation (빈손 방향): every behavioral assert below runs with the EffectRegistry EMPTY for this
// effect — the flip registers NOTHING (registration-stop asserts prove it), so green here IS the proof that the
// new-model half of the union alone carries the behavior (the legacy registry half is force-empty by construction).
//
// NOTE: the second flip candidate (CanNotAffectedStaticEffect) was attempted and REVERTED by the fail-set gate —
// its registry half is still a live behavioral consumer (GainCanNotBeDeletedByBattle grant-refusal +
// ContinuousImmunityGate.BlocksOpponentEffect sink/controller/block sites; witnessed by G9-054/G9-057/P0R).
// Design item RD-W3A-01: it flips only after those consumers are re-housed onto the live
// TopCard.CanNotBeAffected scan (consumer-side follow-up batch).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("control: no effect -> DP resolves to printed base", async () =>
    {
        var f = await Fixture.Create();
        using var _scope = AmbientMatchContext.Enter(f.Ctx);
        AssertEq(new Permanent(f.Ctx, f.D1).DP, 3000, "printed base DP");
        AssertEq(new Permanent(f.Ctx, f.D1).HasDP, true, "HasDP without effect");
    }),

    ("self form (printed): live IDontHaveDPEffect scan strips DP -> HasDP false, DP -1", async () =>
    {
        var f = await Fixture.Create();
        using var _scope = AmbientMatchContext.Enter(f.Ctx);
        var d1Card = new CardSource(f.Ctx, f.D1, P1);
        d1Card.cEntity_EffectController.cEntity_Effect = new NoneTimingEffects(new List<ICardEffect>
        {
            CardEffectFactory.DontHaveDPStaticEffect(permanentCondition: null, isInheritedEffect: false, d1Card, condition: null),
        });
        AssertEq(new Permanent(f.Ctx, f.D1).HasDP, false, "HasDP with self DontHaveDP");
        AssertEq(new Permanent(f.Ctx, f.D1).DP, -1, "DP no-DP sentinel");
        // Self scope really is SELF: the opponent's Digimon keeps its DP.
        AssertEq(new Permanent(f.Ctx, f.D2).DP, 4000, "opponent Digimon untouched by a self grant");
    }),

    ("scoped form: owner's matching Digimon stripped, opponent's untouched; scopeAnyPlayer reaches it", async () =>
    {
        var f = await Fixture.Create();
        using var _scope = AmbientMatchContext.Enter(f.Ctx);
        var holderCard = new CardSource(f.Ctx, f.Holder, P1);
        holderCard.cEntity_EffectController.cEntity_Effect = new NoneTimingEffects(new List<ICardEffect>
        {
            CardEffectFactory.DontHaveDPStaticEffect(permanentCondition: p => p.IsDigimon, isInheritedEffect: false, holderCard, condition: null),
        });
        AssertEq(new Permanent(f.Ctx, f.D1).DP, -1, "owner's Digimon stripped (scoped grant)");
        AssertEq(new Permanent(f.Ctx, f.D2).DP, 4000, "opponent's Digimon out of the owner scope");

        holderCard.cEntity_EffectController.cEntity_Effect = new NoneTimingEffects(new List<ICardEffect>
        {
            CardEffectFactory.DontHaveDPStaticEffect(permanentCondition: p => p.IsDigimon, isInheritedEffect: false, holderCard, condition: null, scopeAnyPlayer: true),
        });
        AssertEq(new Permanent(f.Ctx, f.D2).DP, -1, "scopeAnyPlayer reaches the opponent's Digimon");
    }),

    ("condition gate: a false condition keeps the effect inert", async () =>
    {
        var f = await Fixture.Create();
        using var _scope = AmbientMatchContext.Enter(f.Ctx);
        var d1Card = new CardSource(f.Ctx, f.D1, P1);
        d1Card.cEntity_EffectController.cEntity_Effect = new NoneTimingEffects(new List<ICardEffect>
        {
            CardEffectFactory.DontHaveDPStaticEffect(permanentCondition: null, isInheritedEffect: false, d1Card, condition: () => false),
        });
        AssertEq(new Permanent(f.Ctx, f.D1).DP, 3000, "false condition -> DP kept");
    }),

    ("registration stop: the flipped effect registers NO registry binding (production 0, scan-only)", async () =>
    {
        var f = await Fixture.Create();
        using var _scope = AmbientMatchContext.Enter(f.Ctx);
        var d1Card = new CardSource(f.Ctx, f.D1, P1);
        ICardEffect nodp = CardEffectFactory.DontHaveDPStaticEffect(null, false, d1Card, null);

        // (a) no legacy lowering surface at all: LegacyBindingBridge finds no ToBinding on the kind-class.
        AssertEq(LegacyBindingBridge.TryToBinding(nodp, "w3a:nodp", out _), false, "DontHaveDPClass has no ToBinding");

        // (b) the enter-play registrar registers nothing for it (CardEffectRegistrar.cs:234 bridge miss) —
        // union legacy-half FORCE-EMPTY while the behavior asserts above stay green (빈손 방향 실증).
        IReadOnlyList<EffectBinding> registered = CardEffectRegistrar.RegisterOnEnterPlay(
            f.Ctx, new NoneTimingEffects(new List<ICardEffect> { nodp }), "W3A", d1Card);
        AssertEq(registered.Count, 0, "RegisterOnEnterPlay binding count");
    }),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {(Environment.GetEnvironmentVariable("W3A_TRACE") == "1" ? ex.ToString() : ex.Message)}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

static void AssertEq<T>(T actual, T expected, string what)
{
    if (!EqualityComparer<T>.Default.Equals(actual, expected))
    {
        throw new InvalidOperationException($"{what}: expected {expected}, got {actual}");
    }
}

// Shared board: P1 Tamer holder "H", P1 Digimon "D1" (dp 3000), P2 Digimon "D2" (dp 4000) — all on the battle area.
// Each test body enters AmbientMatchContext (the established unit idiom, e.g. G9-034/C-EoT2) so the AS-IS
// GManager.instance reads inside CanUse/IsDisabled resolve the match — entered in the CALLER (an AsyncLocal
// scope entered inside an async callee does not flow back out).
sealed class Fixture
{
    public EngineContext Ctx = null!;
    public HeadlessEntityId Holder;
    public HeadlessEntityId D1;
    public HeadlessEntityId D2;

    public static async Task<Fixture> Create()
    {
        HeadlessPlayerId P1 = new(1);
        HeadlessPlayerId P2 = new(2);
        var f = new Fixture();
        f.Ctx = EngineContext.CreateDefault(randomSeed: 926);
        f.Ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
        // CanTrigger/CanUse gate on DoneStartGame (mirror proxy: phase past None/Setup).
        f.Ctx.TurnController.SetPhase(HeadlessPhase.Main);
        var cards = (CardDatabase)f.Ctx.CardRepository;

        cards.Upsert(new CardRecord(new HeadlessEntityId("H"), "H", "Holder",
            new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Tamer"));
        cards.Upsert(new CardRecord(new HeadlessEntityId("D1"), "D1", "Friendly",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }, CardType: "Digimon"));
        cards.Upsert(new CardRecord(new HeadlessEntityId("D2"), "D2", "Enemy",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000 }, CardType: "Digimon"));

        f.Holder = await Place(f.Ctx, "p1:H", "H", P1);
        f.D1 = await Place(f.Ctx, "p1:D1", "D1", P1);
        f.D2 = await Place(f.Ctx, "p2:D2", "D2", P2);
        return f;
    }

    static async Task<HeadlessEntityId> Place(EngineContext ctx, string instance, string def, HeadlessPlayerId owner)
    {
        var id = new HeadlessEntityId(instance);
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(def), owner));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
        return id;
    }
}

// The settable cEntity_Effect seam every ported card definition uses — printed continuous shape: the effects
// surface at EffectTiming.None only (the timing every live continuous scan enumerates).
sealed class NoneTimingEffects : CEntity_Effect
{
    private readonly List<ICardEffect> _effects;
    public NoneTimingEffects(List<ICardEffect> effects) { _effects = effects; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) =>
        timing == EffectTiming.None ? _effects : new List<ICardEffect>();
}
