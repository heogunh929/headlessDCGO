// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 정본 카드 — Snatchmon (BT18_065, Digimon / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT18/Black/BT18_065.cs (302 lines, 5 arms)
//    * When Digivolving  :15-113 (OnEnterFieldAnyone + CanTriggerWhenDigivolving — 트래시 [Vemmon] 최대 2장을
//      하단 진화원으로: SelectCardEffect Root.Trash/Mode.Custom → AddDigivolutionCardsBottom)
//    * End of Your Turn  :115-172(OnEndTurn — 진화원 ≥4면 손패 [Vemmon] 텍스트 Digimon으로 재진화:
//      DigivolveIntoHandOrTrashCard, payCost true)
//    * DigiXros from trash:174-211(None — AddMaxTrashCountDigiXrosClass, self=4 트래시 소재 허용;
//      CanUse = 진화원 미보유 non-[Vemmon] Digimon 0체일 때만)
//    * DigiXros           :213-256(None — AddDigiXrosConditionClass, [Vemmon] ×4, min 1)
//    * ESS [All Turns]    :258-297(OnDigivolutionCardReturnToDeckBottom + CanTriggerOnReturnToLibraryBottom
//      DigivolutionCard([Vemmon]) — Once Per Turn: 언서스펜드 + <Blocker> 상대턴종료까지; 상속 효과)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [When Digivolving] → 미러 방언 WhenDigivolving 전용 키(DigivolveAction이 WhenDigivolving만 해소 —
//      이중-키 등록 금지, BT17_026 판례 rule 3). AS-IS는 OnEnterFieldAnyone+CanTriggerWhenDigivolving(:16/40).
//    * None-타이밍 AddMaxTrashCountDigiXrosClass / AddDigiXrosConditionClass = 데이터-홀더 등록
//      (BT21_030/EX10_045 판례); 소비는 인터랙티브 DigiXros 플레이 경로(SelectDigiXrosClass.Select).
//    * ESS는 SetIsInheritedEffect(true) + SetHashString "Unsuspend_BT11_065"(AS-IS :265) Once Per Turn(order 1).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//    * `card.Owner`(AS-IS Player) → HeadlessPlayerId; `Owner.TrashCards` → `new Player(card.Context, card.Owner)
//      .TrashCards`(SelectAssemblyClass 판례).
//    * `card.PermanentOfThisCard()`(가변 필요: AddDigivolutionCardsBottom / DigivolveIntoHandOrTrashCard target /
//      IUnsuspendPermanents / GainBlocker) → `ICardEffect.ResolvePermanentOfThisCard(card)`(EX8_028/BT9_111 판례).
//      읽기-전용 진화원 카운트도 동일 브릿지 사용(IsExistOnBattleArea 가드 하 non-null).
//    * `CardEffectCommons.MatchConditionPermanentCount(pred)`(AS-IS no-card 오버로드) → 미러 card-arg 오버로드
//      `MatchConditionPermanentCount(card, pred)`(양쪽 전장 스캔 동일 의미, Save.cs:25).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT18.Black;

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

public sealed class BT18_065 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:16)이나 미러 방언은 WhenDigivolving 전용 키(BT17_026 rule 3).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Place up to 2 [Vemmon] from trash to digivolution cards.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] You may place up to 2 [Vemmon] from your trash under this Digimon as its bottom digivolution cards.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.CardNames.Contains("Vemmon"))
                {
                    return true;
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
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
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                    {
                        List<CardSource> selectedCards = new List<CardSource>();

                        int maxCount = Math.Min(2, new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition));

                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: CanEndSelectCondition,
                            canNoSelect: () => true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select [Vemmon] to place on bottom of digivolution cards\n(cards will be placed so that cards with lower numbers are on top).",
                            maxCount: maxCount,
                            canEndNotMax: true,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage_ShowCard("Digivolution Card");
                        selectCardEffect.SetUpCustomMessage("Select [Vemmon] to place on bottom of digivolution cards.", "The opponent is selecting [Vemmon] to place on bottom of digivolution cards.");

                        await selectCardEffect.Activate();

                        bool CanEndSelectCondition(List<CardSource> cardSources)
                        {
                            if (CardEffectCommons.HasNoElement(cardSources))
                            {
                                return false;
                            }

                            return true;
                        }

                        Task SelectCardCoroutine(CardSource cardSource)
                        {
                            selectedCards.Add(cardSource);

                            return Task.CompletedTask;
                        }

                        if (selectedCards.Count >= 1)
                        {
                            await ICardEffect.ResolvePermanentOfThisCard(card).AddDigivolutionCardsBottom(selectedCards, activateClass.EffectSourceCard?.InstanceId);
                        }
                    }
                }
            }
        }

        #endregion

        #region End of Turn

        if (timing == EffectTiming.OnEndTurn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Digivolve into a Digimon with Vemmon in text.", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[End of Your Turn] If this Digimon has 4 or more digivolution cards, this Digimon may Digivolve into a Digimon card with [Vemmon] in its text in your hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count >= 4)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return (cardSource.IsDigimon && cardSource.HasText("Vemmon"));
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.DigivolveIntoHandOrTrashCard(
                    targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                    cardCondition: CanSelectCardCondition,
                    payCost: true,
                    reduceCostTuple: (reduceCost: 0, reduceCostCardCondition: null),
                    fixedCostTuple: null,
                    ignoreDigivolutionRequirementFixedCost: -1,
                    isHand: true,
                    activateClass: activateClass,
                    successProcess: null);
            }
        }

        #endregion

        #region DigiXros from trash

        if (timing == EffectTiming.None)
        {
            AddMaxTrashCountDigiXrosClass addMaxTrashCountDigiXrosClass = new AddMaxTrashCountDigiXrosClass();
            addMaxTrashCountDigiXrosClass.SetUpICardEffect($"Trash cards can be selected for DigiXros", CanUseCondition, card);
            addMaxTrashCountDigiXrosClass.SetUpAddMaxTrashCountDigiXrosClass(getMaxTrashCount: GetCount);
            addMaxTrashCountDigiXrosClass.SetNotShowUI(true);
            cardEffects.Add(addMaxTrashCountDigiXrosClass);

            bool CanSelectPermanentCondition1(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (!permanent.TopCard.EqualsCardName("Vemmon"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition1) == 0;
            }

            int GetCount(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    return 4;
                }

                return 0;
            }
        }

        #endregion

        #region DigiXros

        if (timing == EffectTiming.None)
        {
            AddDigiXrosConditionClass addDigiXrosConditionClass = new AddDigiXrosConditionClass();
            addDigiXrosConditionClass.SetUpICardEffect($"DigiXros", CanUseCondition, card);
            addDigiXrosConditionClass.SetUpAddDigiXrosConditionClass(getDigiXrosCondition: GetDigiXros);
            addDigiXrosConditionClass.SetNotShowUI(true);
            cardEffects.Add(addDigiXrosConditionClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            DigiXrosCondition GetDigiXros(CardSource cardSource)
            {
                if (cardSource == card)
                {
                    List<DigiXrosConditionElement> elements = new List<DigiXrosConditionElement>();

                    DigiXrosConditionElement element1 = new DigiXrosConditionElement(CanSelectCardCondition1, "Vemmon");

                    bool CanSelectCardCondition1(CardSource cardSource)
                    {
                        return cardSource != null
                            && cardSource.Owner == card.Owner
                            && cardSource.IsDigimon
                            && cardSource.CardNames_DigiXros.Contains("Vemmon");
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        elements.Add(element1);
                    }

                    DigiXrosCondition digiXrosCondition = new DigiXrosCondition(elements, null, 1);

                    return digiXrosCondition;
                }

                return null;
            }
        }

        #endregion

        #region ESS - All Turns

        if (timing == EffectTiming.OnDigivolutionCardReturnToDeckBottom)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unsuspend this Digimon and it gain Blocker", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("Unsuspend_BT11_065");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns][Once Per Turn] When [Vemmon] is returned from this Digimon's digivolution cards at the bottom of its owner's deck, unsuspend this Digimon, and it gains <Blocker> until the end of your opponent's turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnReturnToLibraryBottomDigivolutionCard(hashtable, cardSource => cardSource.CardNames.Contains("Vemmon"), card);
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
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();

                await CardEffectCommons.GainBlocker(targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card), effectDuration: EffectDuration.UntilOpponentTurnEnd, activateClass: activateClass);
            }
        }

        #endregion

        return cardEffects;
    }
}
