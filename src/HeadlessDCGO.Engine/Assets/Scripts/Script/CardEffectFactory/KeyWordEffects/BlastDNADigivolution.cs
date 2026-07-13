// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/BlastDNADigivolution.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS BlastDNADigivolution.cs factory partial.
// AS-IS declares a top-level `BlastDNACondition` (name + Permanents + CardSources holder). The MIRROR ALREADY HAS
// a `BlastDNACondition` (record, namespace ...CardEffectCommons — CardPortingFramework.cs), so it is NOT redeclared
// here (its shape differs; the body field accesses are masked verbatim-missing members — see missing-log).
// ADAPTATION (substrate only; logic verbatim):
//   * coroutine `IEnumerator ActivateCoroutine` (has yields) -> `async Task ActivateCoroutine`; nested
//     `IEnumerator Select*Coroutine` -> `async Task Select*Coroutine`; `yield return
//     ContinuousController.instance.StartCoroutine(X)` -> `await X`; lone `yield return null` -> `await Task.CompletedTask;`.
//   * selectedCardSource.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(selectedCardSource).
//   * stripped `using UnityEngine;` / `using Photon.Pun;`.
// Replaces the monolith's invented BlastDNADigivolveEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Blast DNA Digivolve]
    public static ActivateClass BlastDNADigivolveEffect(CardSource card, List<BlastDNACondition> blastDNAConditions, Func<bool> condition)
    {
        if (card == null) return null;
        if (!CardEffectCommons.IsExistOnHand(card)) return null;
        if (card.Owner.GetBattleAreaPermanents().Count == 0) return null;
        if (card.Owner.HandCards.Count < 2) return null;

        List<Permanent> fieldPermanents = new List<Permanent>();
        List<Permanent> permanentSources = new List<Permanent>();
        List<CardSource> handSources = new List<CardSource>();

        void FilterDNAPermanents()
        {
            if (blastDNAConditions[0].Permanents.Count >= 1 && blastDNAConditions[0].CardSources.Count == 0)
                blastDNAConditions[1].Permanents.Clear();

            if (blastDNAConditions[1].Permanents.Count >= 1 && blastDNAConditions[1].CardSources.Count == 0)
                blastDNAConditions[0].Permanents.Clear();
        }

        void FilterDNAHandSources()
        {
            if (blastDNAConditions[0].CardSources.Count >= 1 && blastDNAConditions[0].CardSources.Count == 0)
                blastDNAConditions[1].CardSources.Clear();

            if (blastDNAConditions[1].CardSources.Count >= 1 && blastDNAConditions[1].CardSources.Count == 0)
                blastDNAConditions[0].CardSources.Clear();
        }

        bool HasValidDNATargets()
        {
            fieldPermanents = card.Owner.GetBattleAreaDigimons();

            foreach (BlastDNACondition DNACondition in blastDNAConditions)
            {
                DNACondition.Permanents = fieldPermanents.Filter(permanent => permanent.TopCard.EqualsCardName(DNACondition.Name));
                DNACondition.CardSources = card.Owner.HandCards.Filter(cardSource => cardSource.EqualsCardName(DNACondition.Name));

                permanentSources.AddRange(DNACondition.Permanents);
                handSources.AddRange(DNACondition.CardSources);
            }

            FilterDNAPermanents();
            FilterDNAHandSources();

            if (blastDNAConditions[0].Permanents.Count(permanent => !permanent.TopCard.CanNotEvolve(permanent)) > 0 && blastDNAConditions[1].CardSources.Count > 0)
                return true;

            if (blastDNAConditions[0].CardSources.Count > 0 && blastDNAConditions[1].Permanents.Count(permanent => !permanent.TopCard.CanNotEvolve(permanent)) > 0)
                return true;

            return false;
        }

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Blast DNA Digivolve", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, DataBase.BlastDNADigivolveEffectDiscription());
        activateClass.SetIsCounterEffect(true);

        bool CanSelectPermanent(Permanent permanent)
        {
            if(CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
            {
                foreach (BlastDNACondition DNACondition in blastDNAConditions)
                {
                    if (DNACondition.Permanents.Contains(permanent))
                        return true;
                }
            }

            return false;
        }

        bool CanSelectHandSource(CardSource cardSource)
        {
            return handSources.Contains(cardSource);
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
                if (HasValidDNATargets())
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
            CardSource selectedCardSource = null;

            int maxCount = Math.Min(1, permanentSources.Count);

            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

            selectPermanentEffect.SetUp(
                selectPlayer: card.Owner,
                canTargetCondition: CanSelectPermanent,
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                maxCount: maxCount,
                canNoSelect: false,
                canEndNotMax: false,
                selectPermanentCoroutine: SelectPermanentCoroutine,
                afterSelectPermanentCoroutine: null,
                mode: SelectPermanentEffect.Mode.Custom,
                cardEffect: activateClass);

            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to DNA digivolve.", "The opponent is selecting 1 Digimon to DNA digivolve.");

            await selectPermanentEffect.Activate();

            async Task SelectPermanentCoroutine(Permanent permanent)
            {
                selectedPermanent = permanent;

                foreach(string name in selectedPermanent.TopCard.CardNames)
                {
                    handSources = handSources.Filter(source => !source.ContainsCardName(name));
                }

                maxCount = Math.Min(1, handSources.Count);

                SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                selectHandEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectHandSource,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: maxCount,
                    canNoSelect: false,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    mode: SelectHandEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectHandEffect.SetUpCustomMessage("Select 1 Digimon to DNA digivolve.", "The opponent is selecting 1 Digimon to DNA digivolve.");

                await selectHandEffect.Activate();
            }

            async Task SelectCardCoroutine(CardSource cardSource)
            {
                selectedCardSource = cardSource;

                Permanent playedPermanent;
                int frameID = -1;

                FieldCardFrame preferredFrame = selectedCardSource.PreferredFrame();

                if (preferredFrame != null)
                {
                    frameID = preferredFrame.FrameID;
                }

                if (0 <= frameID && frameID < card.Owner.fieldCardFrames.Count)
                {
                    playedPermanent = new Permanent(new List<CardSource>() { selectedCardSource }) { IsSuspended = false };

                    await CardObjectController.CreateNewPermanent(playedPermanent, frameID);
                }

                int[] JogressEvoRootsFrameIDs = { 0, 0 };

                if (selectedPermanent.TopCard.EqualsCardName(blastDNAConditions[0].Name))
                {
                    JogressEvoRootsFrameIDs[0] = selectedPermanent.PermanentFrame.FrameID;
                    JogressEvoRootsFrameIDs[1] = ICardEffect.ResolvePermanentOfThisCard(selectedCardSource).PermanentFrame.FrameID;
                }
                else
                {
                    JogressEvoRootsFrameIDs[0] = ICardEffect.ResolvePermanentOfThisCard(selectedCardSource).PermanentFrame.FrameID;
                    JogressEvoRootsFrameIDs[1] = selectedPermanent.PermanentFrame.FrameID;
                }

                if (card.CanPlayJogress(true))
                {
                    PlayCardClass playCard = new PlayCardClass(
                        cardSources: new List<CardSource>() { card },
                        hashtable: CardEffectCommons.CardEffectHashtable(activateClass),
                        payCost: true,
                        targetPermanent: null,
                        isTapped: false,
                        root: SelectCardEffect.Root.Hand,
                        activateETB: true);

                    playCard.SetJogress(JogressEvoRootsFrameIDs);

                    await playCard.PlayCard();

                    foreach (BlastDNACondition DNACondition in blastDNAConditions)
                    {
                        DNACondition.Permanents = new List<Permanent>();
                        DNACondition.CardSources = new List<CardSource>();
                    }
                }
                else
                {
                    await CardObjectController.AddHandCard(selectedCardSource, false);
                }

            }
        }

        return activateClass;
    }
    #endregion

}
