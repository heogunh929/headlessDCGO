// 1:1 mirror of the original ST3_09 (ST3/Yellow).
//   [When Digivolving] When you have 3 security cards or less, trigger <Recovery +1 (Deck)>.
//   -> RecoveryTriggerEffect (OnEnterFieldAnyone, +1, condition: owner security count <= 3)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST3_09 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.SecurityCount(card) <= 3;
            }

            cardEffects.Add(CardEffectFactory.RecoveryTriggerEffect(
                timing: EffectTiming.OnEnterFieldAnyone,
                amount: 1,
                card: card,
                condition: Condition,
                description: "[When Digivolving] When you have 3 security cards or less, trigger <Recovery +1 (Deck)>."));
        }

        return cardEffects;
    }
}
