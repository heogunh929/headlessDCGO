// Source: DCGO/Assets/Scripts/Script/SelectBurstDigivolutionEffect.cs (345 lines)
// (R5-C) Mirror of the AS-IS SelectBurstDigivolutionEffect component — the Burst-Digivolution pre-play flow the
// play pipeline reaches through GManager.instance.selectBurstDigivolutionEffect. Substrate translations only
// (bigbang §5): IEnumerator -> Task, StartCoroutine(x) -> await x; UI/Photon stripped. STATE fields + SetUp are
// AS-IS verbatim; the two-option method panel is the same ModeChoice ADAPTATION as SelectAppFusionEffect.
//
// PARTIAL (design items RD-R5-01/02): AddTrashTopCardAtTurnEnd (RD-R5-03) is now landed 1:1 (Permanent
// .UntilEachTurnEndEffects + AceOverflowClass + CardObjectController zone statics all present; UI stripped).
// The two remaining STOPs — SelectTamer (RD-R5-01, RE-SCOPED 2026-07-18: the RD-P6C1-2 cost engine
// GetChangedCostItselef is NO LONGER absent — R2-C mirrored it at CardSource.cs:1156; residual = the un-ported
// CanPlayBurst(bool) wrapper + body, a scoped port not an engine gap) and BounceTamer (RD-R5-02, blocked on the
// standalone HandBounceClaass process + IsReturnedToHandByBurst flag)
// — STOP loudly (never guessed). Permanent.CannotReturnToHand (RD-EXT3-04) is now available, so it is no longer
// a blocker for either. The feasible surface (state + SetUp + SelectWheterToBurst + AddTrashTopCardAtTurnEnd) is
// real 1:1. See the per-method notes.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Collections;
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

    // AS-IS :107-220 `IEnumerator SelectTamer()` — enumerate battle-area tamers matching
    // `_card.burstDigivolutionCondition.tamerCondition` that `!CannotReturnToHand(null)`, SelectPermanent one,
    // then route to `_endSelectCoroutine_SelectTamer`. STOP (design item RD-R5-01) — RE-ADJUDICATED 2026-07-18
    // (small-ledger batch): the ORIGINAL premise is now STALE. The AS-IS head guard `_card.CanPlayBurst(_isPayCost)`
    // rides the burst cost engine `CardSource.GetChangedCostItselef`, and that engine NO LONGER "remains a STOP" —
    // R2-C landed a 1:1 mirror at CardSource.cs:1156-1209 (with GetChangedPayingCost :1215, GetPayingCostWithBaseCost
    // :1064). Every other CanPlayBurst callee is also live (Player.MaxMemoryCost, GetBattleAreaDigimons/Permanents,
    // CardSource.CanNotEvolve, Permanent.CannotReturnToHand, BurstDigivolutionConditionOf). The REMAINING blocker is
    // narrow and mechanical, not infrastructural: the `CardSource.CanPlayBurst(bool)` WRAPPER itself (AS-IS
    // CardSource.cs:3071-3134) is not yet ported, and restoring the SelectTamer body (:107-220) is a real burst-play
    // legality change needing its own witness. Kept OPEN as a scoped porting item (no longer an engine gap); lifting
    // it = port CanPlayBurst 1:1 (swap `burstDigivolutionCondition`→`BurstDigivolutionConditionOf()`, `Owner.*`→
    // `new Player(Context, Owner).*`) + restore the body + a burst-affordability witness (e.g. TfxBurstDigivolve). No guess.
    public Task SelectTamer()
    {
        throw new NotSupportedException(
            "STOP: SelectBurstDigivolutionEffect.SelectTamer (AS-IS SelectBurstDigivolutionEffect.cs:107-220) — " +
            "the burst cost engine CardSource.GetChangedCostItselef IS now mirrored (R2-C, CardSource.cs:1156); the " +
            "residual gap is the un-ported CardSource.CanPlayBurst(bool) wrapper (AS-IS CardSource.cs:3071-3134) plus " +
            "the SelectTamer body — a scoped port + burst witness (design item RD-R5-01, re-scoped 2026-07-18).");
    }

    // AS-IS :222-247 `IEnumerator BounceTamer(Permanent tamer)` — bounce the selected tamer to hand with the
    // IsBurst hashtable, then set TamerBounced from `tamer.IsReturnedToHandByBurstDigivolution`. STOP (design
    // item RD-R5-02): the AS-IS `HandBounceClaass` (CardController.cs:2603) is a ~200-line standalone bounce
    // PROCESS with its own leave-field cut-in windows — the mirror has no standalone HandBounceClaass type (the
    // bounce is centralised in the MatchStateMutationSink for SelectPermanentEffect's Mode.Bounce, not a
    // reusable raw-permanent-list-with-IsBurst-hashtable entry), and `Permanent.IsReturnedToHandByBurstDigivolution`
    // has no mirror. (`Permanent.CannotReturnToHand` is now landed — no longer a blocker.) Unreachable today
    // (SelectTamer STOPs upstream on CanPlayBurst). No guess.
    public Task BounceTamer(Permanent tamer)
    {
        _ = tamer;
        throw new NotSupportedException(
            "STOP: SelectBurstDigivolutionEffect.BounceTamer (AS-IS SelectBurstDigivolutionEffect.cs:222-247) — " +
            "requires the standalone HandBounceClaass bounce process (no mirror type) and " +
            "Permanent.IsReturnedToHandByBurstDigivolution (design item RD-R5-02). Permanent.CannotReturnToHand is now available.");
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
