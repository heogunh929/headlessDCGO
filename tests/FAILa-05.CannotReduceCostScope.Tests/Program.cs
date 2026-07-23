using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #5 (mapping remediation) / R2-C ③: a CannotReduceCost immunity must honour WHICH cost it protects
// (AS-IS targetPermanentsCondition): a Digivolve-scoped immunity (BT5_021 "opponent can't reduce DIGIVOLUTION
// costs") blocks the digivolution-cost reduction but NOT the play-cost reduction, and vice versa. The immunity
// is the AS-IS new-model kind-class CannotReduceCostClass, placed on a FIELD permanent and consulted SOLELY by
// the live Player.CanReduceCost scan.
//
// (W3c-final 2차 retarget) The -2 cost reductions are now the AS-IS-canonical kind-classes on the card's own
// EffectList(None) — a self PLAY-cost reduction (CardEffectFactory.MandatorySelfPlayCostReduction) and a self
// DIGIVOLUTION-cost reduction (CardEffectFactory.ChangeDigivolutionCostStaticEffect), folded by
// CardSource.GetChangedPayingCost. They replace the pre-flip INVENTED substrate ContinuousSelfModifierEffect
// NumericModifier bindings (now deleted). This is behaviour-invariant for the immunity: ChangeCostClass.GetCost
// re-derives the SAME Player.CanReduceCost(targetPermanents, cardSource) veto the legacy fold consulted, so the
// scope gating is byte-identical. Scope is expressed STRUCTURALLY, mirroring the immunity's own split (AS-IS
// targetPermanentsCondition / isEvolution): the play reducer applies only with NO evolution target permanent;
// the digivolve reducer only with a real "digivolving FROM" battle permanent (its PermanentsCondition requires
// CardEffectCommons.IsPermanentExistsOnField), which the digivolve resolutions thread as digivolveTargetPermanentId.

const int Base = 5;
const int Reduced = 3; // base 5 with a -2 continuous reduction

// (RD-IDFLIP-01 batch 5) AS-IS targetPermanentsCondition predicates (the invented CostReductionScope enum was
// retired): Digivolve scope = a digivolution supplies >=1 non-null target permanent; Play scope = a plain
// play/option supplies none; Both = the trivial predicate (protect either).
Func<List<Permanent>, bool> DigivolveScope = tp => tp != null && tp.Exists(p => p != null);
Func<List<Permanent>, bool> PlayScope = tp => tp == null || !tp.Exists(p => p != null);
Func<List<Permanent>, bool> Both = _ => true;

var tests = new (string Name, Func<bool> Body)[]
{
    ("Control (no immunity): both play and digivolution cost are reduced", () => Play(null) == Reduced && Digivolve(null) == Reduced),
    ("Digivolve immunity: play cost STILL reduced, digivolution cost NOT reduced", () => Play(DigivolveScope) == Reduced && Digivolve(DigivolveScope) == Base),
    ("Play immunity: play cost NOT reduced, digivolution cost STILL reduced", () => Play(PlayScope) == Base && Digivolve(PlayScope) == Reduced),
    ("Both immunity: neither cost is reduced", () => Play(Both) == Base && Digivolve(Both) == Base),
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

// (#5 playerCondition) BT5_021 shape: P1 grants "your OPPONENT can't reduce DIGIVOLUTION costs" (playerCondition
// = the payer is the opponent P2, costKind Digivolve). It must protect P2's card but NOT P1's own card.
bool OpponentScope()
{
    HeadlessPlayerId P1 = new(1), P2 = new(2);
    EngineContext ctx = NewMatch(P1, P2);
    var cards = (CardDatabase)ctx.CardRepository;

    HeadlessEntityId Mk(HeadlessPlayerId owner, string tag)
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId(tag), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var cid = new HeadlessEntityId($"{owner.Value}:{tag}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(cid, new HeadlessEntityId(tag), owner));
        // -2 self digivolution-cost reduction (AS-IS kind-class ChangeDigivolutionCostStaticEffect), CanReduceCost-gated.
        var cs = new CardSource(ctx, cid, owner);
        ICardEffect digivolve = CardEffectFactory.ChangeDigivolutionCostStaticEffect(
            -2, permanentCondition: null, cardCondition: c => c == cs, rootCondition: null,
            isInheritedEffect: false, card: cs, condition: null, setFixedCost: false);
        cs.cEntity_EffectController.cEntity_Effect = new SelfCostEffects(digivolve);
        return cid;
    }

    Mk(P1, "SRC");                                  // BT5_021 itself (P1) — a field permanent added below
    var oppCard = Mk(P2, "OPP");                    // opponent's Digimon
    var ownCard = new HeadlessEntityId("1:SRC");    // P1's own card

    // "Your opponent can't reduce digivolution costs." (playerCondition = payer is P2, Digivolve scope).
    PlaceImmunity(ctx, P1, playerCondition: p => p is not null && p.PlayerId == P2, cardCondition: _ => true, DigivolveScope);

    // Real "digivolving FROM" battle permanents (one per owner) so the digivolve reducer's PermanentsCondition
    // (IsPermanentExistsOnField) holds; the immunity's Digivolve scope only needs a non-null target permanent.
    HeadlessEntityId oppSrc = PlaceEvolutionSource(ctx, P2, "OPPFROM");
    HeadlessEntityId ownSrc = PlaceEvolutionSource(ctx, P1, "OWNFROM");

    int oppDigivolve = ContinuousModifierGate.ResolveDigivolutionCost(ctx, oppCard, Base, digivolveTargetPermanentId: oppSrc);
    int ownDigivolve = ContinuousModifierGate.ResolveDigivolutionCost(ctx, ownCard, Base, digivolveTargetPermanentId: ownSrc);
    return oppDigivolve == Base && ownDigivolve == Reduced;   // opponent immune (5), own still reduced (3)
}

int Play(Func<List<Permanent>, bool>? immunity) => Resolve(immunity, digivolve: false);
int Digivolve(Func<List<Permanent>, bool>? immunity) => Resolve(immunity, digivolve: true);

int Resolve(Func<List<Permanent>, bool>? immunity, bool digivolve)
{
    HeadlessPlayerId P1 = new(1);
    EngineContext ctx = NewMatch(P1, new HeadlessPlayerId(2));
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId("C"), "C", "C",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId("p1:C");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("C"), P1));
    var card = new CardSource(ctx, id, P1);

    // -2 self PLAY-cost reduction AND -2 self DIGIVOLUTION-cost reduction (AS-IS kind-classes on the card's own
    // EffectList(None), folded by GetChangedPayingCost). Each honours the SAME Player.CanReduceCost immunity.
    ICardEffect playReducer = CardEffectFactory.MandatorySelfPlayCostReduction(2, card);
    ICardEffect digivolveReducer = CardEffectFactory.ChangeDigivolutionCostStaticEffect(
        -2, permanentCondition: null, cardCondition: cs => cs == card, rootCondition: null,
        isInheritedEffect: false, card: card, condition: null, setFixedCost: false);
    card.cEntity_EffectController.cEntity_Effect = new SelfCostEffects(playReducer, digivolveReducer);

    if (immunity is not null)
    {
        // Self-scoped immunity on THIS card (cardCondition = the played card, any payer), scope = the tested path.
        PlaceImmunity(ctx, P1, playerCondition: _ => true, cardCondition: cs => cs is not null && cs.InstanceId == id, immunity);
    }

    if (digivolve)
    {
        // The real "digivolving FROM" battle permanent threaded as the evolution source (digivolve scope).
        HeadlessEntityId src = PlaceEvolutionSource(ctx, P1, "SRC");
        return ContinuousModifierGate.ResolveDigivolutionCost(ctx, id, Base, digivolveTargetPermanentId: src);
    }

    return ContinuousModifierGate.ResolvePlayCost(ctx, id, Base);
}

// A fresh live match past None/Setup (so CanTrigger's DoneStartGame gate passes).
EngineContext NewMatch(HeadlessPlayerId p1, HeadlessPlayerId p2)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 905);
    ctx.TurnController.Initialize(new[] { p1, p2 }, p1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

// A real battle-area permanent (the "digivolving FROM" source) for the digivolve reducer's PermanentsCondition
// (AS-IS requires >= 1 matching CardEffectCommons.IsPermanentExistsOnField target).
HeadlessEntityId PlaceEvolutionSource(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var srcId = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, srcId, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return srcId;
}

// (R2-C ③) Place the AS-IS CannotReduceCostClass on a FIELD permanent owned by `owner` (the live scan walks
// Players_ForTurnPlayer's field permanents' EffectList(None)); dispatched via the TfxCannotReduceCost fixture.
void PlaceImmunity(EngineContext ctx, HeadlessPlayerId owner, Func<Player, bool> playerCondition, Func<CardSource, bool> cardCondition, Func<List<Permanent>, bool> targetPermanentsCondition)
{
    TfxCannotReduceCost.PlayerCondition = playerCondition;
    TfxCannotReduceCost.CardCondition = cardCondition;
    TfxCannotReduceCost.TargetPermanentsCondition = targetPermanentsCondition;

    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId("DEF:GRANT");
    cards.Upsert(new CardRecord(def, "TfxCannotReduceCost", "GRANT", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var srcId = new HeadlessEntityId($"{owner.Value}:battle:GRANT");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, def, owner, Metadata: new Dictionary<string, object?>()));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, srcId, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
}

// Minimal AS-IS-shaped CEntity_Effect surfacing the self cost kind-classes at EffectTiming.None — the same seam
// every ported card definition class uses to expose its printed effect list to CardSource.EffectList.
sealed class SelfCostEffects : CEntity_Effect
{
    private readonly List<ICardEffect> _effects;

    public SelfCostEffects(params ICardEffect[] effects) => _effects = new List<ICardEffect>(effects);

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) =>
        timing == EffectTiming.None ? new List<ICardEffect>(_effects) : new List<ICardEffect>();
}
