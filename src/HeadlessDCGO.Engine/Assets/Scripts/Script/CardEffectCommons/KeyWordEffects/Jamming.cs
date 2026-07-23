
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainJamming` (CardEffectCommons.cs:3425). Kept in the flat `...Script.CardEffectCommons` namespace (not
// the nested `.KeyWordEffects` namespace above) so this is a genuine overload of the same partial
// `CardEffectCommons` type every ported card calls — per the established convention (see
// docs/audit/effect_model_rebuild_design_2026-07-13.md §11.3).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.GainJamming</c>
        /// (KeyWordEffects/Jamming.cs:10), 1:1: build the target-locked <see cref="CardEffectFactory.JammingStaticEffect"/>
        /// (a named "Jamming" <c>CanNotBeDestroyedByBattleClass</c>) and store it in the target permanent's <c>None</c>
        /// duration bucket via <see cref="AddEffectToPermanent"/> — read by <see cref="Permanent.HasJamming"/>'s
        /// <c>EffectList(None)</c> <c>ICanNotBeDestroyedByBattleEffect</c> scan. Replaces the invented
        /// <c>GainKeywordToPermanent</c> funnel. ADAPTATION: the AS-IS terminal <c>CreateBuffEffect</c> VFX is
        /// dropped.</summary>
        public static async Task GainJamming(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
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

            CanNotBeDestroyedByBattleClass jamming = CardEffectFactory.JammingStaticEffect(
                permanentCondition: PermanentCondition, isInheritedEffect: false, card: card, condition: CanUseCondition);

            AddEffectToPermanent(
                targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
                cardEffect: jamming, timing: EffectTiming.None);

            await Task.CompletedTask;
        }
    }
}
