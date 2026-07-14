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
    using System.Collections.Generic;
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

        /// <summary>(R2-A) AS-IS <c>OverclockProcess</c> (KeyWordEffects/Overclock.cs:25): optionally delete one
        /// trait/token ally, and if one IS deleted this Digimon makes an untapped, player-only attack. R1 now
        /// provides <c>Permanent.CanAttack</c>; the AS-IS <c>SelectAttackEffect</c> (no mirror class) is the
        /// established <c>EffectDrivenAttack</c> substrate (AS-IS <c>SetWithoutTap</c> + canAttackPlayer:()=&gt;true
        /// + defenderCondition:_=&gt;false == the withoutTap/player-only offer), the same offer the live
        /// end-of-turn path (<see cref="Headless.Runtime.OverclockEffect"/>) makes. Structure/order verbatim
        /// with AS-IS; substrate translations only.</summary>
        public static async Task OverclockProcess(string trait, CardSource cardSource, ICardEffect activateClass)
        {
            bool CanSelectPermanentCondition(Permanent permanent) =>
                IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, cardSource)
                && permanent.InstanceId != ICardEffect.ResolvePermanentOfThisCard(cardSource)?.InstanceId
                && (permanent.IsToken || permanent.TopCard.ContainsTraits(trait));

            Permanent selectedPermanent = null;
            bool isDeleted = false;

            if (HasMatchConditionPermanent(cardSource, CanSelectPermanentCondition))
            {
                var selectPermanentEffect = GManager.instance!.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: cardSource.Owner,
                    canTargetCondition: (Headless.Services.HeadlessEntityId id) => CanSelectPermanentCondition(PermanentOf(cardSource, id)),
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    selectPermanentCoroutine: (Permanent permanent) => { selectedPermanent = permanent; return Task.CompletedTask; },
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to delete.",
                    "The opponent is selecting 1 Digimon to delete.");

                await selectPermanentEffect.Activate().ConfigureAwait(false);

                if (selectedPermanent != null)
                {
                    await DeletePeremanentAndProcessAccordingToResult(
                        targetPermanents: new List<Permanent> { selectedPermanent },
                        activateClass: activateClass,
                        successProcess: _ => { isDeleted = true; return Task.CompletedTask; },
                        failureProcess: null).ConfigureAwait(false);

                    if (isDeleted)
                    {
                        Permanent attacker = ICardEffect.ResolvePermanentOfThisCard(cardSource);

                        if (attacker != null)
                        {
                            if (attacker.CanAttack(activateClass, withoutTap: true))
                            {
                                // ADAPTATION: AS-IS SelectAttackEffect (SetWithoutTap; canAttackPlayer:()=>true;
                                // defenderCondition:_=>false == player only) has no mirror class — the established
                                // substrate is EffectDrivenAttack (withoutTap, player-only).
                                Headless.Runtime.EffectDrivenAttack.RequestChoice(
                                    cardSource.Context, attacker.InstanceId,
                                    new Headless.Runtime.EffectAttackOptions(WithoutTap: true, AllowPlayerTarget: true, AllowDigimonTarget: false, TargetUnsuspended: false));
                            }
                        }
                    }
                }
            }
        }
    }
}
