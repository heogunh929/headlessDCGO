namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public static class ContinuousEffectEvaluatorFactory
{
    public static ContinuousEvaluationResult Evaluate(
        EffectQueryContext queryContext,
        IReadOnlyList<EffectRequest>? continuousEffects = null,
        CardRecord? card = null,
        CardInstanceRecord? instance = null,
        CardInstanceState? state = null)
    {
        return ContinuousEffectEvaluator.Evaluate(
            new ContinuousEvaluationRequest(queryContext, continuousEffects, card, instance, state));
    }

    public static ContinuousEvaluationResult QueryAndEvaluate(
        IEffectQueryService effectQueryService,
        EffectQueryContext queryContext,
        CardRecord? card = null,
        CardInstanceRecord? instance = null,
        CardInstanceState? state = null)
    {
        return ContinuousEffectEvaluator.Evaluate(effectQueryService, queryContext, card, instance, state);
    }
}
