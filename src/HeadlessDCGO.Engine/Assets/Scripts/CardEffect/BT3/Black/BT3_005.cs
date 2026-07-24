// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_005.cs
// 1:1 mirror of the original BT3_005 (single branch, inherited).
//   [When Attacking][Once Per Turn] If this Digimon is level 7, gain 1 memory.
//   -> AddMemoryTriggerEffect (OnAllyAttack, +1, inherited, mandatory ("gain", not "you may gain" — AS-IS
//      ISOPTIONAL=false), Once Per Turn (AS-IS SetUpActivateClass order=1)). CanUseCondition =
//      CanTriggerOnAttack (the OnAllyAttack timing itself); CanActivateCondition = self is level 7
//      (folded into the factory's `condition`).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_005 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool Condition()
            {
                HeadlessEntityId topId = card.PermanentOfThisCard().TopInstanceId;
                if (topId.IsEmpty)
                {
                    return false;
                }

                var permanent = new Permanent(card.Context, topId, card.Owner);
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && permanent.Level == 7
                    && permanent.TopCard.HasLevel;
            }

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                amount: 1,
                isInheritedEffect: true,
                card: card,
                condition: Condition,
                description: "[When Attacking][Once Per Turn] If this Digimon is level 7, gain 1 memory.",
                maxCountPerTurn: 1,
                hash: "Memory+1_BT3_005",
                isOptional: false));
        }

        return cardEffects;
    }
}
