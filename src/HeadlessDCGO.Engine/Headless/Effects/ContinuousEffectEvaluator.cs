namespace HeadlessDCGO.Engine.Headless.Effects;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed record ContinuousEvaluationRequest
{
    public ContinuousEvaluationRequest(
        EffectQueryContext queryContext,
        IReadOnlyList<EffectRequest>? continuousEffects = null,
        CardRecord? card = null,
        CardInstanceRecord? instance = null,
        CardInstanceState? state = null)
    {
        ArgumentNullException.ThrowIfNull(queryContext);

        QueryContext = queryContext;
        ContinuousEffects = CopyEffects(continuousEffects);
        Card = card;
        Instance = instance;
        State = state;
    }

    public EffectQueryContext QueryContext { get; }

    public IReadOnlyList<EffectRequest> ContinuousEffects { get; }

    public CardRecord? Card { get; }

    public CardInstanceRecord? Instance { get; }

    public CardInstanceState? State { get; }

    private static IReadOnlyList<EffectRequest> CopyEffects(IReadOnlyList<EffectRequest>? effects)
    {
        if (effects is null || effects.Count == 0)
        {
            return Array.AsReadOnly(Array.Empty<EffectRequest>());
        }

        foreach (EffectRequest effect in effects)
        {
            ArgumentNullException.ThrowIfNull(effect);
        }

        return Array.AsReadOnly(effects
            .OrderBy(effect => effect.EffectId.Value, StringComparer.Ordinal)
            .ToArray());
    }
}

public sealed record ContinuousEvaluationResult
{
    internal ContinuousEvaluationResult(
        EffectQueryContext queryContext,
        IReadOnlyList<EffectRequest> continuousEffects,
        IReadOnlyList<NumericModifier> modifiers,
        IReadOnlyList<CannotRestriction> restrictions,
        IReadOnlyList<ReplacementEffect> replacements,
        IReadOnlyDictionary<string, object?> values)
    {
        QueryContext = queryContext;
        ContinuousEffects = Array.AsReadOnly(continuousEffects.ToArray());
        Modifiers = Array.AsReadOnly(modifiers.ToArray());
        Restrictions = Array.AsReadOnly(restrictions.ToArray());
        Replacements = Array.AsReadOnly(replacements.ToArray());
        Values = CopyValues(values);
    }

    public EffectQueryContext QueryContext { get; }

    public IReadOnlyList<EffectRequest> ContinuousEffects { get; }

    public IReadOnlyList<NumericModifier> Modifiers { get; }

    public IReadOnlyList<CannotRestriction> Restrictions { get; }

    public IReadOnlyList<ReplacementEffect> Replacements { get; }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public NumericModifierResult ResolveDp(int baseDp, HeadlessEntityId? targetEntityId = null)
    {
        return ModifierHelpers.ResolveDp(baseDp, Modifiers, targetEntityId);
    }

    public NumericModifierResult ResolvePlayCost(int baseCost, bool checkAvailability = false, bool canReduceCost = true)
    {
        return ModifierHelpers.ResolvePlayCost(baseCost, Modifiers, checkAvailability, canReduceCost);
    }

    public NumericModifierResult ResolveDigivolutionCost(int baseCost, bool checkAvailability = false, bool canReduceCost = true)
    {
        return ModifierHelpers.ResolveDigivolutionCost(baseCost, Modifiers, checkAvailability, canReduceCost);
    }

    public NumericModifierResult ResolveSecurityAttack(int baseSecurityAttack, HeadlessEntityId? targetEntityId = null)
    {
        return ModifierHelpers.ResolveSecurityAttack(baseSecurityAttack, Modifiers, targetEntityId);
    }

    private static IReadOnlyDictionary<string, object?> CopyValues(IReadOnlyDictionary<string, object?> values)
    {
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            copy[pair.Key.Trim()] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}

public static class ContinuousEffectEvaluator
{
    public static ContinuousEvaluationResult Evaluate(ContinuousEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<EffectRequest> continuousEffects = request.ContinuousEffects;
        IReadOnlyList<NumericModifier> modifiers = ModifierHelpers.ReadModifiers(
            request.Card,
            request.Instance,
            request.State,
            continuousEffects);
        IReadOnlyList<CannotRestriction> restrictions = RestrictionHelpers.ReadRestrictions(
            request.Card,
            request.Instance,
            request.State,
            continuousEffects);
        IReadOnlyList<ReplacementEffect> replacements = ReplacementHelpers.ReadReplacements(
            request.Card,
            request.Instance,
            request.State,
            continuousEffects);

        return new ContinuousEvaluationResult(
            request.QueryContext,
            continuousEffects,
            modifiers,
            restrictions,
            replacements,
            BuildValues(request.QueryContext, continuousEffects, modifiers, restrictions, replacements));
    }

    public static ContinuousEvaluationResult Evaluate(
        IEffectQueryService effectQueryService,
        EffectQueryContext queryContext,
        CardRecord? card = null,
        CardInstanceRecord? instance = null,
        CardInstanceState? state = null)
    {
        ArgumentNullException.ThrowIfNull(effectQueryService);
        ArgumentNullException.ThrowIfNull(queryContext);

        IReadOnlyList<EffectRequest> continuousEffects = effectQueryService
            .GetContinuousEffects(queryContext)
            .OrderBy(effect => effect.EffectId.Value, StringComparer.Ordinal)
            .ToArray();

        return Evaluate(new ContinuousEvaluationRequest(queryContext, continuousEffects, card, instance, state));
    }

    public static ContinuousEvaluationResult Recalculate(ContinuousEvaluationRequest request)
    {
        return Evaluate(request);
    }

    private static IReadOnlyDictionary<string, object?> BuildValues(
        EffectQueryContext queryContext,
        IReadOnlyList<EffectRequest> continuousEffects,
        IReadOnlyList<NumericModifier> modifiers,
        IReadOnlyList<CannotRestriction> restrictions,
        IReadOnlyList<ReplacementEffect> replacements)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scope"] = queryContext.Scope,
            ["continuousEffectCount"] = continuousEffects.Count,
            ["continuousEffectIds"] = continuousEffects.Select(effect => effect.EffectId.Value).ToArray(),
            ["modifierCount"] = modifiers.Count,
            ["modifierIds"] = modifiers.Select(modifier => modifier.Id).ToArray(),
            ["restrictionCount"] = restrictions.Count,
            ["restrictionIds"] = restrictions.Select(restriction => restriction.Id).ToArray(),
            ["replacementCount"] = replacements.Count,
            ["replacementIds"] = replacements.Select(replacement => replacement.Id).ToArray(),
        };

        if (queryContext.SourceEntityId is HeadlessEntityId sourceEntityId)
        {
            values["sourceEntityId"] = sourceEntityId.Value;
        }

        if (queryContext.PlayerId is HeadlessPlayerId playerId)
        {
            values["playerId"] = playerId.Value;
        }

        if (queryContext.TargetEntityId is HeadlessEntityId targetEntityId)
        {
            values["targetEntityId"] = targetEntityId.Value;
        }

        return values;
    }
}
