// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Partition.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Partition). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Partition's resolution branch (1:1 with the original
// CardEffectCommons.CanActivatePartition / CanTriggerPartition). The LIVE "play two sources free as new
// permanents" path is engine plumbing in DeletionReplacementTiming (PartitionOption, a repeated single-
// select reusing the Decode play-for-free primitive) — this branch is the grant/mirror layer (resolving
// emits GrantPartition -> hasPartition).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
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
}

// (P6 cluster2, purely additive — see file header) old-model CardEffectCommons Hashtable-based siblings
// (KeyWordEffects/Partition.cs) — a different namespace/type than the KeywordBaseBatch2Effect resolver above.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects; // PartitionCondition

    public static partial class CardEffectCommons
    {
        /// <summary>(P6 cluster2) AS-IS <c>CanTriggerPartition</c> (KeyWordEffects/Partition.cs:10, verbatim).</summary>
        public static bool CanTriggerPartition(Hashtable hashtable, CardSource card) =>
            CanTriggerWhenPermanentRemoveField(hashtable, permanent => permanent.cardSources.Contains(card))
            && !IsByBattle(hashtable)
            && !IsByEffect(hashtable, cardEffect => IsOwnerEffect(cardEffect.EffectSourceCard, card));

        /// <summary>(P6 cluster2) AS-IS <c>CanActivatePartition</c> (KeyWordEffects/Partition.cs:28, verbatim).</summary>
        public static bool CanActivatePartition(Permanent permanent) =>
            IsPermanentExistsOnBattleArea(permanent) && permanent.DigivolutionCards.Count >= 2;

        /// <summary>AS-IS <c>PartitionProcess</c> (KeyWordEffects/Partition.cs:43): play the two selected
        /// digivolution-card groups as new permanents for free. STOP — <c>PartitionClass</c> (the AS-IS
        /// selection/play orchestrator this delegates to) has no mirror (the LIVE Partition path is already
        /// fully implemented independently via DeletionReplacementTiming's PartitionOption, so this old-model
        /// ActivateClass path is dead-relative to actual play); design item RD-P6C2-6.</summary>
        public static Task PartitionProcess(ICardEffect activateClass, Permanent permanent, List<CardSource> firstSources, List<CardSource> secondSources, List<PartitionCondition> partitionConditions)
        {
            throw new NotSupportedException(
                "PartitionProcess: AS-IS PartitionClass has no mirror selection/play orchestrator — design item " +
                "RD-P6C2-6, docs/audit/rebuild_p6_cluster2_notes.md.");
        }
    }
}
