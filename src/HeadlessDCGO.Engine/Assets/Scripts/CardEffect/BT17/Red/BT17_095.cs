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
//    * **RESOLVED (design item RD-S3-BT17_095 — temp-material DNA family, witnessed tests/DNATEMP-Witness.Tests)**:
//      <Delay> 성공분기(SuccessProcess)의 실제 DNA-진화 집행부(AS-IS :251-483)를 1:1 이식. 핵심 설계 결정
//      (OPTION 2): AS-IS의 엔티티-없는 transient `new Permanent(new List<CardSource>(){src})` + `FieldPermanents[0]=
//      transient` sparse-injection은, 미러의 읽기전용 Permanent VIEW(카드 자기 InstanceId + `snapshotZone:
//      BattleArea`)로 1:1 대응된다 — TopCard=해당 카드(모든 TopCard-술어 동일), 그리고 jogress
//      EvoRootCondition을 감싸는 `IsPermanentExistsOnOwnerBattleAreaDigimon` 필드-멤버십 판정이 snapshot에서
//      TRUE로 응답(존 변이/트리거 0). AS-IS raw-slot-write의 무부작용 의미와 완전 등가이며 shadow entity를
//      발명하지 않는다(SnapshotZone은 D-2 기존 transient-view 표면). 실제 material은
//      `CardObjectController.CreateNewPermanent`(live), jogress는 `CardSource.CanJogressFromTargetPermanent`(이번
//      이식) + `PlayCardClass.SetJogress`(live)로 집행 — MIG4-DISCARDEVOROOTS-PUTTOTRASH detach-leaf까지 도달
//      (그 leaf는 별도 설계항목, 병렬 cost-corner 골 소관). 실패 시 롤백은
//      `CardObjectController.AddHandCard(card, isDraw)`(이번 노출)로 material을 손으로 환원. FRAME 적응: 슬롯/
//      capacity 모델 부재 — CreateNewPermanent가 append(RD-P6C1-1/-2 계열). [Main]/[Security]는 이 갭과 무관.
//
// ③ 배선 관례 근거:
//    * [Main] → OptionSkill + CanTriggerOptionMainEffect.
//    * <Delay> → WhenRemoveField(AS-IS 그대로) + CanDeclareOptionDelayEffect + CanTriggerWhenPermanentRemoveField.
//    * [Security] → SecuritySkill + CanTriggerSecurityEffect.
//
// 치환(substrate translations only — <Delay>는 위 RD-S3-BT17_095 설계결정대로 이식됨):
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
using HeadlessDCGO.Engine.Headless.Choices;
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

            // AS-IS :191-194.
            bool IsOwnerPermanentToBeDeletedCondition(Permanent permanent)
            {
                return IsOwnerPermanentCondition(permanent) && permanent.willBeRemoveField;
            }

            // AS-IS :196-207.
            bool IsLevel6HandCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.HasLevel && cardSource.Level == 6)
                    {
                        return true;
                    }
                }

                return false;
            }

            // AS-IS :209-223 (`jogressCondition` → the mirror JogressConditionOf() accessor).
            bool IsOmnimonCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.ContainsCardName("Omnimon") &&
                        cardSource.HasLevel &&
                        cardSource.Level == 7)
                    {
                        if (cardSource.JogressConditionOf().Count > 0)
                            return true;
                    }
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

            // AS-IS :242-249 (IEnumerator→async Task; StartCoroutine→await). `card.PermanentOfThisCard()` (the
            // BT17_095 delay permanent) = its own-id Permanent view (mirror identity by InstanceId — BT17_095 IS
            // the top of its own permanent when the <Delay> fires).
            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
                    targetPermanents: new List<Permanent>() { new Permanent(card.Context, card.InstanceId, card.Owner) },
                    activateClass: activateClass,
                    successProcess: _ => SuccessProcess(),
                    failureProcess: null);
            }

            // AS-IS :251-483 — the temp-material DNA execution (design item RD-S3-BT17_095, RESOLVED this pass).
            async Task SuccessProcess()
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, IsOwnerPermanentToBeDeletedCondition))
                {
                    CardSource selectedLevel7 = null;
                    List<Permanent> allowedPermanents = new List<Permanent>();
                    List<CardSource> allowedCards = new List<CardSource>();
                    Permanent selectedPermanent = null;
                    CardSource selectedCardSource = null;

                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInHand(card, IsOmnimonCardCondition));

                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsOmnimonCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: SelectDNACoroutine,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectHandEffect.SetUpCustomMessage("Select 1 Digimon to DNA digivolve.",
                        "The opponent is selecting 1 Digimon to DNA digivolve.");

                    await selectHandEffect.Activate();

                    Task SelectDNACoroutine(CardSource cardSource)
                    {
                        selectedLevel7 = cardSource;
                        return Task.CompletedTask;
                    }

                    if (selectedLevel7 != null)
                    {
                        JogressCondition dnaCondition = selectedLevel7.JogressConditionOf()[0];

                        if (selectedLevel7.JogressConditionOf().Count > 1)
                        {
                            #region select DNA condition
                            SelectDNACondition selectDNACondition = GManager.instance.GetComponent<SelectDNACondition>();
                            selectDNACondition.SetUp(new Player(card.Context, selectedLevel7.Owner), selectedLevel7, SelectDNA);

                            await selectDNACondition.Activate();

                            Task SelectDNA(int dnaSelection)
                            {
                                dnaCondition = selectedLevel7.JogressConditionOf()[dnaSelection];
                                return Task.CompletedTask;
                            }
                            #endregion
                        }

                        JogressConditionElement[] elements = (JogressConditionElement[])dnaCondition.elements.Clone();

                        for (int i = 0; i < elements.Length; i++)
                        {
                            bool added = false;

                            foreach (Permanent permanent in new Player(card.Context, card.Owner).GetBattleAreaPermanents())
                            {
                                if (elements[i].EvoRootCondition(permanent))
                                {
                                    allowedPermanents.Add(permanent);
                                    added = true;
                                }
                            }

                            if (added)
                            {
                                int inverse = Math.Abs(i - 1);

                                foreach (CardSource source in new Player(card.Context, card.Owner).HandCards.Where(IsLevel6HandCardCondition))
                                {
                                    // (design item RD-S3-BT17_095 — temp-material DNA design decision, OPTION 2)
                                    // AS-IS :330-341 builds an ENTITY-LESS transient `new Permanent(new
                                    // List<CardSource>(){source})` and sparse-injects it at
                                    // `card.Owner.FieldPermanents[0]` (a raw slot write — no ETB/trigger) SOLELY so the
                                    // jogress EvoRootCondition — which the GetJogressConditionClass wrapper gates on
                                    // `IsPermanentExistsOnOwnerBattleAreaDigimon` — reads the material as
                                    // battle-area-resident, then nulls the slot. The FAITHFUL mirror is a read-only
                                    // Permanent VIEW over the card's OWN instance id carrying `snapshotZone:
                                    // BattleArea`: TopCard resolves to `source` (every TopCard-property predicate is
                                    // identical) and IsPermanentExistsOnBattleArea answers TRUE from the snapshot
                                    // (Permanent.cs SnapshotZone / CardEffectCommons.IsPermanentExistsOnBattleArea)
                                    // WITHOUT any zone mutation or trigger — 1:1 with the AS-IS raw-slot-write's
                                    // zero-side-effect semantics. No shadow entity is invented; SnapshotZone is the
                                    // existing (D-2) transient-view surface.
                                    Permanent playedPermanent = new Permanent(card.Context, source.InstanceId, source.Owner, snapshotZone: ChoiceZone.BattleArea);

                                    if (elements[inverse].EvoRootCondition(playedPermanent))
                                        allowedCards.Add(source);
                                }
                            }
                        }

                        #region Selecting Field Permanent for DNA

                        bool PermanentDNASelection(Permanent permanent)
                        {
                            if (IsOwnerPermanentToBeDeletedCondition(permanent))
                            {
                                if (allowedPermanents.Contains(permanent))
                                    return true;
                            }

                            return false;
                        }

                        bool CardDNASelection(CardSource source)
                        {
                            if (IsLevel6HandCardCondition(source))
                            {
                                if (allowedCards.Contains(source))
                                    return true;
                            }

                            return false;
                        }

                        maxCount = Math.Min(1, allowedPermanents.Count);

                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: PermanentDNASelection,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount,
                            canNoSelect: false,
                            canEndNotMax: false,
                            selectPermanentCoroutine: SelectPermanentCoroutine,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to DNA digivolve.",
                            "The opponent is selecting 1 Digimon to DNA digivolve.");

                        await selectPermanentEffect.Activate();

                        #endregion

                        Task SelectPermanentCoroutine(Permanent permanent)
                        {
                            selectedPermanent = permanent;

                            return Task.CompletedTask;
                        }

                        if (selectedPermanent != null)
                        {
                            maxCount = Math.Min(1, allowedCards.Count);

                            SelectHandEffect selectHandDNAEffect = GManager.instance.GetComponent<SelectHandEffect>();

                            selectHandDNAEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CardDNASelection,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: maxCount,
                                canNoSelect: false,
                                canEndNotMax: false,
                                isShowOpponent: true,
                                selectCardCoroutine: SelectCardCoroutine,
                                afterSelectCardCoroutine: null,
                                mode: SelectHandEffect.Mode.Custom,
                                cardEffect: activateClass);

                            selectHandDNAEffect.SetUpCustomMessage("Select 1 Digimon to DNA digivolve.",
                                "The opponent is selecting 1 Digimon to DNA digivolve.");

                            await selectHandDNAEffect.Activate();

                            Task SelectCardCoroutine(CardSource cardSource)
                            {
                                selectedCardSource = cardSource;

                                return Task.CompletedTask;
                            }
                        }
                    }

                    if (selectedLevel7 != null && selectedPermanent != null && selectedCardSource != null)
                    {
                        // AS-IS :435-455 empty-frame search + `new Permanent{IsSuspended} + CreateNewPermanent(perm,
                        // frameID)`. FRAME ADAPTATION (RD-P6C1-1/-2 family — CreateNewPermanent's own doc): no
                        // slot/capacity model — the mirror CreateNewPermanent APPENDS the material to the battle-area
                        // zone and returns its Permanent VIEW; the AS-IS empty-frame search + the
                        // `0<=frameID<fieldCardFrames.Count` guard is the slot-capacity gate the frameless
                        // zone-append subsumes (a materialisation always succeeds here).
                        Permanent materialized = await CardObjectController.CreateNewPermanent(selectedCardSource, isSuspended: false);

                        if (selectedLevel7.CanJogressFromTargetPermanent(selectedPermanent, false))
                        {
                            int[] jogressEvoRootsFrameIDs =
                            {
                                selectedPermanent.PermanentFrame.FrameID, materialized.PermanentFrame.FrameID,
                            };

                            PlayCardClass playCard = new PlayCardClass(
                                cardSources: new List<CardSource>() { selectedLevel7 },
                                hashtable: CardEffectCommons.CardEffectHashtable(activateClass),
                                payCost: true,
                                targetPermanent: null,
                                isTapped: false,
                                root: SelectCardEffect.Root.Hand,
                                activateETB: true);

                            playCard.SetJogress(jogressEvoRootsFrameIDs);

                            await playCard.PlayCard();
                        }
                        else
                        {
                            // ROLLBACK (AS-IS :479): the jogress cannot proceed — un-materialise the temp material
                            // back to hand (the transient un-plays; the card returns to its origin zone).
                            await CardObjectController.AddHandCard(selectedCardSource, false);
                        }
                    }
                }
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
