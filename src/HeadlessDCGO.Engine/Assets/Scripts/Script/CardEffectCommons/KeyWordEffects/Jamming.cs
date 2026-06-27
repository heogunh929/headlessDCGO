// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Jamming.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch1Effect (Jamming). Shared scaffolding lives in
// KeywordBaseBatch1.cs; this file holds only Jamming's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed partial class KeywordBaseBatch1Effect
{
    private CardEffectCanResolveResult CanResolveJamming(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        if (!context.EffectContext.TryGetValue(KeywordBaseBatch1ContextKeys.AttackingCardId, out HeadlessEntityId attackingCardId)
            || attackingCardId != target.InstanceId)
        {
            return Failure("Jamming requires the keyword target to be the attacking card.", "attackingCardId", context, target.InstanceId);
        }

        if (!context.EffectContext.TryGetValue(KeywordBaseBatch1ContextKeys.DefendingCardIsSecurity, out bool isSecurity)
            || !isSecurity)
        {
            return Failure("Jamming only prevents battle deletion against a security Digimon.", "defendingCardIsSecurity", context, target.InstanceId);
        }

        return CardEffectCanResolveResult.Success("Jamming prevents battle deletion against security.", BaseValues(context, target));
    }
}
