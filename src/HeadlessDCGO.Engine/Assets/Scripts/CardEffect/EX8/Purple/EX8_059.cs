// Source: DCGO/Assets/Scripts/CardEffect/EX8/Purple/EX8_059.cs (455 lines) — TRUE AS-IS-verbatim port.
// ① AS-IS 앵커 regions:
//    * Alternate Digivolution :13-31  (timing None — AddSelfDigivolutionRequirementStaticEffect, Lv3 [NSo], cost 2)
//    * Shared On Play/When Digivolving :33-38 (CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon)
//    * On Play               :40-213 (AS-IS timing == OnEnterFieldAnyone + CanTriggerOnPlay gate)
//    * When Digivolving      :215-388(AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving gate —
//                                     **미러 방언 재배선**: WhenDigivolving 전용 키로 이동, EX7_058 idiom §2.7)
//    * ESS - When Attacking  :390-450(timing == OnAllyAttack — Once per turn, Draw 1 + trash 1)
//
// ② 셀프-[On Deletion] grant idiom (RD-3A-02 판정 ③-A): the granted "[On Deletion] Trash 1 card" reactor is
//    stored on the TARGET permanent's UntilOpponentTurnEnd duration bucket at timing OnDestroyedAnyone via the
//    AS-IS `CardEffectCommons.AddEffectToPermanent(targetPermanent, EffectDuration.UntilOpponentTurnEnd, card,
//    cardEffect, EffectTiming.OnDestroyedAnyone)` (GiveEffect/GiveEffectToPermanentOrPlayer.cs:11 — the established
//    bucket idiom, EX8_059.cs:149/324 VERBATIM). Survive-own-leave is live: the collect-BEFORE-removal
//    OnDestroyedAnyone window (MatchStateMutationSink.cs:1488-1532) enumerates the dying permanent's own
//    EffectList_Added(OnDestroyedAnyone) bucket while it is still on the field, so the target's own deletion
//    fires its granted reactor. NO invented AddSelfRemovalEffectToPermanent temp is used (that seat is retired).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return StartCoroutine(X)` / `yield return ContinuousController.instance
//      .StartCoroutine(X)` → `await X`.
//    * `card.Owner.HandCards.Count` → `new Player(card.Context, card.Owner).HandCards.Count`;
//      `_topCard.Owner.HandCards.Count` → `new Player(_topCard.Context, _topCard.Owner).HandCards.Count`.
//    * `CanSelectPermanentCondition`(Func<Permanent,bool>) drives HasMatchConditionPermanent,
//      MatchConditionPermanentCount, and SelectPermanentEffect.SetUp's canonical Func<Permanent,bool> overload
//      directly (no id adapter).
//    * inner `permanent == selectedPermanent` → `permanent.InstanceId == selectedPermanent.InstanceId`
//      (Permanent는 per-access view; BT1_112 idiom).
//    * `IsTopCardInTrashOnDeletion(hashtable1)` — AS-IS 1-arg Hashtable bridge 그대로 (OnDeletion.cs:172).
//    * `targetPermanent.TopCard.IsLevel3` → `.HasLevel && .IsLevel(3)` (CardSource.cs:1141/1590).
//    * `new DrawClass(card.Owner, 1, activateClass)` → `new DrawClass(card.Context, card.Owner, 1, activateClass)`.
//    * `GManager.instance.GetComponent<Effects>().CreateDebuffEffect(selectedPermanent)` = UI 연출 — 스트립(§2.6);
//      감싼 `if(!CanNotBeAffected(...))` 가드는 CreateDebuffEffect 전용(상태효과와 무관), 보존할 가드 없음.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Purple;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX8_059 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.IsLevel(3) && targetPermanent.TopCard.EqualsTraits("NSo");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 2,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null));
        }
        #endregion

        #region Shared On Play/When Digivolving
        bool CanSelectPermanentCondition(Permanent permanent)
        {
            return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
        }

        #endregion

        #region On Play
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Give effects to opponent's Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[On Play] By trashing 1 card in your hand, 1 of your opponent's Digimon gains \"[On Deletion] Trash 1 card in your hand.\" until the end of their turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                    return CardEffectCommons.CanTriggerOnPlay(hashtable, card);

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Count >= 1)
                {
                    bool discarded = false;

                    int discardCount = 1;

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: cardSource => true,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: discardCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: AfterSelectCardCoroutine,
                        mode: SelectHandEffect.Mode.Discard,
                        cardEffect: activateClass);

                    await selectHandEffect.Activate();

                    async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                    {
                        if (cardSources.Count == 1)
                        {
                            discarded = true;

                            await Task.CompletedTask;
                        }
                    }

                    if (discarded)
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                        {
                            Permanent selectedPermanent = null;

                            int maxCount = Math.Min(1,
                                CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                            SelectPermanentEffect selectPermanentEffect =
                                GManager.instance.GetComponent<SelectPermanentEffect>();

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

                            selectPermanentEffect.SetUpCustomMessage(
                                "Select 1 Digimon that will get [On Deletion] Trash 1 card in your hand.",
                                "The opponent is selecting 1 Digimon that will get [On Deletion] Trash 1 card in your hand.");

                            await selectPermanentEffect.Activate();

                            Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                if (permanent != null)
                                    selectedPermanent = permanent;

                                return Task.CompletedTask;
                            }

                            if (selectedPermanent != null)
                            {
                                CardSource _topCard = selectedPermanent.TopCard;

                                ActivateClass activateClass1 = new ActivateClass();
                                activateClass1.SetUpICardEffect("Trash 1 card", CanUseCondition1, selectedPermanent.TopCard);
                                activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, EffectDiscription1());
                                activateClass1.SetEffectSourcePermanent(selectedPermanent);
                                CardEffectCommons.AddEffectToPermanent(targetPermanent: selectedPermanent, effectDuration: EffectDuration.UntilOpponentTurnEnd, card: card, cardEffect: activateClass1, timing: EffectTiming.OnDestroyedAnyone);

                                // AS-IS `if(!CanNotBeAffected) CreateDebuffEffect(selectedPermanent)` = UI 연출 — 스트립(§2.6).

                                string EffectDiscription1()
                                {
                                    return "[On Deletion] Trash 1 card in your hand.";
                                }

                                bool CanUseCondition1(Hashtable hashtable1)
                                {
                                    if (CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent))
                                    {
                                        if (CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable1, (permanent) => permanent.InstanceId == selectedPermanent.InstanceId))
                                        {
                                            if (!selectedPermanent.TopCard.CanNotBeAffected(activateClass))
                                            {
                                                return true;
                                            }
                                        }
                                    }

                                    return false;
                                }

                                bool CanActivateCondition1(Hashtable hashtable1)
                                {
                                    if (CardEffectCommons.IsTopCardInTrashOnDeletion(hashtable1))
                                    {
                                        return new Player(_topCard.Context, _topCard.Owner).HandCards.Count > 0;
                                    }

                                    return false;
                                }

                                async Task ActivateCoroutine1(Hashtable _hashtable1)
                                {
                                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                                    selectHandEffect.SetUp(
                                        selectPlayer: _topCard.Owner,
                                        canTargetCondition: (cardSource) => true,
                                        canTargetCondition_ByPreSelecetedList: null,
                                        canEndSelectCondition: null,
                                        maxCount: 1,
                                        canNoSelect: false,
                                        canEndNotMax: false,
                                        isShowOpponent: true,
                                        selectCardCoroutine: null,
                                        afterSelectCardCoroutine: null,
                                        mode: SelectHandEffect.Mode.Discard,
                                        cardEffect: activateClass);

                                    await selectHandEffect.Activate();
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region When Digivolving
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Give effects to opponent's Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[When Digivolving] By trashing 1 card in your hand, 1 of your opponent's Digimon gains \"[On Deletion] Trash 1 card in your hand.\" until the end of their turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                    return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Count >= 1)
                {
                    bool discarded = false;

                    int discardCount = 1;

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: cardSource => true,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: discardCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: AfterSelectCardCoroutine,
                        mode: SelectHandEffect.Mode.Discard,
                        cardEffect: activateClass);

                    await selectHandEffect.Activate();

                    async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                    {
                        if (cardSources.Count == 1)
                        {
                            discarded = true;

                            await Task.CompletedTask;
                        }
                    }

                    if (discarded)
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                        {
                            Permanent selectedPermanent = null;

                            int maxCount = Math.Min(1,
                                CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                            SelectPermanentEffect selectPermanentEffect =
                                GManager.instance.GetComponent<SelectPermanentEffect>();

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

                            selectPermanentEffect.SetUpCustomMessage(
                                "Select 1 Digimon that will get [On Deletion] Trash 1 card in your hand.",
                                "The opponent is selecting 1 Digimon that will get [On Deletion] Trash 1 card in your hand.");

                            await selectPermanentEffect.Activate();

                            Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                if (permanent != null)
                                    selectedPermanent = permanent;

                                return Task.CompletedTask;
                            }

                            if (selectedPermanent != null)
                            {
                                CardSource _topCard = selectedPermanent.TopCard;

                                ActivateClass activateClass1 = new ActivateClass();
                                activateClass1.SetUpICardEffect("Trash 1 card", CanUseCondition1, selectedPermanent.TopCard);
                                activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, EffectDiscription1());
                                activateClass1.SetEffectSourcePermanent(selectedPermanent);
                                CardEffectCommons.AddEffectToPermanent(targetPermanent: selectedPermanent, effectDuration: EffectDuration.UntilOpponentTurnEnd, card: card, cardEffect: activateClass1, timing: EffectTiming.OnDestroyedAnyone);

                                // AS-IS `if(!CanNotBeAffected) CreateDebuffEffect(selectedPermanent)` = UI 연출 — 스트립(§2.6).

                                string EffectDiscription1()
                                {
                                    return "[On Deletion] Trash 1 card in your hand.";
                                }

                                bool CanUseCondition1(Hashtable hashtable1)
                                {
                                    if (CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent))
                                    {
                                        if (CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable1, (permanent) => permanent.InstanceId == selectedPermanent.InstanceId))
                                        {
                                            if (!selectedPermanent.TopCard.CanNotBeAffected(activateClass))
                                            {
                                                return true;
                                            }
                                        }
                                    }

                                    return false;
                                }

                                bool CanActivateCondition1(Hashtable hashtable1)
                                {
                                    if (CardEffectCommons.IsTopCardInTrashOnDeletion(hashtable1))
                                    {
                                        return new Player(_topCard.Context, _topCard.Owner).HandCards.Count > 0;
                                    }

                                    return false;
                                }

                                async Task ActivateCoroutine1(Hashtable _hashtable1)
                                {
                                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                                    selectHandEffect.SetUp(
                                        selectPlayer: _topCard.Owner,
                                        canTargetCondition: (cardSource) => true,
                                        canTargetCondition_ByPreSelecetedList: null,
                                        canEndSelectCondition: null,
                                        maxCount: 1,
                                        canNoSelect: false,
                                        canEndNotMax: false,
                                        isShowOpponent: true,
                                        selectCardCoroutine: null,
                                        afterSelectCardCoroutine: null,
                                        mode: SelectHandEffect.Mode.Discard,
                                        cardEffect: activateClass);

                                    await selectHandEffect.Activate();
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region ESS - When Attacking
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1 and trash 1 card from hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("Draw_EX8_059");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] [Once Per Turn] <Draw 1> and trash 1 card in your hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();

                if (new Player(card.Context, card.Owner).HandCards.Count >= 1)
                {
                    int discardCount = 1;

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: (cardSource) => true,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: discardCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Discard,
                        cardEffect: activateClass);

                    await selectHandEffect.Activate();
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
