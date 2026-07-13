// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Raid.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainRaid` (CardEffectCommons.cs:3441). AS-IS siblings CanActivateRaid/RaidProcess (same file) are not in
// the bridge map's 91-helper intersection and are left for a later batch.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainRaid(...)</c> (KeyWordEffects/Raid.cs:81) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainRaid(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
    {
        GainRaid(targetPermanent, effectDuration, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }
}
