// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Collision.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainCollision` (CardEffectCommons.cs:3421).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCollision(...)</c> (KeyWordEffects/Collision.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCollision(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
    {
        GainCollision(targetPermanent, effectDuration, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }
}
