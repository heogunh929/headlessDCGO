namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (CV-A1 / F-1) Removes continuous <see cref="EffectBinding"/>s from the registry when their
/// <see cref="EffectBinding.Duration"/> reaches its expiry point. Mirrors the original "reset status until
/// end of turn / battle / attack" cleanup. Bindings with a null duration are permanent and never removed.
///
/// Each expiry entry point removes the duration kinds that elapse at that moment:
/// <list type="bullet">
/// <item>Turn end → <see cref="EffectDuration.UntilEachTurnEnd"/>, plus owner/opponent turn-end scoped by
/// the ending turn player vs the binding controller.</item>
/// <item>Battle end → <see cref="EffectDuration.UntilEndBattle"/>.</item>
/// <item>Attack end → <see cref="EffectDuration.UntilEndAttack"/>.</item>
/// <item>Unsuspend (active phase) → <see cref="EffectDuration.UntilOwnerActivePhase"/>,
/// <see cref="EffectDuration.UntilNextUntap"/>.</item>
/// <item>Fixed-cost calc → <see cref="EffectDuration.UntilCalculateFixedCost"/>.</item>
/// </list>
/// </summary>
public static class EffectDurationExpiry
{
    /// <summary>Expires turn-end durations. <paramref name="endingTurnPlayerId"/> is the player whose turn is ending.</summary>
    public static int ExpireTurnEnd(EffectRegistry registry, HeadlessPlayerId? endingTurnPlayerId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.RemoveWhere(binding => binding.Duration switch
        {
            EffectDuration.UntilEachTurnEnd => true,
            EffectDuration.UntilOwnerTurnEnd => endingTurnPlayerId is { } p && binding.Request.ControllerId == p,
            EffectDuration.UntilOpponentTurnEnd => endingTurnPlayerId is { } p && binding.Request.ControllerId != p,
            _ => false,
        });
    }

    /// <summary>Expires battle-end durations (<see cref="EffectDuration.UntilEndBattle"/>).</summary>
    public static int ExpireBattleEnd(EffectRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.RemoveWhere(binding => binding.Duration == EffectDuration.UntilEndBattle);
    }

    /// <summary>Expires attack-end durations (<see cref="EffectDuration.UntilEndAttack"/>).</summary>
    public static int ExpireAttackEnd(EffectRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.RemoveWhere(binding => binding.Duration == EffectDuration.UntilEndAttack);
    }

    /// <summary>Expires unsuspend-scoped durations for the given (unsuspending) player.</summary>
    public static int ExpireUnsuspend(EffectRegistry registry, HeadlessPlayerId? unsuspendingPlayerId)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.RemoveWhere(binding => binding.Duration switch
        {
            EffectDuration.UntilOwnerActivePhase => unsuspendingPlayerId is { } p && binding.Request.ControllerId == p,
            EffectDuration.UntilNextUntap => true,
            _ => false,
        });
    }

    /// <summary>Expires fixed-cost-calc durations (<see cref="EffectDuration.UntilCalculateFixedCost"/>).</summary>
    public static int ExpireFixedCostCalc(EffectRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.RemoveWhere(binding => binding.Duration == EffectDuration.UntilCalculateFixedCost);
    }
}
