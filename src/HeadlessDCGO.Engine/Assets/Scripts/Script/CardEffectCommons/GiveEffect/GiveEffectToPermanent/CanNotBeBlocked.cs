// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeBlocked.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainCanNotBeBlocked` (CardEffectCommons.cs:3089).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotBeBlocked(...)</c> (GiveEffect/GiveEffectToPermanent/CanNotBeBlocked.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCanNotBeBlocked(Permanent targetPermanent, Func<Permanent, bool> defenderCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        GainCanNotBeBlocked(targetPermanent, defenderCondition, effectDuration, activateClass?.EffectSourceCard, effectName);
        await Task.CompletedTask;
    }
}
