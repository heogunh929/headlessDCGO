// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_114.cs
// Decision: PORT
// Category: CardEffect
// Priority: HIGH
// Migration: Ported per-card effect.
//
// 1:1 mirror of the original BT1_114:
//   [All Turns] Security Attack +2.                          -> ChangeSelfSAttackStaticEffect (continuous)
//   [When Attacking] Lose 5 memory.                           -> AddMemoryTriggerEffect (OnAllyAttack)
//   [Your Turn] DP+3000.                                      -> ChangeSelfDPStaticEffect (continuous, condition)
// AS-IS OnAllyAttack ActivateClass: CanUseCondition = CanTriggerOnAttack(hashtable, card), CanActivateCondition
// = IsExistOnBattleArea(card) — ported explicitly (not folded away) via AddMemoryTriggerEffect's `condition`
// parameter, which maps to the AS-IS CanActivateCondition half.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_114 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(
                changeValue: 2,
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                amount: -5,
                isInheritedEffect: false,
                card: card,
                condition: () => CardEffectCommons.IsExistOnBattleArea(card),
                description: "[When Attacking] Lose 5 memory.",
                triggerGate: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card)));
        }

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(
                changeValue: 3000,
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}
