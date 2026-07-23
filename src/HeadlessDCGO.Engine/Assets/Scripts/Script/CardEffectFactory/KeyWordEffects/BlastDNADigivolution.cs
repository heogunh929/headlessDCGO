// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/BlastDNADigivolution.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS BlastDNADigivolution.cs factory partial.
// AS-IS declares a top-level `BlastDNACondition` (name + Permanents + CardSources holder). The MIRROR ALREADY HAS
// a `BlastDNACondition` (record, namespace ...CardEffectCommons — CardPortingFramework.cs); (P6C1) that record
// now carries the AS-IS shape ADDITIVELY (Name/Permanents/CardSources + the `(string name)` ctor), so the body
// reads it verbatim.
// ADAPTATION (substrate only; logic verbatim):
//   * coroutine `IEnumerator ActivateCoroutine` (has yields) -> `async Task ActivateCoroutine`; nested
//     `IEnumerator Select*Coroutine` -> `async Task Select*Coroutine`; `yield return
//     ContinuousController.instance.StartCoroutine(X)` -> `await X`; lone `yield return null` -> `await Task.CompletedTask;`.
//   * selectedCardSource.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(selectedCardSource).
//   * stripped `using UnityEngine;` / `using Photon.Pun;`.
//   * (P6C1) `card.Owner.HandCards` (a Player list PROPERTY on the bare mirror HeadlessPlayerId) rides the
//     established `new Player(card.Context, card.Owner).HandCards` route; `card.Owner.GetBattleAreaPermanents()`
//     rides the PlayerIdAsIsExtensions bridge (both = the BT2_023 idiom).
//   * (P6C1) the W4 SelectPermanentEffect.SetUp canTargetCondition is the established
//     Func<HeadlessEntityId,bool> id idiom — `CanSelectPermanentById` adapts the VERBATIM AS-IS
//     Permanent predicate (the BT2_097 pattern).
//   * (P6C1 — PORTED 2026-07-23) the jogress-frame play inside SelectPermanentCoroutine is now LIVE. All four
//     original blockers closed: RD-P6C1-7 SelectHandEffect + RD-P6C1-2 CanPlayJogress (available), the field-
//     frame WRITE (SetJogress + frameless zone-append CreateNewPermanent + PlayCardClass), and the jogress
//     COLLAPSE detach leaf Permanent.DiscardEvoRoots(putToTrash:false) (MIG4-DISCARDEVOROOTS-PUTTOTRASH now
//     LIVE at Permanent.cs:3920) + AddHandCard else-branch (RD-P6C1-8 RESOLVED). FRAME ADAPTATION: the AS-IS
//     PreferredFrame()/frameID slot-capacity dance reduces to the frameless CreateNewPermanent zone-append
//     (BT17_095 idiom). This factory is latent (no live card caller); the construction smoke test exercises it.
//     docs/audit/rebuild_p6_cluster1_notes.md.
// Replaces the monolith's invented BlastDNADigivolveEffect.
// (P6C1 FINDING, logged in the notes doc: tests/G9-048.SpecialPlay.Tests expects this factory to register a
// SpecialPlayRecipe (the pre-P4 monolith behavior); the AS-IS-verbatim factory returns an ActivateClass and
// registers nothing — that test's anchor moved, NOT a cluster-1 build item.)

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public partial class CardEffectFactory
{
    #region Trigger effect of [Blast DNA Digivolve]
    public static ActivateClass BlastDNADigivolveEffect(CardSource card, List<BlastDNACondition> blastDNAConditions, Func<bool> condition)
    {
        if (card == null) return null;
        if (!CardEffectCommons.IsExistOnHand(card)) return null;
        if (card.Owner.GetBattleAreaPermanents().Count == 0) return null;
        if (new Player(card.Context, card.Owner).HandCards.Count < 2) return null;

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
                DNACondition.CardSources = new Player(card.Context, card.Owner).HandCards.Filter(cardSource => cardSource.EqualsCardName(DNACondition.Name));

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

        // (P6C1) Id-shape adapter for the W4-bridged SetUp call site (canTargetCondition takes
        // Func<HeadlessEntityId,bool>): resolve the mirror Permanent for the candidate id (the BT2_097 idiom)
        // and evaluate the VERBATIM AS-IS predicate above.
        bool CanSelectPermanentById(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
            && CanSelectPermanent(new Permanent(card.Context, id, rec.OwnerId));

        bool CanSelectHandSource(CardSource cardSource)
        {
            return handSources.Contains(cardSource);
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

                // (P6C1 — PORTED 2026-07-23) The AS-IS remainder (:173-254): hand-material pick + jogress-FRAME
                // play. All four original blockers are now CLOSED — RD-P6C1-7 SelectHandEffect (Script/
                // SelectHandEffect.cs, 1:1), RD-P6C1-2 CardSource.CanPlayJogress (CardSource.cs:549), the field-
                // frame WRITE (SetJogress + CreateNewPermanent zone-append + PlayCardClass, all live), and the
                // jogress COLLAPSE detach leaf Permanent.DiscardEvoRoots(putToTrash:false)
                // (MIG4-DISCARDEVOROOTS-PUTTOTRASH, Permanent.cs:3920 — the bare-detach body is LIVE) plus the
                // private CardObjectController.AddHandCard(cardSource,false) else-branch (RD-P6C1-8 RESOLVED,
                // CardObjectController.cs:398). This factory has no live card caller yet (latent); it is exercised
                // only by the construction smoke test. FRAME ADAPTATION (BT17_095 idiom / CreateNewPermanent doc):
                // the AS-IS PreferredFrame()/frameID/`0<=frameID<fieldCardFrames.Count` slot-capacity dance reduces
                // to the frameless zone-append CreateNewPermanent (materialisation always succeeds); the AS-IS
                // material frame `selectedCardSource.PermanentOfThisCard().PermanentFrame.FrameID` = the returned
                // materialised Permanent's PermanentFrame.FrameID.
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

                async Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCardSource = cardSource;

                    // AS-IS :183-193 material placement (`new Permanent(new List<CardSource>{selectedCardSource})
                    // {IsSuspended=false}` + CreateNewPermanent(perm, frameID)) → the frameless zone-append overload
                    // that returns the materialised Permanent VIEW (see FRAME ADAPTATION above).
                    Permanent materialized = await CardObjectController.CreateNewPermanent(selectedCardSource, isSuspended: false);

                    int[] JogressEvoRootsFrameIDs = { 0, 0 };

                    if (selectedPermanent.TopCard.EqualsCardName(blastDNAConditions[0].Name))
                    {
                        JogressEvoRootsFrameIDs[0] = selectedPermanent.PermanentFrame.FrameID;
                        JogressEvoRootsFrameIDs[1] = materialized.PermanentFrame.FrameID;
                    }
                    else
                    {
                        JogressEvoRootsFrameIDs[0] = materialized.PermanentFrame.FrameID;
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
        }

        return activateClass;
    }
    #endregion

}
