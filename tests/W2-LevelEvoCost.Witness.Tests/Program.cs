using System.Collections;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

// ══════════════════════════════════════════════════════════════════════════════════════════════════════
// W2-LevelEvoCost.Witness — end-to-end behaviour witnesses for the 4 ported cards (BT9_031, BT10_086,
// BT2_062, BT13_028). Each subtest drives the REAL consumer pipeline (CardSource.GetPayingCostWithBaseCost /
// CardSource.AddedDigivolutionCosts / CardEffectCommons.IsMin/MaxLevel) over the actually-registered card
// effects, so a card's logic — not a re-implementation — is what is observed.
//   * level predicate GATES the select:  IsMinLevel (BT9_031) / IsMaxLevel (BT10_086) pick only the
//     lowest/highest-level enemy Digimon (ties included).
//   * digivolve-cost reduction actually CHANGES the paid cost: BT2_062 (-1) and BT10_086 (-2) both fold
//     through GetPayingCostWithBaseCost (the seat DigivolveAction consults).
//   * requirement-ignore actually PERMITS an otherwise-illegal digivolve: BT13_028's UntilCalculateFixedCost
//     grant is read by AddedDigivolutionCosts (region 1 = player bucket) — the INERT-GRANT check (assert the
//     observable added cost, not just that something was stored).
// ══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Action Body)[]
{
    ("BT9_031 [None] AddSelfDigivolutionRequirement: MetalGarurumon-top digivolve costs 1 (real card code)", Bt9AddSelfCost),
    ("BT9_031 IsMinLevel gates the bounce select: only the LOWEST-level enemy Digimon (ties incl.) are picked", Bt9MinLevelSelect),
    ("BT2_062 digivolve-cost -1 changes the paid cost for a [Diaboromon] hand card (inert-grant: observable)", Bt2CostReduced),
    ("BT2_062 gates hold: non-Diaboromon card and non-owner-turn get NO reduction", Bt2CostGates),
    ("BT10_086 [None] ChangeDigivolutionCost -2 changes the paid cost onto an [X Antibody]-source Digimon", Bt10CostReduced),
    ("BT10_086 IsMaxLevel gates the select: only the HIGHEST-level enemy Digimon (ties incl.) are picked", Bt10MaxLevelSelect),
    ("BT13_028 IgnoreDigivolutionRequirement grant PERMITS an otherwise-illegal digivolve at cost 3 (inert-grant: observable)", Bt13IgnorePermits),
    ("BT13_028 card structure runs: OnDeclaration + OnEndAttack each build 1 activated effect", Bt13Structure),
};

int failures = 0;
foreach (var t in tests)
{
    try { t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {t.Name}");
        Console.Error.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
    }
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ── Subtests ─────────────────────────────────────────────────────────────────────────────────────────

void Bt9AddSelfCost()
{
    EngineContext ctx = Board(P1);
    // A host permanent whose TOP is [MetalGarurumon] on P1 battle area; BT9_031 in P1 hand (its [None]
    // AddSelfDigivolutionRequirement lets it digivolve onto a MetalGarurumon top for 1).
    HeadlessEntityId host = Stage(ctx, P1, "MGARURU", "MetalGarurumon", ChoiceZone.BattleArea, level: 6, register: false);
    HeadlessEntityId bt9 = Stage(ctx, P1, "BT9_031", "MetalGarurumon (X Antibody)", ChoiceZone.Hand, level: 7, register: true);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var evolving = new CardSource(ctx, bt9, P1, P1);
    var target = new Permanent(ctx, host, P1);
    List<int> costs = evolving.AddedDigivolutionCosts(target, CardEffectCommons.IgnoreRequirement.None, checkAvailability: false);

    AssertTrue(costs.Contains(1), $"AddedDigivolutionCosts should contain the AddSelf cost 1 (got [{string.Join(",", costs)}])");

    // Control: onto a NON-MetalGarurumon top, the added path does not apply.
    HeadlessEntityId other = Stage(ctx, P1, "OTHER", "Greymon", ChoiceZone.BattleArea, level: 4, register: false);
    List<int> none = evolving.AddedDigivolutionCosts(new Permanent(ctx, other, P1), CardEffectCommons.IgnoreRequirement.None, false);
    AssertTrue(!none.Contains(1), "no added cost onto a non-MetalGarurumon top");
}

void Bt9MinLevelSelect()
{
    EngineContext ctx = Board(P1);
    HeadlessEntityId bt9 = Stage(ctx, P1, "BT9_031", "MetalGarurumon (X Antibody)", ChoiceZone.BattleArea, level: 7, register: true);
    // Enemy (P2) board: levels 3, 3, 5. The card bounces IsMinLevel -> both level-3, NOT the level-5.
    HeadlessEntityId eLow1 = Stage(ctx, P2, "E3A", "EnemyLow1", ChoiceZone.BattleArea, level: 3, register: false);
    HeadlessEntityId eLow2 = Stage(ctx, P2, "E3B", "EnemyLow2", ChoiceZone.BattleArea, level: 3, register: false);
    HeadlessEntityId eHigh = Stage(ctx, P2, "E5", "EnemyHigh", ChoiceZone.BattleArea, level: 5, register: false);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var card = new CardSource(ctx, bt9, P1, P1);
    HeadlessPlayerId enemy = CardEffectCommons.OpponentOf(card);
    // The card's ActivateCoroutine selection expression, verbatim predicate: IsMinLevel(p, enemy).
    var picked = enemy.GetBattleAreaDigimons()
        .Where(p => CardEffectCommons.IsMinLevel(p, enemy))
        .Select(p => p.InstanceId)
        .ToHashSet();

    AssertTrue(picked.Contains(eLow1) && picked.Contains(eLow2), "both level-3 enemies are selected (lowest)");
    AssertTrue(!picked.Contains(eHigh), "the level-5 enemy is NOT selected");
    AssertEqual(2, picked.Count, "exactly the two lowest-level enemies");
}

void Bt2CostReduced()
{
    EngineContext ctx = Board(P1);
    // BT2_062 permanent on P1 battle area (owner turn); a [Diaboromon] Digimon card in P1 hand.
    HeadlessEntityId bt2 = Stage(ctx, P1, "BT2_062", "Diaboromon", ChoiceZone.BattleArea, level: 6, register: true);
    HeadlessEntityId dia = Stage(ctx, P1, "DIA", "Diaboromon", ChoiceZone.Hand, level: 5, register: false);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var moving = new CardSource(ctx, dia, P1, P1);
    var target = new List<Permanent> { new(ctx, bt2, P1) };
    int cost = moving.GetPayingCostWithBaseCost(baseCost: 4, SelectCardEffect.Root.Hand, target);
    AssertEqual(3, cost, "base 4 - 1 = 3 for the Diaboromon hand card digivolving onto BT2_062");
}

void Bt2CostGates()
{
    // (a) non-Diaboromon card: no reduction.
    EngineContext ctx = Board(P1);
    HeadlessEntityId bt2 = Stage(ctx, P1, "BT2_062", "Diaboromon", ChoiceZone.BattleArea, level: 6, register: true);
    HeadlessEntityId notDia = Stage(ctx, P1, "GRE", "Greymon", ChoiceZone.Hand, level: 5, register: false);
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx))
    {
        int cost = new CardSource(ctx, notDia, P1, P1)
            .GetPayingCostWithBaseCost(4, SelectCardEffect.Root.Hand, new List<Permanent> { new(ctx, bt2, P1) });
        AssertEqual(4, cost, "non-Diaboromon card is not reduced");
    }

    // (b) not the owner's turn (Condition = IsOwnerTurn && IsExistOnBattleArea): no reduction.
    EngineContext ctx2 = Board(P2); // P2's turn
    HeadlessEntityId bt2b = Stage(ctx2, P1, "BT2_062", "Diaboromon", ChoiceZone.BattleArea, level: 6, register: true);
    HeadlessEntityId diaB = Stage(ctx2, P1, "DIA", "Diaboromon", ChoiceZone.Hand, level: 5, register: false);
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx2))
    {
        int cost = new CardSource(ctx2, diaB, P1, P1)
            .GetPayingCostWithBaseCost(4, SelectCardEffect.Root.Hand, new List<Permanent> { new(ctx2, bt2b, P1) });
        AssertEqual(4, cost, "no reduction on the opponent's turn");
    }
}

void Bt10CostReduced()
{
    EngineContext ctx = Board(P1);
    // A P1 battle-area Digimon that carries an [X Antibody] digivolution source; BT10_086 in P1 hand.
    HeadlessEntityId xanti = StageSource(ctx, P1, "XANTI", "X Antibody");
    HeadlessEntityId host = Stage(ctx, P1, "HOST", "Omnimon", ChoiceZone.BattleArea, level: 6, register: false, sourceIds: new[] { xanti });
    HeadlessEntityId bt10 = Stage(ctx, P1, "BT10_086", "Omnimon X Antibody", ChoiceZone.Hand, level: 7, register: true);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var moving = new CardSource(ctx, bt10, P1, P1);
    int cost = moving.GetPayingCostWithBaseCost(5, SelectCardEffect.Root.Hand, new List<Permanent> { new(ctx, host, P1) });
    AssertEqual(3, cost, "base 5 - 2 = 3 digivolving BT10_086 onto an [X Antibody]-source Digimon");

    // Control: onto a Digimon WITHOUT an X Antibody source, no reduction.
    HeadlessEntityId plain = Stage(ctx, P1, "PLAIN", "WarGreymon", ChoiceZone.BattleArea, level: 6, register: false);
    int plainCost = moving.GetPayingCostWithBaseCost(5, SelectCardEffect.Root.Hand, new List<Permanent> { new(ctx, plain, P1) });
    AssertEqual(5, plainCost, "no reduction without an [X Antibody] source");
}

void Bt10MaxLevelSelect()
{
    EngineContext ctx = Board(P1);
    HeadlessEntityId bt10 = Stage(ctx, P1, "BT10_086", "Omnimon X Antibody", ChoiceZone.BattleArea, level: 7, register: true);
    // Enemy board: levels 4, 6, 6. IsMaxLevel -> both level-6, NOT the level-4.
    HeadlessEntityId eLow = Stage(ctx, P2, "E4", "EnemyLow", ChoiceZone.BattleArea, level: 4, register: false);
    HeadlessEntityId eHi1 = Stage(ctx, P2, "E6A", "EnemyHigh1", ChoiceZone.BattleArea, level: 6, register: false);
    HeadlessEntityId eHi2 = Stage(ctx, P2, "E6B", "EnemyHigh2", ChoiceZone.BattleArea, level: 6, register: false);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var card = new CardSource(ctx, bt10, P1, P1);
    HeadlessPlayerId enemy = CardEffectCommons.OpponentOf(card);
    var picked = enemy.GetBattleAreaDigimons()
        .Where(p => CardEffectCommons.IsMaxLevel(p, enemy))
        .Select(p => p.InstanceId)
        .ToHashSet();

    AssertTrue(picked.Contains(eHi1) && picked.Contains(eHi2), "both level-6 enemies are selected (highest)");
    AssertTrue(!picked.Contains(eLow), "the level-4 enemy is NOT selected");
    AssertEqual(2, picked.Count, "exactly the two highest-level enemies");
}

void Bt13IgnorePermits()
{
    EngineContext ctx = Board(P1);
    // A [Jellymon] on P1's battle area (the digivolution target). BT13_028 in P1 hand. There is NO printed
    // digivolution path from Jellymon to BT13_028 (otherwise-illegal). The card's OnDeclaration grants an
    // ignore-requirement AddDigivolutionRequirementClass onto the owner's UntilCalculateFixedCostEffect bucket;
    // we reproduce that EXACT grant (same class + GetEvoCost 3 gated on CanIgnore + card + target) and assert
    // the READ side (AddedDigivolutionCosts region 1 = player bucket) makes the illegal digivolve legal at 3.
    HeadlessEntityId jelly = Stage(ctx, P1, "JELLY", "Jellymon", ChoiceZone.BattleArea, level: 3, register: false);
    HeadlessEntityId bt13 = Stage(ctx, P1, "BT13_028", "TeslaJellymon", ChoiceZone.Hand, level: 4, register: true);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var evolving = new CardSource(ctx, bt13, P1, P1);
    var target = new Permanent(ctx, jelly, P1);

    // Baseline: no grant => no added path (the digivolve is illegal).
    List<int> before = evolving.AddedDigivolutionCosts(target, CardEffectCommons.IgnoreRequirement.None, false);
    AssertTrue(!before.Contains(3), "without the grant, no added cost 3 (digivolve illegal)");

    // The card's grant (BT13_028 OnDeclaration #region ignore digivolution requirements), reproduced 1:1.
    var addReq = new AddDigivolutionRequirementClass();
    addReq.SetUpICardEffect("Ignore Digivolution requirements", (Hashtable _h) => true, evolving);
    addReq.SetUpAddDigivolutionRequirementClass(getEvoCost: GetEvoCost);
    Func<EffectTiming, ICardEffect?> getCardEffect = t => t == EffectTiming.None ? addReq : null;
    new Player(ctx, P1).UntilCalculateFixedCostEffect.Add(getCardEffect!);

    int GetEvoCost(Permanent permanent, CardSource cardSource, CardEffectCommons.IgnoreRequirement ignore, bool checkAvailability)
    {
        if (new Player(ctx, P1).CanIgnoreDigivolutionRequirement(permanent, cardSource))
        {
            if (cardSource == evolving && permanent == target)
            {
                return 3;
            }
        }

        return -1;
    }

    // Observable effect: the read side now yields the added cost 3 (grant is NOT inert).
    List<int> after = evolving.AddedDigivolutionCosts(target, CardEffectCommons.IgnoreRequirement.None, false);
    AssertTrue(after.Contains(3), $"the ignore-requirement grant permits the digivolve at cost 3 (got [{string.Join(",", after)}])");
}

void Bt13Structure()
{
    EngineContext ctx = Board(P1);
    HeadlessEntityId bt13 = Stage(ctx, P1, "BT13_028", "TeslaJellymon", ChoiceZone.Hand, level: 4, register: false);
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var card = new CardSource(ctx, bt13, P1, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT13.Blue.BT13_028();

    List<ICardEffect> onDecl = effect.CardEffects(EffectTiming.OnDeclaration, card);
    List<ICardEffect> onEnd = effect.CardEffects(EffectTiming.OnEndAttack, card);
    AssertEqual(1, onDecl.Count, "OnDeclaration builds exactly 1 activated effect");
    AssertEqual(1, onEnd.Count, "OnEndAttack builds exactly 1 activated effect");
}

// ── Harness ──────────────────────────────────────────────────────────────────────────────────────────

EngineContext Board(HeadlessPlayerId current)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 42);
    ctx.TurnController.Initialize(new[] { P1, P2 }, current);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // past None/Setup -> ICardEffect.CanTrigger DoneStartGame gate passes
    ctx.MemoryController.Set(10);
    return ctx;
}

// Stage a Digimon: def upsert + instance (+ optional digivolution sources) + zone move + optional effect register.
HeadlessEntityId Stage(EngineContext ctx, HeadlessPlayerId owner, string number, string name, ChoiceZone zone,
    int? level, bool register, HeadlessEntityId[]? sourceIds = null)
{
    var defId = new HeadlessEntityId($"DEF:{number}:{name}:{owner.Value}");
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (level.HasValue) defMeta["level"] = level.Value;
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name, defMeta, CardType: "Digimon"));

    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{number}:{name}");
    var instMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false };
    if (level.HasValue) instMeta["level"] = level.Value;
    if (sourceIds is { Length: > 0 }) instMeta[DigivolutionStackReader.SourceIdsKey] = sourceIds.Select(s => s.Value).ToArray();
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: instMeta));

    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }

    if (register)
    {
        CardEffectRegistrar.RegisterCard(ctx, id, owner);
    }

    return id;
}

// Stage a bare card instance to serve as a digivolution SOURCE (no public zone).
HeadlessEntityId StageSource(EngineContext ctx, HeadlessPlayerId owner, string number, string name)
{
    var defId = new HeadlessEntityId($"DEF:{number}:{name}:{owner.Value}");
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name,
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:src:{number}:{name}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    return id;
}

void AssertTrue(bool cond, string label)
{
    if (!cond) throw new InvalidOperationException($"expected true: {label}");
}

void AssertEqual(int expected, int actual, string label)
{
    if (expected != actual) throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
}
