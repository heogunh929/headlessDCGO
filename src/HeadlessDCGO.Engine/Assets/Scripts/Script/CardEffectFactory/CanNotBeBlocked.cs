// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotBeBlocked.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of AS-IS CanNotBeBlocked.cs factory partial.
// Returns the ported CannotBlockClass kind-class (via CanNotBlockStaticEffect).
// ADAPTATION: card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // CannotBlockClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that oneself is unblockable
    public static CannotBlockClass CanNotBeBlockedStaticSelfEffect(
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

        return CanNotBlockStaticEffect(
            attackerCondition: AttackerCondition,
            defenderCondition: defenderCondition,
            isInheritedEffect: isInheritedEffect,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);
    }
    #endregion
}
