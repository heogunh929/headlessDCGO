// A8 구조골 GOAL 1 witness: [Blast Digivolve] (CardEffectFactory.BlastDigivolveEffect) — the RD-P6C2-11 STOP
// resolution. The three members (CanSelectPermanentCondition / CanActivateCondition / ActivateCoroutine) used to
// throw NotSupportedException claiming CardSource.CanPlayCardTargetFrame / Permanent.PermanentFrame have no
// mirror; that comment was STALE — the READ-side frame model (CanPlayCardTargetFrame CardSource.cs:475 over
// Permanent.PermanentFrame Permanent.cs:118), CanEvolve/CanEnterField (RD-P6C1-2), and PlayCardClass.PlayCard
// (the free-digivolve path already live via the sibling ArtsDigivolveEffect and the ported BT1_078) all exist.
// This drives the newly un-STOPped members directly (the AS-IS attack-trigger window is not modelled here — the
// gate + resolution are exercised via ActivateClass.CanActivate / .Activate, the same members the window calls):
//
//   (a) a non-Digimon (Tamer) battle target -> CanActivate FALSE, no throw (the IsDigimon gate evaluates).
//   (b) a color-mismatched Digimon base (hand card EvolutionCondition "Green@5:3" onto a RED level-5 base)
//       -> CanActivate FALSE — the CanPlayCardTargetFrame -> CanEvolve -> PermanentFrame read chain DISCRIMINATES,
//       it is not a blanket true.
//   (c) a valid GREEN level-5 Digimon base -> CanActivate TRUE.
//   (d) Activate() free-digivolves the hand card onto the valid base (forced auto-select, exactly-1 candidate):
//       the hand card becomes the battle-area top, the base a digivolution source under it, and no memory is paid.

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;   // CardEffectFactory / CardSource / Permanent
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;         // ActivateClass
using HeadlessDCGO.Engine.Headless.Bridge;                           // EngineContext / AmbientMatchContext
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;                          // HeadlessPhase
using HeadlessDCGO.Engine.Headless.Services;
using System.Collections;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(a) non-Digimon (Tamer) battle target -> CanActivate FALSE (no throw; IsDigimon gate)", A_TamerTargetNotActivatable),
    ("(b) color-mismatched Digimon base -> CanActivate FALSE (frame/CanEvolve read chain discriminates)", B_ColorMismatchNotActivatable),
    ("(c) valid Green lv5 Digimon base -> CanActivate TRUE", C_ValidTargetActivatable),
    ("(d) Activate() free-digivolves the hand card onto the valid base (auto-select, no cost)", D_ActivateFreeDigivolves),
};

int failed = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try { await body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine($"     {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is string st) { Console.WriteLine(string.Join('\n', st.Split('\n').Take(6))); }
    }
}
Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ===== subtests =====================================================================================

async Task A_TamerTargetNotActivatable()
{
    EngineContext ctx = NewContext();
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    ctx.MemoryController.Set(10);
    // A Tamer in the battle area (count >= 1, so the factory does not early-return null), but not a Digimon.
    Place(ctx, "tamer", "TAMER", ChoiceZone.BattleArea, "Tamer", level: null, colors: new[] { "Green" }, evoCost: null, evoCondition: null);
    HeadlessEntityId hand = Place(ctx, "blast", "BLAST", ChoiceZone.Hand, "Digimon", level: 6, colors: new[] { "Green" }, evoCost: 3, evoCondition: "Green@5:3");

    ActivateClass eff = BuildBlast(ctx, hand);
    Assert(eff != null, "factory returned an ActivateClass (battle area not empty)");
    Assert(!eff!.CanActivate(null), "CanActivate FALSE: the only battle permanent is a Tamer (IsDigimon gate), no throw");
}

async Task B_ColorMismatchNotActivatable()
{
    EngineContext ctx = NewContext();
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    ctx.MemoryController.Set(10);
    // A RED level-5 Digimon base — the hand card's "Green@5:3" evolution requirement fails the color gate.
    Place(ctx, "base", "BASE", ChoiceZone.BattleArea, "Digimon", level: 5, colors: new[] { "Red" }, evoCost: null, evoCondition: null);
    HeadlessEntityId hand = Place(ctx, "blast", "BLAST", ChoiceZone.Hand, "Digimon", level: 6, colors: new[] { "Green" }, evoCost: 3, evoCondition: "Green@5:3");

    ActivateClass eff = BuildBlast(ctx, hand);
    Assert(!eff!.CanActivate(null), "CanActivate FALSE: CanEvolve/CanPlayCardTargetFrame rejects the color-mismatched base");
}

async Task C_ValidTargetActivatable()
{
    EngineContext ctx = NewContext();
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    ctx.MemoryController.Set(10);
    Place(ctx, "base", "BASE", ChoiceZone.BattleArea, "Digimon", level: 5, colors: new[] { "Green" }, evoCost: null, evoCondition: null);
    HeadlessEntityId hand = Place(ctx, "blast", "BLAST", ChoiceZone.Hand, "Digimon", level: 6, colors: new[] { "Green" }, evoCost: 3, evoCondition: "Green@5:3");

    ActivateClass eff = BuildBlast(ctx, hand);
    Assert(eff!.CanActivate(null), "CanActivate TRUE: valid Green lv5 base is a legal free-digivolve target (frame + CanEvolve chain)");
}

async Task D_ActivateFreeDigivolves()
{
    EngineContext ctx = NewContext();
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    ctx.MemoryController.Set(10);
    HeadlessEntityId baseId = Place(ctx, "base", "BASE", ChoiceZone.BattleArea, "Digimon", level: 5, colors: new[] { "Green" }, evoCost: null, evoCondition: null);
    HeadlessEntityId hand = Place(ctx, "blast", "BLAST", ChoiceZone.Hand, "Digimon", level: 6, colors: new[] { "Green" }, evoCost: 3, evoCondition: "Green@5:3");

    Assert(InZone(ctx, ChoiceZone.Hand, hand), "precondition: the [Blast Digivolve] card is in hand");
    int memoryBefore = ctx.MemoryController.Current.Current;

    ActivateClass eff = BuildBlast(ctx, hand);
    Assert(eff!.CanActivate(null), "precondition: activatable");

    // Exactly one valid candidate -> SelectPermanentEffect.Activate() forces the selection (no player choice),
    // routes it to the effect's SelectPermanentCoroutine, and the free digivolve runs through PlayCardClass.
    await eff.Activate(null);

    Assert(InZone(ctx, ChoiceZone.BattleArea, hand), "the hand card digivolved: it is the new battle-area top");
    Assert(SourcesOf(ctx, hand).Contains(baseId.Value), "the base is now a digivolution source under the played card");
    Assert(!InZone(ctx, ChoiceZone.Hand, hand), "the card left the hand");
    Assert(ctx.MemoryController.Current.Current == memoryBefore, "no memory paid (free digivolve, payCost:false)");
}

// ===== harness ======================================================================================

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 722);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);   // past None so CanUse / DoneStartGame gates are open
    return ctx;
}

ActivateClass BuildBlast(EngineContext ctx, HeadlessEntityId hand) =>
    CardEffectFactory.BlastDigivolveEffect(new CardSource(ctx, hand, P1), condition: null);

HeadlessEntityId Place(EngineContext ctx, string idSuffix, string number, ChoiceZone zone, string cardType,
    int? level, string[]? colors, int? evoCost, string? evoCondition)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (level is int lv) { meta["level"] = lv; }
    if (colors is not null) { meta["colors"] = colors; }
    var def = new CardRecord(new HeadlessEntityId(number), number, number, meta, CardType: cardType,
        EvolutionCost: evoCost, EvolutionCondition: evoCondition);
    ((CardDatabase)ctx.CardRepository).Upsert(def);

    var id = new HeadlessEntityId($"p1:{idSuffix}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    return id;
}

bool InZone(EngineContext ctx, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);

IReadOnlyList<string> SourcesOf(EngineContext ctx, HeadlessEntityId host)
{
    if (ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec) && rec is not null
        && rec.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> s)
    {
        return s.ToArray();
    }
    return Array.Empty<string>();
}

static void Assert(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
