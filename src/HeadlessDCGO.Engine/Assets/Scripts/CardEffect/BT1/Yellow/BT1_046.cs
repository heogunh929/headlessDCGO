// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_046.cs
// 1:1 headless mirror via the uniform ActivatedEffect (= AS-IS ActivateClass): a conditional
// [When Attacking] draw gated on battle-area presence, a non-empty deck, and a hand size cap.
//   [When Attacking] Trigger <Draw 1>. (Draw 1 card from your deck.)
//   -> ActivatedEffect(OnAllyAttack, CanUse=CanTriggerOnAttack [self-scope],
//      CanActivate=on battle area && library >= 1 && hand <= 4, body=DrawBody(1),
//      maxCountPerTurn=null [AS-IS ORDER=-1], isOptional=false [AS-IS ISOPTIONAL=false]).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_046 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanActivate()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Library).Count >= 1)
                    {
                        if (((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Hand).Count <= 4)
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
                description: "[When Attacking] Trigger <Draw 1>. (Draw 1 card from your deck.)"));
        }

        return cardEffects;
    }
}
