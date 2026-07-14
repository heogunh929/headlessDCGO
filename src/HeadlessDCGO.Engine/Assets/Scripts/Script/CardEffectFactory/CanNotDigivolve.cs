// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotDigivolve.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of AS-IS CanNotDigivolve.cs factory partial.
// Returns the ported CanNotDigivolveClass kind-class (CardEffects/CanNotEvolveClass.cs).
// ADAPTATIONS: (1) card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card).
//   (2) permanent.TopCard.CanNotBeAffected(ICardEffect) -> CanNotBeAffected(EffectSourceCard?.InstanceId).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // CanNotDigivolveClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that oneself can't digivolve
    public static CanNotDigivolveClass CanNotDigivolveStaticSelfEffect(
        Func<CardSource, bool> cardCondition,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        string effectName)
    {
        bool CanUseCondition()
        {
            if (CardEffectCommons.IsExistOnField(card))
            {
                if (condition == null || condition())
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
                if (permanent == ICardEffect.ResolvePermanentOfThisCard(card))  // ADAPTATION (1)
                {
                    return true;
                }
            }

            return false;
        }

        return CanNotDigivolveStaticEffect(
            permanentCondition: PermanentCondition,
            cardCondition: cardCondition,
            isInheritedEffect: isInheritedEffect,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);
    }
    #endregion

    #region Static effect that can't digivolve
    public static CanNotDigivolveClass CanNotDigivolveStaticEffect(
        Func<Permanent, bool> permanentCondition,
        Func<CardSource, bool> cardCondition,
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        string effectName)
    {
        CanNotDigivolveClass canNotEvolveClass = new CanNotDigivolveClass();
        canNotEvolveClass.SetUpICardEffect(effectName, CanUseCondition, card);
        canNotEvolveClass.SetUpCanNotEvolveClass(permanentCondition: PermanentCondition, cardCondition: CardCondition);

        if (isInheritedEffect)
        {
            canNotEvolveClass.SetIsInheritedEffect(true);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return condition == null || condition();
        }

        bool PermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnField(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(canNotEvolveClass))  // ADAPTATION (2)
                {
                    if (permanentCondition == null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CardCondition(CardSource cardSource)
        {
            if (cardSource != null)
            {
                if (cardCondition == null || cardCondition(cardSource))
                {
                    return true;
                }
            }

            return false;
        }

        return canNotEvolveClass;
    }
    #endregion
}
