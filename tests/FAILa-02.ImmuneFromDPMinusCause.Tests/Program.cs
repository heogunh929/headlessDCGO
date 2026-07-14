using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #2 (mapping remediation): ImmuneFromDPMinusStaticEffect must honour its CAUSING-effect predicate
// (AS-IS ImmuneFromDPMinus(permanent, cardEffect)). "Immune to your OPPONENT's DP-minus" must BLOCK an
// opponent-sourced DP reduction but still take a self-sourced one. The port previously dropped ALL negative DP
// modifiers regardless of source (over-immunity). A no-predicate form stays immune to any reduction.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
const int BaseDp = 5000;
const int Minus = -3000;

var tests = new (string Name, Func<Task> Body)[]
{
    ("Opponent-sourced DP-minus is IGNORED (cardEffectCondition matches) → base DP", () => Run(reducerOwner: P2, cardEffectCondition: src => src.Owner != P1, expectedDp: BaseDp)),
    ("Self-sourced DP-minus still APPLIES (cardEffectCondition does not match) → reduced DP", () => Run(reducerOwner: P1, cardEffectCondition: src => src.Owner != P1, expectedDp: BaseDp + Minus)),
    ("Unconditional immunity ignores either source → base DP", () => Run(reducerOwner: P1, cardEffectCondition: null, expectedDp: BaseDp)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Run(HeadlessPlayerId reducerOwner, Func<CardSource, bool>? cardEffectCondition, int expectedDp)
{
    EngineContext ctx = Ctx();
    var protectedCard = await Place(ctx, P1, "PROT", ChoiceZone.BattleArea);
    var reducer = await Place(ctx, reducerOwner, "REDU", ChoiceZone.BattleArea);

    // "PROT is immune to <cardEffectCondition>'s DP-minus."
    // (P7 FAILa-02 resolved SEAM) ImmuneFromDPMinusClass (Assets/Scripts/Script/CardEffects/
    // ImmuneFromDPMinusClass.cs) is a new-model kind-class with no ToBinding/EffectRegistry bridge — the
    // AS-IS-faithful path is the LIVE cEntity_EffectController scan
    // NewModelContinuousScan.HasImmuneFromDpMinus/ContinuousDpGate now performs, evaluated PER REDUCING
    // MODIFIER against its REAL causing source (RD-P6B-13 stand-in), not a blanket presence flag. Attach the
    // built effect via the same seam every ported card definition class uses.
    var protectedCard0 = new CardSource(ctx, protectedCard, P1);
    ICardEffect built = CardEffectFactory.ImmuneFromDPMinusStaticEffect(
        permanentCondition: null,
        cardEffectCondition: cardEffectCondition is null ? null : (ICardEffect ce) => cardEffectCondition(ce.EffectSourceCard),
        isInheritedEffect: false,
        card: protectedCard0, condition: null, effectName: "ImmuneFromDPMinus");
    protectedCard0.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);

    // A continuous "-3000 DP to PROT" sourced from `reducer` (any-player scope, so cross-player applies).
    ctx.EffectRegistry.Register(new PlayerScopeModifierEffect(
        new CardSource(ctx, reducer, reducerOwner), ModifierHelpers.DpDeltaKey, Minus, scopeCardType: "Digimon",
        condition: null, scopePredicate: cs => cs.InstanceId == protectedCard, scopeAnyPlayer: true)
        .ToBinding($"dpm:{reducer.Value}"));

    int dp = ContinuousDpGate.ResolveDp(ctx, protectedCard, BaseDp);
    AssertTrue(dp == expectedDp, $"DP == {expectedDp} (got {dp}; reducer owned by {(reducerOwner == P1 ? "self" : "opponent")})");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 952);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (P7 test-fix) ICardEffect.CanTrigger gates on TurnStateMachine.DoneStartGame (phase past None/Setup) —
    // without this every candidate effect's CanUse(null) trivially returns false and the new-model scan never
    // fires (same fix already documented in rebuild_p6_stageB_notes.md §5).
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = BaseDp, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = BaseDp }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
