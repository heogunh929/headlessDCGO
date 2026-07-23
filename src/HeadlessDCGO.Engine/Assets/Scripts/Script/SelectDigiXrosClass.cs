// Source: DCGO/Assets/Scripts/Script/SelectDigiXrosClass.cs (1048 lines)
// (P6C1 → RD-EXT3-01 / RD-R5-04) 1:1 mirror of the AS-IS SelectDigiXrosClass component — the DigiXros pre-play
// material-selection component the play pipeline resets/consults (PlayCardClass, CardController.cs:306/744-748/
// 791; the AS-IS cost pipeline also reads `playCard`/`selectedDigicrossCards` in
// CardSource.GetPayingCostWithBaseCost).
//
// Mirrored 1:1: the STATE surface (:12-32), the CanSelectDigiXros gate (:307-364), the interactive Select(card)
// area-loop (:368-880), and the two AddDigivolutiuonCards apply halves (:882-1013). The AddDigivolutionCardsInfo
// holder (:1033-1048) sits at the bottom.
//
// Substrate translations ONLY (logic / order / names unchanged, bigbang §5):
//   * IEnumerator -> async Task; `yield return ContinuousController.instance.StartCoroutine(X)` / `yield return
//     StartCoroutine(X)` -> `await X`; lone `yield return null` -> `Task.CompletedTask`; WaitForSeconds/WaitUntil
//     UI beats stripped.
//   * `GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer` -> `new GameContext(context)
//     .Players_ForTurnPlayer` (the established Permanent/Player mirror idiom).
//   * `card.Owner` (AS-IS Player) -> HeadlessPlayerId; `card.Owner.HandCards/TrashCards/GetBattleAreaPermanents()`
//     -> `new Player(card.Context, card.Owner).*` (the BT2_023 Player-handle idiom).
//   * `cardSource.PermanentOfThisCard()` (AS-IS Permanent, non-null-gated) -> `ICardEffect
//     .ResolvePermanentOfThisCard(cardSource)` (returns the real mirror Permanent, null when not a permanent —
//     the established LM_054/BT24_062 bridge; `PermanentOfThisCard()` alone returns a PermanentView).
//   * `card.digiXrosCondition` -> `card.digiXrosCondition`; `card.HasDigiXros` -> `card.HasDigiXros` (mirror
//     CardSource props).
//   * The AS-IS area-choice command panel (:480-543 GManager.commandText/selectCommandPanel + the you/opponent/AI
//     split + photonView.RPC SetTargetDigiXrossIndex + WaitUntil HasPlayerSelection + DequeuePlayerSelection<
//     ValueSelection>) IS ONE `ChoiceType.ModeChoice` request (min 1 / max 1, synthetic labeled options carrying
//     the action index, decoded off the id) — the SelectAppFusionEffect precedent (SelectAppFusionEffect.cs:91-108);
//     the provider is the single decider for you/opponent/AI. The AS-IS SelectedIndex==0 default is preserved.
//   * `cardSource.cEntity_EffectController.InitUseCountThisTurn()` -> the real mirror per-instance controller
//     method (CEntity_EffectController.cs:225). UI-only strips (ShowCardEffect/ShowCardEffect2/OffShowCard2 /
//     CreateDigiXrosSelectCardEffect / RemoveDigivolveRootEffect) are cited at their AS-IS anchors.
// RD-EXT3-05 (landed): the field-permanent material branch (`isBattleAreaCard`) of the two apply halves
//   (AS-IS :901-908 / :985-991) re-parents a battle-area material's TOP card UNDER the play card via
//   `IPlacePermanentToDigivolutionCards` — now mirrored 1:1 in CardController.cs (the re-parent rides
//   CardObjectController.RemoveField + Permanent.AddDigivolutionCardsBottom, NOT the PermanentFrame model as the
//   former STOP rationale over-cautiously assumed). The hand/trash/tamer/security material branches port 1:1 via
//   `Permanent.AddDigivolutionCardsBottom`.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
// Inside namespace ...Script the identifier `CardEffectCommons` binds to the SIBLING NAMESPACE, not the static
// class — alias per the SelectHandEffect.cs / SelectCardEffect.cs precedent.
using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;

public class SelectDigiXrosClass
{
    // AS-IS :12-15.
    public List<CardSource> selectedDigicrossCards { get; private set; } = new List<CardSource>();
    public List<AddDigivolutionCardsInfo> addDigivolutionCardInfos { get; private set; } = new List<AddDigivolutionCardsInfo>();
    public List<CardSource> excludedCards { get; private set; } = new List<CardSource>();
    public CardSource playCard { get; private set; } = null;

    private EngineContext? _context;

    /// <summary>(bridge W4) The match context the AS-IS <c>GManager.instance.GetComponent&lt;…&gt;()</c> route
    /// injects (mirrors SelectHandEffect.AttachContext). Registration on the mirror GManager sets this; a bare
    /// <c>new SelectDigiXrosClass()</c> resolves the context off the play card instead.</summary>
    internal void AttachContext(EngineContext context) => _context = context;

    private EngineContext ContextOf(CardSource card) => _context ?? card.Context;

    // AS-IS :17-22 (verbatim: excludedCards is deliberately NOT reset here).
    public void ResetSelectDigiXrosClass()
    {
        selectedDigicrossCards = new List<CardSource>();
        addDigivolutionCardInfos = new List<AddDigivolutionCardsInfo>();
        playCard = null;
    }

    // AS-IS :24-27.
    public void AddDigivolutionCardInfos(AddDigivolutionCardsInfo digivolutionCardsInfo)
    {
        addDigivolutionCardInfos.Add(digivolutionCardsInfo);
    }

    // AS-IS :29-32.
    public void SetExcludedCards(List<CardSource> excluded)
    {
        excludedCards = excluded;
    }

    #region Max Trash Count
    // AS-IS :35-126.
    int maxTrashCount(CardSource card)
    {
        int maxTrashCount = 0;

        if (card != null)
        {
            List<int> maxTrashCountList = new List<int>();
            EngineContext context = ContextOf(card);

            #region 選択可能トラッシュ枚数を変更する効果
            foreach (Player player in new GameContext(context).Players_ForTurnPlayer)
            {
                #region 場のパーマネントの効果
                foreach (Permanent permanent in player.GetBattleAreaPermanents())
                {
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IAddMaxTrashCountDigiXrosEffect)
                        {
                            if (cardEffect.CanUse(null))
                            {
                                int count = ((IAddMaxTrashCountDigiXrosEffect)cardEffect).GetMaxTrashCount(card);

                                if (count >= 1)
                                {
                                    maxTrashCountList.Add(count);
                                }
                            }
                        }
                    }
                }
                #endregion

                #region プレイヤーの効果
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IAddMaxTrashCountDigiXrosEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            int count = ((IAddMaxTrashCountDigiXrosEffect)cardEffect).GetMaxTrashCount(card);

                            if (count >= 1)
                            {
                                maxTrashCountList.Add(count);
                            }
                        }
                    }
                }
                #endregion
            }

            #region カード自身の効果
            if (ICardEffect.ResolvePermanentOfThisCard(card) == null)
            {
                foreach (ICardEffect cardEffect in card.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IAddMaxTrashCountDigiXrosEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            int count = ((IAddMaxTrashCountDigiXrosEffect)cardEffect).GetMaxTrashCount(card);

                            if (count >= 1)
                            {
                                maxTrashCountList.Add(count);
                            }
                        }
                    }
                }
            }
            #endregion

            #endregion

            if (maxTrashCountList.Count >= 1)
            {
                maxTrashCount = maxTrashCountList.Max();
            }

            int trashCount = new Player(context, card.Owner).TrashCards.Count;
            if (maxTrashCount > trashCount)
            {
                maxTrashCount = trashCount;
            }

            if (maxTrashCount < 0)
            {
                maxTrashCount = 0;
            }
        }

        return maxTrashCount;
    }
    #endregion

    #region Max Tamer Digivolution Cards Count
    // AS-IS :129-217.
    int maxTamerDigivolutionCardsCount(CardSource card)
    {
        int maxTamerDigivolutionCardsCount = 0;

        if (card != null)
        {
            List<int> maxTamerDigivolutionCardsCountList = new List<int>();
            EngineContext context = ContextOf(card);

            #region 選択可能テイマー進化元枚数を変更する効果
            foreach (Player player in new GameContext(context).Players_ForTurnPlayer)
            {
                #region 場のパーマネントの効果
                foreach (Permanent permanent in player.GetBattleAreaPermanents())
                {
                    foreach (ICardEffect cardEffect in permanent.EffectList(EffectTiming.None))
                    {
                        if (cardEffect is IAddMaxUnderTamerCountDigiXrosEffect)
                        {
                            if (cardEffect.CanUse(null))
                            {
                                int count = ((IAddMaxUnderTamerCountDigiXrosEffect)cardEffect).getMaxUnderTamerCount(card);

                                if (count >= 1)
                                {
                                    maxTamerDigivolutionCardsCountList.Add(count);
                                }
                            }
                        }
                    }
                }
                #endregion

                #region プレイヤーの効果
                foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IAddMaxUnderTamerCountDigiXrosEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            int count = ((IAddMaxUnderTamerCountDigiXrosEffect)cardEffect).getMaxUnderTamerCount(card);

                            if (count >= 1)
                            {
                                maxTamerDigivolutionCardsCountList.Add(count);
                            }
                        }
                    }
                }
                #endregion
            }

            #region カード自身の効果
            if (ICardEffect.ResolvePermanentOfThisCard(card) == null)
            {
                foreach (ICardEffect cardEffect in card.EffectList(EffectTiming.None))
                {
                    if (cardEffect is IAddMaxUnderTamerCountDigiXrosEffect)
                    {
                        if (cardEffect.CanUse(null))
                        {
                            int count = ((IAddMaxUnderTamerCountDigiXrosEffect)cardEffect).getMaxUnderTamerCount(card);

                            if (count >= 1)
                            {
                                maxTamerDigivolutionCardsCountList.Add(count);
                            }
                        }
                    }
                }
            }
            #endregion

            #endregion

            if (maxTamerDigivolutionCardsCountList.Count >= 1)
            {
                maxTamerDigivolutionCardsCount = maxTamerDigivolutionCardsCountList.Max();
            }

            if (maxTamerDigivolutionCardsCount < 0)
            {
                maxTamerDigivolutionCardsCount = 0;
            }
        }

        return maxTamerDigivolutionCardsCount;
    }
    #endregion

    #region Is Hand Card
    // AS-IS :219-232.
    bool isHandCard(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (new Player(ContextOf(cardSource), cardSource.Owner).HandCards.Contains(cardSource))
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    #region Is Battle Area Card
    // AS-IS :234-253.
    bool isBattleAreaCard(CardSource cardSource)
    {
        if (cardSource != null)
        {
            Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);
            if (permanent != null)
            {
                if (new Player(ContextOf(cardSource), cardSource.Owner).GetBattleAreaPermanents().Contains(permanent))
                {
                    if (cardSource == permanent.TopCard)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
    #endregion

    #region Is Trash Card
    // AS-IS :255-268.
    bool isTrashCard(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (Commons.IsExistOnTrash(cardSource))
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    #region Is Security Card
    // AS-IS :270-283.
    bool isSecurityCard(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (Commons.IsExistInSecurity(cardSource, false))
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    #region is Tamer Digivolution Card
    // AS-IS :285-304.
    bool isTamerDigivolutionCard(CardSource cardSource)
    {
        if (cardSource != null)
        {
            Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);
            if (permanent != null)
            {
                if (permanent.IsTamer)
                {
                    if (permanent.DigivolutionCards.Contains(cardSource))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
    #endregion

    #region Can Select DigiXros
    // AS-IS :307-364.
    bool CanSelectDigiXros(DigiXrosConditionElement element, CardSource targetCard, CardSource card)
    {
        if (excludedCards.Contains(targetCard))
            return false;

        if (card != targetCard)
        {
            if (card != null)
            {
                if (element != null)
                {
                    if (element.CardCondition != null)
                    {
                        if (targetCard != null)
                        {
                            if (card.digiXrosCondition != null)
                            {
                                if (!targetCard.IsToken)
                                {
                                    if (!selectedDigicrossCards.Contains(targetCard))
                                    {
                                        if (addDigivolutionCardInfos.Count((addDigivolutionCardInfo) => addDigivolutionCardInfo.cardSources.Contains(targetCard)) == 0)
                                        {
                                            if (card.digiXrosCondition.CanTargetCondition_ByPreSelecetedList != null)
                                            {
                                                if (!card.digiXrosCondition.CanTargetCondition_ByPreSelecetedList(selectedDigicrossCards, targetCard))
                                                {
                                                    return false;
                                                }
                                            }

                                            if (element.CardCondition(targetCard))
                                            {
                                                return true;
                                            }

                                            Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(targetCard);
                                            if (targetPermanent != null)
                                            {
                                                if (targetCard == targetPermanent.TopCard)
                                                {
                                                    if (targetPermanent.CanSubstituteForDigiXrosCondition(card))
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
    #endregion

    #region Select
    // AS-IS :368-572.
    public async Task Select(CardSource card)
    {
        selectedDigicrossCards = new List<CardSource>();

        playCard = card;

        if (card != null)
        {
            if (card.HasDigiXros)
            {
                EngineContext context = ContextOf(card);
                DigiXrosCondition digiXrosCondition = card.digiXrosCondition;
                Player owner = new Player(context, card.Owner);

                foreach (DigiXrosConditionElement element in digiXrosCondition.elements)
                {
                    // AS-IS :383-386 ShowCardEffect2 "DigiXros Cards" = UI (stripped).

                    if (_endSelectDigiXros)
                    {
                        _endSelectDigiXros = false;
                        //break; (design item RD-EXT3-01) AS-IS comment: Removed for not triggering digixros in all situations
                    }

                    bool canSelectHand = false;

                    if (owner.HandCards.Count((cardSource) => CanSelectDigiXros(element, cardSource, card)) >= 1)
                    {
                        canSelectHand = true;
                    }

                    bool canSelectField = false;

                    if (Commons.HasMatchConditionOwnersPermanent(card, (permanent) => CanSelectDigiXros(element, permanent.TopCard, card)))
                    {
                        canSelectField = true;
                    }

                    bool canSelectTrash = false;

                    if (Commons.HasMatchConditionOwnersCardInTrash(card, (cardSource) => CanSelectDigiXros(element, cardSource, card)))
                    {
                        if (selectedDigicrossCards.Count(isTrashCard) < maxTrashCount(card))
                        {
                            canSelectTrash = true;
                        }
                    }

                    bool canSelectTamer = false;

                    if (Commons.HasMatchConditionOwnersPermanent(card, (permanent) => permanent.IsTamer && permanent.DigivolutionCards.Count((cardSource) => CanSelectDigiXros(element, cardSource, card)) >= 1))
                    {
                        if (selectedDigicrossCards.Count(isTamerDigivolutionCard) < maxTamerDigivolutionCardsCount(card))
                        {
                            canSelectTamer = true;
                        }
                    }

                    Func<Task> _SelectHandCard = () => SelectHandCard(digiXrosCondition, element, card);
                    Func<Task> _SelectBattleAreaDigimon = () => SelectBattleAreaPermanent(digiXrosCondition, element, card);
                    Func<Task> _SelectTrashCard = () => SelectTrashCard(digiXrosCondition, element, card);
                    Func<Task> _SelectTamerDigivolutionCard = () => SelectTamerDigivolutionCard(digiXrosCondition, element, card);
                    Func<Task> _EndSelectDigiXros = () => EndSelectDigiXros();

                    List<Func<Task>> actions = new List<Func<Task>>() { _SelectHandCard, _SelectBattleAreaDigimon, _SelectTrashCard, _SelectTamerDigivolutionCard, _EndSelectDigiXros };

                    List<Func<Task>> canSelectActions = new List<Func<Task>>();

                    if (canSelectHand)
                    {
                        canSelectActions.Add(_SelectHandCard);
                    }

                    if (canSelectField)
                    {
                        canSelectActions.Add(_SelectBattleAreaDigimon);
                    }

                    if (canSelectTrash)
                    {
                        canSelectActions.Add(_SelectTrashCard);
                    }

                    if (canSelectTamer)
                    {
                        canSelectActions.Add(_SelectTamerDigivolutionCard);
                    }

                    canSelectActions.Add(_EndSelectDigiXros);

                    if (canSelectActions.Count == 1)
                    {
                        if (digiXrosCondition.CanTargetCondition_ByPreSelecetedList != null || element.skipAllIfNoSelect)
                        {
                            break;
                        }

                        else
                        {
                            continue;
                        }
                    }

                    // AS-IS :473-543 area choice: count==2 auto-pick; else the ModeChoice panel (you/opponent/AI
                    // all collapse to the one provider request — SelectAppFusion precedent).
                    if (canSelectActions.Count == 2 && digiXrosCondition.CanTargetCondition_ByPreSelecetedList == null && !element.skipAllIfNoSelect)
                    {
                        _targetIndex = actions.IndexOf(canSelectActions[0]);
                    }
                    else
                    {
                        var candidates = new List<ChoiceCandidate>();
                        for (int i = 0; i < canSelectActions.Count; i++)
                        {
                            int k = actions.IndexOf(canSelectActions[i]);
                            string message = k switch
                            {
                                0 => "Hand",
                                1 => "Field Digimon",
                                2 => "Trash",
                                3 => "Tamer digivolution cards",
                                4 => "End Selection",
                                _ => "",
                            };
                            candidates.Add(new ChoiceCandidate(new HeadlessEntityId($"digiXros#{k}"), message, ChoiceZone.BattleArea, IsSelectable: true, ownerId: card.Owner));
                        }

                        var request = new ChoiceRequest(
                            ChoiceType.ModeChoice, card.Owner, $"From which area will you select {element.selectMessage}?",
                            minCount: 1, maxCount: 1, canSkip: false, ChoiceZone.BattleArea, candidates);
                        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);

                        _targetIndex = 0;   // AS-IS SelectedIndex default (:548 `seletion != null ? … : 0`).
                        if (!result.IsSkipped && result.SelectedIds.Count > 0)
                        {
                            string[] parts = result.SelectedIds[0].Value.Split('#');
                            if (int.TryParse(parts.Length > 1 ? parts[1] : null, out int picked))
                            {
                                _targetIndex = picked;
                            }
                        }
                    }

                    // AS-IS :553-561.
                    if (0 <= _targetIndex && _targetIndex <= actions.Count - 1)
                    {
                        await actions[_targetIndex]().ConfigureAwait(false);
                    }
                }
            }
        }

        // AS-IS :566-571 ShowCard UI beats (stripped).
    }
    #endregion

    #region Select Hand Card
    // AS-IS :575-631.
    async Task SelectHandCard(DigiXrosCondition digiXrosCondition, DigiXrosConditionElement element, CardSource card)
    {
        bool CanSelectCardCondition(CardSource cardSource) => CanSelectDigiXros(element, cardSource, card);

        if (new Player(ContextOf(card), card.Owner).HandCards.Count(CanSelectCardCondition) >= 1)
        {
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
                afterSelectCardCoroutine: AfterSelectCardCoroutine,
                mode: SelectHandEffect.Mode.Custom,
                cardEffect: null);

            selectHandEffect.SetUpCustomMessage($"Select {element.selectMessage}.", $"The opponent is selecting {element.selectMessage}.");
            selectHandEffect.SetUpCustomMessage_ShowCard("Selected Hand Card");
            selectHandEffect.SetDigiXros();

            await selectHandEffect.Activate().ConfigureAwait(false);

            Task SelectCardCoroutine(CardSource cardSource)
            {
                selectedDigicrossCards.Add(cardSource);

                return Task.CompletedTask;
            }

            Task AfterSelectCardCoroutine(List<CardSource> cardSources)
            {
                if (cardSources.Count == 0)
                {
                    if (digiXrosCondition != null)
                    {
                        if (digiXrosCondition.CanTargetCondition_ByPreSelecetedList != null || element.skipAllIfNoSelect)
                        {
                            _ = EndSelectDigiXros();
                        }
                    }
                }

                return Task.CompletedTask;
            }
        }
    }
    #endregion

    #region Select Battle Area Permanent
    // AS-IS :633-689.
    async Task SelectBattleAreaPermanent(DigiXrosCondition digiXrosCondition, DigiXrosConditionElement element, CardSource card)
    {
        bool CanSelectPermanentCondition(Permanent permanent) =>
            new Player(ContextOf(card), permanent.TopCard.Owner).GetBattleAreaPermanents().Contains(permanent)
            && CanSelectDigiXros(element, permanent.TopCard, card);

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("", null, card);

        if (Commons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
        {
            int maxCount = 1;

            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

            selectPermanentEffect.SetUp(
                selectPlayer: card.Owner,
                canTargetCondition: CanSelectPermanentCondition,
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                maxCount: maxCount,
                canNoSelect: true,
                canEndNotMax: false,
                selectPermanentCoroutine: SelectPermanentCoroutine,
                afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                mode: SelectPermanentEffect.Mode.Custom,
                cardEffect: activateClass);

            selectPermanentEffect.SetUpCustomMessage($"Select {element.selectMessage}.", $"The opponent is selecting {element.selectMessage}.");
            selectPermanentEffect.SetDigiXros();

            await selectPermanentEffect.Activate().ConfigureAwait(false);

            Task SelectPermanentCoroutine(Permanent permanent)
            {
                selectedDigicrossCards.Add(permanent.TopCard);

                return Task.CompletedTask;
            }

            Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
            {
                if (permanents.Count == 0)
                {
                    if (digiXrosCondition != null)
                    {
                        if (digiXrosCondition.CanTargetCondition_ByPreSelecetedList != null || element.skipAllIfNoSelect)
                        {
                            _ = EndSelectDigiXros();
                        }
                    }
                }

                return Task.CompletedTask;
            }
        }
    }
    #endregion

    #region Select Tamer Digivolution Card
    // AS-IS :691-810.
    async Task SelectTamerDigivolutionCard(DigiXrosCondition digiXrosCondition, DigiXrosConditionElement element, CardSource card)
    {
        EngineContext context = ContextOf(card);

        bool CanSelectCardCondition(CardSource cardSource) => CanSelectDigiXros(element, cardSource, card);

        bool CanSelectPermanentCondition(Permanent permanent) =>
            permanent.TopCard.Owner == card.Owner && permanent.IsTamer && permanent.DigivolutionCards.Count(CanSelectCardCondition) >= 1;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("", null, card);

        if (new Player(context, card.Owner).GetBattleAreaPermanents().Count(CanSelectPermanentCondition) >= 1)
        {
            Permanent selectedPermanent = null;

            int maxCount = 1;

            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

            selectPermanentEffect.SetUp(
                selectPlayer: card.Owner,
                canTargetCondition: CanSelectPermanentCondition,
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                maxCount: maxCount,
                canNoSelect: true,
                canEndNotMax: false,
                selectPermanentCoroutine: SelectPermanentCoroutine,
                afterSelectPermanentCoroutine: AfterSelectPermanentCoroutine,
                mode: SelectPermanentEffect.Mode.Custom,
                cardEffect: activateClass);

            selectPermanentEffect.SetUpCustomMessage($"Select a Tamer that has {element.selectMessage} in digivolution cards.", $"The opponent is selecting a Tamer that has {element.selectMessage} in digivolution cards.");
            selectPermanentEffect.SetDigiXros();

            await selectPermanentEffect.Activate().ConfigureAwait(false);

            Task SelectPermanentCoroutine(Permanent permanent)
            {
                selectedPermanent = permanent;

                return Task.CompletedTask;
            }

            Task AfterSelectPermanentCoroutine(List<Permanent> permanents)
            {
                if (permanents.Count == 0)
                {
                    if (digiXrosCondition != null)
                    {
                        if (digiXrosCondition.CanTargetCondition_ByPreSelecetedList != null || element.skipAllIfNoSelect)
                        {
                            _ = EndSelectDigiXros();
                        }
                    }
                }

                return Task.CompletedTask;
            }

            if (selectedPermanent != null)
            {
                if (selectedPermanent.DigivolutionCards.Count >= 1)
                {
                    maxCount = 1;

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                                canTargetCondition: CanSelectCardCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                canNoSelect: () => true,
                                selectCardCoroutine: SelectCardCoroutine,
                                afterSelectCardCoroutine: AfterSelectCardCoroutine,
                                message: $"<color=#FF633E>DigiXros</color>: Select {element.selectMessage}.",
                                maxCount: maxCount,
                                canEndNotMax: false,
                                isShowOpponent: true,
                                mode: SelectCardEffect.Mode.Custom,
                                root: SelectCardEffect.Root.Custom,
                                customRootCardList: selectedPermanent.DigivolutionCards.ToList(),
                                canLookReverseCard: true,
                                selectPlayer: card.Owner,
                                cardEffect: null);

                    selectCardEffect.SetUpCustomMessage($"Select {element.selectMessage}.", $"The opponent is selecting {element.selectMessage}.");
                    selectCardEffect.SetUpCustomMessage_ShowCard("Selected Digivolution Card");
                    selectCardEffect.SetDigiXros();

                    await selectCardEffect.Activate().ConfigureAwait(false);

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedDigicrossCards.Add(cardSource);

                        return Task.CompletedTask;
                    }

                    Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                    {
                        if (cardSources.Count == 0)
                        {
                            if (digiXrosCondition != null)
                            {
                                if (digiXrosCondition.CanTargetCondition_ByPreSelecetedList != null || element.skipAllIfNoSelect)
                                {
                                    _ = EndSelectDigiXros();
                                }
                            }
                        }

                        return Task.CompletedTask;
                    }
                }
            }
        }
    }
    #endregion

    #region Select Trash Card
    // AS-IS :812-871.
    async Task SelectTrashCard(DigiXrosCondition digiXrosCondition, DigiXrosConditionElement element, CardSource card)
    {
        bool CanSelectCardCondition(CardSource cardSource) => CanSelectDigiXros(element, cardSource, card);

        if (Commons.HasMatchConditionOwnersCardInTrash(card, (cardSource) => CanSelectCardCondition(cardSource)))
        {
            int maxCount = 1;

            SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

            selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: AfterSelectCardCoroutine,
                        message: $"<color=#FF633E>DigiXros</color>: Select {element.selectMessage} from trash.",
                        maxCount: maxCount,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: null);

            selectCardEffect.SetUpCustomMessage($"Select {element.selectMessage}.", $"The opponent is selecting {element.selectMessage}.");
            selectCardEffect.SetUpCustomMessage_ShowCard("Selected Trash Card");
            selectCardEffect.SetDigiXros();

            await selectCardEffect.Activate().ConfigureAwait(false);

            Task SelectCardCoroutine(CardSource cardSource)
            {
                selectedDigicrossCards.Add(cardSource);

                return Task.CompletedTask;
            }

            Task AfterSelectCardCoroutine(List<CardSource> cardSources)
            {
                if (cardSources.Count == 0)
                {
                    if (digiXrosCondition != null)
                    {
                        if (digiXrosCondition.CanTargetCondition_ByPreSelecetedList != null || element.skipAllIfNoSelect)
                        {
                            _ = EndSelectDigiXros();
                        }
                    }
                }

                return Task.CompletedTask;
            }
        }
    }
    #endregion

    #region End Select DigiXros
    // AS-IS :873-879.
    Task EndSelectDigiXros()
    {
        _endSelectDigiXros = true;
        return Task.CompletedTask;
    }
    #endregion

    #region Add Digivolution Cards
    // AS-IS :881-933.
    public async Task AddDigivolutiuonCards(CardSource card)
    {
        if (selectedDigicrossCards.Count >= 1)
        {
            if (card != null)
            {
                if (card == playCard)
                {
                    Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(card);
                    if (permanent != null)
                    {
                        // AS-IS :892 ShowCardEffect "Digixros Cards" = UI (stripped).

                        foreach (CardSource cardSource in selectedDigicrossCards)
                        {
                            if (isHandCard(cardSource))
                            {
                                await permanent.AddDigivolutionCardsBottom(new List<CardSource>() { cardSource }, null).ConfigureAwait(false);
                            }

                            else if (isBattleAreaCard(cardSource))
                            {
                                // AS-IS :901-908 (RD-EXT3-05 landed): CreateDigiXrosSelectCardEffect (UI, stripped)
                                // + IPlacePermanentToDigivolutionCards re-parents this battle-area permanent's top
                                // card under the play permanent. Substrate: PermanentOfThisCard() →
                                // ICardEffect.ResolvePermanentOfThisCard(...) (card's = the resolved `permanent`).
                                IPlacePermanentToDigivolutionCards placePermanentToDigivolutionCards = new IPlacePermanentToDigivolutionCards(new List<Permanent[]>() { new Permanent[] { ICardEffect.ResolvePermanentOfThisCard(cardSource), permanent } }, false, null, isDigixros: true);
                                placePermanentToDigivolutionCards.SetNotShowCards();
                                await placePermanentToDigivolutionCards.PlacePermanentToDigivolutionCards().ConfigureAwait(false);
                            }

                            else if (isTrashCard(cardSource))
                            {
                                // AS-IS :910-915: CreateDigiXrosSelectCardEffect (UI) + AddDigivolutionCardsBottom.
                                await permanent.AddDigivolutionCardsBottom(new List<CardSource>() { cardSource }, null).ConfigureAwait(false);
                            }

                            else if (isTamerDigivolutionCard(cardSource))
                            {
                                // AS-IS :917-921: RemoveDigivolveRootEffect (UI) + AddDigivolutionCardsBottom.
                                await permanent.AddDigivolutionCardsBottom(new List<CardSource>() { cardSource }, null).ConfigureAwait(false);
                            }

                            cardSource.cEntity_EffectController.InitUseCountThisTurn();
                        }
                    }
                }
            }
        }

        ResetSelectDigiXrosClass();
    }
    #endregion

    #region 効果によって進化元を付与(天野ユウ(BT10),シャウトモンX7スペリオルモード(BT12))
    // AS-IS :936-1013.
    public async Task AddDigivolutiuonCardsByEffect(CardSource card)
    {
        if (addDigivolutionCardInfos.Count >= 1)
        {
            if (card != null)
            {
                Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(card);
                if (permanent != null)
                {
                    List<CardSource> addedCards = new List<CardSource>();

                    foreach (AddDigivolutionCardsInfo info in addDigivolutionCardInfos)
                    {
                        List<CardSource> underTamerCards = new List<CardSource>();
                        List<Permanent> digimonPermanents = new List<Permanent>();
                        List<CardSource> trashCards = new List<CardSource>();
                        List<CardSource> secuirtyCards = new List<CardSource>();

                        foreach (CardSource cardSource in info.cardSources)
                        {
                            if (isTamerDigivolutionCard(cardSource))
                            {
                                underTamerCards.Add(cardSource);
                                addedCards.Add(cardSource);
                            }

                            else if (isBattleAreaCard(cardSource))
                            {
                                digimonPermanents.Add(ICardEffect.ResolvePermanentOfThisCard(cardSource));
                                addedCards.Add(cardSource);
                            }
                            else if (isTrashCard(cardSource))
                            {
                                trashCards.Add(cardSource);
                                addedCards.Add(cardSource);
                            }

                            else if (isSecurityCard(cardSource))
                            {
                                secuirtyCards.Add(cardSource);
                                addedCards.Add(cardSource);
                            }
                        }

                        HeadlessEntityId? cause = info.cardEffect?.EffectSourceCard?.InstanceId;

                        if (underTamerCards.Count >= 1)
                        {
                            await permanent.AddDigivolutionCardsBottom(underTamerCards, cause).ConfigureAwait(false);
                        }

                        if (digimonPermanents.Count >= 1)
                        {
                            // AS-IS :985-991 (RD-EXT3-05 landed): one IPlacePermanentToDigivolutionCards per
                            // battle-area material permanent, re-parenting its top card under the play permanent.
                            foreach (Permanent digimonPermanent in digimonPermanents)
                            {
                                await new IPlacePermanentToDigivolutionCards(new List<Permanent[]>() { new Permanent[] { digimonPermanent, permanent } }, false, info.cardEffect, isDigixros: true).PlacePermanentToDigivolutionCards().ConfigureAwait(false);
                            }
                        }

                        if (trashCards.Count >= 1)
                        {
                            await permanent.AddDigivolutionCardsBottom(trashCards, cause).ConfigureAwait(false);
                        }

                        if (secuirtyCards.Count >= 1)
                        {
                            await permanent.AddDigivolutionCardsBottom(secuirtyCards, cause).ConfigureAwait(false);
                        }
                    }

                    // AS-IS :1004 ShowCardEffect2 "Digivolution Cards" = UI (stripped).
                }
            }
        }

        addDigivolutionCardInfos = new List<AddDigivolutionCardsInfo>();
    }
    #endregion

    // AS-IS :1015-1030 (_targetIndex / _endSelectDigiXros / the RPC SetTargetDigiXrossIndex — the RPC is subsumed
    // by the ChoiceType.ModeChoice request in Select).
    int _targetIndex = 0;

    bool _endSelectDigiXros = false;
}

/// <summary>(P6C1) 1:1 mirror of AS-IS <c>AddDigivolutionCardsInfo</c> (SelectDigiXrosClass.cs:1033-1048): one
/// "these cards were stacked under by this effect" record of the DigiXros/Assembly stacking bookkeeping.</summary>
public class AddDigivolutionCardsInfo
{
    public AddDigivolutionCardsInfo(ICardEffect cardEffect, List<CardSource> cardSources)
    {
        this.cardEffect = cardEffect;
        this.cardSources = new List<CardSource>();

        foreach (CardSource cardSource in cardSources)
        {
            this.cardSources.Add(cardSource);
        }
    }

    public ICardEffect cardEffect { get; private set; } = null;
    public List<CardSource> cardSources { get; private set; } = new List<CardSource>();
}
