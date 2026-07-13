// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/TamerBecomesDigimonThatCanNotDigivolve.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `BecomeDigimonThatCantDigivolve` (CardEffectCommons.cs:2685).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.BecomeDigimonThatCantDigivolve(...)</c> (GiveEffect/GiveEffectToPermanent/TamerBecomesDigimonThatCanNotDigivolve.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task BecomeDigimonThatCantDigivolve(Permanent targetPermanent, int DP, EffectDuration effectDuration, ICardEffect activateClass)
    {
        BecomeDigimonThatCantDigivolve(targetPermanent, DP, effectDuration, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }
}
