// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_074.cs
// 1:1 mirror of the original BT3_074 (two [None] branches, neither inherited).
//   [All Turns] During your turn, this Digimon can't be blocked.  -> CanNotBeBlockedStaticSelfEffect
//     (condition: owner's turn). The AS-IS defenderCondition is null (no defender narrowing) and the
//     headless factory has no effectName parameter to carry the cosmetic "Unblockable" label — both
//     dropped losslessly (no gameplay narrowing; the label is display-only and unsupported by this
//     framework's ChangeSelfDPStaticEffect/CanNotBeBlockedStaticSelfEffect signatures).
//   [All Turns] During your opponent's turn, this Digimon gets +2000 DP.  -> ChangeSelfDPStaticEffect
//     (condition: on battle area + opponent's turn).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_074 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsOwnerTurn(card);
            }

            cardEffects.Add(CardEffectFactory.CanNotBeBlockedStaticSelfEffect(defenderCondition: null, isInheritedEffect: false, card: card, condition: Condition));
        }

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOpponentTurn(card);
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(
                changeValue: 2000,
                isInheritedEffect: false,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}
