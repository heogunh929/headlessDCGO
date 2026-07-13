// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Blocker.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord SYNC slice) 1:1 mirror of the AS-IS Blocker.cs factory partial.
// Returns the ported BlockerClass kind-class (CardEffects/BlockerClass.cs). Replaces the monolith's old
// invented SelfKeywordEffect/ContinuousPlayerScopeKeywordEffect-based BlockerSelfStaticEffect/BlockerStaticEffect.
// ADAPTATION (substrate only; logic verbatim): AS-IS card.PermanentOfThisCard() returns a PermanentView on the
// mirror, not a Permanent -> bridge via ICardEffect.ResolvePermanentOfThisCard(card) (ICardEffect.cs).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // BlockerClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect of [Blocker] on oneself

    public static BlockerClass BlockerSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool> condition, bool isLinkedEffect = false)
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

        return BlockerStaticEffect(permanentCondition: PermanentCondition, isInheritedEffect: isInheritedEffect, card: card, condition: CanUseCondition, isLinkedEffect: isLinkedEffect);
    }

    #endregion

    #region Static effect of [Blocker]

    public static BlockerClass BlockerStaticEffect(Func<Permanent, bool> permanentCondition, bool isInheritedEffect, CardSource card, Func<bool> condition, bool isLinkedEffect = false)
    {
        string effectName = "Blocker";

        BlockerClass blockerClass = new BlockerClass();
        blockerClass.SetUpICardEffect(effectName, CanUseCondition, card);
        blockerClass.SetUpBlockerClass(PermanentCondition: PermanentCondition);

        if (isInheritedEffect)
        {
            blockerClass.SetIsInheritedEffect(true);
        }

        if (isLinkedEffect)
        {
            blockerClass.SetIsLinkedEffect(true);
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

        return blockerClass;
    }

    #endregion
}
