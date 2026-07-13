// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Execute.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Execute.cs factory partial.
// ADAPTATION: card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card); coroutine
// `IEnumerator ActivateCoroutine` (pure delegation) -> non-async `Task ActivateCoroutine`.
// Replaces the monolith's invented ExecuteSelfEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Execute] on oneself

    public static ActivateClass ExecuteSelfEffect(bool isInheritedEffect, CardSource card, Func<bool> condition,
        ICardEffect rootCardEffect = null)
    {
        Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

        bool CanUseCondition()
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                   (condition == null || condition());
        }

        return ExecuteEffect(targetPermanent: targetPermanent, isInheritedEffect: isInheritedEffect, condition: CanUseCondition,
            rootCardEffect: rootCardEffect, card);
    }

    #endregion

    #region Trigger effect of [Execute]

    public static ActivateClass ExecuteEffect(Permanent targetPermanent, bool isInheritedEffect, Func<bool> condition,
        ICardEffect rootCardEffect, CardSource card)
    {
        if (targetPermanent == null) return null;
        if (targetPermanent.TopCard == null) return null;
        if (card == null) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Execute", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, DataBase.ExecuteEffectDiscription());
        activateClass.SetIsInheritedEffect(isInheritedEffect);

        if (rootCardEffect != null)
        {
            activateClass.SetIsInheritedEffect(false);
            activateClass.SetEffectSourcePermanent(targetPermanent);
            activateClass.SetRootCardEffect(rootCardEffect);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleArea(card) &&
                   CardEffectCommons.IsOwnerTurn(card);
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanActivateExecute(targetPermanent.TopCard, activateClass) &&
                   (condition == null || condition());
        }

        Task ActivateCoroutine(Hashtable hashtable)
        {
            return CardEffectCommons.ExecuteProcess(targetPermanent.TopCard, activateClass);
        }

        return activateClass;
    }

    #endregion
}
