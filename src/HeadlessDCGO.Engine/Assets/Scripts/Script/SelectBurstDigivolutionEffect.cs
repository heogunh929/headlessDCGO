// Source: DCGO/Assets/Scripts/Script/SelectBurstDigivolutionEffect.cs (345 lines)
// (R5-C) Mirror of the AS-IS SelectBurstDigivolutionEffect component — the Burst-Digivolution pre-play flow the
// play pipeline reaches through GManager.instance.selectBurstDigivolutionEffect. Substrate translations only
// (bigbang §5): IEnumerator -> Task, StartCoroutine(x) -> await x; UI/Photon stripped. STATE fields + SetUp are
// AS-IS verbatim; the two-option method panel is the same ModeChoice ADAPTATION as SelectAppFusionEffect.
//
// FULLY LANDED (2026-07-20): AddTrashTopCardAtTurnEnd (RD-R5-03) is 1:1 (Permanent.UntilEachTurnEndEffects +
// AceOverflowClass + CardObjectController zone statics all present; UI stripped). SelectTamer (RD-R5-01) is
// RESTORED 1:1 (2026-07-18): its head guard CardSource.CanPlayBurst(bool) is ported (CardSource.cs), riding the
// R2-C burst cost engine GetChangedCostItselef, and the SelectPermanent(Mode.Custom) tamer-selection body is a
// straight substrate port. BounceTamer (RD-R5-02) is RESTORED 1:1 (2026-07-20): the AS-IS HandBounceClaass bounce
// process maps to the MatchStateMutationSink ReturnToHand carrier (its established mirror; RDW-01 leave windows +
// centralised CannotReturnToHand/immunity gates) driven by a causeless burst-marked mutation, and
// Permanent.IsReturnedToHandByBurstDigivolution is now landed (bookkeeping-store sibling of IsBurstDigivolved).
// A FULL burst play now resolves the tamer selection AND the burst bounce end to end. Permanent.CannotReturnToHand
// (RD-EXT3-04) is available. See the per-method notes.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Collections;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public class SelectBurstDigivolutionEffect
{
    // AS-IS :9-23.
    public void SetUp_SelectWheterToBurst(
        CardSource card,
        CardSource evoRoot,
        bool canNoSelect,
        Func<Task> endSelectCoroutine_Digivolve,
        Func<Task> endSelectCoroutine_Burst,
        Func<Task> noSelectCoroutine)
    {
        _card = card;
        _evoRoot = evoRoot;
        _canNoSelect = canNoSelect;
        _endSelectCoroutine_Digivolve = endSelectCoroutine_Digivolve;
        _endSelectCoroutine_Burst = endSelectCoroutine_Burst;
        _noSelectCoroutine = noSelectCoroutine;
    }

    // AS-IS :25-39.
    public void SetUp_SelectTamer(
        CardSource card,
        bool isLocal,
        bool isPayCost,
        bool canNoSelect,
        Func<Permanent, Task> endSelectCoroutine_SelectTamer,
        Func<Task> noSelectCoroutine)
    {
        _card = card;
        _isLocal = isLocal;
        _isPayCost = isPayCost;
        _canNoSelect = canNoSelect;
        _endSelectCoroutine_SelectTamer = endSelectCoroutine_SelectTamer;
        _noSelectCoroutine = noSelectCoroutine;
    }

    // AS-IS :41-51.
    CardSource _card = null;
    CardSource _evoRoot = null;
    bool _isLocal = false;

    bool _isPayCost = false;
    bool _canNoSelect = false;
    Func<Task> _endSelectCoroutine_Digivolve = null;
    Func<Task> _endSelectCoroutine_Burst = null;
    Func<Permanent, Task> _endSelectCoroutine_SelectTamer = null;
    Func<Task> _noSelectCoroutine = null;
    public bool TamerBounced { get; private set; } = false;

    private EngineContext? _context;

    /// <summary>(R5-C) Match-context injection (mirrors SelectCardEffect.AttachContext); GManager registration of
    /// this component is a separate wiring item — see the RD-R5 report.</summary>
    public void AttachContext(EngineContext context) => _context = context;

    private EngineContext RequireContext() => _context ?? AmbientMatchContext.Require();

    // AS-IS :52-105.
    public async Task SelectWheterToBurst()
    {
        if (_card != null)
        {
            if (_evoRoot != null)
            {
                // AS-IS OpenSelectCardPanel two-option ("Normal Digivolution" / "Burst Digivollution")
                // -> ModeChoice (same ADAPTATION as SelectAppFusionEffect; SelectedIndex empty = no-select).
                EngineContext context = RequireContext();
                var candidates = new List<ChoiceCandidate>
                {
                    new ChoiceCandidate(new HeadlessEntityId("burstDigivolution#0"), "Normal Digivolution", ChoiceZone.BattleArea, IsSelectable: true, ownerId: _card.Owner),
                    new ChoiceCandidate(new HeadlessEntityId("burstDigivolution#1"), "Burst Digivollution", ChoiceZone.BattleArea, IsSelectable: true, ownerId: _card.Owner),
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
                            if (_endSelectCoroutine_Burst != null)
                            {
                                await _endSelectCoroutine_Burst().ConfigureAwait(false);
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

    // AS-IS :107-220 `IEnumerator SelectTamer()` (RD-R5-01 RESTORED 2026-07-18) — enumerate battle-area tamers
    // matching `_card.BurstDigivolutionConditionOf().tamerCondition` that `!CannotReturnToHand(null)`, SelectPermanent
    // one (Mode.Custom, the SelectPermanentCoroutine captures the pick), then route the pick to
    // `_endSelectCoroutine_SelectTamer` (or `_noSelectCoroutine` on no pick). The head guard `_card.CanPlayBurst(_isPayCost)`
    // is now live (CardSource.CanPlayBurst ported RD-R5-01; the burst cost engine GetChangedCostItselef is R2-C).
    // SUBSTRATE: IEnumerator→async Task, StartCoroutine(X)→await X, lone `yield return null`→Task.CompletedTask;
    // `_card.burstDigivolutionCondition`→`_card.BurstDigivolutionConditionOf()`; `_card.Owner.*`/`TopCard.Owner.*`
    // (AS-IS Player) → `new Player(context, ownerId).*`; the mirror SelectPermanentEffect canTargetCondition is
    // id-form, so the AS-IS Permanent-predicate is adapted via `new Permanent(context, id)` (BT21_030 precedent).
    // AS-IS QUIRK KEPT 1:1: the pick branch guards on `_endSelectCoroutine_Burst != null` (:214) but invokes
    // `_endSelectCoroutine_SelectTamer` (:216).
    public async Task SelectTamer()
    {
        bool active = false;
        SelectPermanentEffect selectPermanentEffect = null;

        if (_card != null)
        {
            if (_card.CanPlayBurst(_isPayCost))
            {
                if (_card.BurstDigivolutionConditionOf() != null)
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

        if (active)
        {
            EngineContext context = _card.Context;
            Permanent selectedTamer = null;

            BurstDigivolutionCondition burstDigivolutionCondition = _card.BurstDigivolutionConditionOf();

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (permanent != null)
                {
                    if (new Player(context, permanent.TopCard.Owner).GetBattleAreaPermanents().Contains(permanent))
                    {
                        if (burstDigivolutionCondition.tamerCondition != null)
                        {
                            if (burstDigivolutionCondition.tamerCondition(permanent))
                            {
                                if (!permanent.CannotReturnToHand(null))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }

            int maxCount = Math.Min(1, new Player(context, _card.Owner).GetBattleAreaPermanents().Count(CanSelectPermanentCondition));

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

                selectPermanentEffect.SetUpCustomMessage($"Select {burstDigivolutionCondition.selectTamerMessage}.", $"The opponent is selecting {burstDigivolutionCondition.selectTamerMessage}.");

                if (this._isLocal)
                {
                    selectPermanentEffect.SetIsLocal();
                }

                await selectPermanentEffect.Activate().ConfigureAwait(false);

                Task SelectPermanentCoroutine(Permanent permanent)
                {
                    selectedTamer = permanent;

                    return Task.CompletedTask;
                }
            }

            // AS-IS :202-209 — no pick: run the no-select branch.
            if (selectedTamer == null)
            {
                if (_noSelectCoroutine != null)
                {
                    await _noSelectCoroutine().ConfigureAwait(false);
                }
            }

            // AS-IS :211-218 — picked: route the tamer to the burst follow-up (AS-IS quirk: guards on Burst, calls SelectTamer).
            else
            {
                if (_endSelectCoroutine_Burst != null)
                {
                    await _endSelectCoroutine_SelectTamer(selectedTamer).ConfigureAwait(false);
                }
            }
        }
    }

    // AS-IS :222-247 `IEnumerator BounceTamer(Permanent tamer)` (RD-R5-02 RESTORED) — bounce the selected tamer to
    // hand with the IsBurst hashtable, then set TamerBounced from the burst-return flag. SUBSTRATE: IEnumerator→Task,
    // StartCoroutine(X)→await X. The AS-IS `new HandBounceClaass([tamer], hashtable).Bounce()` (CardController.cs:2603,
    // a standalone bounce PROCESS) has no mirror class — the port's established mirror for HandBounceClaass IS the
    // MatchStateMutationSink ReturnToHand carrier (SelectPermanentEffect.cs:574-590; RDW-01 opens the AS-IS
    // OnPermamemtReturnedToHand + OnLeaveFieldAnyone leave windows, and the CannotReturnToHand / CanNotBeAffected
    // immunity gates are centralised there). The AS-IS `hashtable["IsBurst"]=true` (with NO CardEffect key →
    // cardEffect null) maps to the sink's IsBurstKey on a CAUSELESS ReturnToHand: the mutation source is the
    // carrier-seam sentinel `new HeadlessEntityId("select")` (SelectPermanentEffect.cs:113 verbatim — the
    // effect-less-flush convention), so the leave windows fire un-caused exactly as AS-IS's null cardEffect. The
    // burst mark is stamped by the sink after the move (Permanent.IsReturnedToHandByBurstDigivolution, RD-R5-02).
    // NOTE: like every mirror bounce, this inherits the sink's RDW-01 POST windows only — the AS-IS PRE cut-in
    // windows (WhenReturntoHandAnyone/WhenRemoveField, CardController.cs:2644-2660) are the separately-tracked
    // R2-P2-2 deferral, not a BounceTamer-specific gap.
    public async Task BounceTamer(Permanent tamer)
    {
        TamerBounced = false;

        if (tamer != null)
        {
            if (tamer.TopCard != null) // mirror TopCard is never null — kept 1:1 for shape (AS-IS :228).
            {
                EngineContext context = RequireContext();

                if (new Player(context, tamer.TopCard.Owner).GetBattleAreaPermanents().Contains(tamer))
                {
                    if (!tamer.CannotReturnToHand(null))
                    {
                        // AS-IS :234-237 — `new HandBounceClaass([tamer], {"IsBurst":true}).Bounce()` (RDW re-migration
                        // off the retired MatchStateMutationSink). The AS-IS hashtable carries "IsBurst"=true with NO
                        // CardEffect key, so GetCardEffectFromHashtable → null (a CAUSELESS bounce, matching the sink's
                        // "select" sentinel source), and HandBounceClaass.Bounce() stamps
                        // Permanent.IsReturnedToHandByBurstDigivolution via CardEffectCommons.IsBurst(_hashtable) AFTER
                        // the move (CardController.cs:5822). One class call = one AS-IS bounce.
                        await new HandBounceClaass(
                            new List<Permanent> { tamer },
                            new Hashtable { { "IsBurst", true } }).Bounce().ConfigureAwait(false);

                        // AS-IS :239 `tamer.TopCard == null && tamer.IsReturnedToHandByBurstDigivolution`. SUBSTRATE:
                        // the id-keyed mirror Permanent.TopCard view is never null (CardController.cs:498 precedent),
                        // so the AS-IS "top card gone" truth is the field departure the bounce performed — the tamer
                        // is no longer on its owner's battle area (the same field-membership read the head guard uses).
                        if (!new Player(context, tamer.TopCard.Owner).GetBattleAreaPermanents().Contains(tamer)
                            && tamer.IsReturnedToHandByBurstDigivolution)
                        {
                            TamerBounced = true;
                        }
                    }
                }
            }
        }
    }

    // AS-IS :249-344 `void AddTrashTopCardAtTurnEnd(Permanent permanent)` (RD-R5-03 landed) — registers an
    // OnEndTurn ActivateClass on `permanent.UntilEachTurnEndEffects` (now on the mirror Permanent) that at the
    // end of the burst-digivolution turn ace-overflows + trashes the burst top card. Substrate: coroutine → Task,
    // StartCoroutine(X) → await X; `selectedPermanent.TopCard.Owner.GetFieldPermanents()` (Player) →
    // `new Player(context, owner).GetFieldPermanents()`. UI strips (adaptation): Effects.CreateDebuffEffect
    // (:313) / Permanent.ShowingPermanentCard.ShowPermanentData (:326) / Effects.RemoveDigivolveRootEffect (:327)
    // are DOTween/display helpers with no rule mutation. The rule spine — AceOverflowClass.Overflow +
    // CardObjectController.RemoveFromAllArea + AddTrashCard(if !IsToken) — is mirrored 1:1.
    public void AddTrashTopCardAtTurnEnd(Permanent permanent)
    {
        EngineContext context = RequireContext();

        Permanent selectedPermanent = permanent;

        if (selectedPermanent != null)
        {
            if (selectedPermanent.TopCard != null)
            {
                ActivateClass activateClass1 = new ActivateClass();

                activateClass1.SetUpICardEffect("Trash this Digimon's top card\n(Burst Digivolution)", CanUseCondition2, selectedPermanent.TopCard);
                activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, 1, false, EffectDiscription1());
                activateClass1.SetEffectSourcePermanent(selectedPermanent);
                activateClass1.SetHashString("TrashBurstDigivolution");
                selectedPermanent.UntilEachTurnEndEffects.Add(GetCardEffect);

                string EffectDiscription1()
                {
                    return "At the end of the burst digivolution turn, trash this Digimon's top card";
                }

                ChangeDPClass rootEffect = new ChangeDPClass();
                rootEffect.SetUpICardEffect("", null, selectedPermanent.TopCard);
                activateClass1.SetRootCardEffect(rootEffect);

                bool CanUseCondition2(Hashtable hashtable1)
                {
                    if (selectedPermanent.TopCard != null)
                    {
                        if (new Player(context, selectedPermanent.TopCard.Owner).GetFieldPermanents().Contains(selectedPermanent))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                bool CanActivateCondition1(Hashtable hashtable1)
                {
                    if (selectedPermanent.TopCard != null)
                    {
                        if (new Player(context, selectedPermanent.TopCard.Owner).GetFieldPermanents().Contains(selectedPermanent))
                        {
                            if (selectedPermanent.DigivolutionCards.Count >= 1)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }

                async Task ActivateCoroutine1(Hashtable _hashtable1)
                {
                    if (selectedPermanent.TopCard != null)
                    {
                        if (new Player(context, selectedPermanent.TopCard.Owner).GetFieldPermanents().Contains(selectedPermanent))
                        {
                            if (selectedPermanent.DigivolutionCards.Count >= 1)
                            {
                                Permanent permanent = selectedPermanent;

                                // AS-IS :313 Effects.CreateDebuffEffect(permanent) = DOTween UI (stripped).

                                CardSource cardSource = permanent.TopCard;

                                await new AceOverflowClass(new List<CardSource>() { cardSource }).Overflow().ConfigureAwait(false);

                                await CardObjectController.RemoveFromAllArea(cardSource).ConfigureAwait(false);

                                if (!cardSource.IsToken)
                                {
                                    await CardObjectController.AddTrashCard(cardSource).ConfigureAwait(false);
                                }

                                // AS-IS :326-327 ShowingPermanentCard.ShowPermanentData(true) +
                                // Effects.RemoveDigivolveRootEffect(cardSource, permanent) = display (stripped).
                            }
                        }
                    }
                }

                ICardEffect GetCardEffect(EffectTiming _timing)
                {
                    if (_timing == EffectTiming.OnEndTurn)
                    {
                        return activateClass1;
                    }

                    return null;
                }
            }
        }
    }
}
