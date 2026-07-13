// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Progress.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord SYNC slice) 1:1 mirror of the AS-IS Progress.cs factory partial.
// Returns the ported CanNotAffectedClass kind-class (CardEffects/CanNotAffectedClass.cs). Replaces the
// monolith's old invented SelfKeywordBatch2Effect-based ProgressSelfStaticEffect (ProgressStaticEffect was
// mirror-absent).
// ADAPTATION (substrate only; logic verbatim): AS-IS cardSource.PermanentOfThisCard() returns a PermanentView
// on the mirror, not a Permanent -> bridge via ICardEffect.ResolvePermanentOfThisCard(cardSource) (ICardEffect.cs).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // CanNotAffectedClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect of [Progress] on oneself
    public static CanNotAffectedClass ProgressSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool> condition)
    {
        bool CanUseCondition()
        {
            return CardEffectCommons.CanActivateProgress(card) &&
                   (condition == null || condition());
        }

        return ProgressStaticEffect(isInheritedEffect: isInheritedEffect, card: card, condition: CanUseCondition);
    }
    #endregion

    #region Static effect of [Progress]
    public static CanNotAffectedClass ProgressStaticEffect(
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition)
    {
        CanNotAffectedClass canNotAffectedClass = new CanNotAffectedClass();
        canNotAffectedClass.SetUpICardEffect("Progress", CanUseCondition, card);
        canNotAffectedClass.SetUpCanNotAffectedClass(CardCondition: CardCondition, SkillCondition: SkillCondition);
        canNotAffectedClass.SetIsInheritedEffect(isInheritedEffect);
        canNotAffectedClass.SetIsBackgroundProcess(true);

        bool CanUseCondition(Hashtable hashtable)
        {
            if (condition == null || condition())
            {
                return true;
            }

            return false;
        }

        bool CardCondition(CardSource cardSource)
        {
            if (CardEffectCommons.IsExistOnBattleArea(card))
            {
                if (cardSource == card)
                {
                    if (GManager.instance.attackProcess.IsAttacking)
                    {
                        if (GManager.instance.attackProcess.AttackingPermanent == ICardEffect.ResolvePermanentOfThisCard(cardSource))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        bool SkillCondition(ICardEffect cardEffect)
        {
            if (cardEffect != null)
            {
                if (cardEffect.EffectSourceCard != null)
                {
                    if (CardEffectCommons.IsOpponentEffect(cardEffect.EffectSourceCard, card))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        return canNotAffectedClass;
    }
    #endregion
}
