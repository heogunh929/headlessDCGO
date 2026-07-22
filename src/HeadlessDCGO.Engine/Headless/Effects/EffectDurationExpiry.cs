namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (③-B) The EffectRegistry duration-sweep entry points (ExpireTurnEnd / ExpireBattleEnd / ExpireAttackEnd /
/// ExpireUnsuspend + the registry-only ExpireFixedCostCalc(registry)) are RETIRED: with the continuous-binding
/// producer at 0 every registry sweep was a dead write against a permanently-empty store. The live AS-IS duration
/// expiry is the per-carrier BUCKET reset at each choke (HeadlessEndTurnCleanupFlow player/permanent Until*TurnEnd
/// resets + TSM:256/259; BattleResolver UntilEndBattle reset; AttackProcess UntilEndAttack reset).
///
/// The sole surviving member is the fixed-cost-calc bucket reset (<see cref="ExpireFixedCostCalc"/>): AS-IS
/// <c>CardController.cs:961</c> clears <c>Player.UntilCalculateFixedCostEffect</c> on each play, so a bucket-form
/// BeforePayCost effect cannot leak into the next play of the same turn. Called at every pay-completion choke.
/// </summary>
public static class EffectDurationExpiry
{
    /// <summary>(R2-C) Expire the fixed-cost-calc duration on <paramref name="payer"/>'s per-play
    /// <c>Player.UntilCalculateFixedCostEffect</c> bucket (AS-IS <c>CardController.cs:961</c> clears the bucket on
    /// each play). Called at every pay-completion choke so a bucket-form BeforePayCost effect cannot leak into
    /// the next play of the same turn.</summary>
    public static void ExpireFixedCostCalc(HeadlessDCGO.Engine.Headless.Bridge.EngineContext context, HeadlessPlayerId payer)
    {
        ArgumentNullException.ThrowIfNull(context);
        new Assets.Scripts.Script.CardEffectCommons.Player(context, payer).UntilCalculateFixedCostEffect =
            new List<Func<Assets.Scripts.Script.CardEffectCommons.EffectTiming, Assets.Scripts.Script.CardEffectCommons.ICardEffect>>();
    }
}
