namespace HeadlessDCGO.Engine.Headless.Coroutines;

using System.Collections;

public sealed class CoroutineAdapter
{
    public static IEngineTask FromEnumerator(IEnumerator enumerator)
    {
        ArgumentNullException.ThrowIfNull(enumerator);
        return new EnumeratorEngineTask(enumerator);
    }

    private sealed class EnumeratorEngineTask : IEngineTask
    {
        private readonly Stack<IEnumerator> _enumerators = new();

        public EnumeratorEngineTask(IEnumerator enumerator)
        {
            _enumerators.Push(enumerator);
        }

        public EngineTaskStatus Status { get; private set; } = EngineTaskStatus.Pending;

        public EngineWaitCondition? CurrentWait { get; private set; }

        public Exception? Error { get; private set; }

        public Task<EngineTaskStepResult> StepAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Status is EngineTaskStatus.Completed)
            {
                return Task.FromResult(EngineTaskStepResult.Completed());
            }

            if (Status is EngineTaskStatus.Faulted)
            {
                return Task.FromResult(EngineTaskStepResult.Faulted(
                    Error ?? new InvalidOperationException("Enumerator task faulted without an error.")));
            }

            if (CurrentWait is not null)
            {
                if (!CurrentWait.IsSatisfied())
                {
                    Status = EngineTaskStatus.Waiting;
                    return Task.FromResult(EngineTaskStepResult.Waiting(CurrentWait));
                }

                CurrentWait = null;
                Status = EngineTaskStatus.Pending;
            }

            try
            {
                while (_enumerators.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    IEnumerator current = _enumerators.Peek();

                    if (!current.MoveNext())
                    {
                        _enumerators.Pop();
                        continue;
                    }

                    switch (current.Current)
                    {
                        case null:
                            Status = EngineTaskStatus.Pending;
                            return Task.FromResult(EngineTaskStepResult.Pending());

                        case EngineWaitCondition waitCondition:
                            CurrentWait = waitCondition;
                            Status = EngineTaskStatus.Waiting;
                            return Task.FromResult(EngineTaskStepResult.Waiting(waitCondition));

                        case IEnumerator nestedEnumerator:
                            _enumerators.Push(nestedEnumerator);
                            continue;

                        default:
                            Status = EngineTaskStatus.Pending;
                            return Task.FromResult(EngineTaskStepResult.Pending());
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Error = ex;
                Status = EngineTaskStatus.Faulted;
                return Task.FromResult(EngineTaskStepResult.Faulted(ex));
            }

            Status = EngineTaskStatus.Completed;
            return Task.FromResult(EngineTaskStepResult.Completed());
        }
    }
}
