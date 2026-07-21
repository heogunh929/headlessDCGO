// Source: Assets/Scripts/CardEffect/EX8/Green/EX8_074.cs
// (RD-R6-07 RESOLVED — re-port) 1:1 inline mirror of the original EX8_074, region for region, replacing the
// last uniform old-model representation (ActivatedEffect + SuspendCostReductionEffect / ActivatedSelectEffect /
// ReuseWhenDigivolvingEffect). The two documented blockers are both closed at HEAD:
//   (1) R2-C cost pipeline — landed (r2c_cost_pipeline_investigation §F/§G): pay entry is
//       CardSource.GetPayingCostWithBaseCost, the player bucket (UntilCalculateFixedCostEffect) is written by
//       inline ports (EX4_062 / BT25_004 idiom) and read live, and the availability-only IChangeCostEffect
//       fold honours IsCheckAvailability (CardSource.cs GetChangedPayingCost) — so regions #1 AND #2 run
//       through the AS-IS pipeline verbatim.
//   (2) R5 Select stub — SelectPermanentEffect / SelectCardEffect `SetUp(...).Activate()` are the full
//       bridge-W4 AS-IS mirrors (BT9_021 / BT22_040 idiom), and inline ActivateClass resolves live through
//       ActivatedEffectResolver's ActivateICardEffect case (P6 stage A).
//
// Region map (original -> mirror timing key, trigger-wiring-porting-rules):
//   #1 [When Would be Played] suspend 2 -> Play Cost -4   -> BeforePayCost (AS-IS key; gate =
//        CanTriggerWhenPermanentWouldPlay — the WouldEnterFieldHashtable isEvolution split IS the play/digivolve
//        root split, so no invented IsPayCostRoot gate)
//   #2 [None] isCheckAvailability "-4 not shown"          -> None (RESTORED AS-IS availability-only
//        ChangeCostClass; folded by GetPayingCostWithBaseCost(checkAvailability: true) — replaces the invented
//        PlayCardAction.BeforePayCostAvailabilityReduction projection for this card)
//   #3 [All Turns] <Alliance>                             -> OnAllyAttack / AllianceSelfEffect (unchanged)
//   #4 [End of Your Turn] <Vortex>                        -> OnEndTurn / VortexSelfEffect (unchanged)
//   #5 [When Digivolving] suspend 1 -> delete 1 opp <=8000(+3000/other suspended)
//        -> WhenDigivolving dedicated key (mirror dialect rule 3; AS-IS OnEnterFieldAnyone +
//        CanTriggerWhenDigivolving gate kept 1:1 — BT22_040 exemplar)
//   #6 [All Turns] (Once Per Turn) on Digimon played: activate 1 of this Digimon's [When Digivolving] effects
//        -> OnEnterFieldAnyone + CanTriggerOnPermanentPlay (BT1_053 broadcast-reactor idiom; the reuse
//        coroutine is the BT22_040 [All Turns] template with the EffectList query remapped to the
//        WhenDigivolving key where #5 now lives). Delivery = the standard play window (AS-IS
//        CardController.cs:1691 StackSkillInfos broadcast == the C2 window pump), NOT the invented
//        OnPlayReactivation driver (which no-ops for this card once ReuseWhenDigivolvingEffect is gone).
//
// Substrate translations only (BT9_021 / BT22_040 / EX4_062 idioms):
//   * IEnumerator -> async Task; `ContinuousController.instance.StartCoroutine(X)` -> `await X`;
//     lone `yield return null` -> Task.CompletedTask.
//   * `card.Owner.<Player member>` -> `new Player(card.Context, card.Owner).<member>` (mirror CardSource.Owner
//     is the HeadlessPlayerId).
//   * AS-IS `Func<Permanent,bool>` SetUp target predicates -> the entity-id adapter (`PermanentOf(id)`
//     reconstruction, BT9_021 idiom); AS-IS Unity truthiness `cardFromHashtable` / `permanent.TopCard`
//     -> `!= null`.
//   * `new SuspendPermanentsClass(list, CardEffectCommons.CardEffectHashtable(activateClass)).Tap()` (AS-IS
//     hashtable ctor) -> `new SuspendPermanentsClass(list, activateClass, isBlock: false).Tap()` (EX4_062 idiom).
//   * `permanent != card.PermanentOfThisCard()` -> `permanent.InstanceId !=
//     card.PermanentOfThisCard().TopInstanceId` (BT2_073 / BT22_040 idiom — the mirror accessor returns a
//     PermanentView).
//   * UI-only strips with AS-IS anchors: `ContinuousController.instance.PlaySE(BuffSE)` under the
//     `card.Owner.CanReduceCost(null, card)` guard (EX8_074.cs:97-100 — the guard wrapped ONLY the SE, so both
//     are stripped); ShowReducedCost stays as the established no-op mirror call.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Green;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX8_074 : CEntity_Effect
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
            activateClass.SetHashString("PlayCost-4_EX8_074");
            cardEffects.Add(activateClass);

            activateClass.SetIsDigimonEffect(true);

            string EffectDescription()
            {
                return
                    "When this card would be played, by suspending 2 Digimon, reduce the play cost by 4.";
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

            Permanent PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool CanSelectPermanentById(HeadlessEntityId id)
            {
                Permanent permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition(permanent);
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
                    canTargetCondition: CanSelectPermanentById,
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

                        // AS-IS EX8_074.cs:97-100 — `if (card.Owner.CanReduceCost(null, card))
                        // { ContinuousController.instance.PlaySE(...BuffSE); }`: the guard wraps ONLY the
                        // sound effect (UI), so guard + SE are stripped together.

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

        #region Alliance

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.AllianceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        #endregion

        #region Vortex

        if (timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(CardEffectFactory.VortexSelfEffect(isInheritedEffect: false, card: card,
                condition: null));
        }

        #endregion

        #region When Digivolving

        // Mirror dialect rule 3: [When Digivolving] keys on the dedicated WhenDigivolving timing (AS-IS
        // OnEnterFieldAnyone + CanTriggerWhenDigivolving gate — the gate is kept 1:1, BT22_040 exemplar).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Suspend 1 Digimon then Delete 1 Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[When Digivolving] You may suspend 1 Digimon. Then, you may delete 1 of your opponent's 8000 DP or lower Digimon. For each other suspended Digimon, add 3000 to this DP deletion effect's maximum.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanSelectSuspendPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent);
            }

            int DeletionMaxDP()
            {
                return 8000 + (3000 * CardEffectCommons.MatchConditionPermanentCount(card,
                    (Func<Permanent, bool>)(permanent => permanent.IsDigimon && permanent.IsSuspended &&
                        permanent.InstanceId != card.PermanentOfThisCard().TopInstanceId)));
            }

            bool CanSelectDeletePermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) &&
                       permanent.DP <= new Player(card.Context, card.Owner).MaxDP_DeleteEffect(DeletionMaxDP(), activateClass);
            }

            Permanent PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool CanSelectSuspendPermanentById(HeadlessEntityId id)
            {
                Permanent permanent = PermanentOf(id);
                return permanent is not null && CanSelectSuspendPermanentCondition(permanent);
            }

            bool CanSelectDeletePermanentById(HeadlessEntityId id)
            {
                Permanent permanent = PermanentOf(id);
                return permanent is not null && CanSelectDeletePermanentCondition(permanent);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectSuspendPermanentCondition))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectSuspendPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Tap,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage(
                        "Select 1 Digimon that will suspend.",
                        "The opponent is selecting 1 Digimon that will suspend.");

                    await selectPermanentEffect.Activate();
                }

                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectDeletePermanentCondition))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectDeletePermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        #endregion

        #region All Turns

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Activate 1 of this Digimon's [When Digivolving] effects", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetHashString("PlayActivate_EX8_074");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[All Turns] (Once Per Turn) When Digimon are played, you may activate 1 of this Digimon's [When Digivolving] effects.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.CanTriggerOnPermanentPlay(hashtable, PermanentCondition);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                // BT22_040 ③-remap: AS-IS queries EffectList(OnEnterFieldAnyone) — the mirror keys
                // [When Digivolving] under the dedicated WhenDigivolving timing (#5 above), so the same
                // effects are read from that key. The IsWhenDigivolving description filter stays 1:1.
                List<ICardEffect> candidateEffects = ICardEffect.ResolvePermanentOfThisCard(card).EffectList(EffectTiming.WhenDigivolving)
                    .Clone()
                    .Filter(cardEffect =>
                        cardEffect is ActivateICardEffect && !cardEffect.IsSecurityEffect && cardEffect.IsWhenDigivolving);

                if (candidateEffects.Count >= 1)
                {
                    ICardEffect selectedEffect = null;

                    if (candidateEffects.Count == 1)
                    {
                        selectedEffect = candidateEffects[0];
                    }

                    else
                    {
                        List<SkillInfo> skillInfos = candidateEffects
                            .Map(cardEffect => new SkillInfo(cardEffect, null, EffectTiming.None));

                        List<CardSource> cardSources = candidateEffects
                            .Map(cardEffect => cardEffect.EffectSourceCard);

                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: _ => true,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: null,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 effect to activate.",
                            maxCount: 1,
                            canEndNotMax: false,
                            isShowOpponent: false,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Custom,
                            customRootCardList: cardSources,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetNotShowCard();
                        selectCardEffect.SetUpSkillInfos(skillInfos);
                        selectCardEffect.SetUpAfterSelectIndexCoroutine(AfterSelectIndexCoroutine);

                        await selectCardEffect.Activate();

                        Task AfterSelectIndexCoroutine(List<int> selectedIndexes)
                        {
                            if (selectedIndexes.Count == 1)
                            {
                                selectedEffect = candidateEffects[selectedIndexes[0]];
                            }

                            return Task.CompletedTask;
                        }
                    }

                    if (selectedEffect != null)
                    {
                        if (!selectedEffect.IsDisabled)
                        {
                            Hashtable effectHashtable =
                            CardEffectCommons.WhenDigivolvingCheckHashtableOfCard(selectedEffect.EffectSourceCard);

                            selectedEffect.SetIsDigimonEffect(true);

                            await ((ActivateICardEffect)selectedEffect).Activate_Optional_Effect_Execute(effectHashtable);
                        }
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
