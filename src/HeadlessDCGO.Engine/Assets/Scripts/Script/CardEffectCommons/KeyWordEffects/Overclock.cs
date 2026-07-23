
// (P6 cluster2, purely additive — see file header) old-model CardEffectCommons Hashtable-based siblings
// (KeyWordEffects/Overclock.cs) — a different namespace/type than the KeywordBaseBatch2Effect resolver above.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(C-EoT-2) AS-IS <c>CardEffectCommons.GainOverclock</c> (KeyWordEffects/Overclock.cs:106) 1:1:
        /// register a trait-parameterised <see cref="CardEffectFactory.OverclockEffect"/> <c>ActivateClass</c> on the
        /// target permanent's <c>OnEndTurn</c> duration bucket via <see cref="AddEffectToPermanent"/> (W3 live). The
        /// granted Overclock then fires through the SAME OnEndTurn window that collects a printed Overclock
        /// (GetSkillInfos → MultipleSkills → OverclockProcess), NOT the retired EndOfTurnEffectAttack gate.
        /// ADAPTATION (substrate only): the AS-IS <c>Effects.CreateBuffEffect</c> VFX/SE loop is pure UI — stripped
        /// (== the wave2 GainRaid/GainAlliance shape).</summary>
        public static async Task GainOverclock(string trait, Permanent targetPermanent, EffectDuration effectDuration,
            ICardEffect activateClass)
        {
            if (targetPermanent == null) return;
            if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
            if (activateClass == null) return;
            if (activateClass.EffectSourceCard == null) return;

            CardSource card = activateClass.EffectSourceCard;

            bool CanUseCondition()
            {
                return IsPermanentExistsOnBattleArea(targetPermanent) &&
                       !targetPermanent.TopCard.CanNotBeAffected(activateClass);
            }

            ActivateClass overclock = CardEffectFactory.OverclockEffect(
                trait: trait,
                targetPermanent: targetPermanent,
                isInheritedEffect: false,
                condition: CanUseCondition,
                rootCardEffect: activateClass,
                targetPermanent.TopCard);

            AddEffectToPermanent(
                targetPermanent: targetPermanent,
                effectDuration: effectDuration,
                card: card,
                cardEffect: overclock,
                timing: EffectTiming.OnEndTurn);

            // AS-IS :137-141 CreateBuffEffect (pure VFX/SE) — stripped.
            await Task.CompletedTask;
        }

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
                    canTargetCondition: CanSelectPermanentCondition,
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
                                // (R5-B) AS-IS Overclock.cs:79-91 — the in-place SelectAttackEffect mirror
                                // (SetWithoutTap; canAttackPlayer:()=>true; defenderCondition:_=>false == player
                                // only; SetCanNotSelectNotAttack == mandatory), replacing the R2-A
                                // EffectDrivenAttack.RequestChoice ADAPTATION.
                                var selectAttackEffect = GManager.instance!.GetComponent<SelectAttackEffect>();

                                selectAttackEffect.SetUp(
                                    attacker: attacker,
                                    canAttackPlayerCondition: () => true,
                                    defenderCondition: _ => false,
                                    cardEffect: activateClass);

                                selectAttackEffect.SetWithoutTap();
                                selectAttackEffect.SetCanNotSelectNotAttack();

                                await selectAttackEffect.Activate().ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }
    }
}
