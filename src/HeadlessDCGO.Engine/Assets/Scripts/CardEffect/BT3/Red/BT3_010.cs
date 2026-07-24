// 1:1 mirror of the original BT3_010 (BT3/Red).
//   [Continuous] While this is a level 7 Digimon in the battle area on your turn, this Digimon gets +1
//   security attack.
//   -> ChangeSelfSAttackStaticEffect (timing None, condition mirrors IsExistOnBattleArea && IsOwnerTurn &&
//   card.PermanentOfThisCard().Level == 7 && card.PermanentOfThisCard().TopCard.HasLevel).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_010 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card))
                {
                    var topId = card.PermanentOfThisCard().TopInstanceId;
                    return CardEffectCommons.LevelOf(card, topId) == 7 && CardEffectCommons.TopCardHasLevel(card, topId);
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: true, card: card, condition: Condition));
        }

        return cardEffects;
    }
}
