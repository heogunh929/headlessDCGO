
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainPierce` (CardEffectCommons.cs:3413). Kept in the flat `...Script.CardEffectCommons` namespace (not
// the nested `.KeyWordEffects` namespace above) so this is a genuine overload of the same partial
// `CardEffectCommons` type every ported card calls — per the established convention (see
// docs/audit/effect_model_rebuild_design_2026-07-13.md §11.3).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System.Collections;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

    public static partial class CardEffectCommons
    {
        /// <summary>(C-Btl grant rehousing) AS-IS <c>CardEffectCommons.GainPierce</c>
        /// (KeyWordEffects/Pierce.cs:54-85), 1:1: grant a target Digimon the [Piercing] trigger for the duration by
        /// building the <see cref="CardEffectFactory.PierceEffect"/> ActivateClass and storing it in the target
        /// permanent's <c>OnDetermineDoSecurityCheck</c> duration bucket via <see cref="AddEffectToPermanent"/>
        /// (W3 live) — so the AS-IS "determine whether to do security check" window (BattleResolver.FinalizeAsync,
        /// AS-IS CardController.cs:4731) picks it up. Replaces the invented <c>GainKeywordToPermanent</c> funnel
        /// (ContinuousKeywordGate.Piercing continuous marker, which Permanent.HasPierce/the window never read).
        /// ADAPTATION: AS-IS's terminal visual <c>CreateBuffEffect</c> has no headless substrate — dropped.</summary>
        public static async Task GainPierce(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
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

            ActivateClass pierce = CardEffectFactory.PierceEffect(
                targetPermanent: targetPermanent, isInheritedEffect: false, condition: CanUseCondition,
                rootCardEffect: activateClass, targetPermanent.TopCard);

            AddEffectToPermanent(
                targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
                cardEffect: pierce, timing: EffectTiming.OnDetermineDoSecurityCheck);

            await Task.CompletedTask;
        }

        /// <summary>(P6 cluster2) AS-IS <c>CanTriggerPierce</c> (KeyWordEffects/Pierce.cs:9, verbatim): this
        /// Digimon's battle deleted the opponent's Digimon and this Digimon alone survived.</summary>
        public static bool CanTriggerPierce(Hashtable hashtable, Permanent piercePermanent)
        {
            if (piercePermanent?.TopCard is null)
            {
                return false;
            }

            return CanTriggerWhenDeleteOpponentDigimonByBattle(
                hashtable,
                winnerCondition: permanent => permanent.cardSources.Contains(piercePermanent.TopCard),
                loserCondition: permanent => IsOpponentPermanent(permanent, piercePermanent.TopCard),
                isOnlyWinnerSurvive: true);
        }

        /// <summary>(P6 cluster2) AS-IS <c>CanActivatePierce</c> (KeyWordEffects/Pierce.cs:19, verbatim): the
        /// current attack is this Digimon's, security still exists, and the security check has not already
        /// been re-enabled.</summary>
        public static bool CanActivatePierce(Permanent permanent)
        {
            if (permanent?.TopCard is null)
            {
                return false;
            }

            var attackProcess = GManager.instance!.attackProcess;
            if (attackProcess.AttackingPermanent?.TopCard is null)
            {
                return false;
            }

            return IsPermanentExistsOnBattleArea(permanent)
                && new Player(permanent.TopCard.Context, permanent.TopCard.Owner).Enemy!.SecurityCards.Count >= 1
                && attackProcess.AttackingPermanent.cardSources.Contains(permanent.TopCard)
                && !attackProcess.DoSecurityCheck;
        }

        /// <summary>(P6 cluster2) AS-IS <c>PierceProcess</c> (KeyWordEffects/Pierce.cs:45, verbatim): re-enable
        /// the security check for this attack.</summary>
        public static Task PierceProcess()
        {
            GManager.instance!.attackProcess.DoSecurityCheck = true;
            return Task.CompletedTask;
        }
    }
}
