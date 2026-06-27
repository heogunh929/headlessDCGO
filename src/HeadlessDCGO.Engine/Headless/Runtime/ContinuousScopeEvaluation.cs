namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (F-5) Shared continuous evaluation for a single card that folds in BOTH card-targeted continuous
/// effects (matched by <c>targetEntityId</c>) AND player-scope continuous effects (matched by the
/// card's owner + condition, via <see cref="PlayerScopeContinuousHelpers"/>). The continuous gates
/// (DP / restriction) call this so a "your Digimon get +1000 DP / cannot block" effect reaches every
/// applicable permanent without being individually targeted.
/// </summary>
public static class ContinuousScopeEvaluation
{
    public static ContinuousEvaluationResult EvaluateForCard(
        EngineContext context,
        string scope,
        HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var queryContext = new EffectQueryContext(scope, targetEntityId: cardId);
        IEffectQueryService registry = context.EffectRegistry;

        IReadOnlyList<EffectRequest> cardTargeted = registry.GetContinuousEffects(queryContext);

        ResolveCard(context, cardId, out HeadlessPlayerId owner, out CardRecord? card, out CardInstanceRecord? instance);

        IReadOnlyList<EffectRequest> playerScoped = owner.IsEmpty
            ? Array.Empty<EffectRequest>()
            : PlayerScopeContinuousHelpers.CollectApplicable(registry, scope, owner, card);

        EffectRequest[] combined = cardTargeted
            .Concat(playerScoped)
            .GroupBy(effect => effect.EffectId.Value, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(effect => effect.EffectId.Value, StringComparer.Ordinal)
            .ToArray();

        return ContinuousEffectEvaluator.Evaluate(
            new ContinuousEvaluationRequest(queryContext, combined, card, instance, state: null));
    }

    private static void ResolveCard(
        EngineContext context,
        HeadlessEntityId cardId,
        out HeadlessPlayerId owner,
        out CardRecord? card,
        out CardInstanceRecord? instance)
    {
        owner = default;
        card = null;
        instance = null;

        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        instance = record;
        owner = record.OwnerId;
        if (context.CardRepository.TryGetCard(record.DefinitionId, out CardRecord? definition))
        {
            card = definition;
        }
    }
}
