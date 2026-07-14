// Source: DCGO/Assets/Scripts/Script/SelectBurstDigivolutionEffect.cs (345 lines)
// (R5-C) Mirror of the AS-IS SelectBurstDigivolutionEffect component — the Burst-Digivolution pre-play flow the
// play pipeline reaches through GManager.instance.selectBurstDigivolutionEffect. Substrate translations only
// (bigbang §5): IEnumerator -> Task, StartCoroutine(x) -> await x; UI/Photon stripped. STATE fields + SetUp are
// AS-IS verbatim; the two-option method panel is the same ModeChoice ADAPTATION as SelectAppFusionEffect.
//
// PARTIAL (design items RD-R5-01/02/03): three methods depend on R1/R2 mirror members that do not yet exist and
// live in files this batch may not edit — SelectTamer/BounceTamer/AddTrashTopCardAtTurnEnd STOP loudly (never
// guessed). The feasible surface (state + SetUp + SelectWheterToBurst) is real 1:1. See the per-method notes.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
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
    // then route to `_endSelectCoroutine_SelectTamer`. STOP (design item RD-R5-01): the AS-IS guard
    // `_card.CanPlayBurst(_isPayCost)` has no mirror (the AS-IS burst play-cost/requirement check is the
    // unported RD-P6C1-2 cost engine), and the per-candidate `permanent.CannotReturnToHand(null)` aggregate is
    // not on the mirror Permanent yet (only the ICannotReturnToHandEffect interface exists). Both live in
    // CardSource.cs / Permanent.cs — files outside this batch's edit scope. No guess.
    public Task SelectTamer()
    {
        throw new NotSupportedException(
            "STOP: SelectBurstDigivolutionEffect.SelectTamer (AS-IS SelectBurstDigivolutionEffect.cs:107-220) — " +
            "requires CardSource.CanPlayBurst (RD-P6C1-2 burst cost/requirement engine) and the " +
            "Permanent.CannotReturnToHand aggregate, neither present in the mirror (design item RD-R5-01).");
    }

    // AS-IS :222-247 `IEnumerator BounceTamer(Permanent tamer)` — bounce the selected tamer to hand with the
    // IsBurst hashtable, then set TamerBounced from `tamer.IsReturnedToHandByBurstDigivolution`. STOP (design
    // item RD-R5-02): the AS-IS `HandBounceClaass` bounce process, the `Permanent.CannotReturnToHand(null)`
    // aggregate, and the `Permanent.IsReturnedToHandByBurstDigivolution` flag all have no mirror surface.
    public Task BounceTamer(Permanent tamer)
    {
        _ = tamer;
        throw new NotSupportedException(
            "STOP: SelectBurstDigivolutionEffect.BounceTamer (AS-IS SelectBurstDigivolutionEffect.cs:222-247) — " +
            "requires the HandBounceClaass bounce process, Permanent.CannotReturnToHand, and " +
            "Permanent.IsReturnedToHandByBurstDigivolution, none present in the mirror (design item RD-R5-02).");
    }

    // AS-IS :249-344 `void AddTrashTopCardAtTurnEnd(Permanent permanent)` — register an OnEndTurn ActivateClass
    // on `permanent.UntilEachTurnEndEffects` that at end of the burst turn overflows + trashes the burst top
    // card and removes its evo-root effect. STOP (design item RD-R5-03): `Permanent.UntilEachTurnEndEffects`
    // (the AS-IS is on PERMANENT; the mirror only has Player.UntilEachTurnEndEffects), the `Effects`
    // (CreateDebuffEffect / RemoveDigivolveRootEffect) UI+effect helpers, and `Permanent.ShowingPermanentCard`
    // have no mirror surface. (ActivateClass / ChangeDPClass / AceOverflowClass ARE mirrored.)
    public void AddTrashTopCardAtTurnEnd(Permanent permanent)
    {
        _ = permanent;
        throw new NotSupportedException(
            "STOP: SelectBurstDigivolutionEffect.AddTrashTopCardAtTurnEnd (AS-IS " +
            "SelectBurstDigivolutionEffect.cs:249-344) — requires Permanent.UntilEachTurnEndEffects (per-permanent), " +
            "the Effects CreateDebuffEffect/RemoveDigivolveRootEffect helpers, and Permanent.ShowingPermanentCard, " +
            "none present in the mirror (design item RD-R5-03).");
    }
}
