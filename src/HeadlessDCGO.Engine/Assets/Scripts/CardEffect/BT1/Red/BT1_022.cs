// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_022.cs
// 1:1 mirror of the original BT1_022 (BT1/Red).
//   [Piercing] (self, static)
//   -> OnDetermineDoSecurityCheck: CardEffectFactory.PierceSelfEffect (same shape as BT1_026 / ST4_13 /
//      ST7_10 — this timing is a query-time keyword, not a trigger emission).
//   [Your Turn] When this Digimon is blocked, trigger <Draw 1>. (Draw 1 card from your deck.)
//   AS-IS: ActivateClass on EffectTiming.OnBlockAnyone, CanUseCondition = CanTriggerOnAttack(hashtable, card)
//   && IsOwnerTurn(card), CanActivateCondition = IsExistOnBattleArea(card) && Owner.LibraryCards.Count >= 1,
//   ORDER=-1, ISOPTIONAL=false, IsInheritedEffect=true,
//   ActivateCoroutine = new DrawClass(card.Owner, 1, activateClass).Draw().
//   Headless mirror: the uniform ActivatedEffect (= AS-IS ActivateClass) with body=DrawBody(1),
//   maxCountPerTurn=null (AS-IS ORDER=-1), isOptional=false. CanTriggerOnAttack at OnBlockAnyone checks the
//   ATTACKING permanent (this card, since this card's effect fires "when this [attacking] Digimon is
//   blocked") contains this card (BT1_012 / ST1_09 use the identical CanTriggerOnAttack gate for the same
//   "[Your Turn] When this Digimon is blocked" shape at OnBlockAnyone).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_022 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        if (timing == EffectTiming.OnBlockAnyone)
        {
            bool CanActivate()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Library).Count >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnBlockAnyone,
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card) && CardEffectCommons.IsOwnerTurn(card),
                canActivate: CanActivate,
                body: new DrawBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Your Turn] When this Digimon is blocked, trigger <Draw 1>. (Draw 1 card from your deck.)",
                // AS-IS SetIsInheritedEffect(true) — this is a digitama INHERITED effect (fires only while this card
                // is a NON-top digivolution source under the attacker). The original port dropped the flag (declared
                // in the header comment but missing from the construction), so the inherited scan excluded it and the
                // effect never fired — surfaced by the F1-Tier2 OnBlockAnyone e2e witness.
                isInheritedEffect: true));
        }

        return cardEffects;
    }
}
