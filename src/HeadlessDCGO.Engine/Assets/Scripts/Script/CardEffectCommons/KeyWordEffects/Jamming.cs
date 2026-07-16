// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Jamming.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch1Effect (Jamming). Shared scaffolding lives in
// KeywordBaseBatch1.cs; this file holds only Jamming's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.Services;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch1Effect
    {
        private CardEffectCanResolveResult CanResolveJamming(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            if (!context.EffectContext.TryGetValue(KeywordBaseBatch1ContextKeys.AttackingCardId, out HeadlessEntityId attackingCardId)
                || attackingCardId != target.InstanceId)
            {
                return Failure("Jamming requires the keyword target to be the attacking card.", "attackingCardId", context, target.InstanceId);
            }

            if (!context.EffectContext.TryGetValue(KeywordBaseBatch1ContextKeys.DefendingCardIsSecurity, out bool isSecurity)
                || !isSecurity)
            {
                return Failure("Jamming only prevents battle deletion against a security Digimon.", "defendingCardIsSecurity", context, target.InstanceId);
            }

            return CardEffectCanResolveResult.Success("Jamming prevents battle deletion against security.", BaseValues(context, target));
        }
    }
}

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
