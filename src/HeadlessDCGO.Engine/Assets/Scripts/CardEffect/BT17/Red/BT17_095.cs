// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S3 카드 — BT17_095 (Option / Red)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT17/Red/BT17_095.cs (637 lines, 3 regions)
//    * [Main]        :17-160 (OptionSkill — play 1 [Agumon]/[Gabumon] from hand/trash free -> PlaceDelayOptionCards)
//    * <Delay>        :164-484(WhenRemoveField — lvl6 [Greymon]/[Garurumon] would leave field outside battle
//                                -> Delay -> DNA digivolve into [Omnimon] card in hand)
//    * [Security]     :488-631(SecuritySkill — play 1 [Tai Kamiya]/[Matt Ishida] from hand/trash free + AddThisCardToHand)
//
// ② 프리미티브 매핑:
//    * P:PlaceDelayOptionCards, P:CanTriggerOptionMainEffect, P:CanPlayAsNewPermanent, P:PlayPermanentCards,
//      P:DeletePeremanentAndProcessAccordingToResult, P:CanDeclareOptionDelayEffect,
//      P:CanTriggerWhenPermanentRemoveField, P:IsByBattle, P:AddThisCardToHand, P:CanTriggerSecurityEffect
//    * **STOP (design item RD-S3-BT17_095, substrate 갭)**: <Delay> 성공분기(SuccessProcess)의 실제 DNA-진화
//      집행부(AS-IS :435-481) — `selectedCardSource.CanPlayCardTargetFrame(fieldCardFrame, ...)`로 손패카드를
//      "임시로" 특정 fieldCardFrame에 배치해 JogressConditionElement.EvoRootCondition을 필드-permanent와
//      공동판정한 뒤 `PlayCardClass.SetJogress(jogressEvoRootsFrameIDs)`로 집행하는 흐름은, 미러 substrate가
//      이미 동일 메커니즘을 명시적으로 미포팅 STOP 처리한 지점과 동일 성격이다:
//        - `CardSource.CanJogressFromTargetPermanents`(CardController.cs:4535-4541)가 이미
//          `"STOP: CardSource.CanJogressFromTargetPermanents (AS-IS CardSource.cs:2846) — the AS-IS jogress ..."`
//          로 NotSupportedException 선언되어 있음.
//        - `CardEffectCommons.DNADigivolveWithHandOrTrashCardIntoHandOrTrash`(DNADigivolveEffects.cs)도 "손패/
//          트래시 카드를 임시 permanent로 플레이해 필드 permanent와 공동평가 후 PlayCardClass로 집행, 실패 시
//          롤백"하는 바로 그 메커니즘을 "no headless substrate surface"로 명시 STOP함(RD-W3 계열).
//      BT17_095는 이 헬퍼를 직접 호출하지 않고 동일 메커니즘을 카드 파일 안에서 수기 재현하지만, 근본적으로
//      똑같은 "transient permanent 공동평가 + rollback" substrate가 필요하다. 공용층(Script/) 수정은 금지
//      규칙이므로, <Delay> 등재 팔(CanUseCondition/CanActivateCondition — 프레임 모델과 무관한 순수 술어)은
//      실배선하되, ActivateCoroutine은 실행 시 즉시 NotSupportedException을 던진다(부분-뮤테이션 방지 위해
//      AS-IS :244의 DeletePeremanentAndProcessAccordingToResult 호출 전에 STOP — 이미 실존 삭제 부작용이
//      발생한 뒤 이어지는 미구현 집행부로 넘어가는 것을 막는다). [Main]/[Security]는 이 갭과 무관 — 완전 이식.
//
// ③ 배선 관례 근거:
//    * [Main] → OptionSkill + CanTriggerOptionMainEffect.
//    * <Delay> → WhenRemoveField(AS-IS 그대로) + CanDeclareOptionDelayEffect + CanTriggerWhenPermanentRemoveField.
//    * [Security] → SecuritySkill + CanTriggerSecurityEffect.
//
// 치환(substrate translations only, [Main]/[Security] 한정 — <Delay>는 STOP):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`/
//      `yield return StartCoroutine(X)` → `await X`.
//    * `card.Owner.HandCards.Count(cond)` → `new Player(card.Context, card.Owner).HandCards.Count(cond)`.
//    * `CardEffectCommons.PlaceDelayOptionCards(card, activateClass)` — AS-IS 기본 root(Root.Execution) 그대로.
//    * `CardEffectCommons.AddThisCardToHand(card, activateClass)` → mirror `(card, activateClass.EffectSourceCard)`
//      (BT1_096/BT1_112/BT1_108 확립 판례).
//    * `permanent.TopCard.IsLevel6`(AS-IS 파생 프로퍼티, 미러 부재) → `permanent.TopCard.HasLevel &&
//      permanent.TopCard.Level == 6` 인라인(§2.4 IsLevel6 규칙).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT17.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT17_095 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Main Effect

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Play 1 [Agumon] or [Gabumon] from your hand or trash",
                CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[Main] You may play 1 [Agumon]/[Gabumon] from your hand or trash without paying the cost. Then, place this card in the battle area.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                {
                    if (cardSource.IsDigimon)
                    {
                        return cardSource.EqualsCardName("Agumon") ||
                               cardSource.EqualsCardName("Gabumon");
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                bool canSelectHand = new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 1;
                bool canSelectTrash = CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition);

                if (canSelectHand || canSelectTrash)
                {
                    if (canSelectHand && canSelectTrash)
                    {
                        List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>
                        {
                            new(message: "From hand", value: true, spriteIndex: 0),
                            new(message: "From trash", value: false, spriteIndex: 1),
                        };

                        string selectPlayerMessage = "From which area do you play a card?";
                        string notSelectPlayerMessage = "The opponent is choosing from which area to play a card.";

                        GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements,
                            selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage,
                            notSelectPlayerMessage: notSelectPlayerMessage);
                    }

                    else
                    {
                        GManager.instance.userSelectionManager.SetBool(canSelectHand);
                    }

                    await GManager.instance.userSelectionManager
                        .WaitForEndSelect();

                    bool fromHand = GManager.instance.userSelectionManager.SelectedBoolValue;

                    List<CardSource> selectedCards = new List<CardSource>();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);
                        return Task.CompletedTask;
                    }

                    if (fromHand)
                    {
                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectCardCondition,
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

                        selectHandEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                        selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                        await selectHandEffect.Activate();
                    }

                    else
                    {
                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 card to play.",
                            maxCount: 1,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                        selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                        await selectCardEffect.Activate();
                    }

                    SelectCardEffect.Root root = SelectCardEffect.Root.Hand;

                    if (!fromHand)
                    {
                        root = SelectCardEffect.Root.Trash;
                    }

                    await CardEffectCommons.PlayPermanentCards(
                        cardSources: selectedCards, activateClass: activateClass, payCost: false, isTapped: false, root: root,
                        activateETB: true);
                }

                await CardEffectCommons.PlaceDelayOptionCards(card: card, cardEffect: activateClass);
            }
        }

        #endregion

        #region Delay Effect

        if (timing == EffectTiming.WhenRemoveField)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(
                "DNA digivolve into a Digimon card with [Omnimon] in its name",
                CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, true, EffectDescription());
            activateClass.SetHashString("DNAOmnimon_BT17_095");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[All Turns] When one of your level 6 Digimon with [Greymon]/[Garurumon] in its name would leave the battle area outside of a battle, [Delay]. ・That Digimon and a card in the hand may DNA digivolve into a Digimon card with [Omnimon] in its name in the hand.";
            }

            bool IsOwnerPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    // AS-IS TopCard.IsLevel6 파생 프로퍼티(미러 부재) — §2.4 인라인.
                    return permanent.TopCard.HasLevel && permanent.TopCard.Level == 6 &&
                           (permanent.TopCard.ContainsCardName("Greymon") ||
                            permanent.TopCard.ContainsCardName("Garurumon"));
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanDeclareOptionDelayEffect(card))
                {
                    if (CardEffectCommons.CanTriggerWhenPermanentRemoveField(hashtable, IsOwnerPermanentCondition))
                    {
                        if (!CardEffectCommons.IsByBattle(hashtable))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            Task ActivateCoroutine(Hashtable hashtable)
            {
                // STOP (design item RD-S3-BT17_095): 성공분기의 DNA-진화 집행부(임시 손패-permanent 공동평가
                // + PlayCardClass.SetJogress(frameIDs) 집행 + 실패 롤백)는 미러 substrate가 이미 명시 STOP한
                // 메커니즘과 동일 성격(CardSource.CanJogressFromTargetPermanents — CardController.cs:4535-4541;
                // CardEffectCommons.DNADigivolveWithHandOrTrashCardIntoHandOrTrash — DNADigivolveEffects.cs).
                // 공용층 수정 금지 규칙상 여기서 즉시 낙뢰(AS-IS :244 DeletePeremanentAndProcessAccordingToResult
                // 호출 이전 — 실존 부분-뮤테이션 방지).
                throw new NotSupportedException(
                    "STOP: BT17_095 <Delay> DNA-digivolve execution needs a transient-permanent joint-evaluation " +
                    "+ PlayCardClass.SetJogress(frameIDs) substrate — the mirror already declares this exact " +
                    "mechanism unbuilt at CardSource.CanJogressFromTargetPermanents (CardController.cs:4535) and " +
                    "CardEffectCommons.DNADigivolveWithHandOrTrashCardIntoHandOrTrash (DNADigivolveEffects.cs) — " +
                    "design item RD-S3-BT17_095.");
            }
        }

        #endregion

        #region Security Effect

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Play 1 [Tai Kamiya] or [Matt Ishida] from hand or trash and add this card to hand",
                CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return
                    "[Security] You may play 1 card with [Tai Kamiya]/[Matt Ishida] in its name from your hand or trash without paying the cost. Then, add this card to the hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                {
                    if (cardSource.IsTamer)
                    {
                        return cardSource.ContainsCardName("Tai Kamiya") ||
                               cardSource.ContainsCardName("Matt Ishida");
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                bool canSelectHand = new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 1;
                bool canSelectTrash = CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition);

                if (canSelectHand || canSelectTrash)
                {
                    if (canSelectHand && canSelectTrash)
                    {
                        List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>
                        {
                            new(message: "From hand", value: true, spriteIndex: 0),
                            new(message: "From trash", value: false, spriteIndex: 1),
                        };

                        string selectPlayerMessage = "From which area do you play a card?";
                        string notSelectPlayerMessage = "The opponent is choosing from which area to play a card.";

                        GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements,
                            selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage,
                            notSelectPlayerMessage: notSelectPlayerMessage);
                    }

                    else
                    {
                        GManager.instance.userSelectionManager.SetBool(canSelectHand);
                    }

                    await GManager.instance.userSelectionManager
                        .WaitForEndSelect();

                    bool fromHand = GManager.instance.userSelectionManager.SelectedBoolValue;

                    List<CardSource> selectedCards = new List<CardSource>();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);
                        return Task.CompletedTask;
                    }

                    if (fromHand)
                    {
                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectCardCondition,
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

                        selectHandEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                        selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                        await selectHandEffect.Activate();
                    }

                    else
                    {
                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 card to play.",
                            maxCount: 1,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                        selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                        await selectCardEffect.Activate();
                    }

                    SelectCardEffect.Root root = SelectCardEffect.Root.Hand;

                    if (!fromHand)
                    {
                        root = SelectCardEffect.Root.Trash;
                    }

                    await CardEffectCommons.PlayPermanentCards(
                        cardSources: selectedCards, activateClass: activateClass, payCost: false, isTapped: false, root: root,
                        activateETB: true);
                }

                await CardEffectCommons.AddThisCardToHand(card, activateClass.EffectSourceCard);
            }
        }

        #endregion

        return cardEffects;
    }
}
