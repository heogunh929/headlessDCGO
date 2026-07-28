// Source: DCGO/Assets/Scripts/Script/SelectJogressEffect.cs (428 lines)
// (SpecialPlay re-migration) Mirror of the AS-IS SelectJogressEffect component — the Jogress / DNA-Digivolution
// pre-play selection flow the play pipeline reaches through GManager.instance.selectJogressEffect (AS-IS
// TurnStateMachine.cs:2203/2346 and CardEffectCommons/DNADigivolveEffects.cs:559-570).
//
// This file REPLACES the former "left intentionally bodiless" note: the substrate DNA path it deferred to
// (Headless/Runtime/SpecialPlayAction.cs + SpecialPlayRecipeRegistry, an off-AS-IS invention) is deleted, and
// its rule logic re-migrates HERE, its AS-IS home. The card DECLARES its recipe with AddJogressConditionClass
// (CardSource.jogressCondition -> the mirror JogressConditionOf() accessor); this component CONSUMES it.
//
// Substrate translations only (bigbang §5), sibling-for-sibling with SelectBurstDigivolutionEffect.cs:
//   * IEnumerator -> Task; `yield return ContinuousController.instance.StartCoroutine(X)` -> `await X`;
//     a lone `yield return null` callback body -> `Task.CompletedTask`.
//   * `_card.jogressCondition` -> `_card.JogressConditionOf()` (the P6C1 accessor; AS-IS home CardSource.cs is
//     another remediation cluster's file, relocation design item RD-P6C1-9).
//   * AS-IS card-less `CardEffectCommons.MatchConditionPermanentCount(cond)` (global scan) -> the sole mirror
//     overload `MatchConditionPermanentCount(card, cond)` (KeyWordEffects/Save.cs header: any live card is the
//     scan's context handle).
//   * `_card.Owner` (AS-IS Player) -> HeadlessPlayerId + its `GetBattleAreaDigimons()` extension (Player.cs),
//     except SelectDNACondition.SetUp which takes the mirror `Player` -> `new Player(context, _card.Owner)`.
//   * `GManager.instance.selectCardPanel.OpenSelectCardPanel` two-option method panel -> ModeChoice
//     (the ESTABLISHED adaptation, SelectAppFusionEffect / SelectBurstDigivolutionEffect.SelectWheterToBurst;
//     SelectedIndex empty = no-select).
//   * `permanent.ShowingPermanentCard.SetPermanentIndexText/OffPermanentIndexText` = UI (stripped) — the
//     selection-order badge drawn on the already-picked evo roots, no rule effect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

// (SelectDigiXrosClass.cs:50 convention) the AS-IS `CardEffectCommons.X` call shape needs an alias here: from
// inside namespace ...Script the bare name `CardEffectCommons` binds to the NAMESPACE, not the static class.
using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;

public class SelectJogressEffect
{
    // AS-IS :9-23.
    public void SetUp_SelectWheterToJogress(
        CardSource card,
        CardSource evoRoot,
        bool canNoSelect,
        Func<Task> endSelectCoroutine_Digivolve,
        Func<Task> endSelectCoroutine_Jogress,
        Func<Task> noSelectCoroutine)
    {
        _card = card;
        _evoRoot = evoRoot;
        _canNoSelect = canNoSelect;
        _endSelectCoroutine_Digivolve = endSelectCoroutine_Digivolve;
        _endSelectCoroutine_Jogress = endSelectCoroutine_Jogress;
        _noSelectCoroutine = noSelectCoroutine;
    }

    // AS-IS :25-41.
    public void SetUp_SelectDigivolutionRoots(
        CardSource card,
        bool isLocal,
        bool isPayCost,
        bool canNoSelect,
        Func<List<Permanent>, Task> endSelectCoroutine_SelectDigivolutionRoots,
        Func<Task> noSelectCoroutine)
    {
        _card = card;
        _isLocal = isLocal;
        _isPayCost = isPayCost;
        _canNoSelect = canNoSelect;
        _endSelectCoroutine_SelectDigivolutionRoots = endSelectCoroutine_SelectDigivolutionRoots;
        _noSelectCoroutine = noSelectCoroutine;

        _customPermanentConditions = null;
    }

    // AS-IS :43-46.
    public void SetUpCustomPermanentConditions(Func<Permanent, bool>[] customPermanentConditions)
    {
        _customPermanentConditions = customPermanentConditions.CloneArray();
    }

    // AS-IS :48-58.
    CardSource _card = null;
    CardSource _evoRoot = null;
    bool _isLocal = false;

    bool _isPayCost = false;
    bool _canNoSelect = false;
    Func<Task> _endSelectCoroutine_Digivolve = null;
    Func<Task> _endSelectCoroutine_Jogress = null;
    Func<List<Permanent>, Task> _endSelectCoroutine_SelectDigivolutionRoots = null;
    Func<Task> _noSelectCoroutine = null;
    Func<Permanent, bool>[] _customPermanentConditions = null;

    private EngineContext? _context;

    /// <summary>Match-context injection (mirrors SelectBurstDigivolutionEffect.AttachContext).</summary>
    public void AttachContext(EngineContext context) => _context = context;

    private EngineContext RequireContext() => _context ?? _card?.Context ?? AmbientMatchContext.Require();

    // AS-IS :59-138.
    public async Task SelectWheterToJogress()
    {
        if (_card != null)
        {
            if (_evoRoot != null)
            {
                CardSource anotherEvoRootCard = null;

                List<Permanent> anotherEvoRootPermanentCandidates = new List<Permanent>();

                // `_evoRoot.PermanentOfThisCard()` (AS-IS Permanent) -> the established PermanentView bridge
                // `ICardEffect.ResolvePermanentOfThisCard` (CEntity_EffectController.cs:83 / AutoProcessing.cs:875).
                Permanent evoRootPermanent = ICardEffect.ResolvePermanentOfThisCard(_evoRoot);

                foreach (Permanent permanent in _card.Owner.GetBattleAreaDigimons())
                {
                    if (permanent != evoRootPermanent)
                    {
                        if (_card.CanJogressFromTargetPermanent(permanent, false))
                        {
                            if (_card.CanJogressFromTargetPermanents(new List<Permanent>() { evoRootPermanent, permanent }, false))
                            {
                                // AS-IS `TopCard.CardID` = CEntity_Base.CardID = the PRINTED card id -> CardNumber.
                                if (anotherEvoRootPermanentCandidates.Count((permanent1) => permanent1.TopCard.CardNumber == permanent.TopCard.CardNumber) == 0)
                                {
                                    anotherEvoRootPermanentCandidates.Add(permanent);
                                }
                            }
                        }
                    }
                }

                if (anotherEvoRootPermanentCandidates.Count == 1)
                {
                    anotherEvoRootCard = anotherEvoRootPermanentCandidates[0].TopCard;
                }

                // AS-IS :91-105 OpenSelectCardPanel two-option ("Normal Digivolution" / "DNA Digivollution")
                // -> ModeChoice (the established adaptation). The AS-IS `evoRootsArray`
                // ({_evoRoot} / {_evoRoot, anotherEvoRootCard}) is the panel's evo-root PREVIEW artwork — UI
                // only; `anotherEvoRootCard` is computed above verbatim because that computation is AS-IS.
                _ = anotherEvoRootCard;

                EngineContext context = RequireContext();
                var candidates = new List<ChoiceCandidate>
                {
                    new ChoiceCandidate(new HeadlessEntityId("jogressDigivolution#0"), "Normal Digivolution", ChoiceZone.BattleArea, IsSelectable: true, ownerId: _card.Owner),
                    new ChoiceCandidate(new HeadlessEntityId("jogressDigivolution#1"), "DNA Digivollution", ChoiceZone.BattleArea, IsSelectable: true, ownerId: _card.Owner),
                };
                var request = new ChoiceRequest(
                    ChoiceType.ModeChoice, _card.Owner, "With which method would you like to Digivolve?",
                    minCount: _canNoSelect ? 0 : 1, maxCount: 1, canSkip: _canNoSelect, ChoiceZone.BattleArea, candidates);
                ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);

                int selectedIndex = -1;
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
                            if (_endSelectCoroutine_Jogress != null)
                            {
                                await _endSelectCoroutine_Jogress().ConfigureAwait(false);
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

    // AS-IS :139-427.
    public async Task SelectDigivolutionRoots()
    {
        bool active = false;
        SelectPermanentEffect selectPermanentEffect = null;

        if (_card != null)
        {
            if (_card.CanPlayJogress(_isPayCost))
            {
                if (_card.JogressConditionOf().Count > 0)
                {
                    foreach (JogressCondition dnaCondition in _card.JogressConditionOf())
                    {
                        if (dnaCondition != null)
                        {
                            if (dnaCondition.elements.Length == 2)
                            {
                                if (GManager.instance != null)
                                {
                                    if (GManager.instance.turnStateMachine != null)
                                    {
                                        if (GManager.instance.turnStateMachine.gameContext != null)
                                        {
                                            if (GManager.instance.turnStateMachine.gameContext.ActiveCardList.Count >= 1)
                                            {
                                                selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                                                if (selectPermanentEffect != null)
                                                {
                                                    active = true;
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

        if (active)
        {
            List<Permanent> selectedEvoRoots = new List<Permanent>();
            JogressCondition selectedDNA = _card.JogressConditionOf()[0];

            if (_card.JogressConditionOf().Count > 1)
            {
                #region select DNA condition
                SelectDNACondition selectDNACondition = GManager.instance.GetComponent<SelectDNACondition>();
                selectDNACondition.SetUp(new Player(_card.Context, _card.Owner), _card, SelectDNA, _isLocal);

                await selectDNACondition.Activate().ConfigureAwait(false);

                Task SelectDNA(int dnaSelection)
                {
                    selectedDNA = _card.JogressConditionOf()[dnaSelection];

                    return Task.CompletedTask;
                }
                #endregion
            }

            for (int i = 0; i < selectedDNA.elements.Length; i++)
            {
                JogressConditionElement element = selectedDNA.elements[i];

                bool CanSelectPermanentCondition(Permanent permanent)
                {
                    if (Commons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, _card))
                    {
                        if (!_card.CanNotEvolve(permanent))
                        {
                            if (!selectedEvoRoots.Contains(permanent))
                            {
                                if (element.EvoRootCondition != null)
                                {
                                    if (element.EvoRootCondition(permanent))
                                    {
                                        if (_customPermanentConditions != null)
                                        {
                                            if (_customPermanentConditions.Length == 1)
                                            {
                                                if (_customPermanentConditions[0] != null)
                                                {
                                                    if (_card.Owner.GetBattleAreaDigimons().Count(_customPermanentConditions[0]) >= 1)
                                                    {
                                                        if (selectedEvoRoots.Count(_customPermanentConditions[0]) == 0)
                                                        {
                                                            if (!_customPermanentConditions[0](permanent))
                                                            {
                                                                if (i == 1)
                                                                {
                                                                    return false;
                                                                }

                                                                else
                                                                {
                                                                    JogressConditionElement element1 = selectedDNA.elements[1];

                                                                    if (_card.Owner.GetBattleAreaDigimons().Count((permanent1) => permanent1 != permanent && element1.EvoRootCondition(permanent1) && _customPermanentConditions[0](permanent1)) == 0)
                                                                    {
                                                                        return false;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }

                                                    else
                                                    {
                                                        return false;
                                                    }
                                                }
                                            }

                                            else if (_customPermanentConditions.Length == 2)
                                            {
                                                if (_customPermanentConditions[0] != null && _customPermanentConditions[1] != null)
                                                {
                                                    if (_card.Owner.GetBattleAreaDigimons().Count(_customPermanentConditions[0]) >= 1 && _card.Owner.GetBattleAreaDigimons().Count(_customPermanentConditions[1]) >= 1)
                                                    {
                                                        if (i == 1)
                                                        {
                                                            if (selectedEvoRoots.Count(_customPermanentConditions[0]) == 0 && selectedEvoRoots.Count(_customPermanentConditions[1]) == 0)
                                                            {
                                                                return false;
                                                            }

                                                            if (selectedEvoRoots.Count(_customPermanentConditions[0]) == 0 && selectedEvoRoots.Count(_customPermanentConditions[1]) == 1)
                                                            {
                                                                if (!_customPermanentConditions[0](permanent))
                                                                {
                                                                    return false;
                                                                }
                                                            }

                                                            if (selectedEvoRoots.Count(_customPermanentConditions[0]) == 1 && selectedEvoRoots.Count(_customPermanentConditions[1]) == 0)
                                                            {
                                                                if (!_customPermanentConditions[1](permanent))
                                                                {
                                                                    return false;
                                                                }
                                                            }
                                                        }

                                                        else
                                                        {
                                                            if (_customPermanentConditions[0](permanent) || _customPermanentConditions[1](permanent))
                                                            {
                                                                JogressConditionElement element1 = selectedDNA.elements[1];

                                                                if (_customPermanentConditions[0](permanent))
                                                                {
                                                                    if (_card.Owner.GetBattleAreaDigimons().Count((permanent1) => permanent1 != permanent && element1.EvoRootCondition(permanent1) && _customPermanentConditions[1](permanent1)) == 0)
                                                                    {
                                                                        return false;
                                                                    }
                                                                }

                                                                if (_customPermanentConditions[1](permanent))
                                                                {
                                                                    if (_card.Owner.GetBattleAreaDigimons().Count((permanent1) => permanent1 != permanent && element1.EvoRootCondition(permanent1) && _customPermanentConditions[0](permanent1)) == 0)
                                                                    {
                                                                        return false;
                                                                    }
                                                                }
                                                            }

                                                            else
                                                            {
                                                                return false;
                                                            }
                                                        }
                                                    }

                                                    else
                                                    {
                                                        return false;
                                                    }
                                                }
                                            }
                                        }

                                        if (i == 0)
                                        {
                                            List<Permanent> nextCandidates = _card.Owner.GetBattleAreaDigimons()
                                                .Filter(permanent1 => permanent1 != permanent);

                                            if (nextCandidates.Count(selectedDNA.elements[1].EvoRootCondition) == 0)
                                            {
                                                return false;
                                            }
                                        }

                                        return true;
                                    }
                                }
                            }
                        }
                    }

                    return false;
                }

                int maxCount = Math.Min(1, Commons.MatchConditionPermanentCount(_card, CanSelectPermanentCondition));

                Permanent selectedPermanent = null;

                if (maxCount >= 1)
                {
                    selectPermanentEffect.SetUp(
                    selectPlayer: _card.Owner,
                    canTargetCondition: CanSelectPermanentCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: maxCount,
                    canNoSelect: _canNoSelect,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: null);

                    selectPermanentEffect.SetUpCustomMessage($"Select {element.SelectMessage}.", $"The opponent is selecting {element.SelectMessage}.");

                    if (this._isLocal)
                    {
                        selectPermanentEffect.SetIsLocal();
                    }

                    await selectPermanentEffect.Activate().ConfigureAwait(false);

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;

                        return Task.CompletedTask;
                    }
                }

                if (selectedPermanent == null)
                {
                    break;
                }

                else
                {
                    selectedEvoRoots.Add(selectedPermanent);

                    // AS-IS :391-394 `permanent.ShowingPermanentCard.SetPermanentIndexText(selectedEvoRoots)`
                    // = the selection-order badge = UI (stripped).
                }
            }

            //do not jogress
            if (selectedEvoRoots.Count != selectedDNA.elements.Length)
            {
                if (_noSelectCoroutine != null)
                {
                    await _noSelectCoroutine().ConfigureAwait(false);
                }
            }

            //do jogress
            else
            {
                if (_endSelectCoroutine_SelectDigivolutionRoots != null)
                {
                    await _endSelectCoroutine_SelectDigivolutionRoots(selectedEvoRoots).ConfigureAwait(false);
                }
            }

            // AS-IS :416-425 `OffPermanentIndexText()` over every battle-area Digimon = UI (stripped).
        }
    }
}
