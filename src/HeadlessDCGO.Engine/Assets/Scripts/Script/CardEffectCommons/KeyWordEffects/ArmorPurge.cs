
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

            // (C-Del 3c-1) AS-IS ArmorPurgeClass.ArmorPurge sets willBeRemoveField=false after trashing the top
            // and promoting the under-source (the permanent survives in a lower form) — RESTORED now that the AS-IS
            // PRE cut-in window owns survival (not the retired gate). Set on the keyword-process ONLY (not inside
            // the shared ArmorPurgeTopAsync primitive, which de-digivolve paths also call without touching
            // willBeRemoveField). The sweep's survivor-fix reads this cleared flag to spare the permanent.
            permanent.willBeRemoveField = false;
        }
    }
}
