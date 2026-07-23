// Source: Assets/Scripts/Script/SelectCardEffect.cs
// Decision: PORT
// Category: AIUseful
// Migration: Port core engine source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// AS-IS mirror of the original DCGO SelectCardEffect (the card-selection sibling of
// SelectPermanentEffect, operating over a card ROOT zone — hand / library / trash / security / ...
// rather than the battle area). Headless port keeps the authoring shape (Mode + Root enums, SetUp) but
// is deterministic:
//   (1) BuildRequest  — enumerate the select player's cards in the Root zone, filter by the predicate,
//                       and build a Card ChoiceRequest honouring max/canNoSelect/canEndNotMax (F-2.2/F-2.4).
//   (2) BuildMutations — map the Mode to MatchStateMutation(s) per selected card (B-5: Discard = trash,
//                       AddHand = return to hand). PlayForFree/PlayForCost need the effect-Play mutation
//                       (F-3.7) and are not yet mapped.

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
// static class — alias per the AutoProcessing.cs precedent.
using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;

public sealed class SelectCardEffect
{
    // 1:1 with the original SelectCardEffect.Mode.
    public enum Mode
    {
        AddHand,
        Discard,
        PlayForFree,
        PlayForCost,
        Custom,
    }

    // 1:1 with the original SelectCardEffect.Root (the source zone of the selectable cards).
    public enum Root
    {
        Library,
        Trash,
        Clock,
        Security,
        Custom,
        Hand,
        Recollection,
        Execution,
        DigivolutionCards,
        LinkedCards,
        None,
    }

    private HeadlessPlayerId _selectPlayer;
    private int _maxCount = 1;
    private bool _canEndNotMax;
    private Mode _mode = Mode.Custom;
    private Root _root = Root.Hand;
    private HeadlessEntityId _sourceEntityId = new("select");
    private string _message = "Select card(s).";
    private int _playCost;

    /// <summary>(D-8) Memory cost paid per selected card in PlayForCost mode. The effect resolves the
    /// (cost-pipeline-reduced) cost via <c>ContinuousModifierGate</c> and sets it here before Apply;
    /// 0 = play for free.</summary>
    public void SetPlayCost(int memoryCost) => _playCost = memoryCost < 0 ? 0 : memoryCost;

    public void SetUpCustomMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _message = message;
        }
    }

    /// <summary>The <see cref="ChoiceZone"/> the configured <see cref="Root"/> maps to.</summary>
    public ChoiceZone RootZone => MapRoot(_root);

    /// <summary>Enumerate the select player's cards in the Root zone, filter by the predicate, and build
    /// the Card ChoiceRequest. Count rules mirror SelectPermanentEffect / the original.</summary>
    public ChoiceRequest BuildRequest(IZoneStateReader zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        ChoiceZone zone = MapRoot(_root);
        var candidates = new List<ChoiceCandidate>();
        if (zone != ChoiceZone.None)
        {
            foreach (HeadlessEntityId id in zones.GetCards(_selectPlayer, zone))
            {
                // (D9 id-flip batch 5) AS-IS Func<CardSource,bool> predicate — materialise the candidate to a
                // CardSource view and evaluate, mirroring SelectPermanentEffect.BuildRequest's Permanent
                // materialisation (AS-IS SelectCardEffect.cs:145 _canTargetcondition shape).
                if (_canTargetCondition is null
                    || _canTargetCondition(new CardSource(RequireContext(), id, _selectPlayer, _selectPlayer)))
                {
                    candidates.Add(EffectChoiceHelpers.Candidate(id, id.Value, zone, isSelectable: true, _selectPlayer));
                }
            }
        }

        bool canNoSelect = _canNoSelect_Func?.Invoke() ?? false;
        int available = candidates.Count;
        int maxCount = Math.Min(_maxCount, available);
        int minCount = canNoSelect ? 0 : (_canEndNotMax ? Math.Min(1, maxCount) : maxCount);
        bool canSkip = canNoSelect;

        return EffectChoiceHelpers.CreateCardRequest(_selectPlayer, _message, minCount, maxCount, canSkip, zone, candidates);
    }

    /// <summary>Map the Mode to one mutation per selected card. PlayForFree/PlayForCost need the
    /// effect-Play mutation (F-3.7); Custom yields no built-in mutation.</summary>
    public IReadOnlyList<EffectMutation> BuildMutations(IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        var mutations = new List<EffectMutation>();
        foreach (HeadlessEntityId card in selected)
        {
            EffectMutation? mutation = BuildMutation(card);
            if (mutation is not null)
            {
                mutations.Add(mutation);
            }
        }

        return mutations;
    }

    public void Apply(MatchStateMutationSink sink, IEnumerable<HeadlessEntityId> selected)
    {
        ArgumentNullException.ThrowIfNull(sink);
        foreach (EffectMutation mutation in BuildMutations(selected))
        {
            sink.Apply(mutation);
        }
    }

    private EffectMutation? BuildMutation(HeadlessEntityId card)
    {
        return _mode switch
        {
            Mode.AddHand => Mutation(MatchStateMutationSink.ReturnToHandKind, card),
            Mode.Discard => Mutation(MatchStateMutationSink.TrashCardKind, card),
            Mode.PlayForFree => PlayMutation(card, memoryCost: 0),
            // D-8: PlayForCost pays the resolved cost (set via SetPlayCost) per played card.
            Mode.PlayForCost => PlayMutation(card, memoryCost: _playCost),
            Mode.Custom => null,
            _ => null,
        };
    }

    private EffectMutation Mutation(string kind, HeadlessEntityId card)
    {
        return new EffectMutation(
            kind,
            _sourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = card.Value,
            });
    }

    private EffectMutation PlayMutation(HeadlessEntityId card, int memoryCost)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = card.Value,
            [MatchStateMutationSink.FromZoneKey] = MapRoot(_root).ToString(),
        };
        if (memoryCost > 0)
        {
            values[MatchStateMutationSink.MemoryCostKey] = memoryCost;
        }

        return new EffectMutation(MatchStateMutationSink.PlayCardKind, _sourceEntityId, values);
    }

    private static ChoiceZone MapRoot(Root root)
    {
        return root switch
        {
            Root.Library => ChoiceZone.Library,
            Root.Trash => ChoiceZone.Trash,
            Root.Clock => ChoiceZone.Clock,
            Root.Security => ChoiceZone.Security,
            Root.Hand => ChoiceZone.Hand,
            Root.Recollection => ChoiceZone.Recollection,
            Root.Execution => ChoiceZone.Execution,
            Root.DigivolutionCards => ChoiceZone.DigivolutionCards,
            Root.LinkedCards => ChoiceZone.LinkedCards,
            Root.Custom => ChoiceZone.Custom,
            _ => ChoiceZone.None,
        };
    }

    // ================================================================================================
    // (bridge W4) AS-IS SetUp(...).Activate() surface — 1:1 with DCGO SelectCardEffect.cs, the
    // `GManager.instance.GetComponent<SelectCardEffect>()` flow verbatim card ports use (BT1_011 pattern).
    // Substrate translations only (IEnumerator→Task, Player→HeadlessPlayerId, ICardEffect KEPT — its
    // CardSource-shape predicates are used verbatim); UI/Photon statements stripped with AS-IS anchors
    // cited in Activate(). See docs/audit/rebuild_bridge_w4_notes.md.
    // ================================================================================================

    // AS-IS private fields (SelectCardEffect.cs:145-227). AS-IS names kept where free; where the name is
    // taken by a legacy mirror member of a different shape, the W3 …Card suffix convention applies.
    // (D9 id-flip batch 5) The invented id-form _canTargetCondition (HeadlessEntityId-predicate) was retired,
    // so the AS-IS name is free — the CardSource-shape predicate reverts to the AS-IS name _canTargetCondition
    // (one field, mirroring SelectPermanentEffect's consolidated _canTargetCondition).
    private Func<CardSource, bool>? _canTargetCondition;                                  // AS-IS _canTargetCondition
    private Func<List<CardSource>, CardSource, bool>? _canTargetCondition_ByPreSelecetedList;
    private Func<List<CardSource>, bool>? _canEndSelectConditionCard;                     // AS-IS _canEndSelectCondition
    private Func<bool>? _canNoSelect_Func;                                                // AS-IS _canNoSelect (Func<bool>)
    private List<CardSource> _targetCards = new();
    private Func<CardSource, Task>? _selectCardCoroutine;
    private Func<List<CardSource>, Task>? _afterSelectCardCoroutine;
    private bool _isShowOpponent;             // AS-IS reveal-to-opponent UI flag (log/show gating only).
    private bool _canLookReverseCard;
    private List<CardSource>? _customRootCardList;
    private ICardEffect? _cardEffect;
    private bool _isLocal;                    // Photon marker — single-process, state-only.
    private bool _showReverseCard = true;     // UI-only (Show selected card overlay gating).
    private bool _showCard = true;            // UI-only.
    private bool _notAddLog;                  // UI-only (PlayLog gating).
    private bool _isDigiXros;                 // UI banner flag.
    private bool _isAssembly;                 // UI banner flag.
    private bool _isSecurity;                 // AS-IS gates IsSecurityLooking — no live mirror surface (RD-W4-4).
    private bool _allowFaceDown;
    private (int reduceCost, Func<CardSource, bool> reduceCostCardCondition)? _reduceCostTuple;
    private (int fixedCost, Func<CardSource, bool> fixedCostCardCondition)? _fixedCostTuple;
    private bool _isDeckBottom;               // AS-IS auto-order convenience toggle — headless always asks.
    private bool _isDeckTop;
    private string? _customMessage;
    private string? _customMessage_Enemy;     // opponent-side UI text.
    private string? _customMessage_ShowCard;  // UI-only.
    private string? _customCountText;         // UI-only.
    private List<CardEffectCommons.SkillInfo> _skillInfos = new();      // panel decoration (UI-only).
    private List<int> _slectedInexesInList = new();   // AS-IS spelling kept.
    private Func<List<int>, Task>? _afterSelectIndexCoroutine;
    private EngineContext? _context;

    /// <summary>(bridge W4) The match context the AS-IS <c>GManager.instance.GetComponent&lt;…&gt;()</c> route
    /// injects.</summary>
    internal void AttachContext(EngineContext context) => _context = context;

    /// <summary>(bridge W4) AS-IS <c>SetUp</c> (SelectCardEffect.cs:10-63) — the 16-param overload the card
    /// corpus calls. Resets exactly the fields AS-IS resets (:45-62).</summary>
    public void SetUp(
        Func<CardSource, bool> canTargetCondition,
        Func<List<CardSource>, CardSource, bool>? canTargetCondition_ByPreSelecetedList,
        Func<List<CardSource>, bool>? canEndSelectCondition,
        Func<bool>? canNoSelect,
        Func<CardSource, Task>? selectCardCoroutine,
        Func<List<CardSource>, Task>? afterSelectCardCoroutine,
        string message,
        int maxCount,
        bool canEndNotMax,
        bool isShowOpponent,
        Mode mode,
        Root root,
        List<CardSource>? customRootCardList,
        bool canLookReverseCard,
        HeadlessPlayerId selectPlayer,
        ICardEffect cardEffect)
    {
        _canTargetCondition = canTargetCondition;
        _canTargetCondition_ByPreSelecetedList = canTargetCondition_ByPreSelecetedList;
        _canEndSelectConditionCard = canEndSelectCondition;
        _canNoSelect_Func = canNoSelect;
        _selectCardCoroutine = selectCardCoroutine;
        _afterSelectCardCoroutine = afterSelectCardCoroutine;
        _message = message ?? string.Empty;
        _maxCount = maxCount;
        _canEndNotMax = canEndNotMax;
        _isShowOpponent = isShowOpponent;
        _mode = mode;
        _root = root;
        _customRootCardList = customRootCardList;
        _canLookReverseCard = canLookReverseCard;
        _selectPlayer = selectPlayer;
        _cardEffect = cardEffect;
        _sourceEntityId = cardEffect?.EffectSourceCard?.InstanceId is { IsEmpty: false } sourceId
            ? sourceId
            : new HeadlessEntityId("select");

        _isLocal = false;
        _customMessage = null;
        _customMessage_Enemy = null;
        _customMessage_ShowCard = null;
        _customCountText = null;
        _showReverseCard = true;
        _showCard = true;
        _isDigiXros = false;
        _isAssembly = false;
        _isDeckBottom = false;
        _isDeckTop = false;
        _notAddLog = false;
        _isSecurity = false;
        _allowFaceDown = false;

        _skillInfos = new List<CardEffectCommons.SkillInfo>();

        _afterSelectIndexCoroutine = null;
    }

    /// <summary>AS-IS <c>SetIsLocal</c> (:65-68) — Photon bypass marker (state-only).</summary>
    public void SetIsLocal() => _isLocal = true;

    /// <summary>AS-IS <c>SetIsDeckBottom</c> (:70-73) — the auto-deck-bottom-order UI convenience; the
    /// headless flow always issues the real choice.</summary>
    public void SetIsDeckBottom() => _isDeckBottom = true;

    /// <summary>AS-IS <c>SetIsDeckTop</c> (:75-78).</summary>
    public void SetIsDeckTop() => _isDeckTop = true;

    /// <summary>AS-IS <c>SetNotShowCard</c> (:80-83) — UI-only.</summary>
    public void SetNotShowCard() => _showCard = false;

    /// <summary>AS-IS <c>SetNotAddLog</c> (:85-88) — UI-only.</summary>
    public void SetNotAddLog() => _notAddLog = true;

    /// <summary>AS-IS <c>SetDigiXros</c> (:90-93) — UI banner flag.</summary>
    public void SetDigiXros() => _isDigiXros = true;

    /// <summary>AS-IS <c>SetAssembly</c> (:95-98) — UI banner flag.</summary>
    public void SetAssembly() => _isAssembly = true;

    /// <summary>AS-IS <c>SetIsSecurity</c> (:100-103) — flags the security-looking window (the AS-IS
    /// IsSecurityLooking poll has no live mirror surface — MIG6-SECURITYLOOKING / RD-W4-4).</summary>
    public void SetIsSecurity() => _isSecurity = true;

    /// <summary>AS-IS <c>SetUseFaceDown</c> (:105-108) — face-down cards stay selectable.</summary>
    public void SetUseFaceDown() => _allowFaceDown = true;

    /// <summary>AS-IS <c>SetUpSkillInfos</c> (:109-112) — panel decoration (UI-only; stored for shape).</summary>
    public void SetUpSkillInfos(List<CardEffectCommons.SkillInfo> skillInfos) => _skillInfos = new List<CardEffectCommons.SkillInfo>(skillInfos);

    /// <summary>AS-IS <c>SetReducedCostTuple</c> (:114-117). A NON-null tuple reaching Mode.PlayForCost STOPs
    /// (design item RD-W4-1 — the AS-IS ChangeCostClass registration on
    /// <c>Player.UntilCalculateFixedCostEffect</c> has no mirror surface yet).</summary>
    public void SetReducedCostTuple((int reduceCost, Func<CardSource, bool> reduceCostCardCondition)? reduceCostTuple) =>
        _reduceCostTuple = reduceCostTuple;

    /// <summary>AS-IS <c>SetFixedCostTuple</c> (:119-122) — same STOP rule as <see cref="SetReducedCostTuple"/>.</summary>
    public void SetFixedCostTuple((int fixedCost, Func<CardSource, bool> fixedCostCardCondition)? fixedCostTuple) =>
        _fixedCostTuple = fixedCostTuple;

    /// <summary>AS-IS <c>SetUpCustomMessage</c> (:124-128) — the custom prompt texts.</summary>
    public void SetUpCustomMessage(string CustomMessage, string CustomMessage_Enemy)
    {
        _customMessage = CustomMessage;
        _customMessage_Enemy = CustomMessage_Enemy;
    }

    /// <summary>AS-IS <c>SetUpCustomMessage_ShowCard</c> (:130-133) — UI-only.</summary>
    public void SetUpCustomMessage_ShowCard(string CustomMessage_ShowCard) =>
        _customMessage_ShowCard = CustomMessage_ShowCard;

    /// <summary>AS-IS <c>SetUpCustomCountText</c> (:135-138) — UI-only.</summary>
    public void SetUpCustomCountText(string CustomCountText) => _customCountText = CustomCountText;

    /// <summary>AS-IS <c>SetShowReverseCard</c> (:140-143) — UI-only.</summary>
    public void SetShowReverseCard() => _showReverseCard = false;

    /// <summary>AS-IS <c>SetUpAfterSelectIndexCoroutine</c> (:224-227).</summary>
    public void SetUpAfterSelectIndexCoroutine(Func<List<int>, Task> AfterSelectIndexCoroutine) =>
        _afterSelectIndexCoroutine = AfterSelectIndexCoroutine;

    /// <summary>AS-IS <c>RootCardList</c> (:229-275) — the selectable pool: the custom list when given, else
    /// ONLY the four AS-IS-materialised zones (Library / Trash / Security / Recollection=Lost); every other
    /// Root yields an empty pool from this method exactly as AS-IS does (callers of those roots always pass
    /// customRootCardList).</summary>
    public List<CardSource> RootCardList()
    {
        var rootCardList = new List<CardSource>();

        if (_customRootCardList == null)
        {
            EngineContext? context = ResolveContext();
            if (context?.ZoneMover is IZoneStateReader zones && !_selectPlayer.IsEmpty)
            {
                switch (_root)
                {
                    case Root.Library:
                        AddZone(zones, context, ChoiceZone.Library, rootCardList);
                        break;

                    case Root.Trash:
                        AddZone(zones, context, ChoiceZone.Trash, rootCardList);
                        break;

                    case Root.Security:
                        AddZone(zones, context, ChoiceZone.Security, rootCardList);
                        break;

                    case Root.Recollection:
                        AddZone(zones, context, ChoiceZone.Recollection, rootCardList);
                        break;
                }
            }
        }
        else
        {
            foreach (CardSource cardSource in _customRootCardList)
            {
                rootCardList.Add(cardSource);
            }
        }

        return rootCardList;

        void AddZone(IZoneStateReader zones, EngineContext context, ChoiceZone zone, List<CardSource> into)
        {
            foreach (HeadlessEntityId id in zones.GetCards(_selectPlayer, zone))
            {
                into.Add(new CardSource(context, id, _selectPlayer, _selectPlayer));
            }
        }
    }

    /// <summary>AS-IS <c>CanSelectCard</c> (:277-301) — verbatim: hidden-zone flip pass-through, the card
    /// predicate, then the face-down exclusion unless <see cref="SetUseFaceDown"/>.</summary>
    private bool CanSelectCardAsIs(CardSource cardSource)
    {
        if (_root != Root.Library && _root != Root.Security && _root != Root.Custom)
        {
            if (cardSource.IsFlipped)
                return false;
        }

        if (_canTargetCondition != null)
        {
            if (_canTargetCondition(cardSource))
            {
                if (!_allowFaceDown)
                {
                    if (cardSource.IsFlipped)
                        return false;
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>active()</c> (:303-330) — verbatim, INCLUDING the AS-IS side effects
    /// (Library ⇒ SetUseFaceDown; Security + canLookReverseCard ⇒ SetUseFaceDown) and the AS-IS rule that a
    /// non-empty Library/Security pool is always active regardless of matches.</summary>
    public bool active()
    {
        if (RootCardList().Count > 0)
        {
            if (_root != Root.Library && _root != Root.Security)
            {
                if (RootCardList().Count(CanSelectCardAsIs) > 0)
                {
                    return true;
                }
            }
            else
            {
                if (_root == Root.Library)
                    SetUseFaceDown();

                if (_root == Root.Security)
                {
                    if (_canLookReverseCard)
                        SetUseFaceDown();
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>(bridge W4) AS-IS <c>Activate()</c> (SelectCardEffect.cs:332-1011) — the full flow: guards →
    /// selection (ChoiceProvider; batch or per-pick incremental) → per-Mode routing on the verified substrate
    /// carriers → the single AddHandCards batch → the always-run after-coroutines.
    /// UI/Photon strips (AS-IS anchors): IsSelecting save/restore + attacking-permanent outline + Off*Target
    /// (:334-364, :1010), IsSecurityLooking flips (:378-381/:1008 — RD-W4-4/MIG6-SECURITYLOOKING), command
    /// text/messages (:396-435, :585-601, :688-689), the DeckData sort + matching-first panel ordering
    /// (:444-478 — presentation order, RD-W4-7), skillInfos decoration (:480-544), the RPC/WaitUntil selection
    /// transport (:561-686 — the ChoiceProvider request IS the transport; the AI AutoSelect branch collapses
    /// into the same request), ShowCardEffect overlays (:691-734), PlayLog (:736-761, :980-995), the AS-IS
    /// commented-out Library shuffle (:753-756).</summary>
    public async Task Activate()
    {
        List<CardSource> handCards = new List<CardSource>();

        _targetCards = new List<CardSource>();

        _slectedInexesInList = new List<int>();

        if (_maxCount == 0)
        {
            _canNoSelect_Func = () => true;   // AS-IS :366-369
        }

        if (_root == Root.Security)
        {
            SetIsSecurity();                  // AS-IS :371-374
        }

        if (active())
        {
            EngineContext context = RequireContext();

            List<CardSource> rootCards = RootCardList();   // AS-IS :437-442 working copy

            (List<CardSource> selected, List<int> selectedIndices) =
                await RunAsIsSelectionAsync(context, rootCards).ConfigureAwait(false);
            _targetCards = selected;
            _slectedInexesInList = selectedIndices;

            HeadlessEntityId? causeId = _cardEffect?.EffectSourceCard?.InstanceId;   // AS-IS :736 hashtable cause

            switch (_mode)
            {
                case Mode.AddHand:   // AS-IS :765-784
                    foreach (CardSource cardSource in _targetCards)
                    {
                        SetFaceMirror(context, cardSource);   // AS-IS cardSource.SetFace()

                        if (cardSource.IsDigiEgg)
                        {
                            // AS-IS: a selected DigiEgg goes to the LIBRARY BOTTOM instead of the hand.
                            var eggSink = NewSink(context);
                            eggSink.Apply(new EffectMutation(
                                MatchStateMutationSink.ReturnToDeckBottomKind,
                                _sourceEntityId,
                                new Dictionary<string, object?>(StringComparer.Ordinal)
                                {
                                    [MatchStateMutationSink.TargetEntityIdKey] = cardSource.InstanceId.Value,
                                }));
                            await eggSink.FlushAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            PermanentView host = cardSource.PermanentOfThisCard();
                            if (!host.IsEmpty && host.DigivolutionCards.Any(stacked => stacked.InstanceId == cardSource.InstanceId))
                            {
                                // AS-IS RemoveDigivolveRootEffect(cardSource, PermanentOfThisCard()) — detach the
                                // buried source from its stack before the hand add (mirror Permanent.RemoveCardSource,
                                // the MIG4 AS-IS-anchored detach).
                                Permanent hostPermanent = ICardEffect.ResolvePermanentOfThisCard(cardSource);
                                if (hostPermanent != null)
                                {
                                    await hostPermanent.RemoveCardSource(cardSource).ConfigureAwait(false);
                                }
                            }

                            handCards.Add(cardSource);
                        }
                    }

                    break;

                case Mode.Discard:   // AS-IS :786-813 — the AS-IS whole-list quirk KEPT (the hand branch
                                     // discards ALL _targetCards whenever the scanned card is on hand; later
                                     // iterations fall through to the no-op trash move, exactly as AS-IS).
                    foreach (CardSource cardSource in _targetCards)
                    {
                        if (Commons.IsExistOnHand(cardSource))
                        {
                            List<IDiscardHand> discardHands = _targetCards
                                .Select(targetCard => new IDiscardHand(targetCard))
                                .ToList();
                            await new IDiscardHands(discardHands, causeId, _cardEffect).DiscardHands().ConfigureAwait(false);
                        }
                        else if (Commons.IsExistLinked(cardSource))
                        {
                            await new ITrashLinkCards(
                                ICardEffect.ResolvePermanentOfThisCard(cardSource),
                                new List<CardSource> { cardSource },
                                causeId, _cardEffect).TrashLinkCards().ConfigureAwait(false);
                        }
                        // After IsExistLinked, this would be digivolution cards, or topcard which should have
                        // been disbarred by selection condition. (AS-IS comment kept.)
                        else if (Commons.IsExistOnBattleArea(cardSource))
                        {
                            await new ITrashDigivolutionCards(
                                ICardEffect.ResolvePermanentOfThisCard(cardSource),
                                new List<CardSource> { cardSource },
                                causeId, _cardEffect).TrashDigivolutionCards().ConfigureAwait(false);
                        }
                        else
                        {
                            // AS-IS CardObjectController.AddTrashCard(cardSource).
                            var trashSink = NewSink(context);
                            trashSink.Apply(new EffectMutation(
                                MatchStateMutationSink.TrashCardKind,
                                _sourceEntityId,
                                new Dictionary<string, object?>(StringComparer.Ordinal)
                                {
                                    [MatchStateMutationSink.TargetEntityIdKey] = cardSource.InstanceId.Value,
                                }));
                            await trashSink.FlushAsync().ConfigureAwait(false);
                        }
                    }

                    break;

                case Mode.PlayForFree:   // AS-IS :815-823 — the W3 AS-IS-signature PlayPermanentCards bridge.
                    await Commons.PlayPermanentCards(
                        cardSources: _targetCards,
                        activateClass: _cardEffect!,
                        payCost: false,
                        isTapped: false,
                        root: _root,
                        activateETB: true).ConfigureAwait(false);
                    break;

                case Mode.PlayForCost:   // AS-IS :826-962.
                {
                    // AS-IS :828-847 local predicates.
                    bool PermanentsCondition(List<Permanent> targetPermanents)
                    {
                        if (targetPermanents == null)
                        {
                            return true;
                        }
                        else
                        {
                            if (targetPermanents.Count(targetPermanent => targetPermanent != null) == 0)
                            {
                                return true;
                            }
                        }

                        return false;
                    }

                    bool SharedCardCondition(CardSource cardSource) => _targetCards.Contains(cardSource);
                    bool RootCondition(Root root) => true;
                    bool CanUseCondition(Hashtable hashtable) => true;

                    // RD-W4-1: the reduce/fixed-cost halves register a transient ChangeCostClass on the select
                    // player's UntilCalculateFixedCostEffect bucket (LIVE in the mirror Player since W3c) for the
                    // DURATION of the play, then release it — SelectHandEffect.ActivatePlayForCostAsync idiom.
                    var selectPlayer = new Player(context, _selectPlayer);

                    // AS-IS :850-895 reduce cost.
                    Func<EffectTiming, ICardEffect>? getChangeCostEffect = null;
                    if (_reduceCostTuple != null)
                    {
                        bool CardCondition(CardSource cardSource) =>
                            SharedCardCondition(cardSource)
                            && (_reduceCostTuple.Value.reduceCostCardCondition == null || _reduceCostTuple.Value.reduceCostCardCondition(cardSource));

                        int ChangeCost(CardSource cardSource, int Cost, Root root, List<Permanent> targetPermanents)
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

                    // AS-IS :899-943 set fixed cost.
                    Func<EffectTiming, ICardEffect>? getFixedCostEffect = null;
                    if (_fixedCostTuple != null)
                    {
                        bool CardCondition(CardSource cardSource) =>
                            SharedCardCondition(cardSource)
                            && (_fixedCostTuple.Value.fixedCostCardCondition == null || _fixedCostTuple.Value.fixedCostCardCondition(cardSource));

                        int ChangeCost(CardSource cardSource, int Cost, Root root, List<Permanent> targetPermanents)
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

                    // AS-IS :944-951 — NOTE the AS-IS quirk: the play routes with root: Root.Hand here.
                    await Commons.PlayPermanentCards(
                        cardSources: _targetCards,
                        activateClass: _cardEffect!,
                        payCost: true,
                        isTapped: false,
                        root: Root.Hand,
                        activateETB: true).ConfigureAwait(false);

                    // AS-IS :953-960 release effect.
                    if (getChangeCostEffect != null)
                    {
                        selectPlayer.UntilCalculateFixedCostEffect.Remove(getChangeCostEffect);
                    }

                    if (getFixedCostEffect != null)
                    {
                        selectPlayer.UntilCalculateFixedCostEffect.Remove(getFixedCostEffect);
                    }

                    break;
                }

                case Mode.Custom:        // AS-IS :964-972.
                    if (_selectCardCoroutine != null)
                    {
                        foreach (CardSource cardSource in _targetCards)
                        {
                            await _selectCardCoroutine(cardSource).ConfigureAwait(false);
                        }
                    }

                    break;
            }

            if (handCards.Count >= 1)
            {
                // AS-IS :975-978 CardObjectController.AddHandCards(handCards, false, _cardEffect) — ONE call =
                // ONE add-hand batch: one sink flush shares one add-hand batch id across the N cards.
                var handSink = NewSink(context);
                foreach (CardSource cardSource in handCards)
                {
                    handSink.Apply(new EffectMutation(
                        MatchStateMutationSink.ReturnToHandKind,
                        _sourceEntityId,
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            [MatchStateMutationSink.TargetEntityIdKey] = cardSource.InstanceId.Value,
                        }));
                }

                await handSink.FlushAsync().ConfigureAwait(false);
            }
        }

        if (_afterSelectCardCoroutine != null)
        {
            await _afterSelectCardCoroutine(_targetCards).ConfigureAwait(false);
        }

        if (_afterSelectIndexCoroutine != null)
        {
            await _afterSelectIndexCoroutine(_slectedInexesInList).ConfigureAwait(false);
        }
    }

    /// <summary>The player selection (AS-IS SelectCardPanel semantics — the verified W3 formula,
    /// SelectCardPanel.cs:451/527/568): batch ChoiceRequest when no path-dependent per-pick filter (with
    /// <c>canEndSelectCondition</c> as the SelectionValidator), incremental one-pick loop when
    /// <c>canTargetCondition_ByPreSelecetedList</c> is present. Candidates include the whole pool with the
    /// unselectable cards flagged (the AS-IS panel shows them greyed). Count rule: CanEndSelection =
    /// (cond ∧) (canEndNotMax ∨ count==max) — so the minimum is 0 when canNoSelect()/canEndNotMax, else the
    /// max, CLAMPED to the selectable count (the established substrate clamp — AS-IS callers pre-clamp with
    /// Math.Min at every real call site, e.g. BT1_011). Returns the picks plus their indices in the pool
    /// list (AS-IS SelectedIndex; panel ordering differs — RD-W4-7).</summary>
    private async Task<(List<CardSource> Selected, List<int> SelectedIndices)> RunAsIsSelectionAsync(
        EngineContext context, List<CardSource> rootCards)
    {
        var selected = new List<CardSource>();
        var selectedIndices = new List<int>();
        bool canNoSelect = _canNoSelect_Func?.Invoke() ?? false;
        int selectableCount = rootCards.Count(CanSelectCardAsIs);
        int maxCount = Math.Min(_maxCount, selectableCount);
        string message = BuildAsIsMessage();
        ChoiceZone zone = MapRoot(_root);

        if (maxCount < 1)
        {
            return (selected, selectedIndices);
        }

        if (_canTargetCondition_ByPreSelecetedList == null)
        {
            ChoiceCandidate[] candidates = rootCards
                .Select(cardSource => new ChoiceCandidate(
                    cardSource.InstanceId, cardSource.InstanceId.Value, zone,
                    IsSelectable: CanSelectCardAsIs(cardSource), ownerId: cardSource.Owner))
                .ToArray();
            int minCount = (canNoSelect || _canEndNotMax) ? 0 : maxCount;
            var request = new ChoiceRequest(
                ChoiceType.Card, _selectPlayer, message, minCount, maxCount, canSkip: canNoSelect,
                zone, candidates);
            if (_canEndSelectConditionCard != null)
            {
                request = request with
                {
                    SelectionValidator = ids => _canEndSelectConditionCard(
                        ids.Select(id => rootCards.First(cardSource => cardSource.InstanceId == id)).ToList()),
                };
            }

            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);
            if (!result.IsSkipped)
            {
                foreach (HeadlessEntityId id in result.SelectedIds)
                {
                    int index = rootCards.FindIndex(cardSource => cardSource.InstanceId == id);
                    if (index >= 0 && !selectedIndices.Contains(index))
                    {
                        selected.Add(rootCards[index]);
                        selectedIndices.Add(index);
                    }
                }
            }

            return (selected, selectedIndices);
        }

        // Incremental path — the AS-IS panel's per-pick byPreSelectedList re-filter (SelectCardPanel.cs:451/527).
        // The un-pick-at-max corner is unreachable for prefix-monotone AS-IS conditions — RD-W4-2 (== RD-W3-1).
        while (selected.Count < maxCount)
        {
            List<CardSource> legal = rootCards
                .Where(cardSource => !selected.Contains(cardSource)
                    && CanSelectCardAsIs(cardSource)
                    && _canTargetCondition_ByPreSelecetedList(selected, cardSource))
                .ToList();
            if (legal.Count == 0)
            {
                break;
            }

            bool canEndNow = (canNoSelect && selected.Count == 0)
                || (_canEndNotMax && (_canEndSelectConditionCard == null || _canEndSelectConditionCard(selected)));
            var request = new ChoiceRequest(
                ChoiceType.Card, _selectPlayer, message,
                minCount: canEndNow ? 0 : 1, maxCount: 1, canSkip: canEndNow, zone,
                legal.Select(cardSource => new ChoiceCandidate(
                    cardSource.InstanceId, cardSource.InstanceId.Value, zone,
                    IsSelectable: true, ownerId: cardSource.Owner)).ToArray());
            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);
            if (result.IsSkipped || result.SelectedIds.Count == 0)
            {
                break;
            }

            int index = rootCards.FindIndex(cardSource => cardSource.InstanceId == result.SelectedIds[0]);
            if (index < 0)
            {
                break;
            }

            selected.Add(rootCards[index]);
            selectedIndices.Add(index);
        }

        return (selected, selectedIndices);
    }

    /// <summary>The AS-IS player-facing prompt: the panel message when given (AS-IS OpenSelectCardPanel
    /// <c>Message: _message</c>), else the custom command text (:398-401), else the per-Mode default
    /// (:404-427, verbatim strings).</summary>
    private string BuildAsIsMessage()
    {
        if (!string.IsNullOrEmpty(_message) && _message != "Select card(s).")
        {
            return _message;
        }

        if (!string.IsNullOrEmpty(_customMessage))
        {
            return _customMessage!;
        }

        return _mode switch
        {
            Mode.AddHand => "Select cards to add to your hand.",
            Mode.Discard => "Select cards to trash.",
            Mode.PlayForFree => "Select cards to play without paying the cost.",
            Mode.PlayForCost => "Select cards to play.",
            Mode.Custom => "Select cards.",
            _ => "Select cards.",
        };
    }

    /// <summary>AS-IS <c>cardSource.SetFace()</c> — turn the card face up (clear the shared
    /// <c>isFlipped</c> instance flag; the established metadata round-trip pattern).</summary>
    private static void SetFaceMirror(EngineContext context, CardSource cardSource)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardSource.InstanceId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        if (!record.Metadata.ContainsKey("isFlipped"))
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        metadata.Remove("isFlipped");
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    private EngineContext? ResolveContext() => _context ?? _cardEffect?.EffectSourceCard?.Context;

    private EngineContext RequireContext() =>
        ResolveContext()
        ?? throw new InvalidOperationException(
            "SelectCardEffect has no EngineContext — obtain the instance via " +
            "GManager.instance.GetComponent<SelectCardEffect>() (bridge W4).");

    private static MatchStateMutationSink NewSink(EngineContext context) =>
        new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: context.MemoryController,
            context.GameEventQueue, context: context);
}
