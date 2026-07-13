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
    // Player→HeadlessPlayerId, Func<int,IEnumerator>→Func<int,Task>, the Photon RPC/command-panel/AI/auto-min
    // presentation branches (:113-160) collapse into ONE ChoiceProvider request (the provider is the decider;
    // preferMin/isDigivolutionCost are kept as state exactly as AS-IS stores them), the target-permanent
    // orange-outline block (:64-75/:176-183) is UI (stripped). The pre-existing deterministic members above
    // (BuildRequest/ReadSelectedCount, MIG7) stay for their consumers.
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
    /// direct SetCount). Multiple candidates ask the match ChoiceProvider (the AS-IS :113-160 owner-UI /
    /// auto-min / AI presentation split all funnel into the same ValueSelection — headless-side the provider IS
    /// that decider). Then <c>_selectedCount</c> is stored and the AS-IS SelectCountCoroutine continuation runs
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
                if (candidates.Count == 1)
                {
                    // AS-IS :107-111 SetCount(_selectPlayer.PlayerID, candidates[0]) — direct resolution.
                    _selectedCount = candidates[0];
                }
                else
                {
                    // AS-IS :113-160: the owner command-panel buttons / autoMin(autoMax) shortcut / AI branch —
                    // one labelled count pick; _preferMin/_isDigivolutionCost stay available to policy
                    // providers as AS-IS stores them.
                    EngineContext context = _context
                        ?? HeadlessDCGO.Engine.Headless.Bridge.AmbientMatchContext.Current
                        ?? throw new InvalidOperationException(
                            "SelectCountEffect.Activate needs a live match context — resolve the component " +
                            "through GManager.GetComponent<SelectCountEffect>() or call inside a match scope.");

                    ChoiceRequest request = EffectChoiceHelpers.CreateCountRequest(
                        _selectPlayer,
                        string.IsNullOrEmpty(_messageForOwner) ? _message : _messageForOwner,
                        minCount: candidates.Min(),
                        maxCount: candidates.Max(),
                        canSkip: false,
                        candidateCounts: candidates);

                    ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request).ConfigureAwait(false);

                    // AS-IS :165-166 `_selectedCount = valueSelection != null ? valueSelection.ValueAsInt() : 0`.
                    _selectedCount = result.SelectedCount ?? 0;
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

    /// <summary>(P6C1) AS-IS <c>_selectedCount</c> read (the count Activate resolved).</summary>
    public int SelectedCount => _selectedCount;
}
