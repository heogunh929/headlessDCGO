// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_066.cs
// 1:1 mirror of the original BT3_066 (single branch, [None], not inherited).
//   [All Turns] During your opponent's turn, this Digimon gets +1000 DP.
//   -> ChangeSelfDPStaticEffect (continuous, condition: on battle area + opponent's turn).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_066 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOpponentTurn(card);
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(changeValue: 1000, isInheritedEffect: true, card: card, condition: Condition));
        }

        return cardEffects;
    }
}
