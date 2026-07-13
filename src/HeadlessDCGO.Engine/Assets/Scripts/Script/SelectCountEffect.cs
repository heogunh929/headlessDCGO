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
}
