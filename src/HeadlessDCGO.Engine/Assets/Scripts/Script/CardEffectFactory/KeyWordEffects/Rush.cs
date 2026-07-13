// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Rush.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord SYNC slice) 1:1 mirror of the AS-IS Rush.cs factory partial.
// Returns the ported RushClass kind-class (CardEffects/RushClass.cs). Replaces the monolith's old invented
// SelfKeywordBatch2Effect/ContinuousPlayerScopeKeywordEffect-based RushSelfStaticEffect/RushStaticEffect.
// ADAPTATION (substrate only; logic verbatim): AS-IS card.PermanentOfThisCard() returns a PermanentView on the
// mirror, not a Permanent -> bridge via ICardEffect.ResolvePermanentOfThisCard(card) (ICardEffect.cs).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // RushClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect of [Rush] on oneself
    public static RushClass RushSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool> condition)
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

        return RushStaticEffect(permanentCondition: PermanentCondition, isInheritedEffect: isInheritedEffect, card: card, condition: CanUseCondition);
    }
    #endregion

    #region Static effect of [Rush]
    public static RushClass RushStaticEffect(Func<Permanent, bool> permanentCondition, bool isInheritedEffect, CardSource card, Func<bool> condition)
    {
        string effectName = "Rush";

        RushClass rushClass = new RushClass();
        rushClass.SetUpICardEffect(effectName, CanUseCondition, card);
        rushClass.SetUpRushClass(PermanentCondition: PermanentCondition);

        if (isInheritedEffect)
        {
            rushClass.SetIsInheritedEffect(true);
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

        return rushClass;
    }
    #endregion
}
