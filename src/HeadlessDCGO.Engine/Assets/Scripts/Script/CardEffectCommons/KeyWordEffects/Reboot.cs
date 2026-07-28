
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainReboot` (CardEffectCommons.cs:3429). Kept in the flat `...Script.CardEffectCommons` namespace (not
// the nested `.KeyWordEffects` namespace above) so this is a genuine overload of the same partial
// `CardEffectCommons` type every ported card calls — per the established convention (see
// docs/audit/effect_model_rebuild_design_2026-07-13.md §11.3).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

    public static partial class CardEffectCommons
    {
        /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.GainReboot</c>
        /// (KeyWordEffects/Reboot.cs:10), 1:1: build the target-locked <see cref="CardEffectFactory.RebootStaticEffect"/>
        /// and store it in the target permanent's <c>None</c> duration bucket via <see cref="AddEffectToPermanent"/> —
        /// read by <see cref="Permanent.HasReboot"/>'s <c>EffectList(None)</c> <c>IRebootEffect</c> scan. Replaces the
        /// invented <c>GainKeywordToPermanent</c> funnel. ADAPTATION: the AS-IS terminal <c>CreateBuffEffect</c> VFX is
        /// dropped.</summary>
        public static async Task GainReboot(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
        {
            if (targetPermanent == null) return;
            if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
            if (activateClass == null) return;
            if (activateClass.EffectSourceCard == null) return;

            CardSource card = activateClass.EffectSourceCard;

            bool PermanentCondition(Permanent permanent) => permanent == targetPermanent;

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

            RebootClass reboot = CardEffectFactory.RebootStaticEffect(
                permanentCondition: PermanentCondition, isInheritedEffect: false, card: card, condition: CanUseCondition);

            AddEffectToPermanent(
                targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
                cardEffect: reboot, timing: EffectTiming.None);

            await Task.CompletedTask;
        }
    }
}
