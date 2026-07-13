// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Blitz.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Blitz). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Blitz's resolution branch (1:1 with the original layout).
// (EFFECT-MODEL REBUILD / bridge W3) A second, brace-scoped namespace block was appended for the
// AS-IS-signature `BlitzProcess` bridge overload (AS-IS Blitz.cs:31) — the pre-existing file-scoped namespace
// was converted to brace form for that (CS8954, same W1 KeyWordEffects fix; purely syntactic).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch2Effect
    {
        private CardEffectCanResolveResult CanResolveBlitz(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            string requiredReason = TriggerReason ?? "OnPlay";
            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.TriggerReason, out string? actualReason)
                || !string.Equals(actualReason, requiredReason, StringComparison.Ordinal))
            {
                return Failure("Blitz trigger reason does not match.", "triggerReason", context, target.InstanceId);
            }

            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.CanAttack, out bool canAttack)
                || !canAttack)
            {
                return Failure("Blitz requires a target that can attack.", "canAttack", context, target.InstanceId);
            }

            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.OpponentMemory, out int opponentMemory)
                || opponentMemory < 1)
            {
                return Failure("Blitz requires opponent memory to be at least 1.", "opponentMemory", context, target.InstanceId);
            }

            bool isAttacking = context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.IsAttacking, out bool attacking)
                && attacking;
            if (isAttacking)
            {
                return Failure("Blitz cannot resolve while an attack is already running.", "isAttacking", context, target.InstanceId);
            }

            return CardEffectCanResolveResult.Success("Blitz can request an immediate attack.", BaseValues(context, target));
        }
    }
}

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Threading.Tasks;

    public static partial class CardEffectCommons
    {
        /// <summary>(P6 cluster2) AS-IS-signature <c>CanActivateBlitz(cardSource, activateClass)</c> overload —
        /// AS-IS itself ignores <c>activateClass</c> in this gate (Blitz.cs:10-17 reads only the card/board
        /// state); delegates to the verified substrate <c>CanActivateBlitz(cardSource)</c> (CardEffectCommons.cs).</summary>
        public static bool CanActivateBlitz(CardSource cardSource, ICardEffect activateClass) => CanActivateBlitz(cardSource);

        #region Effect process of [Blitz] (AS-IS KeyWordEffects/Blitz.cs:31)

        /// <summary>(BRIDGE W3) AS-IS <c>BlitzProcess(cardSource, activateClass, beforeOnAttackCoroutine)</c> —
        /// AS-IS-signature overload delegating to the verified substrate <c>BlitzProcess(cardSource)</c>
        /// (gate: <c>CanActivateBlitz</c>, then the effect-driven attack offer — player + any Digimon, AS-IS
        /// SelectAttackEffect canAttackPlayer/defender = true, via <c>EffectDrivenAttack.RequestChoice</c>,
        /// whose target enumeration applies the engine's attack-legality scans).
        ///
        /// Dropped-parameter handling (bridge-map ⚠️, §11.11 rule 4 — nothing silent):
        /// - <paramref name="activateClass"/>: AS-IS threads it into the <c>CanAttack(activateClass)</c> gate
        ///   and <c>SelectAttackEffect.SetUp(cardEffect:)</c>, so a cause-conditioned "can't attack"
        ///   restriction (cardEffectCondition) would see the true causing effect. The substrate gate/offer has
        ///   no causing-effect input — design item RD-W3-7 (latent until a cause-conditioned attack
        ///   restriction producer is ported; no such producer exists on the mirror today).
        /// - <paramref name="beforeOnAttackCoroutine"/>: AS-IS runs it after target selection, before the
        ///   OnAttack window. The substrate's effect-driven attack is a DEFERRED choice (declared later by the
        ///   choice controller) with no pre-OnAttack hook, so a non-null hook cannot run at the AS-IS point —
        ///   STOP (design item RD-W3-7; sole AS-IS caller passing it: ST13_06) rather than running it at the
        ///   wrong time.</summary>
        public static async Task BlitzProcess(CardSource cardSource, ICardEffect activateClass, Func<Task> beforeOnAttackCoroutine = null)
        {
            if (beforeOnAttackCoroutine != null)
            {
                throw new NotSupportedException(
                    "BlitzProcess(beforeOnAttackCoroutine:) has no substrate pre-OnAttack hook on the deferred " +
                    "effect-attack choice — design item RD-W3-7 (STOP, strong model).");
            }

            _ = activateClass;   // design item RD-W3-7 (see summary — gate/offer cause-threading).
            BlitzProcess(cardSource);
            await Task.CompletedTask;
        }

        #endregion
    }
}
