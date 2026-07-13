// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Iceclad.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord SYNC slice) 1:1 mirror of the AS-IS Iceclad.cs factory partial.
// Returns the ported IcecladClass kind-class (CardEffects/IcecladClass.cs). Replaces the monolith's old
// invented SelfKeywordByNameEffect-based IcecladSelfStaticEffect (IcecladStaticEffect was mirror-absent).
// ADAPTATION (substrate only; logic verbatim): AS-IS card.PermanentOfThisCard() returns a PermanentView on the
// mirror, not a Permanent -> bridge via ICardEffect.ResolvePermanentOfThisCard(card) (ICardEffect.cs).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // IcecladClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect of [Iceclad] on oneself
    public static IcecladClass IcecladSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool> condition)
    {
        bool PermanentCondition(Permanent permanent) => permanent == ICardEffect.ResolvePermanentOfThisCard(card);

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

        return IcecladStaticEffect(permanentCondition: PermanentCondition, isInheritedEffect: isInheritedEffect, card: card, condition: CanUseCondition);
    }
    #endregion

    #region Static effect of [Iceclad]
    public static IcecladClass IcecladStaticEffect(Func<Permanent, bool> permanentCondition, bool isInheritedEffect, CardSource card, Func<bool> condition)
    {
        string effectName = "Iceclad";

        IcecladClass icecladClass = new IcecladClass();
        icecladClass.SetUpICardEffect(effectName, CanUseCondition, card);
        icecladClass.SetUpIcecladClass(PermanentCondition: PermanentCondition);

        if (isInheritedEffect)
        {
            icecladClass.SetIsInheritedEffect(true);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return condition == null || condition();
        }

        bool PermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
            {
                if (permanentCondition == null || permanentCondition(permanent))
                {
                    return true;
                }
            }

            return false;
        }

        return icecladClass;
    }
    #endregion
}
