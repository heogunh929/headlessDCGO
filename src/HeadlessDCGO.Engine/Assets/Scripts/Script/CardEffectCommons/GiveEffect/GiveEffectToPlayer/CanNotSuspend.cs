// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotSuspend.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainCanNotSuspendPlayerEffect` (CardEffectCommons.cs:3230).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotSuspendPlayerEffect(...)</c> (GiveEffect/GiveEffectToPlayer/CanNotSuspend.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCanNotSuspendPlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass, bool isOnlyActivePhase, string effectName)
    {
        GainCanNotSuspendPlayerEffect(permanentCondition, effectDuration, activateClass?.EffectSourceCard, isOnlyActivePhase, effectName);
        await Task.CompletedTask;
    }
}
