// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotBeDeletedByEffect.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of AS-IS CanNotBeDeletedByEffect.cs factory partial.
// Returns the ported CanNotBeDestroyedBySkillClass kind-class.
// ADAPTATION: permanent.TopCard.CanNotBeAffected(ICardEffect) -> CanNotBeAffected(EffectSourceCard?.InstanceId).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // CanNotBeDestroyedBySkillClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that can't be deleted by effect
    public static CanNotBeDestroyedBySkillClass CanNotBeDestroyedBySkillStaticEffect(Func<Permanent, bool> permanentCondition, Func<ICardEffect, bool> cardEffectCondition, bool isInheritedEffect, CardSource card, Func<bool> condition, string effectName)
    {
        CanNotBeDestroyedBySkillClass canNotBeDestroyedBySkillClass = new CanNotBeDestroyedBySkillClass();
        canNotBeDestroyedBySkillClass.SetUpICardEffect(effectName, CanUseCondition, card);
        canNotBeDestroyedBySkillClass.SetUpCanNotBeDestroyedBySkillClass(canNotBeDestroyedCondition: CanNotBeDestroyedCondition);

        if (isInheritedEffect)
        {
            canNotBeDestroyedBySkillClass.SetIsInheritedEffect(true);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return condition == null || condition();
        }

        bool CanNotBeDestroyedCondition(Permanent permanent, ICardEffect cardEffect)
        {
            if (PermanentCondition(permanent))
            {
                if (CardEffectCondition(cardEffect))
                {
                    return true;
                }
            }

            return false;
        }

        bool PermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnField(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(canNotBeDestroyedBySkillClass))
                {
                    if (permanentCondition == null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CardEffectCondition(ICardEffect cardEffect)
        {
            if (cardEffectCondition == null || cardEffectCondition(cardEffect))
            {
                return true;
            }

            return false;
        }

        return canNotBeDestroyedBySkillClass;
    }
    #endregion
}
