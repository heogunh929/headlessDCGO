// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/VortexCanAttackPlayers.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of the AS-IS VortexCanAttackPlayers.cs factory partial.
// Returns the ported VortexCanAttackPlayersClass kind-class (CardEffects/VortexCanAttackPlayersClass.cs).
// ADAPTATIONS (substrate only; logic verbatim):
//   (1) card.PermanentOfThisCard() (mirror returns PermanentView) -> ICardEffect.ResolvePermanentOfThisCard(card).
//   (2) AS-IS attacker.TopCard.CanNotBeAffected(<ICardEffect>) -> mirror CanNotBeAffected(<class>.EffectSourceCard?.InstanceId).
//   UnityEngine/Photon stripped.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // VortexCanAttackPlayersClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that oneself can't attack
    public static VortexCanAttackPlayersClass VortexCanAttackPlayersSelfStaticEffect(
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

        return VortexCanAttackPlayersStaticEffect(
            attackerCondition: AttackerCondition,
            isInheritedEffect: isInheritedEffect,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);
    }
    #endregion

    #region Static effect that can't attack
    public static VortexCanAttackPlayersClass VortexCanAttackPlayersStaticEffect(
        Func<Permanent, bool> attackerCondition,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        string effectName)
    {
        VortexCanAttackPlayersClass vortexCanAttackPlayersClass = new VortexCanAttackPlayersClass();
        vortexCanAttackPlayersClass.SetUpICardEffect(effectName, CanUseCondition, card);
        vortexCanAttackPlayersClass.SetUpVortexCanAttackPlayersClass(attackerCondition: AttackerCondition);

        if (isInheritedEffect)
        {
            vortexCanAttackPlayersClass.SetIsInheritedEffect(true);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return condition == null || condition();
        }

        bool AttackerCondition(Permanent attacker)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(attacker))
            {
                if (!attacker.TopCard.CanNotBeAffected(vortexCanAttackPlayersClass))  // ADAPTATION (2)
                {
                    if (attackerCondition == null || attackerCondition(attacker))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        return vortexCanAttackPlayersClass;
    }
    #endregion
}
