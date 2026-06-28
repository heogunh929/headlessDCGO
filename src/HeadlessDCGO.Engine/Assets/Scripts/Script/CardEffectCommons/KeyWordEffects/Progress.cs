// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Progress.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Progress). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Progress's resolution branch (1:1 with the original
// CardEffectCommons.CanActivateProgress). The LIVE "while attacking, not affected by opponent effects"
// path is engine plumbing: ProgressImmunity registers a continuous opponent-only ContinuousImmunityGate
// binding (UntilEndAttack) on the attacker, consumed by the mutation sink. Progress is a PASSIVE static
// effect (no agent choice). This branch is the grant/mirror layer (resolving emits GrantProgress -> hasProgress).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

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
