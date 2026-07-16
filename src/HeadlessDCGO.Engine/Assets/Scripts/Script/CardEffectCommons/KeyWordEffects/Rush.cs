// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Rush.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Rush). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Rush's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch2Effect
    {
        private CardEffectCanResolveResult CanResolveRush(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            return CardEffectCanResolveResult.Success("Rush target can attack immediately.", BaseValues(context, target));
        }
    }
}

// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overloads; delegate to the verified substrate
// `GainRush` (CardEffectCommons.cs:3409) / `GainRushPlayerEffect` (CardEffectCommons.cs:3202). Kept in the
// flat `...Script.CardEffectCommons` namespace (not the nested `.KeyWordEffects` namespace above) so these
// are genuine overloads of the same partial `CardEffectCommons` type every ported card calls — per the
// established convention (see docs/audit/effect_model_rebuild_design_2026-07-13.md §11.3).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.GainRush</c> (KeyWordEffects/Rush.cs:10),
        /// 1:1: build the target-locked <see cref="CardEffectFactory.RushStaticEffect"/> and store it in the target
        /// permanent's <c>None</c> duration bucket via <see cref="AddEffectToPermanent"/> — read by
        /// <see cref="Permanent.HasRush"/>'s <c>EffectList(None)</c> <c>IRushEffect</c> scan. Replaces the invented
        /// <c>GainKeywordToPermanent</c> funnel. ADAPTATION: the AS-IS terminal <c>CreateBuffEffect</c> VFX is
        /// dropped.</summary>
        public static async Task GainRush(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
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

            RushClass rush = CardEffectFactory.RushStaticEffect(
                permanentCondition: PermanentCondition, isInheritedEffect: false, card: card, condition: CanUseCondition);

            AddEffectToPermanent(
                targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
                cardEffect: rush, timing: EffectTiming.None);

            await Task.CompletedTask;
        }

        /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.GainRushPlayerEffect</c>
        /// (KeyWordEffects/Rush.cs:46), 1:1: a PLAYER-scope Rush grant stored in the owning player's <c>None</c>
        /// bucket via <see cref="AddEffectToPlayer"/>. ADAPTATION: the AS-IS per-permanent VFX loop is dropped.</summary>
        public static async Task GainRushPlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass)
        {
            if (activateClass == null) return;
            if (activateClass.EffectSourceCard == null) return;

            CardSource card = activateClass.EffectSourceCard;

            bool PermanentCondition(Permanent permanent)
            {
                if (IsPermanentExistsOnBattleArea(permanent))
                {
                    if (!permanent.TopCard.CanNotBeAffected(activateClass))
                    {
                        if (permanentCondition == null || permanentCondition(permanent))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition() => true;

            RushClass rush = CardEffectFactory.RushStaticEffect(
                permanentCondition: PermanentCondition, isInheritedEffect: false, card: card, condition: CanUseCondition);

            AddEffectToPlayer(effectDuration: effectDuration, card: card, cardEffect: rush, timing: EffectTiming.None);

            await Task.CompletedTask;
        }
    }
}
