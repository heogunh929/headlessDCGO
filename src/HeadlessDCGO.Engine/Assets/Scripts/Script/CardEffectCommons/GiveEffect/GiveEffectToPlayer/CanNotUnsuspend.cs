// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotUnsuspend.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainCanNotUnsuspendPlayerEffect` (CardEffectCommons.cs:3217).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotUnsuspendPlayerEffect(...)</c> (GiveEffect/GiveEffectToPlayer/CanNotUnsuspend.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCanNotUnsuspendPlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass, bool isOnlyActivePhase, string effectName)
    {
        GainCanNotUnsuspendPlayerEffect(permanentCondition, effectDuration, activateClass?.EffectSourceCard, isOnlyActivePhase, effectName);
        await Task.CompletedTask;
    }
}
