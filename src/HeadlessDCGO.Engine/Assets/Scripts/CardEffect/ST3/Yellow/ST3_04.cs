// 1:1 mirror of the original ST3_04 (ST3/Yellow).
//   [Your Turn][Once Per Turn] When an opponent's Digimon is deleted by dropping to 0 DP, gain 1 memory.
//   -> AddMemoryTriggerEffect (OnDestroyedAnyone, +1, [Your Turn] condition)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST3_04 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);
            }

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnDestroyedAnyone,
                amount: 1,
                isInheritedEffect: true,
                card: card,
                condition: Condition,
                description: "[Your Turn][Once Per Turn] When an opponent's Digimon is deleted by dropping to 0 DP, gain 1 memory."));
        }

        return cardEffects;
    }
}
