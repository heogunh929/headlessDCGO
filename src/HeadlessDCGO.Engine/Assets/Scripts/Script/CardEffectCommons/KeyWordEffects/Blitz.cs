// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Blitz.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Blitz). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Blitz's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

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
