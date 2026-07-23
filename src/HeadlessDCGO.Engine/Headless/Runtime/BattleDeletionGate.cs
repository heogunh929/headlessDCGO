namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (R2-1 / N-2, C5-1) Deletion-prevention gate for battle: the sibling of <see cref="ContinuousRestrictionGate"/>
/// (which wires the restriction slice). Before a battle would delete a Digimon, this checks whether a continuous
/// "cannot be deleted" / "cannot be deleted by battle" effect protects it. (C5-1) The former empty-union
/// ContinuousScopeEvaluation replacement/value-flag scaffold was retired (producer 0); the live path is the
/// new-model interface scan <see cref="NewModelContinuousScan.HasCanNotBeDestroyed"/> /
/// <see cref="NewModelContinuousScan.HasCanNotBeDestroyedByBattle"/> (AS-IS Permanent.CanBeDestroyed /
/// CanBeDestroyedByBattle), in addition to the static <c>preventBattleDeletion</c> flag the battle resolvers read.
/// </summary>
public static class BattleDeletionGate
{
    public static bool PreventsBattleDeletion(EngineContext context, HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardId.IsEmpty)
        {
            return false;
        }

        // (C5-1) The former ContinuousScopeEvaluation empty-union scaffold — EvaluateForCard → the Delete/Prevent
        // ReplacementEffect scan, plus the separate `preventBattleDeletion` value-flag ApplicableEffects loop (with
        // its AD1-G 4-arg battle predicate) — was retired: ApplicableEffects reached producer 0 (permanently empty),
        // so both arms were production-inert. The live deletion immunity is the new-model interface scan alone
        // (AS-IS Permanent.CanBeDestroyed:3186 / CanBeDestroyedByBattle:3233) — a ported CanNotBeDestroyedStaticEffect
        // / CanNotBeDestroyedByBattleStaticEffect (CanNotBeDestroyedClass:ICanNotBeDestroyedEffect /
        // CanNotBeDestroyedByBattleClass:ICanNotBeDestroyedByBattleEffect) registers no legacy replacement.
        if (Assets.Scripts.Script.CardEffectCommons.NewModelContinuousScan.HasCanNotBeDestroyed(context, cardId))
        {
            return true;
        }

        HeadlessAttackState currentAttack = context.AttackController.Current;
        if (Assets.Scripts.Script.CardEffectCommons.NewModelContinuousScan.HasCanNotBeDestroyedByBattle(
                context, cardId, currentAttack.AttackerId ?? default, currentAttack.TargetId ?? default))
        {
            return true;
        }

        return false;
    }
}
