// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Vortex.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Vortex.cs factory partial.
// ADAPTATION: card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card); coroutine
// `IEnumerator ActivateCoroutine` (pure delegation) -> non-async `Task ActivateCoroutine`. Replaces the old
// mirror-invented `static class Vortex` (.Create; ZERO consumers) plus the monolith's invented VortexSelfEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Vortex] on oneself

    public static ActivateClass VortexSelfEffect(bool isInheritedEffect, CardSource card, Func<bool> condition,
        ICardEffect rootCardEffect = null)
    {
        Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

        bool CanUseCondition()
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                   (condition == null || condition());
        }

        return VortexEffect(targetPermanent: targetPermanent, isInheritedEffect: isInheritedEffect, condition: CanUseCondition,
            rootCardEffect: rootCardEffect, card);
    }

    #endregion

    #region Trigger effect of [Vortex]

    public static ActivateClass VortexEffect(Permanent targetPermanent, bool isInheritedEffect, Func<bool> condition,
        ICardEffect rootCardEffect, CardSource card)
    {
        if (targetPermanent == null) return null;
        if (targetPermanent.TopCard == null) return null;
        if (card == null) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Vortex", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, DataBase.VortexEffectDiscription());
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
            return CardEffectCommons.CanActivateVortex(targetPermanent.TopCard, activateClass) &&
                   (condition == null || condition());
        }

        Task ActivateCoroutine(Hashtable hashtable)
        {
            return CardEffectCommons.VortexProcess(targetPermanent.TopCard, activateClass);
        }

        return activateClass;
    }

    #endregion
}
