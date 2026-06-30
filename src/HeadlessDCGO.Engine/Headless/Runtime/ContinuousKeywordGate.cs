namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (GR-005) Derives whether a card currently HAS a self-static keyword (Blocker / Jamming / Piercing) by
/// querying the EffectRegistry at read time — the same pull pattern that makes continuous MODIFIERS work
/// (<see cref="ContinuousModifierGate"/>, <see cref="ContinuousDpGate"/>).
///
/// Why this exists: a ported <c>&lt;Blocker&gt;</c> (etc.) is registered as a keyword binding in the
/// EffectRegistry when the card enters play. But its consumers (BlockTiming, BattleResolver, SecurityResolver)
/// historically read a per-instance METADATA flag (<c>hasBlocker</c>/<c>hasPiercing</c>/<c>preventBattleDeletion</c>)
/// that is only written when a keyword effect RESOLVES at its timing — which never happens for a self-static
/// in the live flow. So the flag stayed false and the keyword was inert in real games (confirmed: a blocker
/// on the field opened 0 block windows across 223 attacks). Querying the registry binding directly closes
/// that gap, leaving the metadata-flag path intact for keywords GRANTED by other cards' effects.
/// </summary>
public static class ContinuousKeywordGate
{
    public const string Blocker = "Blocker";
    public const string Jamming = "Jamming";
    public const string Piercing = "Piercing";
    // (B-group preemptive seal) same presence-flag pattern as Blocker — consumers read hasReboot/hasRush/
    // hasBlitz/hasRetaliation, which a self-static binding never sets. No card ports these as self-statics
    // yet (latent), so wiring the gate now means a future self-static just works.
    public const string Reboot = "Reboot";
    public const string Rush = "Rush";
    public const string Blitz = "Blitz";
    public const string Retaliation = "Retaliation";

    /// <summary>True if an active self-static <paramref name="keyword"/> binding in the registry is sourced
    /// from (or targets) <paramref name="cardId"/>.</summary>
    public static bool HasKeyword(EngineContext context, HeadlessEntityId cardId, string keyword)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        foreach (EffectBinding binding in context.EffectRegistry.GetKeywordEffects(keyword))
        {
            EffectContext effectContext = binding.Request.Context;
            if (effectContext.SourceEntityId == cardId || effectContext.TargetEntityIds.Contains(cardId))
            {
                return true;
            }
        }

        return false;
    }
}
