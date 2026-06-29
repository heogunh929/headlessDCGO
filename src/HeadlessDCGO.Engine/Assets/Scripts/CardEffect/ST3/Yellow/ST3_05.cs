// 1:1 mirror of the original ST3_05 (ST3/Yellow).
//   [When Attacking] If you have 4 or more security cards, gain 1 memory.
//   -> AddMemoryTriggerEffect (OnAllyAttack, +1, condition: owner security count >= 4)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST3_05 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.SecurityCount(card) >= 4;
            }

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                amount: 1,
                isInheritedEffect: true,
                card: card,
                condition: Condition,
                description: "[When Attacking] If you have 4 or more security cards, gain 1 memory."));
        }

        return cardEffects;
    }
}
