// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 정본 카드 — MirageGaogamon: Burst Mode (BT13_033, Digimon / Blue / Lv.6)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT13/Blue/BT13_033.cs (3 arms)
//    * [None] Burst Digivolution :14-89  (AddBurstDigivolutionConditionClass — Thomas H. Norstein 바운스+MirageGaogamon 위)
//    * [When Digivolving]        :91-172 (AS-IS OnEnterFieldAnyone + CanTriggerWhenDigivolving) — 상대 Digimon 1체 바운스 + 상대 손패/4 만큼 메모리
//    * [When Attacking]          :174-303 (OnAllyAttack) — 상대 손패 9장↑ → 8장 남기고 블라인드로 덱밑, unsuspend self
//
// ② 프리미티브 매핑: P:AddBurstDigivolutionConditionClass, X:BurstDigivolution, T:WhenDigivolving, T:OnAllyAttack,
//    E:SelectPermanentEffect(Bounce), E:SelectCardEffect(Root.Custom blind-pick), P:IUnsuspendPermanents,
//    IZoneMover.ShuffleHandAsync (신규 hand-shuffle zone op — AS-IS RandomUtility.ShuffledDeckCards(HandCards)).
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언; AS-IS는 OnEnterFieldAnyone에 두지만
//      미러 DigivolveAction은 WhenDigivolving만 해소, BT17_026 관례). CanTriggerWhenDigivolving 게이트 유지.
//    * [When Attacking] self-스코프 → CanTriggerOnAttack(hashtable, card) triggerGate 필수(BT17_026 관례).
//    * [None] Burst 조건 등재 → AddBurstDigivolutionConditionClass 그대로(BT25_104 idiom).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask.
//    * `card.Owner.Enemy`(Player) → `new Player(card.Context, card.Owner).Enemy!`(EX8_028 idiom).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`.
//    * `TopCard.CardNames.Contains(name)` → `TopCard.EqualsCardName(name)`(두 표기 변형 유지 — BT17_026 idiom).
//    * `TopCard.Owner.GetFieldPermanents()` → `new Player(card.Context, TopCard.Owner).GetFieldPermanents()`.
//    * `card.Owner.CanAddMemory/AddMemory(...)` → HeadlessPlayerId.CanAddMemory/AddMemory 확장(BT2_010 idiom).
//    * SelectPermanentEffect canTargetCondition = id-형 → PermanentOf(id) 어댑터(BT17_026 idiom; 술어 뭉갬 금지).
//    * `card.Owner.Enemy.HandCards = RandomUtility.ShuffledDeckCards(...)` → `IZoneMover.ShuffleHandAsync(enemyId)`
//      (신규 hand-shuffle op — 시드된 RandomUtility 미러로 손패 존 in-place 셔플; Player.HandCards는 live view라
//      존 재정렬이 곧 AS-IS in-place 재대입). 셔플 후 HandCards를 재-read하여 블라인드-픽의 customRootCardList로 전달.
//    * `CardObjectController.AddLibraryBottomCards(cardSources)` → per-card `IZoneMover.MoveToDeckBottomAsync(
//      enemyId, cardSource.InstanceId)`(EX8_072 idiom) — 상대 손패 카드를 상대 덱 밑으로.
//    * `new IUnsuspendPermanents(list, activateClass).Unsuspend()`(EX8_028 idiom).
//    * AS-IS `card.Owner.isYou` 분기의 SetReverse/SetFace/ShowCardEffect/SetNotAddLog = UI 연출 —
//      미러 확립 관례상 스트립(로컬 관점 뒤집기/연출; 게임 상태 무영향). SetNotShowCard/SetUseFaceDown/
//      SetUpCustomMessage는 블라인드-픽 표면이므로 유지.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT13.Blue;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT13_033 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        #region Burst Digivolution
        if (timing == EffectTiming.None)
        {
            AddBurstDigivolutionConditionClass addBurstDigivolutionConditionClass = new AddBurstDigivolutionConditionClass();
            addBurstDigivolutionConditionClass.SetUpICardEffect($"Burst Digivolution", CanUseCondition, card);
            addBurstDigivolutionConditionClass.SetUpAddBurstDigivolutionConditionClass(getBurstDigivolutionCondition: GetBurstDigivolution);
            addBurstDigivolutionConditionClass.SetNotShowUI(true);
            cardEffects.Add(addBurstDigivolutionConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            BurstDigivolutionCondition? GetBurstDigivolution(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    bool TamerCondition(Permanent permanent)
                    {
                        // AS-IS :30-53: all terms preserved 1:1. `CardNames.Contains(name)` → EqualsCardName
                        // (두 표기 변형 유지). `!CannotReturnToHand(null)`는 Permanent-레벨 aggregate 스캔.
                        return CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card)
                            && !permanent.CannotReturnToHand(null)
                            && (permanent.TopCard.EqualsCardName("Thomas H. Norstein")
                                || permanent.TopCard.EqualsCardName("ThomasH.Norstein"));
                    }

                    bool DigimonCondition(Permanent permanent)
                    {
                        return permanent != null
                            && permanent.TopCard != null
                            && permanent.TopCard.Owner == card.Owner
                            && new Player(card.Context, permanent.TopCard.Owner).GetFieldPermanents().Contains(permanent)
                            && !card.CanNotEvolve(permanent)
                            && permanent.TopCard.EqualsCardName("MirageGaogamon");
                    }

                    BurstDigivolutionCondition burstDigivolutionCondition = new BurstDigivolutionCondition(
                        tamerCondition: TamerCondition,
                        selectTamerMessage: "1 [Thomas H. Norstein]",
                        digimonCondition: DigimonCondition,
                        selectDigimonMessage: "1 [MirageGaogamon]",
                        cost: 0);

                    return burstDigivolutionCondition;
                }

                return null;
            }
        }
        #endregion

        #region When Digivolving
        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:91)이지만 미러 방언은 WhenDigivolving 전용 키(BT17_026 관례).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 Digimon to hand and gain Memory", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Return 1 of your opponent's Digimon to the hand. Then, gain 1 memory for every 4 cards in your opponent's hand.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanSelectPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition(permanent);
            }

            int count()
            {
                return new Player(card.Context, card.Owner).Enemy!.HandCards.Count / 4;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).Enemy!.GetBattleAreaPermanents().Count(CanSelectPermanentCondition) >= 1)
                    {
                        return true;
                    }

                    if (card.Owner.CanAddMemory(activateClass))
                    {
                        if (count() >= 1)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (new Player(card.Context, card.Owner).Enemy!.GetBattleAreaPermanents().Count(CanSelectPermanentCondition) >= 1)
                {
                    int maxCount = Math.Min(1, new Player(card.Context, card.Owner).Enemy!.GetBattleAreaPermanents().Count(CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Bounce,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }

                await card.Owner.AddMemory(count(), activateClass);
            }
        }
        #endregion

        #region When Attacking
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return opponent's hand cards to the bottom of deck to unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] If your opponent has 9 or more cards in their hand, by choosing cards in your opponent's hand without looking and returning them to the bottom of the deck so that 8 remain, unsuspend this Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).Enemy!.HandCards.Count >= 9)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool returned = false;

                Player enemy = new Player(card.Context, card.Owner).Enemy!;

                if (enemy.HandCards.Count >= 9)
                {
                    int maxCount = enemy.HandCards.Count - 8;

                    if (maxCount >= 1)
                    {
                        // AS-IS :206-212 `if (card.Owner.isYou) foreach SetReverse()` = UI(로컬 관점 뒤집기) — 스트립.
                        // AS-IS :214 `card.Owner.Enemy.HandCards = RandomUtility.ShuffledDeckCards(...)` →
                        // 시드된 hand-shuffle zone op(존 in-place 재정렬). 셔플 후 HandCards 재-read = 셔플된 순서.
                        await card.Context.ZoneMover.ShuffleHandAsync(enemy.PlayerId);

                        List<CardSource> shuffledHand = enemy.HandCards;

                        List<CardSource> selectedCards = new List<CardSource>();

                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: (cardSource) => true,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: null,
                            afterSelectCardCoroutine: AfterSelectCardCoroutine,
                            message: "Select cards to return to the bottom of opponent's deck.",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: false,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Custom,
                            customRootCardList: shuffledHand,
                            canLookReverseCard: false,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetNotShowCard();
                        selectCardEffect.SetUseFaceDown();

                        selectCardEffect.SetUpCustomMessage(
                            "Select cards to put on bottom of the deck.",
                            "The opponent is selecting cards to put on bottom of the deck.");

                        await selectCardEffect.Activate();

                        async Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                        {
                            if (cardSources.Count >= 1)
                            {
                                returned = true;

                                // AS-IS :246 CardObjectController.AddLibraryBottomCards(cardSources) →
                                // per-card MoveToDeckBottomAsync(enemyId, cardId)(EX8_072 idiom).
                                foreach (CardSource cardSource in cardSources)
                                {
                                    await card.Context.ZoneMover.MoveToDeckBottomAsync(enemy.PlayerId, cardSource.InstanceId);
                                    selectedCards.Add(cardSource);
                                }
                            }
                        }

                        // AS-IS :270-284 `if (card.Owner.isYou) foreach SetFace() else ShowCardEffect(...)` = UI — 스트립.
                        _ = selectedCards;
                    }
                }

                if (returned)
                {
                    if (CardEffectCommons.IsExistOnBattleArea(card))
                    {
                        Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                        await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
                    }
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
