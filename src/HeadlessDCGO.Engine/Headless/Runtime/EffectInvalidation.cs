namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (D-7 효과 무효화 / invalidation) Whether a card's effects are turned off by a continuous "disable
/// effects" effect from another card. Mirrors the AS-IS <c>CheckEffectDisabledClass.isDisabled</c> /
/// <c>IDisableCardEffect</c>: an effect's <c>CanUse</c> returns false when it <c>IsDisabled</c>.
///
/// Modelled as a continuous effect carrying the <see cref="DisableEffectsKey"/> marker (card-targeted or
/// player-scope, F-5). The trigger loop (<c>GameFlowProcessor</c>) consults this before enqueuing a
/// triggered effect, skipping any whose source card is invalidated; the continuous gates likewise treat
/// a disabled card's own continuous effects as inert.
/// </summary>
public static class EffectInvalidation
{
    /// <summary>Marker (bool true) on a continuous effect: the target card's effects are disabled.</summary>
    public const string DisableEffectsKey = "disableEffects";

    /// <summary>Query scope shared with the continuous gates.</summary>
    public const string Scope = ContinuousDpGate.Scope;

    /// <summary>Whether <paramref name="cardId"/>'s own effects are currently disabled.</summary>
    public static bool IsEffectsDisabled(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return false;
        }

        IEffectQueryService registry = context.EffectRegistry;

        // Card-targeted disable effects.
        foreach (EffectRequest effect in registry.GetContinuousEffects(new EffectQueryContext(Scope, targetEntityId: cardId)))
        {
            if (ReadBool(effect.Context.Values, DisableEffectsKey))
            {
                return true;
            }
        }

        // Player-scope disable effects ("your opponent's Digimon lose their effects").
        if (context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) && instance is not null)
        {
            CardRecord? card = context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) ? def : null;
            foreach (EffectRequest effect in PlayerScopeContinuousHelpers.CollectApplicable(registry, Scope, instance.OwnerId, card))
            {
                if (ReadBool(effect.Context.Values, DisableEffectsKey))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? raw) && raw is bool b && b;
}
