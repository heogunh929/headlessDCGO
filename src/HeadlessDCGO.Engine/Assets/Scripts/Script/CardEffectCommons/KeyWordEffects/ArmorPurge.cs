// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/ArmorPurge.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Armor Purge). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Armor Purge's resolution branch (1:1 with the original).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
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
}

// (P6 cluster2, purely additive — see file header) the OLD-model `CardEffectCommons` static-helper siblings
// AS-IS keeps in this SAME file (CanActivateArmorPurge/ArmorPurgeProcess, KeyWordEffects/ArmorPurge.cs:10-28)
// — a different namespace/type than the KeywordBaseBatch2Effect resolver above, so a second braced namespace
// block (not the file-scoped form, which permits only one per file) coexists without touching it.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Headless.Runtime;

    public static partial class CardEffectCommons
    {
        /// <summary>AS-IS <c>CanActivateArmorPurge</c> (KeyWordEffects/ArmorPurge.cs:9, verbatim).</summary>
        public static bool CanActivateArmorPurge(CardSource card) =>
            IsExistOnBattleArea(card) && ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count >= 1;

        /// <summary>AS-IS <c>ArmorPurgeProcess</c> (KeyWordEffects/ArmorPurge.cs:24) + <c>ArmorPurgeClass.ArmorPurge</c>
        /// (CardEffectCommons.cs KeyWordEffects/ArmorPurge.cs:40-99): trash this Digimon's own top card, promoting
        /// the digivolution card beneath it. <see cref="DeDigivolveHelpers.ArmorPurgeTopAsync"/> is the verified
        /// substrate primitive AS-IS itself documents as this exact shape's mirror (CardController.cs IDegeneration
        /// remarks: "RemoveFromAllArea + AddTrashCard(if !IsToken) — top-trash + promote-under-source"), including
        /// the <c>[When Top Card is Trashed]</c> window emit AS-IS fires manually here — the helper emits it itself,
        /// so no double-fire. CreateDebuffEffect/RemoveDigivolveRootEffect/add-log = UI (stripped, established
        /// convention throughout AutoProcessing.cs's substrate-translation header).</summary>
        public static async Task ArmorPurgeProcess(CardSource card)
        {
            Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(card);
            if (permanent is null || permanent.TopCard is null || permanent.DigivolutionCards.Count < 1)
            {
                return;
            }

            await DeDigivolveHelpers.ArmorPurgeTopAsync(
                card.Context.CardInstanceRepository,
                card.Context.ZoneMover,
                permanent.TopCard.InstanceId,
                gameEventQueue: card.Context.GameEventQueue).ConfigureAwait(false);
        }
    }
}
