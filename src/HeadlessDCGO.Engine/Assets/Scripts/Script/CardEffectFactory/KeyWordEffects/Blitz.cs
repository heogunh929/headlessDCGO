// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Blitz.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Blitz.cs factory partial.
// ADAPTATION (substrate only; logic verbatim):
//   * card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card).
//   * `Func<IEnumerator> beforeOnAttackCoroutine` -> `Func<Task> beforeOnAttackCoroutine` (coroutine->Task).
//   * coroutine `IEnumerator ActivateCoroutine` (pure delegation) -> non-async `Task ActivateCoroutine`.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Blitz] on oneself
    public static ActivateClass BlitzSelfEffect(
        bool isInheritedEffect,
        CardSource card,
        Func<bool> condition,
        bool isWhenDigivolving,
        ICardEffect rootCardEffect = null)
    {
        Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card) ?? new Permanent(new List<CardSource>() { card });

        bool CanUseCondition()
        {
            if (condition == null || condition())
            {
                return true;
            }

            return false;
        }

        return BlitzEffect(
            targetPermanent: targetPermanent,
            isInheritedEffect: isInheritedEffect,
            condition: CanUseCondition,
            isWhenDigivolving: isWhenDigivolving,
            rootCardEffect: rootCardEffect,
            card: card);
    }
    #endregion

    #region Trigger effect of [Blitz]
    public static ActivateClass BlitzEffect(
        Permanent targetPermanent,
        bool isInheritedEffect,
        Func<bool> condition,
        bool isWhenDigivolving,
        ICardEffect rootCardEffect,
        CardSource card,
        Func<Task> beforeOnAttackCoroutine = null)
    {
        if (targetPermanent == null) return null;
        if (targetPermanent.TopCard == null) return null;
        if (card == null) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Blitz", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
        activateClass.SetIsInheritedEffect(isInheritedEffect);

        string EffectDiscription()
        {
            if (isWhenDigivolving)
            {
                return $"[When Digivolving] {DataBase.BilitzEffectDiscription()}.";
            }

            else
            {
                return $"[On Play] {DataBase.BilitzEffectDiscription()}.";
            }
        }

        if (rootCardEffect != null)
        {
            activateClass.SetIsInheritedEffect(false);
            activateClass.SetEffectSourcePermanent(targetPermanent);
            activateClass.SetRootCardEffect(rootCardEffect);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (isWhenDigivolving)
            {
                if (CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card))
                {
                    return true;
                }
            }

            else
            {
                if (CardEffectCommons.CanTriggerOnPlay(hashtable, card))
                {
                    return true;
                }
            }

            return false;
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.CanActivateBlitz(targetPermanent.TopCard, activateClass))
            {
                if (condition == null || condition())
                {
                    return true;
                }
            }

            return false;
        }

        Task ActivateCoroutine(Hashtable _hashtable)
        {
            return CardEffectCommons.BlitzProcess(targetPermanent.TopCard, activateClass, beforeOnAttackCoroutine);
        }

        return activateClass;
    }
    #endregion
}
