// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Reboot.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch1Effect (Reboot). Shared scaffolding lives in
// KeywordBaseBatch1.cs; this file holds only Reboot's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects
{
    using HeadlessDCGO.Engine.Headless.Effects;
    using HeadlessDCGO.Engine.Headless.State;

    public sealed partial class KeywordBaseBatch1Effect
    {
        private CardEffectCanResolveResult CanResolveReboot(
            CardEffectResolveContext context,
            CardInstanceState target)
        {
            return CardEffectCanResolveResult.Success("Reboot target can unsuspend on opponent unsuspend.", BaseValues(context, target));
        }
    }
}

// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainReboot` (CardEffectCommons.cs:3429). Kept in the flat `...Script.CardEffectCommons` namespace (not
// the nested `.KeyWordEffects` namespace above) so this is a genuine overload of the same partial
// `CardEffectCommons` type every ported card calls — per the established convention (see
// docs/audit/effect_model_rebuild_design_2026-07-13.md §11.3).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
{
    using System.Threading.Tasks;
    using HeadlessDCGO.Engine.Headless.Effects;

    public static partial class CardEffectCommons
    {
        /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainReboot(...)</c> (KeyWordEffects/Reboot.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
        public static async Task GainReboot(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
        {
            GainReboot(targetPermanent, effectDuration, activateClass?.EffectSourceCard);
            await Task.CompletedTask;
        }
    }
}
