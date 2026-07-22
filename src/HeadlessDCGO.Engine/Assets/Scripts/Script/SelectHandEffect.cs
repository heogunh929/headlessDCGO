// Source: Assets/Scripts/Script/SelectHandEffect.cs
// Decision: PORT
// Category: AIUseful
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// (EFFECT-MODEL REBUILD / bridge W4) 1:1 mirror of the original DCGO SelectHandEffect — the HAND-scoped sibling
// of SelectCardEffect / SelectPermanentEffect. AS-IS SelectHandEffect ONLY ever selects from the select player's
// hand (`_selectPlayer.HandCards` / `HandCardObjects`), so there is no Root enum: the pool is always the hand.
// Card ports reach the one instance via `GManager.instance.GetComponent<SelectHandEffect>()` (the W4 route, which
// injects the match context) then SetUp(...).Activate(), verbatim from the AS-IS card corpus (BT1_039 pattern).
//
// Substrate translations ONLY (logic / order / names unchanged):
//   * Player -> HeadlessPlayerId (the select player); `_selectPlayer.HandCards` -> new Player(ctx, id).HandCards
//     (the established BT2_023 Player-handle idiom; a bare id cannot carry the extension property).
//   * IEnumerator -> async Task; StartCoroutine(X) -> await X.
//   * The AS-IS interactive selection transport (HandCard.AddClickTarget / OnClickHandCard / CheckEndSelect /
//     EndSelect_RPC / photonView.RPC(SetTargetHandCards) / WaitUntil HasPlayerSelection / DequeuePlayerSelection,
//     AND the IsAI AutoSelect branch) IS a single ChoiceProvider request in the mirror — the ChoiceProvider is the
//     transport for BOTH the you-select and opponent/AI paths (same collapse as SelectCardEffect W4).
//   * CardObjectController.AddLibraryTopCards / AddLibraryBottomCards (no mirror method) -> the sink
//     ReturnToDeckTop / ReturnToDeckBottom mutation kinds (the established SelectPermanentEffect PutLibrary*
//     substrate); CardObjectController.AddSecurityCard(cardSource, false, _isFaceUp) -> the mirror
//     CardObjectController.AddSecurityCard(toTop:false, faceUp:_isFaceUp) (real 1:1 mirror method); Discard ->
//     IDiscardHands (AS-IS :727-730 + :899-902); PlayFor* -> CardEffectCommons.PlayPermanentCards.
// UI/Photon strips (AS-IS anchors cited in Activate): IsSelecting save/restore (:165/:192/:923), Off*Target
//   (:182-186/:620-623), commandText / sideBar / selectCommandPanel / BackButton / outline plumbing
//   (:190-458/:614-644), ShowCardEffect overlays (:649-703), PlayLog (:712-914).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Inside namespace ...Script the identifier `CardEffectCommons` binds to the SIBLING NAMESPACE, not the
// static class — alias per the SelectCardEffect.cs precedent.
using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;

public sealed class SelectHandEffect
{
    // 1:1 with the original SelectHandEffect.Mode (SelectHandEffect.cs:125-134).
    public enum Mode
    {
        Discard,
        PutLibraryTop,
        PutLibraryBottom,
        PutSecurityBottom,
        PlayForFree,
        PlayForCost,
        Custom,
    }

    // AS-IS private fields (SelectHandEffect.cs:94-144). Player -> HeadlessPlayerId; IEnumerator -> Task.
    private HeadlessPlayerId _selectPlayer;
    private Func<CardSource, bool> _canTargetCondition = static _ => true;              // AS-IS :97
    private Func<List<CardSource>, CardSource, bool>? _canTargetCondition_ByPreSelecetedList; // AS-IS :99
    private Func<List<CardSource>, bool>? _canEndSelectCondition;                       // AS-IS :101
    private int _maxCount;                                                              // AS-IS :103
    private bool _canNoSelect;                                                          // AS-IS :105
    private bool _canEndNotMax;                                                         // AS-IS :107
    private bool _isShowOpponent;                                                       // AS-IS :109 (reveal UI flag)
    private Func<CardSource, Task>? _selectCardCoroutine;                               // AS-IS :111
    private Func<List<CardSource>, Task>? _afterSelectCardCoroutine;                    // AS-IS :113
    private Mode _mode = Mode.Custom;                                                   // AS-IS :115
    private ICardEffect? _cardEffect;                                                   // AS-IS :117
    private bool _showCard = true;                                                      // AS-IS :118 (UI-only)
    private bool _digiXros;                                                             // AS-IS :119 (UI banner)
    private bool _isLocal;                                                              // AS-IS :120 (Photon marker)
    private bool _isFaceUp;                                                             // AS-IS :121
    private (int reduceCost, Func<CardSource, bool> reduceCostCardCondition)? _reduceCostTuple; // AS-IS :122
    private (int fixedCost, Func<CardSource, bool> fixedCostCardCondition)? _fixedCostTuple;    // AS-IS :123

    private List<CardSource> _targetCards = new();      // AS-IS :137
    private bool _noSelect;                              // AS-IS :139
    private string? _customMessage_ShowCard;            // AS-IS :141 (UI-only)
    private string _customMessage = string.Empty;       // AS-IS :142
    private string _customMessage_Enemy = string.Empty; // AS-IS :143 (opponent-side UI text)
    private bool _showOpponentMessage = true;           // AS-IS :144 (UI-only)

    private HeadlessEntityId _sourceEntityId = new("select");
    private EngineContext? _context;

    /// <summary>(bridge W4) The match context the AS-IS <c>GManager.instance.GetComponent&lt;…&gt;()</c> route injects.</summary>
    internal void AttachContext(EngineContext context) => _context = context;

    /// <summary>AS-IS <c>SetUp</c> (SelectHandEffect.cs:11-46) — the 12-param overload the hand-select card corpus
    /// calls. Resets exactly the fields AS-IS resets (:37-45).</summary>
    public void SetUp(
        HeadlessPlayerId selectPlayer,
        Func<CardSource, bool> canTargetCondition,
        Func<List<CardSource>, CardSource, bool>? canTargetCondition_ByPreSelecetedList,
        Func<List<CardSource>, bool>? canEndSelectCondition,
        int maxCount,
        bool canNoSelect,
        bool canEndNotMax,
        bool isShowOpponent,
        Func<CardSource, Task>? selectCardCoroutine,
        Func<List<CardSource>, Task>? afterSelectCardCoroutine,
        Mode mode,
        ICardEffect cardEffect)
    {
        _selectPlayer = selectPlayer;
        _canTargetCondition = canTargetCondition ?? (static _ => true);
        _canTargetCondition_ByPreSelecetedList = canTargetCondition_ByPreSelecetedList;
        _canEndSelectCondition = canEndSelectCondition;
        _maxCount = maxCount;
        _canNoSelect = canNoSelect;
        _canEndNotMax = canEndNotMax;
        _isShowOpponent = isShowOpponent;
        _selectCardCoroutine = selectCardCoroutine;
        _afterSelectCardCoroutine = afterSelectCardCoroutine;
        _mode = mode;
        _cardEffect = cardEffect;
        _showCard = true;
        _digiXros = false;

        _customMessage_ShowCard = null;
        _customMessage = string.Empty;
        _customMessage_Enemy = string.Empty;
        _showOpponentMessage = true;
        _isLocal = false;
        _isFaceUp = false;

        _sourceEntityId = cardEffect?.EffectSourceCard?.InstanceId is { IsEmpty: false } sourceId
            ? sourceId
            : new HeadlessEntityId("select");
    }

    /// <summary>AS-IS <c>SetIsLocal</c> (:48-51) — Photon bypass marker (state-only).</summary>
    public void SetIsLocal() => _isLocal = true;

    /// <summary>AS-IS <c>SetNotShowCard</c> (:53-56) — UI-only.</summary>
    public void SetNotShowCard() => _showCard = false;

    /// <summary>AS-IS <c>SetDigiXros</c> (:58-61) — UI banner flag.</summary>
    public void SetDigiXros() => _digiXros = true;

    /// <summary>AS-IS <c>SetIsFaceup</c> (:63-66) — PutSecurityBottom face state.</summary>
    public void SetIsFaceup() => _isFaceUp = true;

    /// <summary>AS-IS <c>SetReducedCostTuple</c> (:68-71).</summary>
    public void SetReducedCostTuple((int reduceCost, Func<CardSource, bool> reduceCostCardCondition)? reduceCostTuple) =>
        _reduceCostTuple = reduceCostTuple;

    /// <summary>AS-IS <c>SetFixedCostTuple</c> (:73-76).</summary>
    public void SetFixedCostTuple((int fixedCost, Func<CardSource, bool> fixedCostCardCondition)? fixedCostTuple) =>
        _fixedCostTuple = fixedCostTuple;

    /// <summary>AS-IS <c>SetUpCustomMessage_ShowCard</c> (:78-81) — UI-only.</summary>
    public void SetUpCustomMessage_ShowCard(string CustomMessage_ShowCard) => _customMessage_ShowCard = CustomMessage_ShowCard;

    /// <summary>AS-IS <c>SetUpCustomMessage</c> (:83-87) — the custom prompt texts.</summary>
    public void SetUpCustomMessage(string CustomMessage, string CustomMessage_Enemy)
    {
        _customMessage = CustomMessage;
        _customMessage_Enemy = CustomMessage_Enemy;
    }

    /// <summary>AS-IS <c>SetNotShowOpponentMessage</c> (:89-92) — UI-only.</summary>
    public void SetNotShowOpponentMessage() => _showOpponentMessage = false;

    /// <summary>AS-IS <c>active()</c> (:147-160) — verbatim: <c>_maxCount &gt; 0</c> AND the select player's hand has
    /// at least one card passing <see cref="_canTargetCondition"/>. Returns false when the context is unresolved
    /// (a context-less unit stub).</summary>
    public bool active()
    {
        if (_maxCount <= 0)
        {
            return false;
        }

        EngineContext? context = ResolveContext();
        if (context is null || _selectPlayer.IsEmpty)
        {
            return false;
        }

        List<CardSource> handCards = new Player(context, _selectPlayer).HandCards;
        return handCards.Count(cardSource => _canTargetCondition(cardSource)) > 0;
    }

    /// <summary>AS-IS <c>Activate()</c> (SelectHandEffect.cs:163-925) — the full hand-select flow: active() guard →
    /// (maxCount==0 ⇒ canNoSelect) → selection (ChoiceProvider) → the always-run per-selected select-coroutine →
    /// the per-Mode routing on the verified substrate carriers → the deferred Discard batch → the always-run
    /// after-coroutine.</summary>
    public async Task Activate()
    {
        var discardHands = new List<IDiscardHand>();

        _noSelect = false;

        if (active())
        {
            EngineContext context = RequireContext();

            if (_maxCount == 0)
            {
                _canNoSelect = true;   // AS-IS :175-178
            }

            // AS-IS :180 _targetCards reset; :253/:255 PreSelectedHandCards over the select player's hand.
            List<CardSource> handPool = new Player(context, _selectPlayer).HandCards;

            (List<CardSource> selected, bool noSelect) = await RunAsIsSelectionAsync(context, handPool).ConfigureAwait(false);
            _targetCards = selected;
            _noSelect = noSelect;

            if (!_noSelect)
            {
                HeadlessEntityId? causeId = _cardEffect?.EffectSourceCard?.InstanceId;   // AS-IS :705-710 hashtable {"CardEffect": _cardEffect}

                // AS-IS :716-723 — the per-selected select-coroutine runs for EVERY mode, in selection order,
                // BEFORE the mode switch (the AS-IS foreach is outside the switch).
                foreach (CardSource cardSource in _targetCards)
                {
                    if (_selectCardCoroutine != null)
                    {
                        await _selectCardCoroutine(cardSource).ConfigureAwait(false);
                    }
                }

                switch (_mode)
                {
                    case Mode.Discard:   // AS-IS :727-730 build IDiscardHand list, run after switch (:899-902).
                        discardHands = _targetCards.Select(cardSource => new IDiscardHand(cardSource)).ToList();
                        break;

                    case Mode.PutLibraryTop:   // AS-IS :732-735 CardObjectController.AddLibraryTopCards(_targetCards).
                    {
                        var sink = NewSink(context);
                        foreach (CardSource cardSource in _targetCards)
                        {
                            sink.Apply(Mutation(MatchStateMutationSink.ReturnToDeckTopKind, cardSource));
                        }

                        await sink.FlushAsync().ConfigureAwait(false);
                        break;
                    }

                    case Mode.PutLibraryBottom:   // AS-IS :737-740 CardObjectController.AddLibraryBottomCards(_targetCards).
                    {
                        var sink = NewSink(context);
                        foreach (CardSource cardSource in _targetCards)
                        {
                            sink.Apply(Mutation(MatchStateMutationSink.ReturnToDeckBottomKind, cardSource));
                        }

                        await sink.FlushAsync().ConfigureAwait(false);
                        break;
                    }

                    case Mode.PutSecurityBottom:   // AS-IS :742-746 per-card AddSecurityCard(cardSource, false, _isFaceUp).
                        foreach (CardSource cardSource in _targetCards)
                        {
                            await CardObjectController.AddSecurityCard(cardSource, toTop: false, faceUp: _isFaceUp).ConfigureAwait(false);
                        }

                        break;

                    case Mode.PlayForFree:   // AS-IS :748-757.
                        await Commons.PlayPermanentCards(
                            cardSources: _targetCards,
                            activateClass: _cardEffect!,
                            payCost: false,
                            isTapped: false,
                            root: SelectCardEffect.Root.Hand,
                            activateETB: true).ConfigureAwait(false);
                        break;

                    case Mode.PlayForCost:   // AS-IS :759-895.
                        await ActivatePlayForCostAsync(context).ConfigureAwait(false);
                        break;

                    case Mode.Custom:        // AS-IS: no mode action (the per-selected select-coroutine above is the whole action).
                        break;
                }

                // AS-IS :899-902 — one IDiscardHands call over the whole selected list (one shared discard batch id).
                if (discardHands.Count > 0)
                {
                    await new IDiscardHands(discardHands, causeId, _cardEffect).DiscardHands().ConfigureAwait(false);
                }
            }

            // AS-IS :917-920 — the after-select coroutine runs once, only inside the active() block (as AS-IS does).
            if (_afterSelectCardCoroutine != null)
            {
                await _afterSelectCardCoroutine(_targetCards).ConfigureAwait(false);
            }
        }
    }

    /// <summary>AS-IS Mode.PlayForCost (SelectHandEffect.cs:759-895) — registers the optional reduce/fixed-cost
    /// ChangeCostClass on the select player's <c>UntilCalculateFixedCostEffect</c> for the DURATION of the play,
    /// runs <see cref="Commons.PlayPermanentCards"/> (payCost:true, root:Hand), then releases the registration.
    /// The cost condition closures (PermanentsCondition/SharedCardCondition/RootCondition/CanUseCondition) and the
    /// ChangeCost bodies are AS-IS verbatim.</summary>
    private async Task ActivatePlayForCostAsync(EngineContext context)
    {
        // AS-IS local predicates (:761-781).
        bool PermanentsCondition(List<Permanent> targetPermanents)
        {
            if (targetPermanents == null)
            {
                return true;
            }

            if (targetPermanents.Count(targetPermanent => targetPermanent != null) == 0)
            {
                return true;
            }

            return false;
        }

        bool SharedCardCondition(CardSource cardSource) => _targetCards.Contains(cardSource);
        bool RootCondition(SelectCardEffect.Root root) => true;
        bool CanUseCondition(Hashtable hashtable) => true;

        var selectPlayer = new Player(context, _selectPlayer);

        // AS-IS :783-826 reduce cost.
        Func<EffectTiming, ICardEffect>? getChangeCostEffect = null;
        if (_reduceCostTuple != null)
        {
            bool CardCondition(CardSource cardSource) =>
                SharedCardCondition(cardSource)
                && (_reduceCostTuple.Value.reduceCostCardCondition == null || _reduceCostTuple.Value.reduceCostCardCondition(cardSource));

            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
            {
                if (PermanentsCondition(targetPermanents))
                {
                    Cost -= _reduceCostTuple.Value.reduceCost;
                }

                return Cost;
            }

            bool isUpDown() => true;

            ChangeCostClass changeCostClass = new ChangeCostClass();
            changeCostClass.SetUpICardEffect($"Play Cost -{_reduceCostTuple.Value.reduceCost}", CanUseCondition, _cardEffect!.EffectSourceCard);
            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);
            getChangeCostEffect = GetCardEffect;

            ICardEffect? GetCardEffect(EffectTiming _timing) => _timing == EffectTiming.None ? changeCostClass : null;

            selectPlayer.UntilCalculateFixedCostEffect.Add(getChangeCostEffect);
        }

        // AS-IS :830-873 set fixed cost.
        Func<EffectTiming, ICardEffect>? getFixedCostEffect = null;
        if (_fixedCostTuple != null)
        {
            bool CardCondition(CardSource cardSource) =>
                SharedCardCondition(cardSource)
                && (_fixedCostTuple.Value.fixedCostCardCondition == null || _fixedCostTuple.Value.fixedCostCardCondition(cardSource));

            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
            {
                if (PermanentsCondition(targetPermanents))
                {
                    Cost = _fixedCostTuple.Value.fixedCost;
                }

                return Cost;
            }

            bool isUpDown() => false;

            ChangeCostClass changeCostClass = new ChangeCostClass();
            changeCostClass.SetUpICardEffect($"Play Cost {_fixedCostTuple.Value.fixedCost}", CanUseCondition, _cardEffect!.EffectSourceCard);
            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);
            getFixedCostEffect = GetCardEffect;

            ICardEffect? GetCardEffect(EffectTiming _timing) => _timing == EffectTiming.None ? changeCostClass : null;

            selectPlayer.UntilCalculateFixedCostEffect.Add(getFixedCostEffect);
        }

        // AS-IS :877-884.
        await Commons.PlayPermanentCards(
            cardSources: _targetCards,
            activateClass: _cardEffect!,
            payCost: true,
            isTapped: false,
            root: SelectCardEffect.Root.Hand,
            activateETB: true).ConfigureAwait(false);

        // AS-IS :886-892 release effect.
        if (getChangeCostEffect != null)
        {
            selectPlayer.UntilCalculateFixedCostEffect.Remove(getChangeCostEffect);
        }

        if (getFixedCostEffect != null)
        {
            selectPlayer.UntilCalculateFixedCostEffect.Remove(getFixedCostEffect);
        }
    }

    /// <summary>The AS-IS hand selection (SelectHandEffect.cs OnClickHandCard / CheckEndSelect / CanEndSelect and
    /// the IsAI AutoSelect branch, :253-593): a batch ChoiceRequest over the whole hand (matching cards selectable,
    /// non-matching greyed) with <see cref="_canEndSelectCondition"/> as the SelectionValidator when there is no
    /// path-dependent per-pick filter; an incremental one-pick loop when
    /// <see cref="_canTargetCondition_ByPreSelecetedList"/> is present (AS-IS OnClickHandCard :278-296). Count rule
    /// (AS-IS CanEndSelect :575-593): min = 0 when canNoSelect / canEndNotMax else max, CLAMPED to the selectable
    /// count. Returns the picks plus the no-selection flag (AS-IS _noSelect).</summary>
    private async Task<(List<CardSource> Selected, bool NoSelect)> RunAsIsSelectionAsync(EngineContext context, List<CardSource> handPool)
    {
        var selected = new List<CardSource>();
        int selectableCount = handPool.Count(cardSource => _canTargetCondition(cardSource));
        int maxCount = Math.Min(_maxCount, selectableCount);
        string message = BuildAsIsMessage();

        if (maxCount < 1)
        {
            return (selected, true);
        }

        if (_canTargetCondition_ByPreSelecetedList == null)
        {
            ChoiceCandidate[] candidates = handPool
                .Select(cardSource => new ChoiceCandidate(
                    cardSource.InstanceId, cardSource.InstanceId.Value, ChoiceZone.Hand,
                    IsSelectable: _canTargetCondition(cardSource), ownerId: cardSource.Owner))
                .ToArray();
            int minCount = (_canNoSelect || _canEndNotMax) ? 0 : maxCount;
            var request = new ChoiceRequest(
                ChoiceType.Card, _selectPlayer, message, minCount, maxCount, canSkip: _canNoSelect,
                ChoiceZone.Hand, candidates);
            if (_canEndSelectCondition != null)
            {
                request = request with
                {
                    SelectionValidator = ids => _canEndSelectCondition(
                        ids.Select(id => handPool.First(cardSource => cardSource.InstanceId == id)).ToList()),
                };
            }

            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);
            if (result.IsSkipped)
            {
                return (selected, true);
            }

            foreach (HeadlessEntityId id in result.SelectedIds)
            {
                int index = handPool.FindIndex(cardSource => cardSource.InstanceId == id);
                if (index >= 0 && !selected.Contains(handPool[index]))
                {
                    selected.Add(handPool[index]);
                }
            }

            // AS-IS _noSelect = the returned card list is empty (:604).
            return (selected, selected.Count == 0);
        }

        // Incremental path — the AS-IS per-pick byPreSelectedList re-filter (OnClickHandCard :278-296). The
        // un-pick-at-max corner is unreachable for prefix-monotone AS-IS conditions (RD-W4-2).
        while (selected.Count < maxCount)
        {
            List<CardSource> legal = handPool
                .Where(cardSource => !selected.Contains(cardSource)
                    && _canTargetCondition(cardSource)
                    && _canTargetCondition_ByPreSelecetedList(selected, cardSource))
                .ToList();
            if (legal.Count == 0)
            {
                break;
            }

            bool canEndNow = (_canNoSelect && selected.Count == 0)
                || (_canEndNotMax && (_canEndSelectCondition == null || _canEndSelectCondition(selected)));
            var request = new ChoiceRequest(
                ChoiceType.Card, _selectPlayer, message,
                minCount: canEndNow ? 0 : 1, maxCount: 1, canSkip: canEndNow, ChoiceZone.Hand,
                legal.Select(cardSource => new ChoiceCandidate(
                    cardSource.InstanceId, cardSource.InstanceId.Value, ChoiceZone.Hand,
                    IsSelectable: true, ownerId: cardSource.Owner)).ToArray());
            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);
            if (result.IsSkipped || result.SelectedIds.Count == 0)
            {
                break;
            }

            int index = handPool.FindIndex(cardSource => cardSource.InstanceId == result.SelectedIds[0]);
            if (index < 0)
            {
                break;
            }

            selected.Add(handPool[index]);
        }

        return (selected, selected.Count == 0);
    }

    /// <summary>The AS-IS player prompt: the custom message when given (AS-IS :195-198), else the per-Mode default
    /// (:204-244, verbatim strings).</summary>
    private string BuildAsIsMessage()
    {
        if (!string.IsNullOrEmpty(_customMessage))
        {
            return _customMessage;
        }

        return _mode switch
        {
            Mode.Discard => "Select cards to discard.",
            Mode.PutLibraryTop => "Select cards to put on top of the deck.",
            Mode.PutLibraryBottom => _maxCount >= 2
                ? "Select cards to put on bottom of the deck\n(cards will be placed back to the bottom of the deck so that cards with lower numbers are on top)."
                : "Select cards to put on bottom of the deck.",
            Mode.PutSecurityBottom => $"Select card(s) to put on bottom of the security {(_isFaceUp ? "faceup" : "facedown")}.",
            Mode.PlayForFree => "Select cards to play for without paying the cost.",
            Mode.PlayForCost => "Select cards to play.",
            Mode.Custom => "Select cards in your hand.",
            _ => "Select cards in your hand.",
        };
    }

    private EffectMutation Mutation(string kind, CardSource card)
    {
        return new EffectMutation(
            kind,
            _sourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value,
            });
    }

    private EngineContext? ResolveContext() => _context ?? _cardEffect?.EffectSourceCard?.Context;

    private EngineContext RequireContext() =>
        ResolveContext()
        ?? throw new InvalidOperationException(
            "SelectHandEffect has no EngineContext — obtain the instance via " +
            "GManager.instance.GetComponent<SelectHandEffect>() (bridge W4).");

    private static MatchStateMutationSink NewSink(EngineContext context) =>
        new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: context.MemoryController,
            context.GameEventQueue, context: context);
}
