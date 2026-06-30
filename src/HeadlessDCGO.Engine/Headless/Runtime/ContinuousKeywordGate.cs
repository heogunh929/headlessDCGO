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
    // (C-group preemptive seal) also presence-flag pattern (hasAlliance/hasOverclock/hasProgress/
    // hasArmorPurge/hasDecode/hasPartition). Latent (no self-static factory/card yet). Names match
    // KeywordBaseBatch2Factory.KeywordName — note "Armor Purge" has a space.
    public const string Alliance = "Alliance";
    public const string Overclock = "Overclock";
    public const string Progress = "Progress";
    public const string ArmorPurge = "Armor Purge";
    public const string Decode = "Decode";
    public const string Partition = "Partition";
    public const string Vortex = "Vortex"; // GR-006: end-of-turn effect-driven attack (opponent Digimon).

    /// <summary>True if an active self-static <paramref name="keyword"/> binding in the registry is sourced
    /// from (or targets) <paramref name="cardId"/>.</summary>
    public static bool HasKeyword(EngineContext context, HeadlessEntityId cardId, string keyword)
    {
        ArgumentNullException.ThrowIfNull(context);
        return HasKeyword(context.EffectRegistry, cardId, keyword);
    }

    /// <summary>Registry-only overload for consumers that hold an <see cref="EffectRegistry"/> but not the
    /// full <see cref="EngineContext"/> (e.g. DeletionReplacementGate's context-less resolution methods).</summary>
    public static bool HasKeyword(EffectRegistry registry, HeadlessEntityId cardId, string keyword)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (cardId.IsEmpty || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        foreach (EffectBinding binding in registry.GetKeywordEffects(keyword))
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
