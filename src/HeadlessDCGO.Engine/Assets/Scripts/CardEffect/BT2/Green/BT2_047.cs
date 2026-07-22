// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 정본 카드 — MetalGreymon (BT2_047, Digimon / Green)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT2/Green/BT2_047.cs (387 lines, 2 regions)
//    * <Digisorption -3>      :14-276 (BeforePayCost — 진화-흡수 시 아군 1체 서스펜드→진화 코스트 -3)
//    * [When Attacking]       :278-382(OnAllyAttack — inherited: 손패 lvl3 녹색 디지몬 1장 서스펜드-무코스트 플레이)
//
// ② 1:1 근거: <Digisorption -3> BeforePayCost 팔은 BT3_056(같은 digisorption-3 카드군)의 정본 region 1과
//    바이트-동일(동일 hash "Digisorption-3_BT2_047"·동일 CanUseCondition 해시테이블 검사·동일 컷인/ChangeCost
//    파이프). 그 상환 표면(Player.CanTapWhenAbsorbEvolution(_CheckAvailability)·SelectPermanentEffect(Mode.Tap)·
//    ChangeCostClass) 재사용, 신규 발명 0.
//
// ③ 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return ContinuousController.instance.StartCoroutine(X)`/`StartCoroutine(X)`
//      →`await X`; lone `yield return null`→제거/Task.CompletedTask.
//    * `card.Owner.*`(AS-IS live Player) Player 조작 → `new Player(card.Context, card.Owner).*`
//      (CanTapWhenAbsorbEvolution(_CheckAvailability)/GetBattleAreaPermanents/GetBattleAreaDigimons/HandCards/
//      UntilCalculateFixedCostEffect).
//    * `GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer` → `new GameContext(card.Context).
//      Players_ForTurnPlayer` (미러 확립 idiom; List<Player>).
//    * `SelectPermanentEffect` canTargetCondition는 id-형 — AS-IS Permanent-술어를 PermanentOf(id) 어댑터로 전달
//      (BT3_056/BT21_030 판례).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (ST1_09/BT2_081 판례).
//    * `CardColor.Green`(AS-IS enum) → `"Green"` (미러 HasCardColor(string) idiom — BT2_044 헤더).
//    * `card.Owner.CanReduceCost(new List<Permanent>() { new Permanent(new List<CardSource>()) }, card)`(AS-IS :188)
//      + PlaySE(BuffSE) = SE 연출 게이트 — 스트립(BT3_056 헤더 ③ / ST17_13 판례; 실 감액 게이트는 ChangeCostClass
//      내부 CanReduceCost).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Green;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_047 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // 미러 SelectPermanentEffect는 id-형 canTargetCondition — AS-IS Permanent-술어의 id 어댑터(BT3_056 판례).
        Permanent? PermanentOf(HeadlessEntityId id) =>
            card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                ? new Permanent(card.Context, id, rec.OwnerId)
                : null;

        #region Digisorption -3 (BeforePayCost)

        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Digisorption -3", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            activateClass.SetHashString("Digisorption-3_BT2_047");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "<Digisorption -3> (When one of your Digimon digivolves into this card from your hand, you may suspend 1 of your Digimon to reduce the memory cost of the digivolution by 3.)";
            }

            bool CanSelectCondition_CheckAvailability(Permanent permanent)
            {
                if (new Player(card.Context, card.Owner).CanTapWhenAbsorbEvolution_CheckAvailability(permanent, activateClass))
                {
                    if (permanent.CanSelectBySkill(activateClass))
                    {
                        if (!permanent.TopCard.CanNotBeAffected(activateClass))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (new Player(card.Context, card.Owner).CanTapWhenAbsorbEvolution(permanent, activateClass))
                {
                    if (permanent.CanSelectBySkill(activateClass))
                    {
                        if (!permanent.TopCard.CanNotBeAffected(activateClass))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanSelectPermanentById(HeadlessEntityId id) =>
                PermanentOf(id) is Permanent p && CanSelectPermanentCondition(p);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (hashtable != null)
                {
                    if (hashtable.ContainsKey("Card"))
                    {
                        if (hashtable["Card"] is CardSource)
                        {
                            CardSource Card = (CardSource)hashtable["Card"];

                            if (Card == card)
                            {
                                if (hashtable.ContainsKey("isEvolution"))
                                {
                                    if (hashtable["isEvolution"] is bool)
                                    {
                                        bool isEvolution = (bool)hashtable["isEvolution"];

                                        if (isEvolution)
                                        {
                                            if (hashtable.ContainsKey("Permanents"))
                                            {
                                                if (hashtable["Permanents"] is List<Permanent>)
                                                {
                                                    List<Permanent> Permanents = (List<Permanent>)hashtable["Permanents"];

                                                    if (Permanents != null)
                                                    {
                                                        if (Permanents.Count((permanent) => permanent.TopCard.Owner == card.Owner && new Player(card.Context, permanent.TopCard.Owner).GetBattleAreaPermanents().Contains(permanent)) >= 1)
                                                        {
                                                            if (new GameContext(card.Context).Players_ForTurnPlayer.Count((player) => player.GetBattleAreaDigimons().Count(CanSelectCondition_CheckAvailability) >= 1) >= 1)
                                                            {
                                                                return true;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (new GameContext(card.Context).Players_ForTurnPlayer.Count((player) => player.GetBattleAreaDigimons().Count(CanSelectCondition_CheckAvailability) >= 1) >= 1)
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                #region 진화-흡수 컷인(WhenDigisorption) 개방
                Hashtable hashtable = new Hashtable();
                hashtable.Add("CardEffect", activateClass);

                foreach (Player player in new GameContext(card.Context).Players_ForTurnPlayer)
                {
                    #region 장 파마넌트의 WhenDigisorption
                    foreach (Permanent permanent1 in player.GetFieldPermanents())
                    {
                        foreach (ICardEffect cardEffect in permanent1.EffectList(EffectTiming.WhenDigisorption))
                        {
                            if (cardEffect is ActivateICardEffect)
                            {
                                if (cardEffect.CanTrigger(hashtable))
                                {
                                    GManager.instance.autoProcessing_CutIn.PutStackedSkill(new SkillInfo(cardEffect, hashtable, EffectTiming.WhenDigisorption));
                                }
                            }
                        }
                    }
                    #endregion

                    #region 플레이어의 WhenDigisorption
                    foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.WhenDigisorption))
                    {
                        if (cardEffect is ActivateICardEffect)
                        {
                            if (cardEffect.CanTrigger(hashtable))
                            {
                                GManager.instance.autoProcessing_CutIn.PutStackedSkill(new SkillInfo(cardEffect, hashtable, EffectTiming.WhenDigisorption));
                            }
                        }
                    }
                    #endregion
                }

                await GManager.instance.autoProcessing_CutIn.TriggeredSkillProcess(false, AutoProcessing.HasExecutedSameEffect);
                #endregion

                if (new GameContext(card.Context).Players_ForTurnPlayer.Count((player) => player.GetBattleAreaDigimons().Count(CanSelectPermanentCondition) >= 1) >= 1)
                {
                    int maxCount = 1;

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentById,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                        mode: SelectPermanentEffect.Mode.Tap,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();

                    async Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
                    {
                        if (permanents.Count >= 1)
                        {
                            // AS-IS :188-191 CanReduceCost 판정 + PlaySE(BuffSE) = SE 연출 게이트 — 스트립(헤더 ③).

                            ChangeCostClass changeCostClass = new ChangeCostClass();
                            changeCostClass.SetUpICardEffect("Digivolution Cost -3", CanUseCondition1, card);
                            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);
                            new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => changeCostClass);

                            await CardEffectCommons.ShowReducedCost(_hashtable);

                            bool CanUseCondition1(Hashtable hashtable)
                            {
                                return true;
                            }

                            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
                            {
                                if (CardSourceCondition(cardSource))
                                {
                                    if (RootCondition(root))
                                    {
                                        if (PermanentsCondition(targetPermanents))
                                        {
                                            Cost -= 3;
                                        }
                                    }
                                }

                                return Cost;
                            }

                            bool PermanentsCondition(List<Permanent> targetPermanents)
                            {
                                if (targetPermanents != null)
                                {
                                    if (targetPermanents.Count(PermanentCondition) >= 1)
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }

                            bool PermanentCondition(Permanent targetPermanent)
                            {
                                if (targetPermanent.TopCard != null)
                                {
                                    if (targetPermanent.TopCard.Owner == card.Owner)
                                    {
                                        if (new Player(card.Context, targetPermanent.TopCard.Owner).GetBattleAreaPermanents().Contains(targetPermanent))
                                        {
                                            return true;
                                        }
                                    }
                                }

                                return false;
                            }

                            bool CardSourceCondition(CardSource cardSource)
                            {
                                if (cardSource != null)
                                {
                                    if (cardSource == card)
                                    {
                                        return true;
                                    }
                                }

                                return false;
                            }

                            bool RootCondition(SelectCardEffect.Root root)
                            {
                                return true;
                            }

                            bool isUpDown()
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region When Attacking (OnAllyAttack) — inherited

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 Digimon from hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] You may play 1 level 3 green Digimon card from your hand suspended without paying its memory cost.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource != null)
                {
                    if (cardSource.IsDigimon)
                    {
                        if (cardSource.Owner == card.Owner)
                        {
                            if (cardSource.HasCardColor("Green"))
                            {
                                if (cardSource.Level == 3)
                                {
                                    if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                                    {
                                        if (cardSource.HasLevel)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).HandCards.Count >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (isExistOnField(card))
                {
                    if (new Player(card.Context, card.Owner).GetBattleAreaDigimons().Contains(ICardEffect.ResolvePermanentOfThisCard(card)))
                    {
                        if (new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 1)
                        {
                            List<CardSource> selectedCards = new List<CardSource>();

                            int maxCount = 1;

                            SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                            selectHandEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CanSelectCardCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: maxCount,
                                canNoSelect: true,
                                canEndNotMax: false,
                                isShowOpponent: true,
                                selectCardCoroutine: SelectCardCoroutine,
                                afterSelectCardCoroutine: null,
                                mode: SelectHandEffect.Mode.Custom,
                                cardEffect: activateClass);

                            selectHandEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                            selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                            await selectHandEffect.Activate();

                            Task SelectCardCoroutine(CardSource cardSource)
                            {
                                selectedCards.Add(cardSource);

                                return Task.CompletedTask;
                            }

                            await CardEffectCommons.PlayPermanentCards(cardSources: selectedCards, activateClass: activateClass, payCost: false, isTapped: true, root: SelectCardEffect.Root.Hand, activateETB: true);
                        }
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
