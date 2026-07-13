// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Overclock.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Overclock). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Overclock's resolution branch (1:1 with the original
// CardEffectCommons.CanActivateOverclock). The LIVE end-of-turn "delete a trait/token ally -> untapped
// player attack" path is engine plumbing in OverclockEffect (S3 trait + DeletionReplacementGate.SacrificeAsync
// + the S1 hub EffectDrivenAttack) — this branch is the grant/mirror layer (resolving emits GrantOverclock
// -> hasOverclock).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch2Effect
    {
        private CardEffectCanResolveResult CanResolveOverclock(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            // AS-IS CanActivateOverclock: this Digimon is on the battle area (the dispatch already enforces
            // battle area). The full "a trait/token ally != self exists" eligibility is enforced live by
            // OverclockEffect.GetTraitAllyCandidates when the end-of-turn window opens.
            return CardEffectCanResolveResult.Success("Overclock can delete a trait ally for an untapped attack.", BaseValues(context, target));
        }
    }
}

// (P6 cluster2, purely additive — see file header) old-model CardEffectCommons Hashtable-based siblings
// (KeyWordEffects/Overclock.cs) — a different namespace/type than the KeywordBaseBatch2Effect resolver above.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Threading.Tasks;

    public static partial class CardEffectCommons
    {
        /// <summary>(P6 cluster2) AS-IS <c>CanActivateOverclock</c> (KeyWordEffects/Overclock.cs:15, verbatim).</summary>
        public static bool CanActivateOverclock(string trait, CardSource cardSource, ICardEffect activateClass)
        {
            bool CanSelectPermanentCondition(Permanent permanent) =>
                IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, cardSource)
                && permanent.InstanceId != ICardEffect.ResolvePermanentOfThisCard(cardSource)?.InstanceId
                && (permanent.IsToken || permanent.TopCard.ContainsTraits(trait));

            return IsExistOnBattleArea(cardSource) && HasMatchConditionPermanent(cardSource, CanSelectPermanentCondition);
        }

        /// <summary>AS-IS <c>OverclockProcess</c> (KeyWordEffects/Overclock.cs:25): delete a trait/token ally,
        /// then attack a player without suspending. STOP — the follow-up attack offer needs
        /// <c>Permanent.CanAttack</c> + <c>SelectAttackEffect</c> (both unported, out of this cluster's scope —
        /// the LIVE end-of-turn Overclock path is already fully implemented independently by
        /// <see cref="Headless.Runtime.OverclockEffect"/> + <see cref="Headless.Runtime.EndOfTurnEffectAttack"/>,
        /// so this old-model ActivateClass path is dead-relative to actual play); design item RD-P6C2-5.</summary>
        public static Task OverclockProcess(string trait, CardSource cardSource, ICardEffect activateClass)
        {
            throw new NotSupportedException(
                "OverclockProcess: AS-IS Permanent.CanAttack/SelectAttackEffect have no mirror — design item " +
                "RD-P6C2-5, docs/audit/rebuild_p6_cluster2_notes.md.");
        }
    }
}
