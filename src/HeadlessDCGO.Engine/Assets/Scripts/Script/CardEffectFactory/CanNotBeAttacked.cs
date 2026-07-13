// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotBeAttacked.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of AS-IS CanNotBeAttacked.cs factory partial.
// Returns the ported CanNotAttackTargetDefendingPermanentClass kind-class (via CanNotAttackStaticEffect).
// ADAPTATION: card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // CanNotAttackTargetDefendingPermanentClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that oneself can't be attacked
    public static CanNotAttackTargetDefendingPermanentClass CanNotBeAttackedSelfStaticEffect(
        Func<Permanent, bool> attackerCondition,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        string effectName)
    {
        bool CanUseCondition()
        {
            if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
            {
                if (condition == null || condition())
                {
                    return true;
                }
            }

            return false;
        }

        bool DefenderCondition(Permanent attacker)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(attacker))
            {
                if (attacker == ICardEffect.ResolvePermanentOfThisCard(card))  // ADAPTATION (1)
                {
                    return true;
                }
            }

            return false;
        }

        return CanNotAttackStaticEffect(
            attackerCondition: attackerCondition,
            defenderCondition: DefenderCondition,
            isInheritedEffect: isInheritedEffect,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);
    }
    #endregion
}
