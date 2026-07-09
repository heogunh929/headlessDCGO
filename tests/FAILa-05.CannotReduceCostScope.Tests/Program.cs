using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #5 (mapping remediation): a CannotReduceCost immunity must honour WHICH cost it protects (AS-IS
// targetPermanentsCondition): a Digivolve-scoped immunity (BT5_021 "opponent can't reduce DIGIVOLUTION costs")
// blocks the digivolution-cost reduction but NOT the play-cost reduction, and vice versa. The port previously
// applied every immunity to BOTH cost paths (over-restriction).

const int Base = 5;
const int Reduced = 3; // base 5 with a -2 continuous reduction

var tests = new (string Name, Func<bool> Body)[]
{
    ("Control (no immunity): both play and digivolution cost are reduced", () => Play(null) == Reduced && Digivolve(null) == Reduced),
    ("Digivolve immunity: play cost STILL reduced, digivolution cost NOT reduced", () => Play(CostReductionScope.Digivolve) == Reduced && Digivolve(CostReductionScope.Digivolve) == Base),
    ("Play immunity: play cost NOT reduced, digivolution cost STILL reduced", () => Play(CostReductionScope.Play) == Base && Digivolve(CostReductionScope.Play) == Reduced),
    ("Both immunity: neither cost is reduced", () => Play(CostReductionScope.Both) == Base && Digivolve(CostReductionScope.Both) == Base),
    ("Opponent-scoped Digivolve immunity (BT5_021 shape): OPPONENT's digivolve cost NOT reduced, OWN cost reduced", OpponentScope),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { if (t.Body()) Console.WriteLine($"PASS {t.Name}"); else { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}"); } }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// (#5 playerCondition) BT5_021 shape: P1 registers "your OPPONENT can't reduce DIGIVOLUTION costs" (scopeAnyPlayer
// + permanentCondition = owned by the opponent). It must protect P2's card but NOT P1's own card.
bool OpponentScope()
{
    HeadlessPlayerId P1 = new(1), P2 = new(2);
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 951);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;

    HeadlessEntityId Mk(HeadlessPlayerId owner, string tag)
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId(tag), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var cid = new HeadlessEntityId($"{owner.Value}:{tag}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(cid, new HeadlessEntityId(tag), owner));
        // -2 digivolution-cost reduction on this card.
        ctx.EffectRegistry.Register(new ContinuousSelfModifierEffect(new CardSource(ctx, cid, owner), ModifierHelpers.DigivolutionCostDeltaKey, -2, false, null).ToBinding($"dc:{cid.Value}"));
        return cid;
    }

    var effectOwnerCard = Mk(P1, "SRC");           // BT5_021 itself (P1)
    var oppCard = Mk(P2, "OPP");                    // opponent's Digimon
    var ownCard = new HeadlessEntityId("1:SRC");    // P1's own card = effectOwnerCard

    // "Your opponent can't reduce digivolution costs of their Digimon."
    ctx.EffectRegistry.Register(CardEffectFactory.CanNotReduceCostStaticEffect(
        permanentCondition: p => p is not null && p.OwnerId == P2,
        isInheritedEffect: false, new CardSource(ctx, effectOwnerCard, P1), condition: null,
        costKind: CostReductionScope.Digivolve, scopeAnyPlayer: true).ToBinding($"imm:{effectOwnerCard.Value}"));

    int oppDigivolve = ContinuousModifierGate.ResolveDigivolutionCost(ctx, oppCard, Base);
    int ownDigivolve = ContinuousModifierGate.ResolveDigivolutionCost(ctx, ownCard, Base);
    return oppDigivolve == Base && ownDigivolve == Reduced;   // opponent immune (5), own still reduced (3)
}

int Play(CostReductionScope? immunity) => Resolve(immunity, digivolve: false);
int Digivolve(CostReductionScope? immunity) => Resolve(immunity, digivolve: true);

int Resolve(CostReductionScope? immunity, bool digivolve)
{
    HeadlessPlayerId P1 = new(1);
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 905);
    ctx.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId("C"), "C", "C",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId("p1:C");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("C"), P1));
    var card = new CardSource(ctx, id, P1);

    // -2 continuous reduction to BOTH the play cost and the digivolution cost.
    ctx.EffectRegistry.Register(new ContinuousSelfModifierEffect(card, ModifierHelpers.PlayCostDeltaKey, -2, false, null).ToBinding($"pc:{id.Value}"));
    ctx.EffectRegistry.Register(new ContinuousSelfModifierEffect(card, ModifierHelpers.DigivolutionCostDeltaKey, -2, false, null).ToBinding($"dc:{id.Value}"));
    if (immunity is CostReductionScope scope)
    {
        ctx.EffectRegistry.Register(CardEffectFactory.CanNotReduceCostStaticEffect(
            permanentCondition: null, isInheritedEffect: false, card, condition: null, costKind: scope).ToBinding($"imm:{id.Value}"));
    }

    return digivolve
        ? ContinuousModifierGate.ResolveDigivolutionCost(ctx, id, Base)
        : ContinuousModifierGate.ResolvePlayCost(ctx, id, Base);
}
