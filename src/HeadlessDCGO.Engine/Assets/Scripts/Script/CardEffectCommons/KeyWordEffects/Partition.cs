// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Partition.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Partition). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Partition's resolution branch (1:1 with the original
// CardEffectCommons.CanActivatePartition / CanTriggerPartition). The LIVE "play two sources free as new
// permanents" path is engine plumbing in DeletionReplacementTiming (PartitionOption, a repeated single-
// select reusing the Decode play-for-free primitive) — this branch is the grant/mirror layer (resolving
// emits GrantPartition -> hasPartition).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed partial class KeywordBaseBatch2Effect
{
    private CardEffectCanResolveResult CanResolvePartition(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.RemovedFromField, out bool removedFromField)
            || !removedFromField)
        {
            return Failure("Partition requires a field removal event.", "removedFromField", context, target.InstanceId);
        }

        if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.RemovedCardId, out HeadlessEntityId removedCardId)
            || removedCardId != target.InstanceId)
        {
            return Failure("Partition requires the keyword target to be removed.", "removedCardId", context, target.InstanceId);
        }

        // AS-IS CanTriggerPartition: not by battle (and not by the owner's own effect).
        if (context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.DeletedByBattle, out bool deletedByBattle)
            && deletedByBattle)
        {
            return Failure("Partition does not trigger on battle removal.", "deletedByBattle", context, target.InstanceId);
        }

        // AS-IS CanActivatePartition: DigivolutionCards.Count >= 2.
        if (target.SourceIds.Count < 2)
        {
            return Failure("Partition requires at least two digivolution sources.", "sourceIds", context, target.InstanceId);
        }

        return CardEffectCanResolveResult.Success("Partition can play two digivolution sources for free.", BaseValues(context, target));
    }
}
