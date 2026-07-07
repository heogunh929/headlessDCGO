// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_049.cs
// 1:1 headless mirror via the uniform ActivatedEffect (= AS-IS ActivateClass): a
// [Your Turn] triggered draw gated on an opponent's Digimon being deleted by dropping to 0 DP.
//   [Your Turn] When an opponent's Digimon is deleted by dropping to 0 DP, trigger <Draw 1>
//   (Draw 1 card from your deck).
//   -> ActivatedEffect(OnDestroyedAnyone,
//      CanUse=on battle area && owner's turn && CanTriggerOnPermanentDeleted(opponent's Digimon) && IsDPZeroDelete,
//      CanActivate=on battle area && library >= 1, body=DrawBody(1),
//      maxCountPerTurn=null [AS-IS ORDER=-1], isOptional=false [AS-IS ISOPTIONAL=false]).
// AS-IS also sets SetIsInheritedEffect(true); the uniform ActivatedEffect primitive does not model
// inherited-effect (buried-under-digivolution) firing — same accepted gap as the sibling BT1_006 port.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_049 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
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
                timing: EffectTiming.OnDestroyedAnyone,
                canUse: ctx => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.CanTriggerOnPermanentDeleted(card, ctx, id => CardEffectCommons.IsOpponentOwnedDigimon(card, id))
                    && CardEffectCommons.IsDPZeroDelete(card, ctx),
                canActivate: CanActivate,
                body: new DrawBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Your Turn] When an opponent's Digimon is deleted by dropping to 0 DP, trigger <Draw 1> (Draw 1 card from your deck)."));
        }

        return cardEffects;
    }
}
