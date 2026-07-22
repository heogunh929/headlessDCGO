// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/BlastDigivolution.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS BlastDigivolution.cs factory partial.
//
// RD-P6C2-11 RESOLUTION (A8 구조골 GOAL 1, 2026-07-22 — was the P4/P6C2/P6C3 deferred STOP): the read-side of the
// field-frame slot model that this keyword needs has since LANDED and is live — `CardSource.CanPlayCardTargetFrame`
// (CardSource.cs:475, the AS-IS :1116-1194 seat) over `Permanent.PermanentFrame` (Permanent.cs:118, FrameID =
// field-list index ADAPTATION, RD-P6C1-1) — and `CanEvolve`/`CanEnterField`/`PayingCost` (RD-P6C1-2) plus the
// Hashtable-ctor `PlayCardClass.PlayCard()` (CardController.cs:2803) are all live for the occupied-frame
// free-digivolve path. This is the exact shape of the already-live sibling keyword `ArtsDigivolveEffect`
// (ArtsDigivolve.cs) and the ported card BT1_078 (`CanPlayCardTargetFrame(..., false, ...)` then
// `new PlayCardClass(payCost:false, targetPermanent:...).PlayCard()`), so the STOP was stale — the AS-IS body is
// mirrored verbatim here. (BlastDNADigivolution.cs stays STOPped — it needs the WRITE side of the slot model
// (PreferredFrame UI-geometry slot pick / new Permanent(List) / frame-indexed CreateNewPermanent / SetJogress),
// which has no headless substrate; see that file's design item note.)
//
// ADAPTATION (substrate only; logic verbatim; conventions per ArtsDigivolve.cs header):
//   * coroutine `IEnumerator ActivateCoroutine` (has yields) -> `async Task ActivateCoroutine`; nested
//     `IEnumerator SelectPermanentCoroutine` -> `async Task`; `yield return ContinuousController.instance.
//     StartCoroutine(X)` -> `await X`; lone `yield return null` -> `await Task.CompletedTask;`.
//   * `card.Owner.GetBattleAreaPermanents()` -> `new Player(card.Context, card.Owner).GetBattleAreaPermanents()`;
//     `card.Owner.HandCards` -> `new Player(card.Context, card.Owner).HandCards` (the BT2_023 idiom).
//   * AS-IS global `HasMatchConditionPermanent(pred)` / `MatchConditionPermanentCount(pred)` -> the mirror
//     card-scoped overloads `HasMatchConditionPermanent(card, pred)` / `MatchConditionPermanentCount(card, pred)`
//     (CardEffectCommons.cs:3912 / Save.cs:25 — same all-players+breeding scan; the predicate's own
//     IsPermanentExistsOnOwnerBattleArea term makes the card-context arg scope-neutral).
//   * SelectPermanentEffect.SetUp's id-based canTargetCondition (Func<HeadlessEntityId,bool>) -> `CanSelectPermanentById`
//     adapting the VERBATIM AS-IS Permanent predicate (the ArtsDigivolve/BT2_097 idiom).
//   * stripped `using UnityEngine;`. Replaces the monolith's invented BlastDigivolveEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;    // GManager
using HeadlessDCGO.Engine.Headless.Services;  // HeadlessEntityId, CardInstanceRecord

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

        bool CanSelectPermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
            {
                if (permanent.IsDigimon)
                {
                    if (card.CanPlayCardTargetFrame(permanent.PermanentFrame, false, activateClass))
                    {
                        if (!permanent.TopCard.CanNotBeAffected(activateClass))
                            return true;
                    }
                }
            }

            return false;
        }

        // (RD-P6C2-11) Id-shape adapter for the W4-bridged SetUp call site (canTargetCondition takes
        // Func<HeadlessEntityId,bool>): resolve the mirror Permanent for the candidate id (the ArtsDigivolve/
        // BT2_097 idiom) and evaluate the VERBATIM AS-IS predicate above.
        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        bool CanSelectPermanentById(HeadlessEntityId id)
        {
            Permanent? permanent = PermanentOf(id);
            return permanent is not null && CanSelectPermanentCondition(permanent);
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
            if (new Player(card.Context, card.Owner).HandCards.Contains(card))
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
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

            int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

            selectPermanentEffect.SetUp(
                selectPlayer: card.Owner,
                canTargetCondition: CanSelectPermanentById,
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
