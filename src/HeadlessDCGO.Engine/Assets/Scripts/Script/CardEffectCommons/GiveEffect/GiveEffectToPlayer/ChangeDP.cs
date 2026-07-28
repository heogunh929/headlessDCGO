// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeDP.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `ChangeDigimonDPPlayerEffect` (CardEffectCommons.cs:1824).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.ChangeDigimonDPPlayerEffect(...)</c> (GiveEffect/GiveEffectToPlayer/ChangeDP.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task ChangeDigimonDPPlayerEffect(Func<Permanent, bool> permanentCondition, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)
    {
        ChangeDigimonDPPlayerEffect(permanentCondition, changeValue, effectDuration, activateClass?.EffectSourceCard, activateClass);
        await Task.CompletedTask;
    }
}
