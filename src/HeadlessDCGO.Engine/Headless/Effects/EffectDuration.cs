namespace HeadlessDCGO.Engine.Headless.Effects;

/// <summary>
/// (CV-A1 / F-1) How long a temporary effect or modifier lasts before it is cleaned up. Mirrors the
/// original DCGO <c>EffectDuration</c> enum (<c>ICardEffect.cs</c>). A continuous <see cref="EffectBinding"/>
/// carries an optional <see cref="EffectBinding.Duration"/>; a null duration means the effect is permanent
/// (static) and never auto-expires. <see cref="EffectDurationExpiry"/> removes bindings at the matching
/// expiry point (turn end, battle end, attack end, next unsuspend, fixed-cost calc).
/// </summary>
public enum EffectDuration
{
    /// <summary>Until the end of the current/next turn, whoever's turn it is.</summary>
    UntilEachTurnEnd = 0,

    /// <summary>Until the end of the controller's (owner's) turn.</summary>
    UntilOwnerTurnEnd = 1,

    /// <summary>Until the end of the opponent's turn.</summary>
    UntilOpponentTurnEnd = 2,

    /// <summary>Until the current attack finishes resolving.</summary>
    UntilEndAttack = 3,

    /// <summary>Until the current battle finishes resolving.</summary>
    UntilEndBattle = 4,

    /// <summary>Until the controller's next active (unsuspend) phase.</summary>
    UntilOwnerActivePhase = 5,

    /// <summary>Until the affected permanent next unsuspends.</summary>
    UntilNextUntap = 6,

    /// <summary>Until the fixed-cost calculation completes (cost-modifier scoped).</summary>
    UntilCalculateFixedCost = 7,
}
