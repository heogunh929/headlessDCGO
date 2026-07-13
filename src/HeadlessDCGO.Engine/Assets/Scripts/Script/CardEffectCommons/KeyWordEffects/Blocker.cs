// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Blocker.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch1Effect (Blocker). Shared scaffolding lives in
// KeywordBaseBatch1.cs; this file holds only Blocker's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch1Effect
    {
        private CardEffectCanResolveResult CanResolveBlocker(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            return CardEffectCanResolveResult.Success("Blocker target can block.", BaseValues(context, target));
        }
    }
}

// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overloads; delegate to the verified substrate
// `GainBlocker` (CardEffectCommons.cs:3405) / `GainBlockerPlayerEffect` (CardEffectCommons.cs:3198). Kept in
// the flat `...Script.CardEffectCommons` namespace (not the nested `.KeyWordEffects` namespace above) so
// these are genuine overloads of the same partial `CardEffectCommons` type every ported card calls —
// per the established convention (see docs/audit/effect_model_rebuild_design_2026-07-13.md §11.3).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System;
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainBlocker(...)</c> (KeyWordEffects/Blocker.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
        public static async Task GainBlocker(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
        {
            GainBlocker(targetPermanent, effectDuration, activateClass?.EffectSourceCard);
            await Task.CompletedTask;
        }

        /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainBlockerPlayerEffect(...)</c> (KeyWordEffects/Blocker.cs:46) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
        public static async Task GainBlockerPlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass)
        {
            GainBlockerPlayerEffect(permanentCondition, effectDuration, activateClass?.EffectSourceCard);
            await Task.CompletedTask;
        }
    }
}
