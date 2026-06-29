namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

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

        // Card-targeted MAIN effects (an inherited effect targeting its own source is excluded here — it
        // reaches the top card only through the inherited-source path below).
        IReadOnlyList<EffectRequest> cardTargeted = registry.GetContinuousEffects(queryContext)
            .Where(effect => !IsInherited(effect))
            .ToArray();

        // Inherited effects: every active (non-flipped) digivolution source under this card grants its
        // inherited continuous effects to this card (the top). Mirrors the original Permanent.GetDP /
        // GetSAttack folding in under-card inherited effects.
        IReadOnlyList<EffectRequest> inherited = CollectInheritedEffects(context, registry, scope, cardId);

        ResolveCard(context, cardId, out HeadlessPlayerId owner, out CardRecord? card, out CardInstanceRecord? instance);

        IReadOnlyList<EffectRequest> playerScoped = owner.IsEmpty
            ? Array.Empty<EffectRequest>()
            : PlayerScopeContinuousHelpers.CollectApplicable(registry, scope, owner, card, ResolveZoneName(context, owner, cardId));

        EffectRequest[] combined = cardTargeted
            .Concat(inherited)
            .Concat(playerScoped)
            .GroupBy(effect => effect.EffectId.Value, StringComparer.Ordinal)
            .Select(group => group.First())
            // D-7: a continuous effect whose SOURCE card is invalidated ("disable effects") is inert.
            // (IsEffectsDisabled queries the registry directly, not EvaluateForCard, so no recursion.)
            .Where(effect => !EffectInvalidation.IsEffectsDisabled(context, effect.Context.SourceEntityId))
            // A card-authored `condition` predicate (Func<bool>) gates the effect at read time.
            .Where(ConditionPasses)
            // A card-authored dynamic delta (Func<int>) is resolved to a concrete value at read time.
            .Select(ResolveDynamicValue)
            .OrderBy(effect => effect.EffectId.Value, StringComparer.Ordinal)
            .ToArray();

        return ContinuousEffectEvaluator.Evaluate(
            new ContinuousEvaluationRequest(queryContext, combined, card, instance, state: null));
    }

    // The zones a player-scope effect can scope to (battle-area buffs and security-Digimon buffs). Returns
    // the zone name for ScopeZoneKey matching, or null if the card is in neither.
    private static string? ResolveZoneName(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId cardId)
    {
        var zones = (IZoneStateReader)context.ZoneMover;
        foreach (ChoiceZone zone in new[] { ChoiceZone.BattleArea, ChoiceZone.Security })
        {
            if (zones.GetCards(owner, zone).Contains(cardId))
            {
                return zone.ToString();
            }
        }

        return null;
    }

    private static bool IsInherited(EffectRequest effect) =>
        effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.InheritedEffectKey, out object? raw)
        && raw is bool flag && flag;

    private static bool ConditionPasses(EffectRequest effect)
    {
        if (effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.ConditionKey, out object? raw)
            && raw is Func<bool> condition)
        {
            return condition();
        }

        return true;
    }

    private static EffectRequest ResolveDynamicValue(EffectRequest effect)
    {
        if (!effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.DynamicValueKey, out object? rawValue)
            || rawValue is not Func<int> compute
            || !effect.Context.Values.TryGetValue(ContinuousSelfModifierEffect.DynamicMetricKey, out object? rawMetric)
            || rawMetric is not string metricKey)
        {
            return effect;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in effect.Context.Values)
        {
            if (pair.Key == ContinuousSelfModifierEffect.DynamicValueKey
                || pair.Key == ContinuousSelfModifierEffect.DynamicMetricKey)
            {
                continue;
            }

            values[pair.Key] = pair.Value;
        }

        values[metricKey] = compute();

        EffectContext source = effect.Context;
        var rewritten = new EffectContext(
            source.SourcePlayerId,
            source.OwnerPlayerId,
            source.SourceEntityId,
            source.TriggerEntityId,
            source.TargetEntityIds,
            values);
        return new EffectRequest(effect.EffectId, effect.ControllerId, effect.Timing, rewritten);
    }

    private static IReadOnlyList<EffectRequest> CollectInheritedEffects(
        EngineContext context,
        IEffectQueryService registry,
        string scope,
        HeadlessEntityId topCardId)
    {
        DigivolutionStack stack = DigivolutionStackReader.Read(
            context.CardInstanceRepository, context.CardRepository, topCardId);
        if (stack.IsEmpty)
        {
            return Array.Empty<EffectRequest>();
        }

        // Sources are considered non-flipped here; flip-state gating is a later refinement.
        IReadOnlyList<HeadlessEntityId> activeSources =
            InheritedEffectHelpers.ActiveInheritedSources(stack, _ => false, permanentIsDigimon: true);
        if (activeSources.Count == 0)
        {
            return Array.Empty<EffectRequest>();
        }

        var collected = new List<EffectRequest>();
        foreach (HeadlessEntityId source in activeSources)
        {
            IReadOnlyList<EffectRequest> sourceEffects = registry.GetContinuousEffects(
                new EffectQueryContext(scope, targetEntityId: source));
            collected.AddRange(sourceEffects.Where(IsInherited));
        }

        return collected;
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
