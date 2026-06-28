// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Alliance.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Alliance). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Alliance's resolution branch (1:1 with the original
// CardEffectCommons.CanActivateAlliance / CanTriggerOnPermanentAttack). The LIVE suspend-an-ally +DP/+1SA
// path is engine plumbing in AllianceAttackBoost (consumed by AttackPipeline before block timing) — this
// branch is the grant/mirror layer (resolving emits GrantAlliance -> hasAlliance).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.State;

public sealed partial class KeywordBaseBatch2Effect
{
    private CardEffectCanResolveResult CanResolveAlliance(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        // AS-IS CanTriggerOnPermanentAttack(this permanent is the attacker): Alliance only triggers when
        // THIS Digimon attacks.
        if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.IsAttacking, out bool isAttacking)
            || !isAttacking)
        {
            return Failure("Alliance requires this Digimon to be attacking.", "isAttacking", context, target.InstanceId);
        }

        return CardEffectCanResolveResult.Success("Alliance can suspend an ally to boost the attacker.", BaseValues(context, target));
    }
}
