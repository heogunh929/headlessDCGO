namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (B-2) Sibling of <see cref="ContinuousDpGate"/> for the other numeric metrics that continuous
/// effects modify: Security Attack and play / digivolution cost. Each folds in card-targeted AND
/// player-scope continuous modifiers (via <see cref="ContinuousScopeEvaluation"/>) and resolves them
/// over a base value, mirroring the original per-access rescans. Because the modifiers are sourced from
/// continuous registry bindings, an <see cref="HeadlessDCGO.Engine.Headless.Effects.EffectDuration"/>
/// tag (F-1) makes a "+1 Security Attack until end of turn" effect expire automatically — no special
/// handling needed here. With no registered continuous effects each method returns the base unchanged.
/// </summary>
public static class ContinuousModifierGate
{
    /// <summary>Query scope used for continuous re-evaluation (shared with the other gates).</summary>
    public const string Scope = ContinuousDpGate.Scope;

    public static int ResolveSecurityAttack(EngineContext context, HeadlessEntityId cardId, int baseSecurityAttack)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return baseSecurityAttack;
        }

        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(context, Scope, cardId);
        return ModifierHelpers.ResolveSecurityAttack(baseSecurityAttack, result.Modifiers, cardId).FinalValue;
    }

    public static int ResolvePlayCost(EngineContext context, HeadlessEntityId cardId, int basePlayCost, bool canReduceCost = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return basePlayCost;
        }

        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(context, Scope, cardId);
        return ModifierHelpers.ResolvePlayCost(basePlayCost, result.Modifiers, canReduceCost: canReduceCost).FinalValue;
    }

    public static int ResolveDigivolutionCost(EngineContext context, HeadlessEntityId cardId, int baseDigivolutionCost, bool canReduceCost = true)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return baseDigivolutionCost;
        }

        ContinuousEvaluationResult result = ContinuousScopeEvaluation.EvaluateForCard(context, Scope, cardId);
        return ModifierHelpers.ResolveDigivolutionCost(baseDigivolutionCost, result.Modifiers, canReduceCost: canReduceCost).FinalValue;
    }
}
