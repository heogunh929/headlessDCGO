namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class OptionalPromptQueue
{
    public const string DefaultMessage = "Multiple optional effects are triggered. Choose which effect to activate.";

    private readonly Queue<OptionalEffectPrompt> _prompts = new();
    private long _nextPromptSequence;

    public int Count => _prompts.Count;

    public bool HasPendingPrompt => _prompts.Count > 0;

    public IReadOnlyList<OptionalEffectPrompt> Snapshot()
    {
        return _prompts.ToArray();
    }

    public OptionalPromptQueueResult EnqueuePrompt(
        IEnumerable<TimingWindowTrigger> optionalTriggers,
        HeadlessPlayerId playerId,
        string? message = null)
    {
        if (playerId.IsEmpty)
        {
            return OptionalPromptQueueResult.Failure("Optional prompt player id must not be empty.");
        }

        OptionalPromptBuildResult buildResult = BuildPrompt(optionalTriggers, playerId, message);
        if (!buildResult.IsSuccess)
        {
            return OptionalPromptQueueResult.Failure(buildResult.FailureReason);
        }

        _prompts.Enqueue(buildResult.Prompt!);
        return OptionalPromptQueueResult.Success(
            buildResult.Prompt!,
            HeadlessChoiceState.Empty,
            enqueuedCount: 0,
            promptCount: _prompts.Count);
    }

    public OptionalPromptQueueResult RequestNextChoice(IHeadlessChoiceController choiceController)
    {
        ArgumentNullException.ThrowIfNull(choiceController);

        if (!_prompts.TryPeek(out OptionalEffectPrompt? prompt) || prompt is null)
        {
            return OptionalPromptQueueResult.Failure("No optional prompts are queued.");
        }

        if (choiceController.Current.IsPending)
        {
            return OptionalPromptQueueResult.Failure("Cannot request an optional prompt while another choice is pending.");
        }

        HeadlessChoiceState state;
        try
        {
            state = choiceController.RequestChoice(prompt.ToChoiceRequest(), prompt.PromptId);
        }
        catch (InvalidOperationException ex)
        {
            return OptionalPromptQueueResult.Failure(ex.Message);
        }

        return OptionalPromptQueueResult.Success(
            prompt,
            state,
            enqueuedCount: 0,
            promptCount: _prompts.Count);
    }

    public OptionalPromptQueueResult ResolveChoice(
        ChoiceResult result,
        IHeadlessChoiceController choiceController,
        EffectScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(choiceController);
        ArgumentNullException.ThrowIfNull(scheduler);

        if (!_prompts.TryPeek(out OptionalEffectPrompt? prompt) || prompt is null)
        {
            return OptionalPromptQueueResult.Failure("No optional prompt is waiting for resolution.");
        }

        if (!choiceController.Current.IsPending || choiceController.PendingRequest is null)
        {
            return OptionalPromptQueueResult.Failure("Optional prompt resolution requires a pending choice.");
        }

        HeadlessChoiceState state;
        try
        {
            state = choiceController.ResolveChoice(result);
        }
        catch (InvalidOperationException ex)
        {
            return OptionalPromptQueueResult.Failure(ex.Message);
        }

        int enqueuedCount = 0;
        if (!result.IsSkipped)
        {
            foreach (HeadlessEntityId selectedId in result.SelectedIds)
            {
                TimingWindowTrigger trigger = prompt.FindTrigger(selectedId)
                    ?? throw new InvalidOperationException(
                        $"Selected optional effect '{selectedId.Value}' was not found in the prompt.");

                scheduler.Enqueue(trigger.Request, trigger.Mode);
                enqueuedCount++;
            }
        }

        _prompts.Dequeue();
        return OptionalPromptQueueResult.Success(
            prompt,
            state,
            enqueuedCount,
            promptCount: _prompts.Count,
            skipped: result.IsSkipped);
    }

    public int Clear()
    {
        int removed = _prompts.Count;
        _prompts.Clear();
        return removed;
    }

    private OptionalPromptBuildResult BuildPrompt(
        IEnumerable<TimingWindowTrigger> optionalTriggers,
        HeadlessPlayerId playerId,
        string? message)
    {
        if (optionalTriggers is null)
        {
            return OptionalPromptBuildResult.Failure("Optional trigger list must not be null.");
        }

        var triggers = new List<TimingWindowTrigger>();
        foreach (TimingWindowTrigger? trigger in optionalTriggers)
        {
            if (trigger is null)
            {
                return OptionalPromptBuildResult.Failure("Optional trigger list must not contain null values.");
            }

            if (trigger.Kind != TimingWindowTriggerKind.Optional)
            {
                return OptionalPromptBuildResult.Failure("Optional prompt queue accepts only optional triggers.");
            }

            if (trigger.Request.ControllerId != playerId)
            {
                return OptionalPromptBuildResult.Failure("Optional trigger controller must match the prompt player.");
            }

            triggers.Add(trigger);
        }

        if (triggers.Count == 0)
        {
            return OptionalPromptBuildResult.Failure("Optional prompt requires at least one trigger.");
        }

        HeadlessEntityId promptId = new($"optional-prompt:{playerId.Value}:{++_nextPromptSequence}");
        return OptionalPromptBuildResult.Success(new OptionalEffectPrompt(
            promptId,
            playerId,
            string.IsNullOrWhiteSpace(message) ? DefaultMessage : message!.Trim(),
            triggers));
    }
}

public sealed record OptionalEffectPrompt
{
    public OptionalEffectPrompt(
        HeadlessEntityId promptId,
        HeadlessPlayerId playerId,
        string message,
        IReadOnlyList<TimingWindowTrigger> triggers)
    {
        if (promptId.IsEmpty)
        {
            throw new ArgumentException("Optional prompt id must not be empty.", nameof(promptId));
        }

        if (playerId.IsEmpty)
        {
            throw new ArgumentException("Optional prompt player id must not be empty.", nameof(playerId));
        }

        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(triggers);

        TimingWindowTrigger[] triggerSnapshot = triggers.ToArray();
        if (triggerSnapshot.Length == 0)
        {
            throw new ArgumentException("Optional prompt requires at least one trigger.", nameof(triggers));
        }

        if (triggerSnapshot.Any(trigger => trigger.Kind != TimingWindowTriggerKind.Optional))
        {
            throw new ArgumentException("Optional prompt triggers must all be optional.", nameof(triggers));
        }

        PromptId = promptId;
        PlayerId = playerId;
        Message = message.Trim();
        Triggers = Array.AsReadOnly(triggerSnapshot);
    }

    public HeadlessEntityId PromptId { get; }

    public HeadlessPlayerId PlayerId { get; }

    public string Message { get; }

    public IReadOnlyList<TimingWindowTrigger> Triggers { get; }

    public ChoiceRequest ToChoiceRequest()
    {
        ChoiceCandidate[] candidates = Triggers
            .Select(trigger => new ChoiceCandidate(
                trigger.Request.EffectId,
                $"Optional effect {trigger.Request.EffectId.Value}",
                ChoiceZone.Custom,
                IsSelectable: true,
                ownerId: trigger.Request.ControllerId))
            .ToArray();

        return new ChoiceRequest(
            ChoiceType.OptionalEffect,
            PlayerId,
            Message,
            minCount: 0,
            maxCount: 1,
            canSkip: true,
            ChoiceZone.Custom,
            candidates);
    }

    public TimingWindowTrigger? FindTrigger(HeadlessEntityId effectId)
    {
        return Triggers.FirstOrDefault(trigger => trigger.Request.EffectId == effectId);
    }
}

public sealed record OptionalPromptQueueResult(
    bool IsSuccess,
    OptionalEffectPrompt? Prompt,
    HeadlessChoiceState ChoiceState,
    int EnqueuedCount,
    int PromptCount,
    bool IsSkipped,
    string FailureReason)
{
    public static OptionalPromptQueueResult Success(
        OptionalEffectPrompt prompt,
        HeadlessChoiceState choiceState,
        int enqueuedCount,
        int promptCount,
        bool skipped = false)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        return new OptionalPromptQueueResult(
            true,
            prompt,
            choiceState,
            enqueuedCount,
            promptCount,
            skipped,
            string.Empty);
    }

    public static OptionalPromptQueueResult Failure(string failureReason)
    {
        return new OptionalPromptQueueResult(
            false,
            Prompt: null,
            HeadlessChoiceState.Empty,
            EnqueuedCount: 0,
            PromptCount: 0,
            IsSkipped: false,
            FailureReason: failureReason ?? string.Empty);
    }
}

internal sealed record OptionalPromptBuildResult(
    bool IsSuccess,
    OptionalEffectPrompt? Prompt,
    string FailureReason)
{
    public static OptionalPromptBuildResult Success(OptionalEffectPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return new OptionalPromptBuildResult(true, prompt, string.Empty);
    }

    public static OptionalPromptBuildResult Failure(string failureReason)
    {
        return new OptionalPromptBuildResult(false, null, failureReason ?? string.Empty);
    }
}
