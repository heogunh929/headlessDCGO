// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotAttack.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of AS-IS CanNotAttack.cs factory partial.
// Returns the ported CanNotAttackTargetDefendingPermanentClass kind-class.
// ADAPTATIONS: (1) card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card).
//   (2) permanent.TopCard.CanNotBeAffected(ICardEffect) -> CanNotBeAffected(EffectSourceCard?.InstanceId).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // CanNotAttackTargetDefendingPermanentClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that oneself can't attack
    public static CanNotAttackTargetDefendingPermanentClass CanNotAttackSelfStaticEffect(
        Func<Permanent, bool> defenderCondition,
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

        bool AttackerCondition(Permanent attacker)
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
            attackerCondition: AttackerCondition,
            defenderCondition: defenderCondition,
            isInheritedEffect: isInheritedEffect,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);
    }
    #endregion

    #region Static effect that can't attack
    public static CanNotAttackTargetDefendingPermanentClass CanNotAttackStaticEffect(
        Func<Permanent, bool> attackerCondition,
        Func<Permanent, bool> defenderCondition,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        string effectName)
    {
        CanNotAttackTargetDefendingPermanentClass canNotAttackClass = new CanNotAttackTargetDefendingPermanentClass();
        canNotAttackClass.SetUpICardEffect(effectName, CanUseCondition, card);
        canNotAttackClass.SetUpCanNotAttackTargetDefendingPermanentClass(attackerCondition: AttackerCondition, defenderCondition: DefenderCondition);

        if (isInheritedEffect)
        {
            canNotAttackClass.SetIsInheritedEffect(true);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return condition == null || condition();
        }

        bool AttackerCondition(Permanent attacker)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(attacker))
            {
                if (!attacker.TopCard.CanNotBeAffected(canNotAttackClass))  // ADAPTATION (2)
                {
                    if (attackerCondition == null || attackerCondition(attacker))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool DefenderCondition(Permanent defender)
        {
            return defenderCondition == null || defenderCondition(defender);
        }

        return canNotAttackClass;
    }
    #endregion
}
