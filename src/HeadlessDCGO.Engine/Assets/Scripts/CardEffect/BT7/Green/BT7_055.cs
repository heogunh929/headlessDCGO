// Source: DCGO/Assets/Scripts/CardEffect/BT7/Green/BT7_055.cs (1:1 mirror).
//
// S6 롱테일 트랜치 — 감사 시절 ⚠STOP-예상(AddSkillClass)이었으나 표면 실존 확인: AddSkillClass 는 미러에
// CardEffects/AddSkillClass.cs로 1:1 존재(SetUpAddSkillClass(cardSourceCondition, getEffects) 시그니처 동일).
//   * [When Digivolving] — 상대 디지몬 1체 서스펜드 + 상대 서스펜드 디지몬 수만큼 메모리 획득.
//   * [None] — AddSkillClass: 상대 턴에 "손패 1장 트래시하지 않으면 이 디지몬은 언서스펜드 불가"를 상대
//     디지몬(자신의 TopCard == 이 카드)에 WhenUntapAnyone 타이밍으로 부여(SetRootCardEffect로 addSkillClass에
//     귀속) — CardEffectCommons.GainCanNotUnsuspend(EffectDuration.UntilNextUntap)로 실제 언서스펜드 봉쇄.
//
// 치환(substrate translations only): IEnumerator→async Task, `ContinuousController.instance.StartCoroutine(X)`
// 및 bare `StartCoroutine(X)`→`await X`; `card.Owner.Enemy.GetBattleAreaDigimons()` → 확장 메서드
// `card.Owner.GetBattleAreaDigimons()`은 소유자 직결(§2.2 예외)이나 상대는 `new Player(...).Enemy!.PlayerId.
// GetBattleAreaDigimons()`; `cardSource.Owner.GetBattleAreaDigimons()`도 동형; `cardSource.PermanentOfThisCard()`
// → `ICardEffect.ResolvePermanentOfThisCard(cardSource)`; SelectPermanentEffect.canTargetCondition은 정본
// Func<Permanent,bool> — Permanent 술어 직결(id 어댑터 없음).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT7.Green;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT7_055 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region [When Digivolving] Suspend 1 Digimon and gain Memory
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Suspend 1 Digimon and gain Memory", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription() =>
                "[When Digivolving] Suspend 1 of your opponent's Digimon. Then, gain 1 memory for each of your opponent's suspended Digimon.";

            bool CanSelectPermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }

                    if (card.Owner.CanAddMemory(activateClass))
                    {
                        int count = new Player(card.Context, card.Owner).Enemy!.PlayerId.GetBattleAreaDigimons().Count(p => p.IsSuspended);

                        if (count >= 1)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Tap,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }

                int count = new Player(card.Context, card.Owner).Enemy!.PlayerId.GetBattleAreaDigimons().Count(p => p.IsSuspended);

                if (count >= 1)
                {
                    await card.Owner.AddMemory(count, activateClass);
                }
            }
        }
        #endregion

        #region [None] AddSkill — opponent's Digimon can't unsuspend unless opponent trashes a hand card
        if (timing == EffectTiming.None)
        {
            AddSkillClass addSkillClass = new AddSkillClass();
            addSkillClass.SetUpICardEffect("Opponent's Digimon can't unsuspend unless opponent trash a card from hand", CanUseCondition, card);
            addSkillClass.SetUpAddSkillClass(cardSourceCondition: CardSourceCondition, getEffects: GetEffects);

            cardEffects.Add(addSkillClass);

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOpponentTurn(card);

            bool PermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
                && !permanent.TopCard.CanNotBeAffected(addSkillClass);

            bool CardSourceCondition(CardSource cardSource) =>
                CardEffectCommons.IsExistOnBattleArea(cardSource)
                && cardSource == ICardEffect.ResolvePermanentOfThisCard(cardSource).TopCard
                && PermanentCondition(ICardEffect.ResolvePermanentOfThisCard(cardSource));

            List<ICardEffect> GetEffects(CardSource cardSource, List<ICardEffect> innerCardEffects, EffectTiming _timing)
            {
                if (_timing == EffectTiming.WhenUntapAnyone)
                {
                    ActivateClass activateClass1 = new ActivateClass();
                    activateClass1.SetUpICardEffect("Trash 1 card from hand or this Digimon can't unsuspend", CanUseCondition1, cardSource);
                    activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, EffectDiscription());
                    activateClass1.SetRootCardEffect(addSkillClass);
                    innerCardEffects.Add(activateClass1);

                    string EffectDiscription() => "[Your Turn] You must trash 1 card in your hand to unsuspend this Digimon.";

                    bool CanUseCondition1(Hashtable hashtable) =>
                        CardEffectCommons.IsOwnerTurn(cardSource) && CardSourceCondition(cardSource);

                    bool CanActivateCondition1(Hashtable hashtable) =>
                        CardSourceCondition(cardSource) && ICardEffect.ResolvePermanentOfThisCard(cardSource).IsSuspended;

                    async Task ActivateCoroutine1(Hashtable _hashtable)
                    {
                        if (CardSourceCondition(cardSource))
                        {
                            Permanent selfPermanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);
                            if (cardSource.Owner.GetBattleAreaDigimons().Contains(selfPermanent))
                            {
                                bool discarded = false;

                                if (new Player(cardSource.Context, cardSource.Owner).HandCards.Count >= 1)
                                {
                                    int discardCount = 1;

                                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                                    selectHandEffect.SetUp(
                                        selectPlayer: cardSource.Owner,
                                        canTargetCondition: (_) => true,
                                        canTargetCondition_ByPreSelecetedList: null,
                                        canEndSelectCondition: null,
                                        maxCount: discardCount,
                                        canNoSelect: true,
                                        canEndNotMax: false,
                                        isShowOpponent: true,
                                        selectCardCoroutine: null,
                                        afterSelectCardCoroutine: AfterSelectCardCoroutine,
                                        mode: SelectHandEffect.Mode.Discard,
                                        cardEffect: activateClass1);

                                    await selectHandEffect.Activate();

                                    async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                                    {
                                        if (cardSources.Count >= 1)
                                        {
                                            discarded = true;
                                        }

                                        await Task.CompletedTask;
                                    }
                                }

                                if (!discarded)
                                {
                                    Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);

                                    if (selectedPermanent != null)
                                    {
                                        string effectName = "This Digimon can't unsuspend.";

                                        bool CanUseCondition2() =>
                                            CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent);

                                        CardEffectCommons.GainCanNotUnsuspend(
                                            targetPermanent: selectedPermanent,
                                            effectDuration: EffectDuration.UntilNextUntap,
                                            sourceCard: cardSource,
                                            condition: CanUseCondition2,
                                            effectName: effectName);
                                    }
                                }
                            }
                        }
                    }
                }

                return innerCardEffects;
            }
        }
        #endregion

        return cardEffects;
    }
}
