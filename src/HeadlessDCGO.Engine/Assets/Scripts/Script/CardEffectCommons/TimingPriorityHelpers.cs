namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Rules;
using HeadlessDCGO.Engine.Headless.Services;

public static class TimingPriorityHelperFactory
{
    public static TimingPriorityOrderResult Order(
        IEnumerable<TimingWindowTrigger> triggers,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId = null)
    {
        return TimingPriorityHelpers.Order(triggers, turnPlayerId, nonTurnPlayerId);
    }

    public static TimingPriorityOrderResult OrderAndEnqueueMandatory(
        IEnumerable<TimingWindowTrigger> triggers,
        EffectScheduler scheduler,
        HeadlessPlayerId turnPlayerId,
        HeadlessPlayerId? nonTurnPlayerId = null)
    {
        return TimingPriorityHelpers.OrderAndEnqueueMandatory(triggers, scheduler, turnPlayerId, nonTurnPlayerId);
    }
}
