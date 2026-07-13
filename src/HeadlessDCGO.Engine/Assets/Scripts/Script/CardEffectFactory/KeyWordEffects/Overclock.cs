// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Overclock.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Overclock.cs factory partial.
// ADAPTATION: card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card); coroutine
// `IEnumerator ActivateCoroutine` (pure delegation) -> non-async `Task ActivateCoroutine`. Replaces the old
// mirror-invented `static class Overclock` (.Create; ZERO consumers) plus the monolith's invented OverclockSelfEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Overclock] on oneself

    public static ActivateClass OverclockSelfEffect(string trait, bool isInheritedEffect, CardSource card, Func<bool> condition,
        ICardEffect rootCardEffect = null)
    {
        Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

        bool CanUseCondition()
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                   (condition == null || condition());
        }

        return OverclockEffect(trait: trait, targetPermanent: targetPermanent, isInheritedEffect: isInheritedEffect,
            condition: CanUseCondition,
            rootCardEffect: rootCardEffect, card);
    }

    #endregion

    #region Trigger effect of [Overclock]

    public static ActivateClass OverclockEffect(string trait, Permanent targetPermanent, bool isInheritedEffect, Func<bool> condition,
        ICardEffect rootCardEffect, CardSource card)
    {
        if (targetPermanent == null) return null;
        if (targetPermanent.TopCard == null) return null;
        if (card == null) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Overclock", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, DataBase.OverclockEffectDiscription(trait));
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
            return CardEffectCommons.CanActivateOverclock(trait, targetPermanent.TopCard, activateClass) &&
                   (condition == null || condition());
        }

        Task ActivateCoroutine(Hashtable hashtable)
        {
            return CardEffectCommons.OverclockProcess(trait, targetPermanent.TopCard, activateClass);
        }

        return activateClass;
    }

    #endregion
}
