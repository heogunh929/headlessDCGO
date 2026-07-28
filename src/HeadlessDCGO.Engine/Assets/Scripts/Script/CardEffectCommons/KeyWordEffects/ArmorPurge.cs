
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
        /// (KeyWordEffects/ArmorPurge.cs:40-99): trash this Digimon's own top card, promoting the digivolution card
        /// beneath it. <see cref="Permanent.ArmorPurgeTopAsync"/> is the re-homed physical half AS-IS itself
        /// documents as this exact shape (CardController.cs IDegeneration remarks: "RemoveFromAllArea +
        /// AddTrashCard(if !IsToken) — top-trash + promote-under-source"); the <c>[When Top Card is Trashed]</c>
        /// window is opened HERE, at the AS-IS position (ArmorPurge.cs:69-79, payload
        /// <c>{Permanent, CardSources=[topCard]}</c>) — the primitive itself is emit-free so the batching callers
        /// (IDegeneration / IMassDegeneration / ITrashStack) keep their single per-batch emit.
        /// CreateDebuffEffect/RemoveDigivolveRootEffect/add-log = UI (stripped, established convention throughout
        /// AutoProcessing.cs's substrate-translation header).</summary>
        public static async Task ArmorPurgeProcess(CardSource card)
        {
            Permanent permanent = ICardEffect.ResolvePermanentOfThisCard(card);
            if (permanent is null || permanent.TopCard is null || permanent.DigivolutionCards.Count < 1)
            {
                return;
            }

            // AS-IS :47 `CardSource topCard = _permanent.TopCard;` — captured BEFORE the purge (it is the trashed
            // card carried in the WhenTopCardTrashed payload).
            CardSource topCard = permanent.TopCard;

            await Permanent.ArmorPurgeTopAsync(
                card.Context.CardInstanceRepository,
                card.Context.ZoneMover,
                topCard.InstanceId).ConfigureAwait(false);

            // (C-Del 3c-1) AS-IS ArmorPurgeClass.ArmorPurge sets willBeRemoveField=false after trashing the top
            // and promoting the under-source (the permanent survives in a lower form) — RESTORED now that the AS-IS
            // PRE cut-in window owns survival (not the retired gate). Set on the keyword-process ONLY (not inside
            // the shared ArmorPurgeTopAsync primitive, which de-digivolve paths also call without touching
            // willBeRemoveField). The sweep's survivor-fix reads this cleared flag to spare the permanent.
            permanent.willBeRemoveField = false;

            // AS-IS :69-79 `StackSkillInfos({"Permanent", _permanent}, {"CardSources", {topCard}},
            // EffectTiming.WhenTopCardTrashed)` — the SOLE opener on this path (IDegeneration precedent).
            await HeadlessDCGO.Engine.Assets.Scripts.Script.AutoProcessing.For(card.Context).StackSkillInfos(
                new System.Collections.Hashtable
                {
                    { "Permanent", permanent },
                    { "CardSources", new List<CardSource> { topCard } },
                },
                EffectTiming.WhenTopCardTrashed).ConfigureAwait(false);
        }
    }
}
