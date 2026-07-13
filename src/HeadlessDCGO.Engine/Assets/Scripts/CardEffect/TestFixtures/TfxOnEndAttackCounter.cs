// TEST FIXTURE (not a real card). An UNCAPPED [All Turns] OnEndAttack reactor: whenever THIS Digimon's attack
// ends, its owner gains 1 memory — with NO once-per-turn cap. Uncapped so the observable is unmasked:
//   * one attack fires the reactor exactly ONCE (+1) — a per-attack event has a single subject (the attacker),
//     so there is no batch collapse to hide; +2 would signal a double-collect (hook + bridge).
//   * a card that did NOT attack does NOT gain (+0) — the self/attacker gate (CanTriggerOnAttack) rejects it.
//   * an attacker deleted before end-of-attack does NOT gain (+0) — the emit is guarded on attacker-alive.
// Self-scope (the reacting card must be a cardSource of the AttackingPermanent), mirroring every AS-IS OnEndAttack
// reactor. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class TfxOnEndAttackCounter : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEndAttack)
        {
            // AS-IS CanUseCondition mirror: CanTriggerOnAttack (the reacting card is a cardSource of the
            // AttackingPermanent — the self/attacker gate shared by every OnEndAttack reactor).
            bool CanUse(CardEffectResolveContext ctx) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerOnAttack(ctx, card);

            effects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEndAttack,
                canUse: CanUse,
                canActivate: () => CardEffectCommons.IsExistOnBattleAreaDigimon(card),
                body: new MemoryBody(1),
                maxCountPerTurn: null,   // UNCAPPED — no cap to mask a per-event over-fire (double-collect).
                isOptional: false,
                description: "[All Turns] When this Digimon's attack ends, gain 1 memory (uncapped)."));
        }

        return effects;
    }
}
