// Source: DCGO/Assets/Scripts/Script/SelectAppFusionEffect.cs (241 lines)
// (R5-C) 1:1 mirror of the AS-IS SelectAppFusionEffect component — the App-Fusion pre-digivolve selection
// flow the play pipeline reaches through GManager.instance.selectAppFusionEffect (CardController.cs:786-793,
// the "which method to digivolve" branch + the link-card -> source conversion). Substrate translations only
// (bigbang §5): IEnumerator -> Task, StartCoroutine(x) -> await x, ContinuousController.instance.StartCoroutine
// -> await; UI/Photon stripped. Candidate enumeration, gates, ordering and the STATE fields are AS-IS verbatim.
//
// The one ADAPTATION is the two-option "With which method would you like to Digivolve?" panel
// (SelectAppFusionEffect.cs:60-74 GManager.instance.selectCardPanel.OpenSelectCardPanel with two card copies +
// titleStrings, read back as SelectedIndex): the established ChoicePort substrate has no card-panel-with-titles
// surface, so it maps to a ChoiceType.ModeChoice request (min 1 / max 1 / canSkip = _canNoSelect, two synthetic
// labeled options), exactly the UserSelectionManager.WaitForEndSelect / SelectCardEffect selection formula
// (synthetic id "appFusion#{index}", index parsed back off the id). The AS-IS SelectedIndex semantics
// (empty = no-select -> _noSelectCoroutine) are preserved.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public class SelectAppFusionEffect
{
    // AS-IS :9-23.
    public void SetUp_SelectWheterToAppFusion(
        CardSource card,
        CardSource evoRoot,
        bool canNoSelect,
        Func<Task> endSelectCoroutine_Digivolve,
        Func<Task> endSelectCoroutine_AppFusion,
        Func<Task> noSelectCoroutine)
    {
        _card = card;
        EvoRoot = evoRoot;
        _canNoSelect = canNoSelect;
        _endSelectCoroutine_Digivolve = endSelectCoroutine_Digivolve;
        _endSelectCoroutine_AppFusion = endSelectCoroutine_AppFusion;
        _noSelectCoroutine = noSelectCoroutine;
    }

    // AS-IS :25-39.
    public void SetUp_SelectLink(
        CardSource card,
        bool isLocal,
        bool isPayCost,
        bool canNoSelect,
        Func<CardSource, Task> endSelectCoroutine_SelectLink,
        Func<Task> noSelectCoroutine)
    {
        _card = card;
        _isLocal = isLocal;
        _isPayCost = isPayCost;
        _canNoSelect = canNoSelect;
        _endSelectCoroutine_SelectLink = endSelectCoroutine_SelectLink;
        _noSelectCoroutine = noSelectCoroutine;
    }

    // AS-IS :41-52.
    CardSource _card = null;
    public CardSource EvoRoot = null;
    bool _isLocal = false;

    bool _isPayCost = false;
    bool _canNoSelect = false;
    Func<Task> _endSelectCoroutine_Digivolve = null;
    Func<Task> _endSelectCoroutine_AppFusion = null;
    Func<CardSource, Task> _endSelectCoroutine_SelectLink = null;
    Func<Task> _noSelectCoroutine = null;
    public bool LinkAdded { get; private set; } = false;
    public CardSource selectedLink = null;

    private EngineContext? _context;

    /// <summary>(R5-C) The match context the AS-IS <c>GManager.instance.GetComponent&lt;…&gt;()</c> route injects
    /// (mirrors SelectCardEffect.AttachContext). Registration of this component on the mirror GManager is a
    /// separate wiring item — see the file header / RD-R5 report.</summary>
    public void AttachContext(EngineContext context) => _context = context;

    private EngineContext RequireContext() => _context ?? AmbientMatchContext.Require();

    // AS-IS :54-107.
    public async Task SelectWheterToAppFusion()
    {
        if (_card != null)
        {
            if (EvoRoot != null)
            {
                // AS-IS OpenSelectCardPanel two-option ("Normal Digivolution" / "App Fusion") -> ModeChoice.
                EngineContext context = RequireContext();
                var candidates = new List<ChoiceCandidate>
                {
                    new ChoiceCandidate(new HeadlessEntityId("appFusion#0"), "Normal Digivolution", ChoiceZone.BattleArea, IsSelectable: true, ownerId: _card.Owner),
                    new ChoiceCandidate(new HeadlessEntityId("appFusion#1"), "App Fusion", ChoiceZone.BattleArea, IsSelectable: true, ownerId: _card.Owner),
                };
                var request = new ChoiceRequest(
                    ChoiceType.ModeChoice, _card.Owner, "With which method would you like to Digivolve?",
                    minCount: _canNoSelect ? 0 : 1, maxCount: 1, canSkip: _canNoSelect, ChoiceZone.BattleArea, candidates);
                ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);

                int selectedIndex = -1;   // AS-IS SelectedIndex.Count > 0 <=> a pick was made.
                if (!result.IsSkipped && result.SelectedIds.Count > 0)
                {
                    string[] parts = result.SelectedIds[0].Value.Split('#');
                    if (int.TryParse(parts.Length > 1 ? parts[1] : null, out int picked))
                    {
                        selectedIndex = picked;
                    }
                }

                if (selectedIndex >= 0)
                {
                    int index = selectedIndex;

                    switch (index)
                    {
                        case 0:
                            if (_endSelectCoroutine_Digivolve != null)
                            {
                                await _endSelectCoroutine_Digivolve().ConfigureAwait(false);
                            }
                            break;

                        case 1:
                            if (_endSelectCoroutine_AppFusion != null)
                            {
                                await _endSelectCoroutine_AppFusion().ConfigureAwait(false);
                            }
                            break;
                    }
                }

                else
                {
                    if (_noSelectCoroutine != null)
                    {
                        await _noSelectCoroutine().ConfigureAwait(false);
                    }
                }
            }
        }
    }

    // AS-IS :109-218.
    public async Task SelectLink(Permanent targetPermanent)
    {
        bool active = false;
        SelectCardEffect selectCardEffect = null;

        if (_card != null && EvoRoot != null)
        {
            if (_card.AppFusionConditionOf() != null)
            {
                if (GManager.instance != null)
                {
                    if (GManager.instance.turnStateMachine != null)
                    {
                        if (GManager.instance.turnStateMachine.gameContext != null)
                        {
                            if (GManager.instance.turnStateMachine.gameContext.ActiveCardList.Count >= 1)
                            {
                                selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                                if (selectCardEffect != null)
                                {
                                    active = true;
                                }
                            }
                        }
                    }
                }
            }
        }

        if (active)
        {
            AppFusionCondition appFusionCondition = _card.AppFusionConditionOf();

            bool CanSelectSourceCondition(CardSource link)
            {
                if (appFusionCondition != null)
                {
                    if (appFusionCondition.linkedCondition != null)
                    {
                        if (link != null)
                        {
                            if (appFusionCondition.linkedCondition(targetPermanent, link))
                            {
                                return true;
                            }
                        }
                    }
                }


                return false;
            }

            int maxCount = Math.Min(1, targetPermanent.LinkedCards.Count(CanSelectSourceCondition));

            if (maxCount >= 1)
            {
                selectCardEffect.SetUp(
                canTargetCondition: CanSelectSourceCondition,
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                canNoSelect: () => _canNoSelect,
                selectCardCoroutine: SelectCardCoroutine,
                afterSelectCardCoroutine: null,
                message: $"Select 1 card to add as source.",
                maxCount: maxCount,
                canEndNotMax: false,
                isShowOpponent: true,
                mode: SelectCardEffect.Mode.Custom,
                root: SelectCardEffect.Root.Custom,
                customRootCardList: targetPermanent.LinkedCards,
                canLookReverseCard: false,
                selectPlayer: _card.Owner,
                cardEffect: null);

                if (this._isLocal)
                {
                    selectCardEffect.SetIsLocal();
                }

                await selectCardEffect.Activate().ConfigureAwait(false);

                Task SelectCardCoroutine(CardSource source)
                {
                    selectedLink = source;

                    return Task.CompletedTask;
                }
            }

            // バースト進化しない
            if (selectedLink == null)
            {
                if (_noSelectCoroutine != null)
                {
                    await _noSelectCoroutine().ConfigureAwait(false);
                }
            }

            // バースト進化する
            else
            {
                // AS-IS quirk KEPT (SelectAppFusionEffect.cs:212): the guard tests _endSelectCoroutine_AppFusion
                // but the invoked delegate is _endSelectCoroutine_SelectLink (verbatim, no-simplification rule).
                if (_endSelectCoroutine_AppFusion != null)
                {
                    await _endSelectCoroutine_SelectLink(selectedLink).ConfigureAwait(false);
                }
            }
        }
    }

    // AS-IS :220-240.
    public async Task AddToSources(CardSource link)
    {
        LinkAdded = false;

        if (link != null)
        {
            // AS-IS link.PermanentOfThisCard() (Permanent, non-null-gated) — the mirror materialises the owning
            // stack as a real Permanent via the established ICardEffect.ResolvePermanentOfThisCard idiom.
            Permanent linkPermanent = ICardEffect.ResolvePermanentOfThisCard(link);
            if (linkPermanent != null)
            {
                if (link.Owner.GetBattleAreaDigimons().Contains(linkPermanent))
                {
                    // AS-IS UnityEngine.Debug.Log($"ADD SOURCES: {linkPermanent}, {link}") = UI/log (stripped).
                    await linkPermanent.RemoveLinkedCard(link).ConfigureAwait(false);
                    await linkPermanent.AddDigivolutionCardsTop(new List<CardSource> { link, linkPermanent.TopCard }, null).ConfigureAwait(false);

                    LinkAdded = true;
                }
            }
        }
    }
}
