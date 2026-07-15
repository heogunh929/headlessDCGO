// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Training.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Training.cs factory partial.
// ADAPTATION: coroutine `IEnumerator ActivateCoroutine` (has yields) -> `async Task ActivateCoroutine`;
// `yield return ContinuousController.instance.StartCoroutine(X)` -> `await X`; card.PermanentOfThisCard() ->
// ICardEffect.ResolvePermanentOfThisCard(card); stripped `using UnityEngine;`. Replaces the monolith's
// invented TrainingEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Training]
    public static ActivateClass TrainingEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Training", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, DataBase.TrainingEffectDiscription());

        bool CanUseCondition(Hashtable hashtable)
        {
            return true;
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanActivateSuspendCostEffect(card, true);
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

            await new SuspendPermanentsClass(
                    new List<Permanent>() { thisPermanent },
                    activateClass, isBlock: false).Tap();

            // AS-IS reads card.Owner.LibraryCards after the Tap coroutine completes; the mirror property
            // materializes a snapshot per read, so the capture must stay on the post-Tap side.
            List<CardSource> libraryCards = new Player(card.Context, card.Owner).LibraryCards;

            if (libraryCards.Count > 0)
                await thisPermanent.AddDigivolutionCardsBottom(
                            new List<CardSource> { libraryCards[0] }, activateClass?.EffectSourceCard?.InstanceId, isFacedown: true);
        }

        return activateClass;
    }
    #endregion
}
