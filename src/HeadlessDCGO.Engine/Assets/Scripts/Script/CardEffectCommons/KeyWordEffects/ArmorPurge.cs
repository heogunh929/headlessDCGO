// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/ArmorPurge.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Armor Purge). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Armor Purge's resolution branch (1:1 with the original).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed partial class KeywordBaseBatch2Effect
{
    private CardEffectCanResolveResult CanResolveArmorPurge(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.RemovedFromField, out bool removedFromField)
            || !removedFromField)
        {
            return Failure("Armor Purge requires a field removal event.", "removedFromField", context, target.InstanceId);
        }

        if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.RemovedCardId, out HeadlessEntityId removedCardId)
            || removedCardId != target.InstanceId)
        {
            return Failure("Armor Purge requires the keyword target to be removed.", "removedCardId", context, target.InstanceId);
        }

        if (target.SourceIds.Count < 1)
        {
            return Failure("Armor Purge requires at least one digivolution source.", "sourceIds", context, target.InstanceId);
        }

        return CardEffectCanResolveResult.Success("Armor Purge can replace field removal.", BaseValues(context, target));
    }
}
