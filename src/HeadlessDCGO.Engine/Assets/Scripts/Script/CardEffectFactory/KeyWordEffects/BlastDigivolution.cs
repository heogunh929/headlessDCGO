// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/BlastDigivolution.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS BlastDigivolution.cs factory partial.
// ADAPTATION (substrate only; logic verbatim):
//   * coroutine `IEnumerator ActivateCoroutine` (has yields) -> `async Task ActivateCoroutine`; nested
//     `IEnumerator SelectPermanentCoroutine` -> `async Task`; `yield return StartCoroutine(X)` -> `await X`;
//     lone `yield return null` -> `await Task.CompletedTask;`.
//   * permanent.TopCard.CanNotBeAffected(activateClass) -> .CanNotBeAffected(activateClass).
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
        if (new Player(card.Context, card.Owner).GetBattleAreaPermanents().Count == 0) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Blast Digivolve", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, DataBase.BlastDigivolveEffectDiscription());
        activateClass.SetIsCounterEffect(true);

        // STOP (design item RD-P6C2-11, docs/audit/rebuild_p6_cluster2_notes.md): AS-IS `CanSelectPermanentCondition`
        // depends on `CardSource.CanPlayCardTargetFrame`/`Permanent.PermanentFrame` — out of this cluster's
        // KeyWordEffects/CanUseEffects/kind-class/DataBase scope (CardSource.cs/Permanent.cs). This gates BOTH
        // CanActivateCondition and ActivateCoroutine (AS-IS-verbatim: the resolution/free-digivolve process
        // itself is unreachable without it), so both throw when actually invoked.
        bool CanSelectPermanentCondition(Permanent permanent)
        {
            throw new NotSupportedException(
                "BlastDigivolveEffect.CanSelectPermanentCondition: AS-IS CardSource.CanPlayCardTargetFrame/" +
                "Permanent.PermanentFrame have no mirror — design item RD-P6C2-11, " +
                "docs/audit/rebuild_p6_cluster2_notes.md.");
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, permanent => CardEffectCommons.IsOpponentPermanent(permanent, card)))
            {
                if (new Player(card.Context, card.Owner).HandCards.Contains(card))
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
            throw new NotSupportedException(
                "BlastDigivolveEffect.CanActivateCondition: AS-IS CardSource.CanPlayCardTargetFrame/" +
                "Permanent.PermanentFrame have no mirror — design item RD-P6C2-11, " +
                "docs/audit/rebuild_p6_cluster2_notes.md.");
        }

        Task ActivateCoroutine(Hashtable _hashtable)
        {
            throw new NotSupportedException(
                "BlastDigivolveEffect.ActivateCoroutine: AS-IS CardSource.CanPlayCardTargetFrame/" +
                "Permanent.PermanentFrame/PlayCardClass(Hashtable ctor) have no mirror — design item " +
                "RD-P6C2-11, docs/audit/rebuild_p6_cluster2_notes.md.");
        }

        return activateClass;
    }
    #endregion
}
