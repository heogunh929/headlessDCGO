// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/ImmuneFromDPMinus.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of AS-IS ImmuneFromDPMinus.cs factory partial.
// Returns the ported ImmuneFromDPMinusClass kind-class (CardEffects/ImmuneFromDPMinusClass.cs).
// ADAPTATION: permanent.TopCard.CanNotBeAffected(ICardEffect) -> CanNotBeAffected(EffectSourceCard?.InstanceId).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // ImmuneFromDPMinusClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that DP is not reduced by effect
    public static ImmuneFromDPMinusClass ImmuneFromDPMinusStaticEffect(Func<Permanent, bool> permanentCondition, Func<ICardEffect, bool> cardEffectCondition, bool isInheritedEffect, CardSource card, Func<bool> condition, string effectName)
    {
        ImmuneFromDPMinusClass immuneFromDPMinusClass = new ImmuneFromDPMinusClass();
        immuneFromDPMinusClass.SetUpICardEffect(effectName, CanUseCondition, card);
        immuneFromDPMinusClass.SetUpImmuneFromDPMinusClass(permanentCondition: PermanentCondition, cardEffectCondition: CardEffectCondition);

        if (isInheritedEffect)
        {
            immuneFromDPMinusClass.SetIsInheritedEffect(true);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return condition == null || condition();
        }

        bool PermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(immuneFromDPMinusClass.EffectSourceCard?.InstanceId))
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

        return immuneFromDPMinusClass;
    }
    #endregion
}
