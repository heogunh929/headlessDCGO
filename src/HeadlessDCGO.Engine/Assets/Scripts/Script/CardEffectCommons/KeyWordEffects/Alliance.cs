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
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(C-Atk) AS-IS <c>CardEffectCommons.GainAlliance</c> (KeyWordEffects/Alliance.cs:136) 1:1:
        /// register a <see cref="CardEffectFactory.AllianceEffect"/> <c>ActivateClass</c> on the target
        /// permanent's <c>OnAllyAttack</c> duration bucket via <see cref="AddEffectToPermanent"/> (W3 live).
        /// The granted Alliance then fires through the SAME OnAllyAttack window that collects a printed
        /// Alliance (GetSkillInfos → MultipleSkills), NOT the retired AllianceAttackBoost gate. ADAPTATION
        /// (substrate only): the AS-IS <c>CreateBuffEffect</c> VFX loop (Effects.cs:1433) is stripped (pure
        /// UI, no state); the coroutine becomes a completed <see cref="Task"/>.</summary>
        public static async Task GainAlliance(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
        {
            if (targetPermanent == null) return;
            if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
            if (activateClass == null) return;
            if (activateClass.EffectSourceCard == null) return;

            CardSource card = activateClass.EffectSourceCard;

            bool CanUseCondition()
            {
                if (IsPermanentExistsOnBattleArea(targetPermanent))
                {
                    if (!targetPermanent.TopCard.CanNotBeAffected(activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            // AS-IS names the local `retaliation` (a copy-paste from GainRetaliation); kept 1:1.
            ActivateClass retaliation = CardEffectFactory.AllianceEffect(
                targetPermanent: targetPermanent,
                isInheritedEffect: false,
                condition: CanUseCondition,
                rootCardEffect: activateClass,
                card: targetPermanent.TopCard);

            AddEffectToPermanent(
                targetPermanent: targetPermanent,
                effectDuration: effectDuration,
                card: card,
                cardEffect: retaliation,
                timing: EffectTiming.OnAllyAttack);

            // AS-IS :172-175 CreateBuffEffect (pure VFX/SE, Effects.cs:1433) — stripped.
            await Task.CompletedTask;
        }

        /// <summary>(J-4) 1:1 mirror of AS-IS <c>GainAlliancePlayerEffect</c> (KeyWordEffects/Alliance.cs:180-219):
        /// the OWNING PLAYER gains a timed "its Digimon have [Alliance]" grant. Builds the AS-IS
        /// <see cref="CardEffectFactory.AllianceStaticEffect"/> ActivateClass (EffectName "Alliance", the folded
        /// PermanentCondition = on-battle-area && !TopCard.CanNotBeAffected(cause) && caller predicate; CanUse =
        /// true) and stores it in the owning player's <c>OnAllyAttack</c> duration bucket via
        /// <see cref="AddEffectToPlayer"/> — DIFFERENT timing from the restriction grants (Alliance is a
        /// firing-window keyword). Read LIVE by <see cref="Permanent.HasAlliance"/> / NewModelContinuousScan.HasAlliance
        /// (scan player.EffectList(OnAllyAttack) for EffectName=="Alliance" && CanTrigger), surfaced by
        /// ContinuousKeywordGate.HasKeyword — the retired GainToPlayerScope keyword funnel is gone (RD-RC-03 resolved).
        /// AS-IS coroutine only drove the per-permanent CreateBuffEffect UI visual (dropped). The public
        /// AS-IS-signature `Task` overload threads the LIVE `activateClass` as the CanNotBeAffected cause; the
        /// CardSource-only substrate overload (CardEffectCommons.cs) collapses the cause to BareCauseEffect.For(sourceCard).</summary>
        public static async Task GainAlliancePlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass)
        {
            // AS-IS :182-183 guards (activateClass / EffectSourceCard null).
            if (activateClass is null || activateClass.EffectSourceCard is null)
            {
                await Task.CompletedTask;
                return;
            }

            GainAlliancePlayerEffectImpl(permanentCondition, effectDuration, card: activateClass.EffectSourceCard, cause: activateClass);
            await Task.CompletedTask;
        }

        /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
        /// overload (CardEffectCommons.cs). Mirrors AS-IS GainAlliancePlayerEffect :180-219.</summary>
        internal static bool GainAlliancePlayerEffectImpl(
            Func<Permanent, bool>? permanentCondition,
            EffectDuration effectDuration,
            CardSource? card,
            ICardEffect? cause)
        {
            if (card is null || cause is null) return false;   // AS-IS :182-183

            bool PermanentCondition(Permanent permanent)   // AS-IS :187-201
            {
                if (IsPermanentExistsOnBattleArea(permanent))
                {
                    if (!permanent.TopCard.CanNotBeAffected(cause))
                    {
                        if (permanentCondition is null || permanentCondition(permanent))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition() => true;   // AS-IS :203-206

            ICardEffect alliance = CardEffectFactory.AllianceStaticEffect(  // AS-IS :208
                permanentCondition: PermanentCondition,
                isInheritedEffect: false,
                card: card,
                condition: CanUseCondition);

            AddEffectToPlayer(  // AS-IS :210
                effectDuration: effectDuration,
                card: card,
                cardEffect: alliance,
                timing: EffectTiming.OnAllyAttack);

            // AS-IS :212-218 iterated PermanentsForTurnPlayer running CreateBuffEffect (UI visual) — dropped headless.
            return true;
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
                canTargetCondition: CanSelectPermanentCondition,
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

            // (C-Atk fidelity) AS-IS Alliance.cs:95 guards the SUSPEND on `!selected.TopCard.CanNotBeAffected(activateClass)`
            // — an ally immune to this effect is NOT suspended (and grants no buff). Restored here (was dropped in the
            // P6 cluster2 early-return rewrite).
            if (selected?.TopCard is null
                || selected.TopCard.CanNotBeAffected(activateClass)
                || !CanActivateSuspendCostEffect(selected.TopCard))
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
            ChangeDigimonDP(targetPermanent, plusDp, EffectDuration.UntilEndAttack, card, activateClass);
            ChangeDigimonSAttack(targetPermanent, 1, EffectDuration.UntilEndAttack, card, activateClass: activateClass);
        }
    }
}
