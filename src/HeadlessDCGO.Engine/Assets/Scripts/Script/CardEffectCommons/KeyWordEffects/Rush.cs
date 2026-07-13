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
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainRush(...)</c> (KeyWordEffects/Rush.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
        public static async Task GainRush(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
        {
            GainRush(targetPermanent, effectDuration, activateClass?.EffectSourceCard);
            await Task.CompletedTask;
        }

        /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainRushPlayerEffect(...)</c> (KeyWordEffects/Rush.cs:46) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
        public static async Task GainRushPlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass)
        {
            GainRushPlayerEffect(permanentCondition, effectDuration, activateClass?.EffectSourceCard);
            await Task.CompletedTask;
        }
    }
}
