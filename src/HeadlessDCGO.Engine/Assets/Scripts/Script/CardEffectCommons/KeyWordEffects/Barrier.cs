// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Barrier.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainBarrier` (CardEffectCommons.cs:3461).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainBarrier(...)</c> (KeyWordEffects/Barrier.cs:65) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainBarrier(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
    {
        GainBarrier(targetPermanent, effectDuration, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }
}
