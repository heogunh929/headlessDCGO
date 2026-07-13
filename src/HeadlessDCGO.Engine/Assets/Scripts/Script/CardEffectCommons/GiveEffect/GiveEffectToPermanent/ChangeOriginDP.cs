// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeOriginDP.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `ChangeBaseDigimonDP` (CardEffectCommons.cs:3392).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.ChangeBaseDigimonDP(...)</c> (GiveEffect/GiveEffectToPermanent/ChangeOriginDP.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task ChangeBaseDigimonDP(Permanent targetPermanent, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)
    {
        ChangeBaseDigimonDP(targetPermanent, changeValue, effectDuration, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }
}
