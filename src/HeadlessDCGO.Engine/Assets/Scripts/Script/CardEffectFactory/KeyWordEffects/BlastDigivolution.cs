// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/BlastDigivolution.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS BlastDigivolution.cs factory partial.
// ADAPTATION (substrate only; logic verbatim):
//   * coroutine `IEnumerator ActivateCoroutine` (has yields) -> `async Task ActivateCoroutine`; nested
//     `IEnumerator SelectPermanentCoroutine` -> `async Task`; `yield return StartCoroutine(X)` -> `await X`;
//     lone `yield return null` -> `await Task.CompletedTask;`.
//   * permanent.TopCard.CanNotBeAffected(activateClass) -> .CanNotBeAffected(activateClass.EffectSourceCard?.InstanceId).
//   * stripped `using UnityEngine;`. Replaces the monolith's invented BlastDigivolveEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Blast Digivolve]
    public static ActivateClass BlastDigivolveEffect(CardSource card, Func<bool> condition)
    {
        if (card == null) return null;
        if (!CardEffectCommons.IsExistOnHand(card)) return null;
        if (card.Owner.GetBattleAreaPermanents().Count == 0) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Blast Digivolve", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, DataBase.BlastDigivolveEffectDiscription());
        activateClass.SetIsCounterEffect(true);

        bool CanSelectPermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
            {
                if (permanent.IsDigimon)
                {
                    if (card.CanPlayCardTargetFrame(permanent.PermanentFrame, false, activateClass))
                    {
                        if (!permanent.TopCard.CanNotBeAffected(activateClass.EffectSourceCard?.InstanceId))
                            return true;
                    }
                }
            }

            return false;
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, permanent => CardEffectCommons.IsOpponentPermanent(permanent, card)))
            {
                if (card.Owner.HandCards.Contains(card))
                {
                    if (condition == null || condition())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            if (card.Owner.HandCards.Contains(card))
            {
                if (CardEffectCommons.HasMatchConditionPermanent(CanSelectPermanentCondition))
                {
                    if (condition == null || condition())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            Permanent selectedPermanent = null;

            int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(CanSelectPermanentCondition));

            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

            selectPermanentEffect.SetUp(
                selectPlayer: card.Owner,
                canTargetCondition: CanSelectPermanentCondition,
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                maxCount: maxCount,
                canNoSelect: false,
                canEndNotMax: false,
                selectPermanentCoroutine: SelectPermanentCoroutine,
                afterSelectPermanentCoroutine: null,
                mode: SelectPermanentEffect.Mode.Custom,
                cardEffect: activateClass);

            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to digivolve.", "The opponent is selecting 1 Digimon to digivolve.");

            await selectPermanentEffect.Activate();

            async Task SelectPermanentCoroutine(Permanent permanent)
            {
                selectedPermanent = permanent;

                await Task.CompletedTask;
            }

            if (selectedPermanent != null)
            {
                if (card.CanPlayCardTargetFrame(selectedPermanent.PermanentFrame, false, activateClass))
                {
                    PlayCardClass playCardClass = new PlayCardClass(
                        cardSources: new List<CardSource>() { card },
                        hashtable: CardEffectCommons.CardEffectHashtable(activateClass),
                        payCost: false,
                        targetPermanent: selectedPermanent,
                        isTapped: false,
                        root: SelectCardEffect.Root.Hand,
                        activateETB: true);

                    await playCardClass.PlayCard();
                }
            }
        }

        return activateClass;
    }
    #endregion
}
