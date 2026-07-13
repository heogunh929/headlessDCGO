// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_006.cs
// 1:1 headless mirror via the uniform ActivatedEffect (= AS-IS ActivateClass): a conditional
// [When Attacking] draw gated on the owner's security-stack size and a non-empty deck.
//   [When Attacking] If you have 5 or more security cards, trigger <Draw 1> (Draw 1 card from your deck).
//   -> ActivatedEffect(OnAllyAttack, CanUse=CanTriggerOnAttack [self-scope],
//      CanActivate=on battle area && SecurityCount>=5 && library not empty, body=DrawBody(1),
//      maxCountPerTurn=null [AS-IS ORDER=-1], isOptional=false [AS-IS ISOPTIONAL=false]).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_006 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanActivate()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.SecurityCount(card) >= 5)
                    {
                        if (((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Library).Count >= 1)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                canActivate: CanActivate,
                body: new DrawBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Attacking] If you have 5 or more security cards, trigger <Draw 1> (Draw 1 card from your deck)."));
        }

        return cardEffects;
    }
}
