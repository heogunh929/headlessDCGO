// Source: Assets/Scripts/Script/SelectCountEffect.cs
// Decision: PORT
// Category: AIUseful
// Migration: AS-IS mirror (goal 7) — the count-picker Select* component.
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// (MIG7) 1:1 mirror of the original SelectCountEffect: the "choose a number 0..MaxCount" picker (de-digivolve
// count, trash-N-stack count, etc.). The AS-IS coroutine + Func<int,IEnumerator> callback becomes the same
// deterministic BuildRequest shape the sibling mirror Select* components use (SelectCardEffect /
// SelectPermanentEffect): SetUp stores the config, BuildRequest builds a ChoiceType.Count ChoiceRequest, and
// the consuming effect reads the resolved count via ReadSelectedCount after the pending choice resolves (the
// AS-IS SelectCountCoroutine(count) body becomes the consumer's post-resolve continuation).
//
// The count-choice substrate is fully pre-built: ChoiceType.Count, EffectChoiceHelpers.CreateCountRequest, and
// ChoiceResult.SelectedCount, with provider support (Policy / Scripted / Deferred) and validation. This class
// is the AS-IS-named authoring surface over it, so a local-LLM card port mirrors
// GetComponent<SelectCountEffect>().SetUp(...).Activate() mechanically.
//
// NOTE (design item MIG3-DEGEN-COUNTSELECT): the mirror IDegeneration / IMassDegeneration consumers still take
// their count from the constructor ruling (they do not yet PARK on this choice). Wiring them to build this
// request, park, and continue on the resolved count is the consumer-side follow-up; this component supplies
// the reusable building block that wiring needs.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.PlayerSelection;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class SelectCountEffect
{
    private HeadlessPlayerId _selectPlayer;
    private int _maxCount = 1;
    private bool _canNoSelect;
    private string _message = "Choose a number.";

    /// <summary>1:1 with the original <c>SelectCountEffect.SetUp(SelectPlayer, targetPermanent, MaxCount,
    /// CanNoSelect, Message, Message_Enemy, SelectCountCoroutine)</c> — the deterministic port drops the
    /// coroutine callback (the consumer resumes on the resolved count) and the UI-only messages, keeping the
    /// count semantics: the selecting player, the inclusive maximum, and whether 0 is a legal pick.</summary>
    public void SetUp(HeadlessPlayerId selectPlayer, int maxCount, bool canNoSelect)
    {
        if (maxCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must not be negative.");
        }

        _selectPlayer = selectPlayer;
        _maxCount = maxCount;
        _canNoSelect = canNoSelect;
    }

    public void SetUpMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _message = message;
        }
    }

    /// <summary>Build the <see cref="ChoiceType.Count"/> request over 0..MaxCount. AS-IS shows a button per
    /// count and skips the "0" button when <c>!CanNoSelect</c> — so the minimum is 1 unless 0 is allowed. Not
    /// skippable (AS-IS always resolves to a number, 0 when CanNoSelect).</summary>
    public ChoiceRequest BuildRequest() =>
        EffectChoiceHelpers.CreateCountRequest(
            _selectPlayer,
            _message,
            minCount: _canNoSelect ? 0 : Math.Min(1, _maxCount),
            maxCount: _maxCount,
            canSkip: false);

    /// <summary>Read the resolved count (AS-IS <c>_selectedCount</c>); 0 when absent (a canNoSelect skip or an
    /// empty resolution), matching AS-IS's <c>valueSelection != null ? ValueAsInt() : 0</c>.</summary>
    public static int ReadSelectedCount(ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.SelectedCount ?? 0;
    }

    // ================================================================================================
    // (P6C1) AS-IS component surface — the 1:1 members the AS-IS-verbatim play pipeline calls
    // (PlayCardClass.SelectCost, CardController.cs:534-566): the full SetUp overload + SetCandidates/
    // SetPreferMin/SetIsDigivolutionCost + the driving Activate coroutine (AS-IS SelectCountEffect.cs:11-186,
    // IEnumerator→Task). Substrate translations only, per the W4 Select* convention (SelectPermanentEffect):
    // Player→HeadlessPlayerId, Func<int,IEnumerator>→Func<int,Task>, the target-permanent orange-outline block
    // (:64-75/:176-183) is UI (stripped). The pre-existing deterministic members above (BuildRequest/
    // ReadSelectedCount, MIG7) stay for their consumers.
    //
    // (isAI restore) The AS-IS three-way presentation split at :113-160 — owner + autoMaxCardCount/
    // autoMinDigivolutionCost shortcut / owner + command panel / opponent + IsAI / opponent + banner — is now
    // mirrored branch-for-branch, with SetCount (:185) ported too. Only the panel/banner ARMS are stripped as
    // UI: their delivery is the ChoiceProvider request standing in for the AS-IS :163 WaitUntil. Both
    // `Player.isYou` and `GManager.IsAI` are false headless, so the live path is always the opponent + banner
    // arm ⇒ the provider request, exactly as before this restore.
    // ================================================================================================

    private EngineContext? _context;
    private Permanent? _targetPermanent;
    private string _messageForOwner = "";
    private Func<int, Task>? _selectCountCoroutine;
    private List<int> _candidates = new List<int>();
    private int _selectedCount;
    private bool _preferMin = true;
    private bool _isDigivolutionCost;

    internal void AttachContext(EngineContext context) => _context = context;

    /// <summary>(P6C1) AS-IS <c>SetUp</c> (SelectCountEffect.cs:11-31) — the 7-param overload the play
    /// pipeline calls with AS-IS named arguments.</summary>
    public void SetUp(
        HeadlessPlayerId SelectPlayer,
        Permanent targetPermanent,
        int MaxCount,
        bool CanNoSelect,
        string Message,
        string Message_Enemy,
        Func<int, Task> SelectCountCoroutine)
    {
        _selectPlayer = SelectPlayer;
        _targetPermanent = targetPermanent;
        _maxCount = MaxCount;
        _canNoSelect = CanNoSelect;
        _messageForOwner = Message;
        _ = Message_Enemy; // AS-IS _messageForEnemy — opponent-facing prompt text = UI only.
        _selectCountCoroutine = SelectCountCoroutine;
        _preferMin = false;
        _isDigivolutionCost = false;

        _candidates = new List<int>();
    }

    /// <summary>(P6C1) AS-IS <c>SetCandidates</c> (:33-36) — an explicit (possibly non-contiguous) candidate
    /// list overriding the default 0..MaxCount range.</summary>
    public void SetCandidates(List<int> candidates)
    {
        _candidates = candidates;
    }

    /// <summary>(P6C1) AS-IS <c>SetPreferMin</c> (:38-41).</summary>
    public void SetPreferMin(bool preferMin)
    {
        _preferMin = preferMin;
    }

    /// <summary>(P6C1) AS-IS <c>SetIsDigivolutionCost</c> (:43-46).</summary>
    public void SetIsDigivolutionCost(bool isDigivolutionCost)
    {
        _isDigivolutionCost = isDigivolutionCost;
    }

    /// <summary>(P6C1) AS-IS <c>Activate</c> (SelectCountEffect.cs:60-186), IEnumerator→Task. Candidate
    /// construction is verbatim (:77-104): 0..MaxCount skipping 0 unless CanNoSelect, replaced wholesale by
    /// SetCandidates when given, then Distinct+ascending. A single candidate auto-resolves (:107-111, the AS-IS
    /// direct SetCount). Multiple candidates run the AS-IS three-way isYou/IsAI split (:113-160) verbatim; the
    /// two arms that AS-IS leaves to a person (the owner command panel, the opponent banner) queue nothing, so
    /// the ChoiceProvider request standing in for the AS-IS :163 WaitUntil supplies the value. Then
    /// <c>_selectedCount</c> is dequeued (:165-166) and the AS-IS SelectCountCoroutine continuation runs
    /// (:170-173).</summary>
    public async Task Activate()
    {
        if (_maxCount >= 1)
        {
            // AS-IS :64-75 target-permanent orange outline = UI (stripped; _targetPermanent kept as state).
            _ = _targetPermanent;

            List<int> candidates = new List<int>();

            for (int i = 0; i < _maxCount + 1; i++)
            {
                if (i == 0)
                {
                    if (!_canNoSelect)
                    {
                        continue;
                    }
                }

                candidates.Add(i);
            }

            if (_candidates != null)
            {
                if (_candidates.Count >= 1)
                {
                    candidates = new List<int>();

                    foreach (int candidate in _candidates)
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            candidates = candidates.Distinct().OrderBy((value) => value).ToList();

            if (candidates.Count >= 1)
            {
                EngineContext context = _context
                    ?? HeadlessDCGO.Engine.Headless.Bridge.AmbientMatchContext.Current
                    ?? throw new InvalidOperationException(
                        "SelectCountEffect.Activate needs a live match context — resolve the component " +
                        "through GManager.GetComponent<SelectCountEffect>() or call inside a match scope.");

                // SUBSTRATE: the restored branches read the process-global `GManager.instance` (IsAI, the
                // GetPlayerFromID inside SetCount) the way AS-IS does; the mirror resolves that from
                // AmbientMatchContext, so scope the match for the rest of the method (a caller already in the
                // same scope re-enters harmlessly) — the CardSource.GetPayingCostWithBaseCost idiom.
                using HeadlessDCGO.Engine.Headless.Bridge.AmbientMatchContext.Scope _matchScope =
                    HeadlessDCGO.Engine.Headless.Bridge.AmbientMatchContext.Enter(context);

                // AS-IS `_selectPlayer` is a `Player`; the mirror stores the seat id (the Select* convention), so
                // the AS-IS `_selectPlayer.isYou` / `.HasPlayerSelection()` / `.DequeuePlayerSelection<>()` member
                // reads go through the Player view of that seat (the established `new Player(Context, id)` idiom).
                Player selectPlayer = new Player(context, _selectPlayer);

                if (candidates.Count == 1)
                {
                    // AS-IS :107-111.
                    SetCount(_selectPlayer, candidates[0]);
                }

                else
                {
                    if (selectPlayer.isYou)
                    {
                        if ((!_isDigivolutionCost && ContinuousController.instance!.autoMaxCardCount && !_preferMin)
                        || (_isDigivolutionCost && ContinuousController.instance!.autoMinDigivolutionCost && _preferMin))
                        {
                            int preferedNumber = _preferMin ? candidates.Min() : candidates.Max();
                            // AS-IS :123 `photonView.RPC("SetCount", RpcTarget.All, _selectPlayer.PlayerID,
                            // preferedNumber)` — Photon dispatch translated to the direct call, same arguments.
                            SetCount(_selectPlayer, preferedNumber);
                        }

                        else
                        {
                            // AS-IS :127-145: commandText.OpenCommandText(_messageForOwner) + one
                            // Command_SelectCommand button per candidate, each firing the same SetCount RPC =
                            // UI (stripped) — the pick arrives through the ChoiceProvider request below, which
                            // stands in for the AS-IS :163 WaitUntil.
                        }
                    }

                    else
                    {
                        #region AIモード
                        if (GManager.instance!.IsAI)
                        {
                            int preferedNumber = _preferMin ? candidates.Min() : candidates.Max();
                            SetCount(_selectPlayer, preferedNumber);
                        }
                        #endregion

                        else
                        {
                            // AS-IS :156-159 commandText.OpenCommandText(_messageForEnemy) — the opponent's
                            // waiting banner = UI (stripped).
                        }
                    }
                }

                // AS-IS :163 `yield return new WaitUntil(() => _selectPlayer.HasPlayerSelection());` — the AS-IS
                // coroutine blocks until a ValueSelection is queued by one of the branches above. The mirror's
                // ChoiceProvider request IS that delivery (the W4 Select* convention), so it is issued exactly
                // when no branch already queued one, i.e. on the two AS-IS arms that wait for a person (the owner
                // command panel :127-145 and the opponent banner :156-159). _preferMin/_isDigivolutionCost stay
                // available to policy providers as AS-IS stores them.
                if (!selectPlayer.HasPlayerSelection())
                {
                    ChoiceRequest request = EffectChoiceHelpers.CreateCountRequest(
                        _selectPlayer,
                        string.IsNullOrEmpty(_messageForOwner) ? _message : _messageForOwner,
                        minCount: candidates.Min(),
                        maxCount: candidates.Max(),
                        canSkip: false,
                        candidateCounts: candidates);

                    ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);

                    SetCount(_selectPlayer, result.SelectedCount ?? 0);
                }

                // AS-IS :165-166 `_selectedCount = valueSelection != null ? valueSelection.ValueAsInt() : 0`.
                // The HasPlayerSelection guard has no AS-IS counterpart: AS-IS's WaitUntil cannot fall through
                // with an empty queue, whereas headless SetCount's AS-IS null-seat early return (or a skipped
                // provider result) can leave it empty — the guard keeps the AS-IS "absent selection ⇒ 0" answer
                // instead of throwing on an empty dequeue.
                if (selectPlayer.HasPlayerSelection())
                {
                    ValueSelection? valueSelection = selectPlayer.DequeuePlayerSelection<ValueSelection>();
                    _selectedCount = valueSelection != null ? valueSelection.ValueAsInt() : 0;
                }

                // AS-IS :167-168 commandText close = UI (stripped).

                if (_selectCountCoroutine != null)
                {
                    await _selectCountCoroutine(_selectedCount).ConfigureAwait(false);
                }
            }

            // AS-IS :176-183 outline restore = UI (stripped).
        }
    }

    /// <summary>(isAI restore) AS-IS <c>[PunRPC] public void SetCount(int playerID, int selectedCount)</c>
    /// (SelectCountEffect.cs:185-196): look the seat up and queue a <see cref="ValueSelection"/> on it, which is
    /// what the <c>Activate</c> WaitUntil above consumes. Body ported 1:1 (the AS-IS null-seat early return
    /// included). SUBSTRATE: the first parameter carries the mirror's seat-identity type — AS-IS callers pass the
    /// int <c>Player.PlayerID</c> (Player.cs:738) and the mirror has no int seat index (see
    /// <c>GManager.GetPlayerFromID</c>); the parameter name, order and count are unchanged. <c>[PunRPC]</c> is
    /// Photon substrate (stripped) — AS-IS's own <c>photonView.RPC("SetCount", …)</c> dispatch is translated to a
    /// direct call with the same arguments.</summary>
    public void SetCount(HeadlessPlayerId playerID, int selectedCount)
    {
        Player? selectionPlayer = GManager.instance!.GetPlayerFromID(playerID);

        if (selectionPlayer == null)
        {
            return;
        }

        selectionPlayer.QueuePlayerSelection(new ValueSelection(selectedCount));
    }

    /// <summary>(P6C1) AS-IS <c>_selectedCount</c> read (the count Activate resolved).</summary>
    public int SelectedCount => _selectedCount;
}
