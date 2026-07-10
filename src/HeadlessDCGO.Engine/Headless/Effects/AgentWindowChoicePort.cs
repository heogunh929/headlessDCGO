namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (Stage 5, Phase 3) The live, agent-driven <see cref="IWindowChoicePort"/>: a KEY-BASED record-replay provider
/// that drives the window's order / optional decisions through the engine's <see cref="IHeadlessChoiceController"/>.
///
/// The window loop suspends by unwinding an exception, so this port cannot block for the agent. Instead: the FIRST
/// time it meets a distinct choice (keyed by the offered effect-ids / the confirmed effect-id) it opens a
/// <see cref="ChoiceType.WindowChoice"/> choice on the controller and throws <see cref="WindowChoicePendingException"/>
/// to suspend the window; the agent answers via a ResolveChoice action, which records the answer on the
/// <see cref="WindowResolutionController"/> (keyed) and re-drives the window. On that re-drive — and every later one —
/// this port REPLAYS the recorded answer for each choice it re-encounters. Because the key is the effect identity,
/// not a call ordinal, a resolved pick whose choice point vanishes from later passes is simply never re-asked
/// (adversarial finding P1-a). Mirrors the AS-IS <c>MultipleSkills.OpenSelectCardPanel</c> (order, _MaxCount:1,
/// _CanNoSelect = all skippable) and <c>Activate_Optional</c> (yes/no) prompts.
/// </summary>
public sealed class AgentWindowChoicePort : IWindowChoicePort
{
    private readonly IHeadlessChoiceController _choiceController;
    private readonly WindowResolutionController _windowResolution;

    public AgentWindowChoicePort(IHeadlessChoiceController choiceController, WindowResolutionController windowResolution)
    {
        _choiceController = choiceController ?? throw new ArgumentNullException(nameof(choiceController));
        _windowResolution = windowResolution ?? throw new ArgumentNullException(nameof(windowResolution));
    }

    /// <summary>(RD-14/15) Open the "which effect resolves first" panel (AS-IS OpenSelectCardPanel, _MaxCount:1):
    /// one candidate per offered trigger keyed by its effect id, skippable only when the caller allows it (all
    /// offered optional). Returns the chosen index (or null = skip) by replaying the recorded answer; suspends the
    /// first time the agent has not yet answered.</summary>
    public Task<int?> ChooseOrderAsync(IReadOnlyList<TimingWindowTrigger> side, bool canSkip, CancellationToken cancellationToken)
    {
        string key = OrderKey(side);
        if (_windowResolution.TryGetAnswer(key, out ChoiceResult? recorded) && recorded is not null)
        {
            if (recorded.IsSkipped)
            {
                return Task.FromResult<int?>(null);
            }

            HeadlessEntityId pickedId = recorded.SelectedIds[0];
            for (int i = 0; i < side.Count; i++)
            {
                if (side[i].Request.EffectId == pickedId)
                {
                    return Task.FromResult<int?>(i);
                }
            }

            throw new InvalidOperationException(
                $"Recorded window order pick '{pickedId.Value}' is not among the current side [{OrderKey(side)}].");
        }

        var candidates = side
            .Select(t => new ChoiceCandidate(
                t.Request.EffectId,
                $"Effect {t.Request.EffectId.Value}",
                ChoiceZone.Custom,
                IsSelectable: true,
                ownerId: t.Request.ControllerId))
            .ToArray();

        var request = new ChoiceRequest(
            ChoiceType.WindowChoice,
            side[0].Request.ControllerId,
            "Multiple effects are triggered. Choose which to resolve first.",
            minCount: canSkip ? 0 : 1,
            maxCount: 1,
            canSkip: canSkip,
            ChoiceZone.Custom,
            candidates);

        _choiceController.RequestChoice(request, new HeadlessEntityId(key));
        throw new WindowChoicePendingException(key);
    }

    /// <summary>(RD-13) Ask the controlling player whether to activate an optional effect (AS-IS Activate_Optional).
    /// A single-candidate skippable choice: selecting it = yes, skipping = no. Replays the recorded answer; suspends
    /// the first time the agent has not yet answered.</summary>
    public Task<bool> ConfirmOptionalAsync(TimingWindowTrigger trigger, CancellationToken cancellationToken)
    {
        string key = ConfirmKey(trigger);
        if (_windowResolution.TryGetAnswer(key, out ChoiceResult? recorded) && recorded is not null)
        {
            // yes = the agent selected the effect (SelectedIds non-empty); no = skipped.
            return Task.FromResult(!recorded.IsSkipped);
        }

        var candidate = new ChoiceCandidate(
            trigger.Request.EffectId,
            $"Activate optional effect {trigger.Request.EffectId.Value}",
            ChoiceZone.Custom,
            IsSelectable: true,
            ownerId: trigger.Request.ControllerId);

        var request = new ChoiceRequest(
            ChoiceType.WindowChoice,
            trigger.Request.ControllerId,
            $"Activate optional effect {trigger.Request.EffectId.Value}?",
            minCount: 0,
            maxCount: 1,
            canSkip: true,
            ChoiceZone.Custom,
            new[] { candidate });

        _choiceController.RequestChoice(request, new HeadlessEntityId(key));
        throw new WindowChoicePendingException(key);
    }

    /// <summary>The order choice's stable identity: the offered effect ids, order-independent (a re-run pass offers
    /// the same set until one resolves), so the recorded answer replays for exactly that choice point.</summary>
    private static string OrderKey(IReadOnlyList<TimingWindowTrigger> side) =>
        "O:" + string.Join(",", side.Select(t => t.Request.EffectId.Value).OrderBy(v => v, StringComparer.Ordinal));

    private static string ConfirmKey(TimingWindowTrigger trigger) => "C:" + trigger.Request.EffectId.Value;
}
