namespace HeadlessDCGO.Engine.Headless.Coroutines;

public interface IEngineTask
{
    EngineTaskStatus Status { get; }

    EngineWaitCondition? CurrentWait { get; }

    Exception? Error { get; }

    bool IsCompleted => Status == EngineTaskStatus.Completed;

    bool IsFaulted => Status == EngineTaskStatus.Faulted;

    bool IsTerminal => Status is EngineTaskStatus.Completed or EngineTaskStatus.Faulted or EngineTaskStatus.Canceled;

    Task<EngineTaskStepResult> StepAsync(CancellationToken cancellationToken = default);
}

public enum EngineTaskStatus
{
    Pending,
    Waiting,
    Completed,
    Faulted,
    Canceled
}

public sealed record EngineTaskStepResult(
    EngineTaskStatus Status,
    EngineWaitCondition? Wait = null,
    Exception? Error = null)
{
    public bool IsTerminal => Status is EngineTaskStatus.Completed or EngineTaskStatus.Faulted or EngineTaskStatus.Canceled;

    public static EngineTaskStepResult Pending()
    {
        return new EngineTaskStepResult(EngineTaskStatus.Pending);
    }

    public static EngineTaskStepResult Waiting(EngineWaitCondition wait)
    {
        ArgumentNullException.ThrowIfNull(wait);
        return new EngineTaskStepResult(EngineTaskStatus.Waiting, wait);
    }

    public static EngineTaskStepResult Completed()
    {
        return new EngineTaskStepResult(EngineTaskStatus.Completed);
    }

    public static EngineTaskStepResult Faulted(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new EngineTaskStepResult(EngineTaskStatus.Faulted, Error: error);
    }

    public static EngineTaskStepResult Canceled()
    {
        return new EngineTaskStepResult(EngineTaskStatus.Canceled);
    }
}
