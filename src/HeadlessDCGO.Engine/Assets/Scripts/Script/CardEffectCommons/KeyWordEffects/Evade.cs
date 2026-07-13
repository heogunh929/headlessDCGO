// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Evade.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainEvade` (CardEffectCommons.cs:3437). AS-IS siblings CanTriggerEvade/CanActivateEvade/EvadeProcess
// (same file) are not in the bridge map's 91-helper intersection and are left for a later batch.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainEvade(...)</c> (KeyWordEffects/Evade.cs:53) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainEvade(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
    {
        GainEvade(targetPermanent, effectDuration, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }
}
