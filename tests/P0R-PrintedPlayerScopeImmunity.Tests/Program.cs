using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// (P0-restr) A PRINTED player-scope "cannot attack" exempts a subject that is IMMUNE to the printing card's
// effects — AS-IS Permanent.CanAttackTargetDigimon checks `!TopCard.CanNotBeAffected(cardEffect)` (Permanent.cs:
// 2267/2290) on the attacker. A non-immune subject is still restricted. (The granted player-scope form already
// embedded immunity via GainToPlayerScope; this covers the STATIC/printed form via ContinuousPlayerScopeRestrictionEffect.)

HeadlessPlayerId P1 = new(1);   // printing (holder) side
HeadlessPlayerId P2 = new(2);   // scoped / restricted side
HeadlessEntityId Holder = new("p1:HOLDER");
HeadlessEntityId ImmuneSub = new("p2:IMMUNE");
HeadlessEntityId PlainSub = new("p2:PLAIN");

EngineContext context = EngineContext.CreateDefault(randomSeed: 5);
var cards = (CardDatabase)context.CardRepository;
cards.Upsert(new CardRecord(new HeadlessEntityId("DEF"), "DEF", "Def",
    new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
foreach ((HeadlessEntityId id, HeadlessPlayerId owner) in new[] { (Holder, P1), (ImmuneSub, P2), (PlainSub, P2) })
{
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("DEF"), owner));
}

// Printed "the scoped player (P2)'s Digimon cannot attack", sourced by the holder (P1).
context.EffectRegistry.Register(
    CardEffectFactory.CanNotAttackStaticEffect(P2, isInheritedEffect: false, new CardSource(context, Holder, P1, P1), condition: null)
        .ToBinding("cannot-attack"));

// ImmuneSub is immune to the OPPONENT's (P1's) effects (AS-IS CanNotAffectedClass, SkillCondition = IsOpponentEffect).
context.EffectRegistry.Register(
    CardEffectFactory.CanNotAffectedStaticEffect(permanentCondition: null, skillCondition: src => src.Owner != P2,
        isInheritedEffect: false, new CardSource(context, ImmuneSub, P2, P2), condition: null)
        .ToBinding("immunity"));

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

Check(ContinuousRestrictionGate.EvaluateAttack(context, PlainSub).IsRestricted,
    "non-immune P2 Digimon is restricted by the printed player-scope cannot-attack");
Check(!ContinuousRestrictionGate.EvaluateAttack(context, ImmuneSub).IsRestricted,
    "immune P2 Digimon is EXEMPT (AS-IS !TopCard.CanNotBeAffected)");

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\n2 test(s) passed.");
