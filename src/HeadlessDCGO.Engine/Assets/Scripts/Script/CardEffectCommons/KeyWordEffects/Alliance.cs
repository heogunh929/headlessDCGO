// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Alliance.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Alliance). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Alliance's resolution branch (1:1 with the original
// CardEffectCommons.CanActivateAlliance / CanTriggerOnPermanentAttack). The LIVE suspend-an-ally +DP/+1SA
// path is engine plumbing in AllianceAttackBoost (consumed by AttackPipeline before block timing) — this
// branch is the grant/mirror layer (resolving emits GrantAlliance -> hasAlliance).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch2Effect
    {
        private CardEffectCanResolveResult CanResolveAlliance(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            // AS-IS CanTriggerOnPermanentAttack(this permanent is the attacker): Alliance only triggers when
            // THIS Digimon attacks.
            if (!context.EffectContext.TryGetValue(KeywordBaseBatch2ContextKeys.IsAttacking, out bool isAttacking)
                || !isAttacking)
            {
                return Failure("Alliance requires this Digimon to be attacking.", "isAttacking", context, target.InstanceId);
            }

            return CardEffectCanResolveResult.Success("Alliance can suspend an ally to boost the attacker.", BaseValues(context, target));
        }
    }
}

// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainAlliance` (CardEffectCommons.cs:3433). Kept in the flat `...Script.CardEffectCommons` namespace (not
// the nested `.KeyWordEffects` namespace above) so this is a genuine overload of the same partial
// `CardEffectCommons` type every ported card calls — per the established convention (see
// docs/audit/effect_model_rebuild_design_2026-07-13.md §11.3).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainAlliance(...)</c> (KeyWordEffects/Alliance.cs:137) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
        public static async Task GainAlliance(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
        {
            GainAlliance(targetPermanent, effectDuration, activateClass?.EffectSourceCard);
            await Task.CompletedTask;
        }

        /// <summary>(P6 cluster2) AS-IS <c>CanActivateAlliance</c> (KeyWordEffects/Alliance.cs:10, verbatim).</summary>
        public static bool CanActivateAlliance(Hashtable hashtable, CardSource card)
        {
            bool CanSelectPermanentCondition(Permanent permanent) =>
                IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && permanent.InstanceId != ICardEffect.ResolvePermanentOfThisCard(card)?.InstanceId
                && CanActivateSuspendCostEffect(permanent.TopCard);

            return IsExistOnBattleArea(card) && HasMatchConditionPermanent(card, CanSelectPermanentCondition);
        }

        /// <summary>(P6 cluster2) AS-IS <c>AllianceProcess</c> (KeyWordEffects/Alliance.cs:41): owner suspends 1
        /// other Digimon; this Digimon (the attacker) gains that Digimon's DP and +1 Security Attack for the
        /// attack.</summary>
        public static async Task AllianceProcess(Hashtable hashtable, ICardEffect activateClass, Permanent targetPermanent, CardSource card)
        {
            bool CanSelectPermanentCondition(Permanent permanent) =>
                IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && permanent.InstanceId != targetPermanent.InstanceId
                && CanActivateSuspendCostEffect(permanent.TopCard);

            if (!HasMatchConditionPermanent(card, CanSelectPermanentCondition))
            {
                return;
            }

            int maxCount = Math.Min(1, MatchConditionPermanentCount(card, CanSelectPermanentCondition));
            var selectPermanentEffect = GManager.instance!.GetComponent<SelectPermanentEffect>();
            Permanent? selected = null;
            selectPermanentEffect.SetUp(
                selectPlayer: card.Owner,
                canTargetCondition: (Headless.Services.HeadlessEntityId id) => CanSelectPermanentCondition(PermanentOf(card, id)),
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                maxCount: maxCount,
                canNoSelect: true,
                canEndNotMax: false,
                selectPermanentCoroutine: (Permanent p) => { selected = p; return Task.CompletedTask; },
                afterSelectPermanentCoroutine: null,
                mode: SelectPermanentEffect.Mode.Custom,
                cardEffect: activateClass);
            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to suspend.", "The opponent is selecting 1 Digimon to suspend.");
            await selectPermanentEffect.Activate().ConfigureAwait(false);

            if (selected?.TopCard is null || !CanActivateSuspendCostEffect(selected.TopCard))
            {
                return;
            }

            Permanent tapPermanent = selected;
            await new SuspendPermanentsClass(new List<Permanent> { tapPermanent }, activateClass, isBlock: false)
                .Tap().ConfigureAwait(false);

            if (tapPermanent.TopCard is null || !tapPermanent.IsSuspended || !IsPermanentExistsOnOwnerBattleAreaDigimon(targetPermanent, card))
            {
                return;
            }

            int plusDp = tapPermanent.DP;
            ChangeDigimonDP(targetPermanent, plusDp, EffectDuration.UntilEndAttack, card);
            ChangeDigimonSAttack(targetPermanent, 1, EffectDuration.UntilEndAttack, card);
        }
    }
}
