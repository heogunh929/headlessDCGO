namespace HeadlessDCGO.Engine.Headless.Coroutines;

public sealed class EngineTaskRunner
{
    private readonly Queue<IEngineTask> _tasks = new();

    public int PendingTaskCount => _tasks.Count;

    public void Enqueue(IEngineTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        _tasks.Enqueue(task);
    }

    public async Task RunAsync(IEngineTask task, CancellationToken cancellationToken = default)
    {
        Enqueue(task);
        await RunUntilIdleAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StepAsync(CancellationToken cancellationToken = default)
    {
        int taskCount = _tasks.Count;

        for (int i = 0; i < taskCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEngineTask task = _tasks.Dequeue();
            if (task.IsTerminal)
            {
                continue;
            }

            if (task.CurrentWait is not null && !task.CurrentWait.IsSatisfied())
            {
                _tasks.Enqueue(task);
                continue;
            }

            EngineTaskStepResult result = await task.StepAsync(cancellationToken).ConfigureAwait(false);
            if (result.Status == EngineTaskStatus.Faulted)
            {
                throw result.Error ?? task.Error ?? new InvalidOperationException("Engine task faulted without an error.");
            }

            if (!task.IsTerminal)
            {
                _tasks.Enqueue(task);
            }
        }
    }

    public async Task RunUntilIdleAsync(CancellationToken cancellationToken = default)
    {
        while (!IsIdle())
        {
            await StepAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public bool IsIdle()
    {
        return !_tasks.Any(task =>
            !task.IsTerminal &&
            (task.CurrentWait is null || task.CurrentWait.IsSatisfied()));
    }

    public void Clear()
    {
        _tasks.Clear();
    }
}
