namespace HeadlessDCGO.Engine.Headless.Bridge;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class GManagerBridge
{
    public GManagerBridge(EngineContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public EngineContext Context { get; }

    public IHeadlessTurnController Turn => Context.TurnController;

    public EffectScheduler Effects => Context.EffectScheduler;

    public EffectScheduler AutoProcessing => Context.EffectScheduler;

    public IHeadlessAttackController Attack => Context.AttackController;

    public ObservationSnapshot State => Context.CurrentState;

    public ILogSink Log => Context.LogSink;

    public DcgoMatch? CurrentMatch => Context.CurrentMatch;

    public IHeadlessTurnController GetTurnStateMachine()
    {
        return Turn;
    }

    public EffectScheduler GetAutoProcessing()
    {
        return AutoProcessing;
    }

    public IHeadlessAttackController GetAttackProcess()
    {
        return Attack;
    }

    public EffectScheduler GetEffectScheduler()
    {
        return Effects;
    }

    public ObservationSnapshot GetCurrentState()
    {
        return State;
    }

    public ILogSink GetLog()
    {
        return Log;
    }

    public TService GetService<TService>()
        where TService : class
    {
        return Context.GetService<TService>();
    }

    public object GetService(Type serviceType)
    {
        return Context.GetService(serviceType);
    }

    public bool TryGetService<TService>(out TService? service)
        where TService : class
    {
        return Context.TryGetService(out service);
    }
}
