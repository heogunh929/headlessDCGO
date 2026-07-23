// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect mirroring EX8_074's "When this card
// would be played, by suspending 2 Digimon, reduce the play cost by 4" IN ISOLATION so tests/G9-006 (pre-payment
// window E2E) and tests/G9-007 (availability + mandatory/optional coupling) can exercise the real play action.
// (uniform-사멸 flip) Re-written from the retired invented `ActivatedEffect` + `SuspendCostReductionEffect` body to
// the literal AS-IS inline shape of EX8_074 (EX8_074.cs regions #1/#2, 1:1 adapted):
//   * #1 [When Would be Played] (EffectTiming.BeforePayCost) — ActivateClass whose coroutine runs the
//     SelectPermanentEffect(Mode.Custom) suspend-2 select (canNoSelect = affordable-full-cost coupling, AS-IS
//     EX8_074.cs:122-129) and, on 2 selected, SuspendPermanentsClass.Tap() + a ChangeCostClass registered on the
//     owner's UntilCalculateFixedCostEffect bucket (pay-time -4).
//   * #2 [None] availability half — a hidden isCheckAvailability ChangeCostClass (SetNotShowUI) so the LEGAL-move
//     cost check reads the reduced cost through the AS-IS GetPayingCostWithBaseCost fold (checkAvailability:true)
//     — replaces the invented PlayCardAction.BeforePayCostAvailabilityReduction projection.
// No real card has the number "TfxBeforePayCost", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxBeforePayCost : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region When Would be Played

        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Suspend 2 Digimon to get Play Cost -4", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetHashString("PlayCost-4_TfxBeforePayCost");
            cardEffects.Add(activateClass);

            activateClass.SetIsDigimonEffect(true);

            string EffectDescription()
            {
                return "When this card would be played, by suspending 2 Digimon, reduce the play cost by 4.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenPermanentWouldPlay(hashtable, CardCondition);
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource == card && CardEffectCommons.IsExistOnHand(cardSource);
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent) &&
                       permanent != null &&
                       permanent.TopCard != null &&
                       !permanent.TopCard.CanNotBeAffected(activateClass) &&
                       !permanent.IsSuspended && permanent.CanSuspend;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition) >= 2;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                bool canNoSelect = true;
                CardSource cardFromHashtable = CardEffectCommons.GetCardFromHashtable(hashtable);

                if (cardFromHashtable != null && cardFromHashtable.PayingCost(SelectCardEffect.Root.Hand, null, checkAvailability: false) >
                    new Player(card.Context, cardFromHashtable.Owner).MaxMemoryCost)
                {
                    canNoSelect = false;
                }

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 2,
                    canNoSelect: canNoSelect,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 2 Digimon to suspend.",
                    "The opponent is selecting 2 Digimon to suspend.");

                await selectPermanentEffect.Activate();

                async Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
                {
                    if (permanents.Count == 2)
                    {
                        foreach (var selectedPermanent in permanents)
                        {
                            await new SuspendPermanentsClass(new List<Permanent>() { selectedPermanent },
                                activateClass, isBlock: false).Tap();
                        }

                        ChangeCostClass changeCostClass = new ChangeCostClass();
                        changeCostClass.SetUpICardEffect("Play Cost -4", CanUseCondition1, card);
                        changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition,
                            rootCondition: RootCondition, isUpDown: IsUpDown, isCheckAvailability: () => false,
                            isChangePayingCost: () => true);
                        new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add(_ => changeCostClass);

                        await CardEffectCommons.ShowReducedCost(hashtable);

                        bool CanUseCondition1(Hashtable hashtable1)
                        {
                            return true;
                        }

                        int ChangeCost(CardSource cardSource, int cost, SelectCardEffect.Root root,
                            List<Permanent> targetPermanents)
                        {
                            if (CardSourceCondition(cardSource) &&
                                RootCondition(root) &&
                                PermanentsCondition(targetPermanents))
                            {
                                cost -= 4;
                            }

                            return cost;
                        }

                        bool PermanentsCondition(List<Permanent> targetPermanents)
                        {
                            return targetPermanents == null || targetPermanents.Count(targetPermanent => targetPermanent != null) == 0;
                        }

                        bool CardSourceCondition(CardSource cardSource)
                        {
                            return cardSource == card;
                        }

                        bool RootCondition(SelectCardEffect.Root root)
                        {
                            return true;
                        }

                        bool IsUpDown()
                        {
                            return true;
                        }
                    }
                }
            }
        }

        #endregion

        #region Reduce Play Cost - Not Shown

        if (timing == EffectTiming.None)
        {
            ChangeCostClass changeCostClass = new ChangeCostClass();
            changeCostClass.SetUpICardEffect("Play Cost -4", CanUseCondition1, card);
            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition,
                rootCondition: RootCondition, isUpDown: IsUpDown, isCheckAvailability: () => true,
                isChangePayingCost: () => true);

            changeCostClass.SetNotShowUI(true);
            cardEffects.Add(changeCostClass);

            bool CanUseCondition1(Hashtable hashtable1)
            {
                return CardEffectCommons.MatchConditionPermanentCount(card, PermanentCondition) >= 2;
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent) &&
                       !permanent.IsSuspended && permanent.CanSuspend;
            }

            int ChangeCost(CardSource cardSource, int cost, SelectCardEffect.Root root,
                List<Permanent> targetPermanents)
            {
                if (CardSourceCondition(cardSource) &&
                    RootCondition(root) &&
                    PermanentsCondition(targetPermanents))
                {
                    cost -= 4;
                }

                return cost;
            }

            bool PermanentsCondition(List<Permanent> targetPermanents)
            {
                return targetPermanents == null ||
                       targetPermanents.Count(targetPermanent => CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(targetPermanent) &&
                                                                 targetPermanent != null &&
                                                                 targetPermanent.TopCard != null &&
                                                                 !targetPermanent.TopCard.CanNotBeAffected(changeCostClass) &&
                                                                 !targetPermanent.IsSuspended && targetPermanent.CanSuspend) < 2;
            }

            bool CardSourceCondition(CardSource cardSource)
            {
                return cardSource == card;
            }

            bool RootCondition(SelectCardEffect.Root root)
            {
                return true;
            }

            bool IsUpDown()
            {
                return true;
            }
        }

        #endregion

        return cardEffects;
    }
}
