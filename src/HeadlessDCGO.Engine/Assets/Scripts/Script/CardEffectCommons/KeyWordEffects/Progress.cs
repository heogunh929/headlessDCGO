// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Progress.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Progress). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Progress's resolution branch (1:1 with the original
// CardEffectCommons.CanActivateProgress). The LIVE "while attacking, not affected by opponent effects"
// path is engine plumbing: ProgressImmunity registers a continuous opponent-only ContinuousImmunityGate
// binding (UntilEndAttack) on the attacker, consumed by the mutation sink. Progress is a PASSIVE static
// effect (no agent choice). This branch is the grant/mirror layer (resolving emits GrantProgress -> hasProgress).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch2Effect
    {
        private CardEffectCanResolveResult CanResolveProgress(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            // AS-IS CanActivateProgress: this Digimon is on the battle area (dispatch enforces) and is the
            // attacker. The immunity itself is applied live by ProgressImmunity at attack declaration.
            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.IsAttacking, out bool isAttacking)
                || !isAttacking)
            {
                return Failure("Progress requires this Digimon to be attacking.", "isAttacking", context, target.InstanceId);
            }

            return CardEffectCanResolveResult.Success("Progress grants opponent-effect immunity while attacking.", BaseValues(context, target));
        }
    }
}

// (P6 cluster2, purely additive — see file header) old-model CardEffectCommons sibling (KeyWordEffects/Progress.cs)
// — a different namespace/type than the KeywordBaseBatch2Effect resolver above.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Threading.Tasks;

    public static partial class CardEffectCommons
    {
        /// <summary>(P6 cluster2) AS-IS <c>CanActivateProgress</c> (KeyWordEffects/Progress.cs:44, verbatim).</summary>
        public static bool CanActivateProgress(CardSource cardSource) =>
            IsExistOnBattleAreaDigimon(cardSource)
            && GManager.instance!.attackProcess.IsAttacking
            && GManager.instance!.attackProcess.AttackingPermanent?.InstanceId == ICardEffect.ResolvePermanentOfThisCard(cardSource)?.InstanceId;

        /// <summary>(R2-A) AS-IS <c>ProgressProcess</c> (KeyWordEffects/Progress.cs:62): while this Digimon
        /// attacks, add an UntilEndAttack "not affected by the opponent's Digimon's effects" CanNotAffectedClass
        /// to the attacker. RD-R2-01 (the missing <c>Permanent.UntilEndAttackEffects</c> W3 bucket) is now
        /// RESOLVED — the bucket is live (ExecuteProcess uses it) — so this window-append form is portable, but
        /// Progress was outside the window-firing rehousing scope and remains unported here. The live Progress
        /// immunity is applied independently by <see cref="Headless.Runtime.ProgressImmunity"/> at attack
        /// declaration, so behaviour is unaffected.</summary>
        public static Task ProgressProcess(CardSource cardSource, ICardEffect activateClass, Func<Task> beforeOnAttackCoroutine = null)
        {
            throw new NotSupportedException(
                "ProgressProcess: AS-IS appends a CanNotAffectedClass to Permanent.UntilEndAttackEffects, which " +
                "has no mirror Permanent member — design item RD-R2-01. The live Progress immunity is applied by " +
                "Headless.Runtime.ProgressImmunity.");
        }
    }
}
