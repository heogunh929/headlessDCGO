using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
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
// (R3-W3c-1) the exemption reads the live CanNotBeAffected scan whose CanUse(null) gates on DoneStartGame (phase
// past None/Setup); initialise the turn to Main so ImmuneSub's immunity is active.
context.TurnController.Initialize(new[] { P1, P2 }, P1);
context.TurnController.SetPhase(HeadlessPhase.Main);
var cards = (CardDatabase)context.CardRepository;
cards.Upsert(new CardRecord(new HeadlessEntityId("DEF"), "DEF", "Def",
    new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
foreach ((HeadlessEntityId id, HeadlessPlayerId owner) in new[] { (Holder, P1), (ImmuneSub, P2), (PlainSub, P2) })
{
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("DEF"), owner));
    // (R3-W3c-1) on the battle area so the LIVE CardSource.CanNotBeAffected scan (which iterates field permanents)
    // can find ImmuneSub's immunity — the restriction-exemption term was rehomed to that scan.
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
}

// Printed "the scoped player (P2)'s Digimon cannot attack", sourced by the holder (P1). The AS-IS-mirrored
// CardEffectFactory.CanNotAttackStaticEffect (CardEffectFactory/CanNotAttack.cs) is the pure attackerCondition-
// based kind class (no player-scope convenience any more) — the printed STATIC player-scope form is built
// directly from ContinuousPlayerScopeRestrictionEffect (its own header: "covers the STATIC/printed form"),
// which already folds the AS-IS !TopCard.CanNotBeAffected exemption for RestrictionHelpers.CannotAttackKey.
context.EffectRegistry.Register(
    new ContinuousPlayerScopeRestrictionEffect(
        card: new CardSource(context, Holder, P1, P1),
        scopePlayerId: P2,
        restrictionKey: RestrictionHelpers.CannotAttackKey,
        scopeCardType: null,
        isInheritedEffect: false,
        condition: null)
    .ToBinding("cannot-attack"));

// (R3-W3c-1) ImmuneSub is immune to the OPPONENT's (P1's) effects (AS-IS CanNotAffectedClass, SkillCondition =
// IsOpponentEffect). The flipped CanNotAffectedStaticEffect returns a new-model CanNotAffectedClass consumed by the
// LIVE CardSource.CanNotBeAffected scan (no registry). Attach it to ImmuneSub's live effect list; the printed
// player-scope restriction's exemption term (!subject.CanNotBeAffected(this), ContinuousAndRestrictionEffects.cs:283)
// then reads it. null skillCondition = opponent-only (the causing effect's source is not ImmuneSub's controller).
var immuneSubCard = new CardSource(context, ImmuneSub, P2, P2);
immuneSubCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(
    CardEffectFactory.CanNotAffectedStaticEffect(permanentCondition: null, skillCondition: null,
        isInheritedEffect: false, immuneSubCard, condition: null));

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

// (R3-W3c-1) EvaluateAttack's exemption reads GManager.instance via CanUse->IsDisabled — enter the ambient scope.
using (AmbientMatchContext.Enter(context))
{
    Check(ContinuousRestrictionGate.EvaluateAttack(context, PlainSub).IsRestricted,
        "non-immune P2 Digimon is restricted by the printed player-scope cannot-attack");
    Check(!ContinuousRestrictionGate.EvaluateAttack(context, ImmuneSub).IsRestricted,
        "immune P2 Digimon is EXEMPT (AS-IS !TopCard.CanNotBeAffected)");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\n2 test(s) passed.");

// (R3-W3c-1) attaches the built CanNotAffectedClass to a card's live effect list (the seam a ported card uses).
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
