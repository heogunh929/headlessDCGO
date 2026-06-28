// Source: Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Partition.cs
// AS-IS mirror of CardEffectFactory.PartitionEffect — a convenience factory that builds the Partition keyword
// effect (KeywordBaseBatch2). 1:1 map: trigger WhenRemoveField && !IsByBattle && !IsByOwnerEffect
// (CanTriggerPartition); CanActivatePartition (DigivolutionCards.Count >= 2) -> DeletionReplacementTiming
// PartitionOption; PartitionProcess (play one source per colour group as new permanents, payCost:false) ->
// DeletionReplacementGate.TryPartitionPlaySourceAsync driven as a repeated single-select (2 picks). The
// per-card colour groups (PartitionConditions) map to the IDeletionReplacementCandidateConditions seam
// (default = any Digimon source).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Services;

public static class Partition
{
    public static KeywordBaseBatch2Effect Create(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null)
    {
        return KeywordBaseBatch2Factory.Create(
            KeywordBaseBatch2Kind.Partition,
            sourceEntityId,
            targetEntityId);
    }
}
