// Source: Assets/Scripts/CardEffect/BT2/Yellow/BT2_034.cs
// [On Deletion] If you have 3 or fewer security cards, trigger <Recovery +1 (Deck)>.
//   -> ActivatedEffect(OnDestroyedAnyone,
//        canUse=CanTriggerOnDeletion,
//        canActivate=() => IsExistOnTrash && SecurityCount<=3,
//        body=RecoveryBody(1),
//        maxCountPerTurn=null [AS-IS ORDER=-1], isOptional=false)
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_034 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnDestroyedAnyone,
                canUse: ctx => CardEffectCommons.CanTriggerOnDeletion(ctx, card),
                canActivate: () => CardEffectCommons.IsExistOnTrash(card)
                    && CardEffectCommons.SecurityCount(card) <= 3,
                body: new RecoveryBody(1),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[On Deletion] If you have 3 or fewer security cards, trigger <Recovery +1 (Deck)>. (Place the top card of your deck on top of your security stack.)"));
        }

        return cardEffects;
    }
}