// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotBlock.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of AS-IS CanNotBlock.cs factory partial.
// Returns the ported CannotBlockClass kind-class (CardEffects/CannotBlockClass.cs).
// ADAPTATIONS: (1) card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card) (PermanentView bridge).
//   (2) permanent.TopCard.CanNotBeAffected(ICardEffect) -> CanNotBeAffected(EffectSourceCard?.InstanceId).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // CannotBlockClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that oneself can't block
    public static CannotBlockClass CanNotBlockStaticSelfEffect(
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

        bool DefenderCondition(Permanent defender)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(defender))
            {
                if (defender == ICardEffect.ResolvePermanentOfThisCard(card))  // ADAPTATION (1)
                {
                    return true;
                }
            }

            return false;
        }

        return CanNotBlockStaticEffect(
            attackerCondition: attackerCondition,
            defenderCondition: DefenderCondition,
            isInheritedEffect: isInheritedEffect,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);
    }
    #endregion

    #region Static effect that can't block
    public static CannotBlockClass CanNotBlockStaticEffect(
        Func<Permanent, bool> attackerCondition,
        Func<Permanent, bool> defenderCondition,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        string effectName)
    {
        CannotBlockClass cannotBlockClass = new CannotBlockClass();
        cannotBlockClass.SetUpICardEffect(effectName, CanUseCondition, card);
        cannotBlockClass.SetUpCannotBlockClass(permanentsCondition: PermanentsCondition);

        if (isInheritedEffect)
        {
            cannotBlockClass.SetIsInheritedEffect(true);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return condition == null || condition();
        }

        bool PermanentsCondition(Permanent attacker, Permanent defender)
        {
            return AttackerCondition(attacker) && DefenderCondition(defender);
        }

        bool AttackerCondition(Permanent attacker)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(attacker))
            {
                if (!attacker.TopCard.CanNotBeAffected(cannotBlockClass.EffectSourceCard?.InstanceId))  // ADAPTATION (2)
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
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(defender))
            {
                if (defenderCondition == null || defenderCondition(defender))
                {
                    return true;
                }
            }

            return false;
        }

        return cannotBlockClass;
    }
    #endregion
}
