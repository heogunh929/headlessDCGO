// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Collision.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord SYNC slice) 1:1 mirror of the AS-IS Collision.cs factory partial.
// Returns the ported CollisionClass kind-class (CardEffects/CollisionClass.cs). Replaces the monolith's old
// invented SelfKeywordByNameEffect/ContinuousPlayerScopeKeywordEffect-based CollisionSelfStaticEffect/CollisionStaticEffect.
// ADAPTATION (substrate only; logic verbatim): AS-IS card.PermanentOfThisCard() returns a PermanentView on the
// mirror, not a Permanent -> bridge via ICardEffect.ResolvePermanentOfThisCard(card) (ICardEffect.cs).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // CollisionClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect of [Collision] on oneself
    public static CollisionClass CollisionSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool> condition, bool isLinkedEffect = false)
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

        return CollisionStaticEffect(permanentCondition: PermanentCondition, isInheritedEffect: isInheritedEffect, card: card, condition: CanUseCondition, isLinkedEffect: isLinkedEffect);
    }
    #endregion

    #region Static effect of [Collision]
    public static CollisionClass CollisionStaticEffect(
        Func<Permanent, bool> permanentCondition,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        bool isLinkedEffect = false)
    {
        CollisionClass collisionClass = new();
        collisionClass.SetUpICardEffect("Collision", CanUseCondition, card);
        collisionClass.SetUpCollisionClass(PermanentCondition);
        collisionClass.SetIsInheritedEffect(isInheritedEffect);
        collisionClass.SetIsLinkedEffect(isLinkedEffect);

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

        bool CanUseCondition(Hashtable hashtable)
        {
            return condition == null || condition();
        }

        return collisionClass;
    }
    #endregion
}
