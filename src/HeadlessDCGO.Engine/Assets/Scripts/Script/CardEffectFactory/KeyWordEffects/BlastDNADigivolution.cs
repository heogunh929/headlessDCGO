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
//   * (P6C1) STOP — the jogress-frame play inside SelectPermanentCoroutine. (수리-9 재판정: RD-P6C1-7
//     SelectHandEffect and RD-P6C1-2 CanPlayJogress are now CLOSED/available; the block now stands ONLY on
//     design items RD-P6C1-1 (field-frame SLOT model — Player.fieldCardFrames / PreferredFrame /
//     `new Permanent(List<CardSource>)` ctor / frame-indexed CreateNewPermanent, live STOPs at
//     CardController.cs:2820/2936/3078) + RD-P6C1-8 (its zone statics). docs/audit/rebuild_p6_cluster1_notes.md.
//     The AS-IS remainder is preserved as comments at the STOP.
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

                // (unused until the STOP below lifts — kept as AS-IS state)
                _ = selectedCardSource;
                _ = (Func<CardSource, bool>)CanSelectHandSource;

                // (P6C1) STOP — the AS-IS remainder (:173-254) is the hand-material pick + the jogress-frame
                // play. (수리-9 재판정) TWO of the four original blockers have since LANDED and are struck:
                //   - RD-P6C1-7 SelectHandEffect — CLOSED (Script/SelectHandEffect.cs, 550-line 1:1, R5-A 00552dbf).
                //   - RD-P6C1-2 CardSource.CanPlayJogress — CLOSED (CardSource.cs:549, live).
                // TWO REMAIN and still hard-block this specific jogress-FRAME play:
                //   - RD-P6C1-1 field-frame SLOT model — Player.fieldCardFrames + PreferredFrame() +
                //     `new Permanent(List<CardSource>)` ctor + CardObjectController.CreateNewPermanent(perm,
                //     frameID) are absent (live STOPs remain at CardController.cs:2820/2936/3078); PermanentFrame
                //     READ exists but the writable slot ARRAY does not.
                //   - RD-P6C1-8 zone statics — CardObjectController.AddHandCard(cardSource,false) is private and
                //     the frame-indexed CreateNewPermanent overload is tied to the missing slot model.
                // So the block stands on RD-P6C1-1/-8 (NOT -7/-2). AS-IS body preserved verbatim:
                //
                //     SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();
                //
                //     selectHandEffect.SetUp(
                //         selectPlayer: card.Owner,
                //         canTargetCondition: CanSelectHandSource,
                //         canTargetCondition_ByPreSelecetedList: null,
                //         canEndSelectCondition: null,
                //         maxCount: maxCount,
                //         canNoSelect: false,
                //         canEndNotMax: false,
                //         isShowOpponent: true,
                //         selectCardCoroutine: SelectCardCoroutine,
                //         afterSelectCardCoroutine: null,
                //         mode: SelectHandEffect.Mode.Custom,
                //         cardEffect: activateClass);
                //
                //     selectHandEffect.SetUpCustomMessage("Select 1 Digimon to DNA digivolve.", "The opponent is selecting 1 Digimon to DNA digivolve.");
                //
                //     yield return ContinuousController.instance.StartCoroutine(selectHandEffect.Activate());
                //
                // IEnumerator SelectCardCoroutine(CardSource cardSource)
                // {
                //     selectedCardSource = cardSource;
                //
                //     Permanent playedPermanent;
                //     int frameID = -1;
                //
                //     FieldCardFrame preferredFrame = selectedCardSource.PreferredFrame();
                //
                //     if (preferredFrame != null)
                //     {
                //         frameID = preferredFrame.FrameID;
                //     }
                //
                //     if (0 <= frameID && frameID < card.Owner.fieldCardFrames.Count)
                //     {
                //         playedPermanent = new Permanent(new List<CardSource>() { selectedCardSource }) { IsSuspended = false };
                //
                //         yield return ContinuousController.instance.StartCoroutine(CardObjectController.CreateNewPermanent(playedPermanent, frameID));
                //     }
                //
                //     int[] JogressEvoRootsFrameIDs = { 0, 0 };
                //
                //     if (selectedPermanent.TopCard.EqualsCardName(blastDNAConditions[0].Name))
                //     {
                //         JogressEvoRootsFrameIDs[0] = selectedPermanent.PermanentFrame.FrameID;
                //         JogressEvoRootsFrameIDs[1] = selectedCardSource.PermanentOfThisCard().PermanentFrame.FrameID;
                //     }
                //     else
                //     {
                //         JogressEvoRootsFrameIDs[0] = selectedCardSource.PermanentOfThisCard().PermanentFrame.FrameID;
                //         JogressEvoRootsFrameIDs[1] = selectedPermanent.PermanentFrame.FrameID;
                //     }
                //
                //     if (card.CanPlayJogress(true))
                //     {
                //         PlayCardClass playCard = new PlayCardClass(
                //             cardSources: new List<CardSource>() { card },
                //             hashtable: CardEffectCommons.CardEffectHashtable(activateClass),
                //             payCost: true,
                //             targetPermanent: null,
                //             isTapped: false,
                //             root: SelectCardEffect.Root.Hand,
                //             activateETB: true);
                //
                //         playCard.SetJogress(JogressEvoRootsFrameIDs);
                //
                //         yield return ContinuousController.instance.StartCoroutine(playCard.PlayCard());
                //
                //         foreach (BlastDNACondition DNACondition in blastDNAConditions)
                //         {
                //             DNACondition.Permanents = new List<Permanent>();
                //             DNACondition.CardSources = new List<CardSource>();
                //         }
                //     }
                //     else
                //     {
                //         yield return ContinuousController.instance.StartCoroutine(CardObjectController.AddHandCard(selectedCardSource, false));
                //     }
                // }
                throw new NotSupportedException(
                    "STOP: [Blast DNA Digivolve] jogress-FRAME play — the field-frame SLOT model " +
                    "(Player.fieldCardFrames / PreferredFrame / new Permanent(List) / frame-indexed " +
                    "CreateNewPermanent) and its zone statics are unported (design items RD-P6C1-1/-8). " +
                    "NOTE (수리-9): RD-P6C1-7 SelectHandEffect and RD-P6C1-2 CanPlayJogress are now CLOSED " +
                    "(available), so only -1/-8 remain. " +
                    "A8 구조골 GOAL 1 재판정 (2026-07-22): the sibling [Blast Digivolve] (BlastDigivolveEffect) " +
                    "was RESOLVED this pass because it needs only the READ side of the slot model " +
                    "(CanPlayCardTargetFrame/PermanentFrame — live). This DNA path needs the WRITE side, which " +
                    "is genuinely unportable as-is: PreferredFrame() (CardSource.cs:2290) selects an empty slot " +
                    "by Unity CANVAS GEOMETRY (Frame.transform.parent.localPosition + a hardcoded UI layout " +
                    "order) — headless has no slot array or canvas positions to mirror 1:1, so PreferredFrame + " +
                    "the frame-indexed CreateNewPermanent(perm, frameID) placement + SetJogress(frameIDs) have " +
                    "no substrate translation without inventing a slot geometry (forbidden). STOP stands on " +
                    "RD-P6C1-1(write)/-8. docs/audit/rebuild_p6_cluster1_notes.md.");
            }
        }

        return activateClass;
    }
    #endregion

}
