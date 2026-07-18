// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T1 정본 카드 — Kuzuhamon (P_223, Digimon / Purple / Lv.6)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/P/Purple/P_223.cs (461 lines, 6 regions)
//    * Alt Digivolution Condition       :15-27  (timing == None, AddSelfDigivolutionRequirementStaticEffect)
//    * Before Pay Cost 조건 효과        :31-131 (timing == BeforePayCost — 보안 3장 이하 시 플레이 코스트 -4)
//    * Reduce Play Cost - Not Shown     :133-221(timing == None, ChangeCostClass isCheckAvailability=true)
//    * Name Rule                        :225-250(timing == None, ChangeCardNamesClass — [Sakuyamon] 병기)
//    * [On Play]/[When Digivolving] 공유 :252-422(공유 코루틴 SharedActivateCoroutine + 2 등록)
//    * [All Turns][Once Per Turn]       :424-456(timing == OnUseOption — [Pipe Fox] 토큰 플레이)
//
// ② 프리미티브 매핑 (감사 축 이름 — coverage_exemplar_audit_2026-07-18.md §4 #8):
//    * P:ChangeCardNamesClass — Name Rule 상시 효과 (AS-IS :227-248)
//    * P:PlayOptionCards      — 공유 몸통의 손/트래시 옵션 무료 사용 (AS-IS :346-351, :379-384)
//    * P:PlayPipeFox          — OnUseOption 몸통 (AS-IS :450-453; TokenSpecs["PipeFox"] 경유)
//    * T:OnUseOption          — [All Turns] 옵션-사용 창 (AS-IS :426; 미러 emit=OptionActivateAction.cs:95)
//    * (+P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect), P:ChangeCostClass,
//       T:BeforePayCost, E:SelectHandEffect/SelectCardEffect(Root.Trash), UserSelectionManager bool 선택)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [On Play] → OnEnterFieldAnyone + CanTriggerOnPlay(hashtable, card) 게이트(AS-IS :399-402 그대로;
//      rule 3: [On Play]→OnEnterFieldAnyone).
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언; AS-IS는 OnEnterFieldAnyone
//      :409 + CanTriggerWhenDigivolving 게이트 — 게이트는 그대로, 키만 방언. 이중-키 등록 금지).
//    * [All Turns] 옵션-사용 트리거 → OnUseOption + CanTriggerWhenOwnerUseOption(hashtable, null, null,
//      card) 게이트(AS-IS :439-443 그대로) + SetHashString("PlayToken_P_223") once-per-turn 키.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * `card.Owner.HandCards/SecurityCards` → `new Player(card.Context, card.Owner).*`.
//    * `cardSource.HasOnmyoOrPluginTraits` (AS-IS CardSource.cs:2099-2114) — 미러 CardSource에 해당 파생
//      속성이 없어 AS-IS 몸통(EqualsTraits("Plug-In") || EqualsTraits("Onmyōjutsu"))을 로컬 술어로 1:1
//      인라인(파생-getter 인라인은 BT22_035의 HasAppmonTraits→EqualsTraits("Appmon") 확립 idiom).
//    * userSelectionManager Set/Wait/SelectedBoolValue — 미러 UserSelectionManager 1:1 표면 그대로
//      (GManager.cs:74; BT1_111 기존 사용례).
//    * ShowReducedCost — 미러는 UI-only no-op 브릿지(ShowReducedCost.cs:17)지만 AS-IS 호출 위치 유지.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.P.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class P_223 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alt Digivolution Condition

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.ContainsCardName("Maid Mode") && targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 6;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 1, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region Reduced play cost

        #region Before Pay Cost - Condition Effect

        if (timing == EffectTiming.BeforePayCost)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play cost reduction -4", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetHashString("P_223_ReducePlayCost");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "When this card would be played from the hand, if you have 3 or fewer security, reduce the play cost by 4.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenPermanentWouldPlay(hashtable, CardCondition);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnHand(card)
                    && CardEffectCommons.HasMatchConditionPermanent(card, (Func<HeadlessEntityId, bool>)(x => new Player(card.Context, card.Owner).SecurityCards.Count <= 3));
            }

            bool CardCondition(CardSource cardSource)
            {
                return CardEffectCommons.IsExistOnHand(cardSource)
                    && cardSource == card;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                ChangeCostClass changeCostClass = new ChangeCostClass();
                changeCostClass.SetUpICardEffect("Play Cost -4", CanUseCondition1, card);
                changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);
                new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add((_timing) => changeCostClass);

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

                                if (new Player(cardSource.Context, cardSource.Owner).SecurityCards.Count <= 3)
                                {
                                    Cost -= 4;
                                }
                            }
                        }
                    }

                    return Cost;
                }

                bool PermanentsCondition(List<Permanent> targetPermanents)
                {
                    if (targetPermanents == null)
                    {
                        return true;
                    }
                    else
                    {
                        if (targetPermanents.Count((targetPermanent) => targetPermanent != null) == 0)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                bool CardSourceCondition(CardSource cardSource)
                {
                    return cardSource == card;
                }

                bool RootCondition(SelectCardEffect.Root root)
                {
                    return root == SelectCardEffect.Root.Hand;
                }

                bool isUpDown()
                {
                    return true;
                }

                await CardEffectCommons.ShowReducedCost(_hashtable);
            }
        }

        #endregion

        #region Reduce Play Cost - Not Shown

        if (timing == EffectTiming.None)
        {
            ChangeCostClass changeCostClass = new ChangeCostClass();
            changeCostClass.SetUpICardEffect("Play Cost -4", CanUseCondition, card);
            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => true, isChangePayingCost: () => true);
            changeCostClass.SetNotShowUI(true);
            cardEffects.Add(changeCostClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (new Player(card.Context, card.Owner).HandCards.Contains(card))
                {
                    ICardEffect activateClass = card.EffectList(EffectTiming.BeforePayCost).Find(cardEffect => cardEffect.EffectName == "Play cost reduction -4");

                    if (activateClass != null)
                    {
                        if (new Player(card.Context, card.Owner).SecurityCards.Count <= 3)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
            {
                if (CardSourceCondition(cardSource))
                {
                    if (RootCondition(root))
                    {
                        if (PermanentsCondition(targetPermanents))
                        {
                            if (new Player(cardSource.Context, cardSource.Owner).SecurityCards.Count <= 3)
                            {
                                Cost -= 4;
                            }
                        }
                    }
                }

                return Cost;
            }

            bool PermanentsCondition(List<Permanent> targetPermanents)
            {
                if (targetPermanents == null)
                {
                    return true;
                }
                else
                {
                    if (targetPermanents.Count((targetPermanent) => targetPermanent != null) == 0)
                    {
                        return true;
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
                return root == SelectCardEffect.Root.Hand;
            }

            bool isUpDown()
            {
                return true;
            }
        }

        #endregion

        #endregion

        #region Name Rule

        if (timing == EffectTiming.None)
        {
            ChangeCardNamesClass changeCardNamesClass = new ChangeCardNamesClass();
            changeCardNamesClass.SetUpICardEffect("Also treated as [Sakuyamon]", CanUseCondition, card);
            changeCardNamesClass.SetUpChangeCardNamesClass(changeCardNames: changeCardNames);
            cardEffects.Add(changeCardNamesClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return true;
            }

            List<string> changeCardNames(CardSource cardSource, List<string> CardNames)
            {
                if (cardSource == card)
                {
                    CardNames.Add("Sakuyamon");
                }

                return CardNames;
            }
        }

        #endregion

        #region On Play / When Digivolving Shared

        string SharedEffectDiscription(string tag)
        {
            return $"[{tag}] You may use 1 [Onmyōjutsu] or [Plug-In] trait Option card from your hand or trash without paying the cost.";
        }

        bool SharedCanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
        }

        bool CanSelectOptionCard(CardSource cardSource)
        {
            if (cardSource.IsOption)
            {
                // AS-IS cardSource.HasOnmyoOrPluginTraits (CardSource.cs:2099-2114) 몸통 1:1 인라인 —
                // 미러 CardSource엔 파생-getter가 없음(BT22_035 EqualsTraits idiom).
                if (cardSource.EqualsTraits("Plug-In") || cardSource.EqualsTraits("Onmyōjutsu"))
                {
                    if (!cardSource.CanNotPlayThisOption)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            bool canSelectHand = CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectOptionCard);
            bool canSelectTrash = CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectOptionCard);
            bool playFromHand = false;
            bool playFromTrash = false;
            if (canSelectHand || canSelectTrash)
            {
                List<CardSource> selectedCards = new List<CardSource>();

                #region User Selection - Select Card Location

                if (canSelectHand && canSelectTrash)
                {
                    List<SelectionElement<bool>> selectionElements1 = new List<SelectionElement<bool>>()
                    {
                        new SelectionElement<bool>(message: $"From hand", value : true, spriteIndex: 0),
                        new SelectionElement<bool>(message: $"From trash", value : false, spriteIndex: 1),
                    };

                    string selectPlayerMessage1 = "From which area do you select a card?";
                    string notSelectPlayerMessage1 = "The opponent is choosing from which area to select a card.";

                    GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements1, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage1, notSelectPlayerMessage: notSelectPlayerMessage1);
                }
                else
                {
                    GManager.instance.userSelectionManager.SetBool(canSelectHand);
                }

                await GManager.instance.userSelectionManager.WaitForEndSelect();

                var handOrTrashSelection = GManager.instance.userSelectionManager.SelectedBoolValue;
                if (handOrTrashSelection) playFromHand = true;
                else playFromTrash = true;

                #endregion

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCards.Add(cardSource);
                    return Task.CompletedTask;
                }

                SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                if (playFromHand)
                {
                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectOptionCard,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectHandEffect.SetUpCustomMessage("Select 1 option to use", "The opponent is selecting 1 option to use");

                    await selectHandEffect.Activate();

                    await CardEffectCommons.PlayOptionCards(
                        cardSources: selectedCards,
                        activateClass: activateClass,
                        payCost: false,
                        root: SelectCardEffect.Root.Hand);
                }
                if (playFromTrash)
                {
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();
                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectOptionCard,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 Option to use",
                        maxCount: 1,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select 1 option to use", "The opponent is selecting 1 option to use");
                    selectCardEffect.SetUpCustomMessage_ShowCard("Used Card");

                    await selectCardEffect.Activate();

                    await CardEffectCommons.PlayOptionCards(
                        cardSources: selectedCards,
                        activateClass: activateClass,
                        payCost: false,
                        root: SelectCardEffect.Root.Trash);
                }
            }
        }

        #endregion

        #region On Play

        // ③ 배선: [On Play] → OnEnterFieldAnyone (trigger-wiring rule 3) + CanTriggerOnPlay 게이트(AS-IS :399-402).
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may use an [Onmyōjutsu] or [Plug-In] Option from hand or trash", CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, hash => SharedActivateCoroutine(hash, activateClass), -1, true, SharedEffectDiscription("On Play"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:409)이지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — trigger-wiring rule 3).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may use an [Onmyōjutsu] or [Plug-In] Option from hand or trash", CanUseCondition, card);
            activateClass.SetUpActivateClass(SharedCanActivateCondition, hash => SharedActivateCoroutine(hash, activateClass), -1, true, SharedEffectDiscription("When Digivolving"));
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card) && CardEffectCommons.IsExistOnBattleArea(card);
            }
        }

        #endregion

        #region All Turns

        if (timing == EffectTiming.OnUseOption)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play a [Pipe Fox] Token", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("PlayToken_P_223");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns][Once Per Turn] When you use Option cards, you may play 1 [Pipe Fox] Token (Digimon/Yellow/6000 DP/<Blocker>).";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenOwnerUseOption(hashtable, null, null, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.PlayPipeFox(activateClass);
            }
        }

        #endregion

        return cardEffects;
    }
}
