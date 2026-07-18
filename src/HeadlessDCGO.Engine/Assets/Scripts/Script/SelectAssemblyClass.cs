// Source: Assets/Scripts/Script/SelectAssemblyClass.cs
// Decision: PORT
// Category: AIUseful
// Priority: MEDIUM
// Migration: Port core engine source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
// Inside namespace ...Script the identifier `CardEffectCommons` binds to the SIBLING NAMESPACE, not the static
// class — alias per the SelectHandEffect.cs / SelectDigiXrosClass.cs precedent.
using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;

/// <summary>
/// (AD1-A → RD-EXT3-02) Mirror of the AS-IS <c>SelectAssemblyClass</c>. Two co-resident surfaces:
/// <list type="bullet">
/// <item>The STATIC FEASIBILITY / matching half the LIVE parameterized play path (PlayCardAction) rides —
/// <see cref="TryMatchMaterials"/> (mirror of <c>CanFulfillConditions</c>, SelectAssemblyClass.cs:110-160:
/// per-element BACKTRACKING assignment over the OWNER'S TRASH, honoring
/// <c>CanTargetCondition_ByPreSelecetedList</c>, distinct cards), <see cref="CanFulfillConditions(EngineContext,
/// CardSource, AssemblyCondition)"/>, <see cref="ValidateMaterials"/>.</item>
/// <item>The AS-IS INSTANCE component surface the component play pipeline (PlayCardClass, CardController.cs:753-761)
/// reaches via <c>GManager.GetComponent&lt;SelectAssemblyClass&gt;()</c> — the state fields + <see cref="Select"/>
/// (the interactive per-element trash pick) + the <see cref="AddDigivolutiuonCards"/> apply half, mirrored 1:1 with
/// substrate translations only (IEnumerator→Task; UI/Photon strips; <c>card.PermanentOfThisCard()</c>→
/// <c>ICardEffect.ResolvePermanentOfThisCard</c>; <c>card.Owner.TrashCards</c>→<c>new Player(ctx, card.Owner)
/// .TrashCards</c>). Field-permanent substitution now resolves via the ported
/// <c>Permanent.CanSubstituteForAssemblyCondition</c> (Permanent.cs).</item>
/// </list>
/// </summary>
public class SelectAssemblyClass
{
    // ===== (RD-EXT3-02) AS-IS instance surface (SelectAssemblyClass.cs:12-357) =====

    // AS-IS :12-15.
    public List<CardSource> selectedAssemblyCards { get; private set; } = new List<CardSource>();
    public List<AddDigivolutionCardsInfo> addDigivolutionCardInfos { get; private set; } = new List<AddDigivolutionCardsInfo>();
    public List<CardSource> excludedCards { get; private set; } = new List<CardSource>();
    public CardSource playCard { get; private set; } = null;

    private EngineContext? _instanceContext;

    /// <summary>(RD-EXT3-02) The match context the AS-IS <c>GManager.GetComponent&lt;…&gt;()</c> route injects
    /// (mirrors SelectDigiXrosClass.AttachContext). A bare <c>new SelectAssemblyClass()</c> resolves off the
    /// play card instead.</summary>
    internal void AttachContext(EngineContext context) => _instanceContext = context;

    private EngineContext ContextOf(CardSource card) => _instanceContext ?? card.Context;

    // AS-IS :17-22.
    public void ResetSelectAssemblyClass()
    {
        selectedAssemblyCards = new List<CardSource>();
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

    #region Is Trash Card
    // AS-IS :34-47.
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

    #region Can Select Assembly
    // AS-IS :49-108.
    bool CanSelectAssembly(AssemblyConditionElement element, CardSource targetCard, CardSource card)
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
                            if (card.AssemblyConditionOf() != null)
                            {
                                if (!targetCard.IsToken)
                                {
                                    if (!selectedAssemblyCards.Contains(targetCard))
                                    {
                                        if (addDigivolutionCardInfos.Count((addDigivolutionCardInfo) => addDigivolutionCardInfo.cardSources.Contains(targetCard)) == 0)
                                        {
                                            if (element.CanTargetCondition_ByPreSelecetedList != null)
                                            {
                                                if (!element.CanTargetCondition_ByPreSelecetedList(selectedAssemblyCards, targetCard))
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
                                                    if (targetPermanent.CanSubstituteForAssemblyCondition(card))
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

    #region CanFulfillConditions (instance)
    // AS-IS :110-129.
    bool CanFulfillConditions(CardSource card)
    {
        if (card != null)
        {
            if (card.HasAssembly)
            {
                AssemblyCondition AssemblyCondition = card.AssemblyConditionOf();

                if (AssemblyCondition.elements != null)
                {
                    if (AssemblyCondition.elements.Count == 1)
                        return Commons.MatchConditionOwnersCardCountInTrash(card, (cardSource) => CanSelectAssembly(AssemblyCondition.elements[0], cardSource, card)) >= AssemblyCondition.elementCount;
                    else
                        return CanFulfillEachElementCondition(card, AssemblyCondition);
                }
            }
        }
        return false;
    }

    // AS-IS :131-160.
    bool CanFulfillEachElementCondition(CardSource card, AssemblyCondition AssemblyCondition, int index = 0, List<CardSource> usedCards = null, int currentElementCount = 0)
    {
        if (index < 0 || AssemblyCondition.elements == null)
            return false;
        if (index >= AssemblyCondition.elements.Count)
            return true;

        usedCards ??= new List<CardSource>();

        AssemblyConditionElement currentElement = AssemblyCondition.elements[index];

        List<CardSource> validCards = new Player(ContextOf(card), card.Owner).TrashCards.Where(currentElement.CardCondition).Except(usedCards).ToList();

        foreach (CardSource validCard in validCards)
        {
            List<CardSource> addedSoFar = new List<CardSource>() { validCard };
            addedSoFar.AddRange(usedCards);
            if (currentElementCount + 1 >= currentElement.ElementCount)
            {
                if (CanFulfillEachElementCondition(card, AssemblyCondition, index + 1, addedSoFar, 0))
                    return true;
            }
            else
            {
                if (CanFulfillEachElementCondition(card, AssemblyCondition, index, addedSoFar, currentElementCount + 1))
                    return true;
            }
        }
        return false;
    }
    #endregion

    #region Select
    // AS-IS :163-209.
    public async Task Select(CardSource card)
    {
        selectedAssemblyCards = new List<CardSource>();

        playCard = card;

        if (CanFulfillConditions(card))
        {
            AssemblyCondition AssemblyCondition = card.AssemblyConditionOf();

            foreach (AssemblyConditionElement element in AssemblyCondition.elements)
            {
                // AS-IS :177-180 ShowCardEffect2 "Assembly Cards" = UI (stripped).

                if (_endSelectAssembly)
                {
                    _endSelectAssembly = false;
                    //break; (design item RD-EXT3-02) AS-IS comment: Removed for not triggering Assembly in all situations
                }

                bool canSelectTrash = false;

                if (Commons.MatchConditionOwnersCardCountInTrash(card, (cardSource) => CanSelectAssembly(element, cardSource, card)) >= element.ElementCount)
                {
                    canSelectTrash = true;
                }


                if (canSelectTrash)
                {
                    await SelectTrashCard(element, card).ConfigureAwait(false);
                }
            }
        }

        // AS-IS :203-208 ShowCard UI beats (stripped).
    }
    #endregion

    #region Select Trash Card
    // AS-IS :212-271.
    async Task SelectTrashCard(AssemblyConditionElement AssemblyConditionElement, CardSource card)
    {
        bool CanSelectCardCondition(CardSource cardSource) => CanSelectAssembly(AssemblyConditionElement, cardSource, card);

        if (Commons.MatchConditionOwnersCardCountInTrash(card, CanSelectCardCondition) >= AssemblyConditionElement.ElementCount)
        {
            int maxCount = AssemblyConditionElement.ElementCount;

            SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

            selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: AssemblyConditionElement.CanTargetCondition_ByPreSelecetedList,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: AfterSelectCardCoroutine,
                        message: $"<color=#FF633E>Assembly</color>: Select {AssemblyConditionElement.selectMessage} from trash.",
                        maxCount: maxCount,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.Trash,
                        customRootCardList: new Player(ContextOf(card), card.Owner).TrashCards.Except(selectedAssemblyCards).ToList(),//don't include cards already chosen
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: null);

            selectCardEffect.SetUpCustomMessage($"Select {AssemblyConditionElement.selectMessage}.", $"The opponent is selecting {AssemblyConditionElement.selectMessage}.");
            selectCardEffect.SetUpCustomMessage_ShowCard("Selected Trash Card");
            selectCardEffect.SetAssembly();

            await selectCardEffect.Activate().ConfigureAwait(false);

            Task SelectCardCoroutine(CardSource cardSource)
            {
                selectedAssemblyCards.Add(cardSource);

                return Task.CompletedTask;
            }

            Task AfterSelectCardCoroutine(List<CardSource> cardSources)
            {
                if (cardSources.Count == 0)
                {
                    if (AssemblyConditionElement != null)
                    {
                        if (AssemblyConditionElement.CanTargetCondition_ByPreSelecetedList != null || AssemblyConditionElement.skipAllIfNoSelect)
                        {
                            _ = EndSelectAssembly();
                        }
                    }
                }

                return Task.CompletedTask;
            }
        }
    }
    #endregion

    #region End Select Assembly
    // AS-IS :273-279.
    Task EndSelectAssembly()
    {
        _endSelectAssembly = true;
        return Task.CompletedTask;
    }
    #endregion

    #region Add Digivolution Cards
    // AS-IS :281-312.
    public async Task AddDigivolutiuonCards(CardSource card)
    {
        if (card != null)
        {
            if (card == playCard)
            {
                Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(card);
                if (permanent != null)
                {
                    if (selectedAssemblyCards.Count == playCard.AssemblyConditionOf().elementCount)
                    {
                        // AS-IS :292 ShowCardEffect "Assembly Cards" = UI (stripped).

                        foreach (CardSource cardSource in selectedAssemblyCards)
                        {
                            if (isTrashCard(cardSource))
                            {
                                // AS-IS :298 CreateAssemblySelectCardEffect = UI (stripped).
                                await permanent.AddDigivolutionCardsBottom(new List<CardSource>() { cardSource }, null).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        ResetSelectAssemblyClass();
    }
    #endregion

    #region Add Digivolution Card
    // AS-IS :314-355.
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

                        foreach (CardSource cardSource in info.cardSources)
                        {
                            if (isTrashCard(cardSource))
                            {
                                trashCards.Add(cardSource);
                                addedCards.Add(cardSource);
                            }
                        }

                        if (trashCards.Count >= 1)
                        {
                            await permanent.AddDigivolutionCardsBottom(trashCards, info.cardEffect?.EffectSourceCard?.InstanceId).ConfigureAwait(false);
                        }
                    }

                    // AS-IS :346 ShowCardEffect2 "Digivolution Cards" = UI (stripped).
                }
            }
        }

        addDigivolutionCardInfos = new List<AddDigivolutionCardsInfo>();
    }
    #endregion

    bool _endSelectAssembly = false;

    // ===== (AD1-A) STATIC feasibility / matching half — the LIVE parameterized play path (PlayCardAction) =====

    /// <summary>Backtracking material assignment from the owner's trash. Returns the materials in ELEMENT
    /// ORDER (element 0's cards first — the order they are stacked under the permanent).</summary>
    public static bool TryMatchMaterials(
        EngineContext context,
        CardSource playCard,
        AssemblyCondition condition,
        out List<HeadlessEntityId> materials)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(playCard);
        ArgumentNullException.ThrowIfNull(condition);
        materials = new List<HeadlessEntityId>();

        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        // AS-IS: materials come from the owner's trash (MatchConditionOwnersCardCountInTrash — TrashCards),
        // tokens/excluded barred (a trash zone holds no tokens headless-side).
        IReadOnlyList<HeadlessEntityId> trash = zones.GetCards(playCard.Owner, ChoiceZone.Trash);

        // Flatten elements into per-slot predicates (element repeated ElementCount times), keeping the
        // owning element for the pre-selected-list gate.
        var slots = new List<AssemblyConditionElement>();
        foreach (AssemblyConditionElement element in condition.elements)
        {
            for (int i = 0; i < element.ElementCount; i++)
            {
                slots.Add(element);
            }
        }

        if (slots.Count == 0)
        {
            return false;
        }

        var selectedViews = new List<CardSource>();
        return Assign(0, materials, selectedViews);

        bool Assign(int slotIndex, List<HeadlessEntityId> assigned, List<CardSource> selected)
        {
            if (slotIndex >= slots.Count)
            {
                return true;
            }

            AssemblyConditionElement slot = slots[slotIndex];
            foreach (HeadlessEntityId id in trash)
            {
                if (assigned.Contains(id))
                {
                    continue;
                }

                var candidate = new CardSource(context, id, playCard.Owner, playCard.Owner);
                if (!slot.CardCondition(candidate))
                {
                    continue;
                }

                if (slot.CanTargetCondition_ByPreSelecetedList is not null &&
                    !slot.CanTargetCondition_ByPreSelecetedList(selected, candidate))
                {
                    continue;
                }

                assigned.Add(id);
                selected.Add(candidate);
                if (Assign(slotIndex + 1, assigned, selected))
                {
                    return true;
                }

                assigned.RemoveAt(assigned.Count - 1);
                selected.RemoveAt(selected.Count - 1);
            }

            return false;
        }
    }

    /// <summary>Mirror of AS-IS <c>CanFulfillConditions</c>: enough matching trash cards to fill EVERY
    /// element (full set — partial assembly gives no discount and is never offered).</summary>
    public static bool CanFulfillConditions(EngineContext context, CardSource playCard, AssemblyCondition condition) =>
        TryMatchMaterials(context, playCard, condition, out _);

    /// <summary>Validates an explicit material list (a parameterized action's payload) against the
    /// condition: element order, per-slot predicates, pre-selected-list gates, distinctness, and that every
    /// card is in the OWNER'S TRASH right now.</summary>
    public static bool ValidateMaterials(
        EngineContext context,
        CardSource playCard,
        AssemblyCondition condition,
        IReadOnlyList<HeadlessEntityId> materials)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(playCard);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(materials);

        if (materials.Count != condition.elementCount ||
            materials.Distinct().Count() != materials.Count ||
            context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        IReadOnlyList<HeadlessEntityId> trash = zones.GetCards(playCard.Owner, ChoiceZone.Trash);
        var selected = new List<CardSource>();
        int index = 0;
        foreach (AssemblyConditionElement element in condition.elements)
        {
            for (int i = 0; i < element.ElementCount; i++, index++)
            {
                HeadlessEntityId id = materials[index];
                if (!trash.Contains(id))
                {
                    return false;
                }

                var candidate = new CardSource(context, id, playCard.Owner, playCard.Owner);
                if (!element.CardCondition(candidate))
                {
                    return false;
                }

                if (element.CanTargetCondition_ByPreSelecetedList is not null &&
                    !element.CanTargetCondition_ByPreSelecetedList(selected, candidate))
                {
                    return false;
                }

                selected.Add(candidate);
            }
        }

        return true;
    }
}
