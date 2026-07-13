// PRIM fidelity: CanNotBeBlockedStaticSelfEffect(defenderCondition) — the attacker can't be blocked ONLY BY
// defenders matching the predicate (AS-IS defenderCondition, e.g. "Digimon with no digivolution cards").
// Restored param (was simplified to unconditional). Verified via ContinuousRestrictionGate.EvaluateBeBlocked.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
EngineContext ctx = EngineContext.CreateDefault(randomSeed: 88);
ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
ctx.TurnController.SetPhase(HeadlessPhase.Main);

async Task<HeadlessEntityId> Card(string tag, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId(tag);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), owner, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

var attacker = await Card("ATT", P1);
var noblock = await Card("NOBLOCK", P2);   // the defender the attacker can't be blocked by
var other = await Card("OTHER", P2);       // an ordinary blocker

// attacker: "can't be blocked by NOBLOCK" (defenderCondition matches only NOBLOCK)
// (P7 stage-B SEAM) CanNotBeBlockedStaticSelfEffect returns CannotBlockClass
// (Assets/Scripts/Script/CardEffects/CannotBlockClass.cs), a new-model kind-class with no
// ToBinding/EffectRegistry bridge — the AS-IS-faithful path is the LIVE cEntity_EffectController scan
// NewModelContinuousScan/ContinuousRestrictionGate now performs. Attach the built effect to the attacker's
// controller via the same seam every ported card definition class uses.
var attackerCard = new CardSource(ctx, attacker, P1);
ICardEffect builtCantBeBlocked = CardEffectFactory.CanNotBeBlockedStaticSelfEffect(
    defenderCondition: p => p.InstanceId == noblock,
    isInheritedEffect: false,
    card: attackerCard,
    condition: null,
    effectName: "CantBeBlocked");
attackerCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(builtCantBeBlocked);

bool restrictedByNoblock = ContinuousRestrictionGate.EvaluateBeBlocked(ctx, attacker, noblock).IsRestricted;
bool restrictedByOther = ContinuousRestrictionGate.EvaluateBeBlocked(ctx, attacker, other).IsRestricted;

int fails = 0;
void Check(bool cond, string label) { Console.WriteLine((cond ? "PASS " : "FAIL ") + label); if (!cond) fails++; }
Check(restrictedByNoblock, "can't be blocked BY the matching defender (NOBLOCK) -> restricted");
Check(!restrictedByOther, "CAN be blocked by a non-matching defender (OTHER) -> not restricted");
if (fails > 0) { Environment.Exit(1); }
Console.WriteLine("\n2 test(s) passed.");

sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
