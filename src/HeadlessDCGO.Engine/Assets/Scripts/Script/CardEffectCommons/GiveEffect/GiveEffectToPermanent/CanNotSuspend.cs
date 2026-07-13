// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotSuspend.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overloads; delegate to the verified substrate
// `GainCanNotSuspend` (CardEffectCommons.cs:3096) / `GainCantSuspendUntilOpponentTurnEnd` (CardEffectCommons.cs:3103).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCantSuspendUntilOpponentTurnEnd(...)</c> (GiveEffect/GiveEffectToPermanent/CanNotSuspend.cs:8) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCantSuspendUntilOpponentTurnEnd(Permanent targetPermanent, ICardEffect activateClass)
    {
        GainCantSuspendUntilOpponentTurnEnd(targetPermanent, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotSuspend(...)</c> (GiveEffect/GiveEffectToPermanent/CanNotSuspend.cs:34) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCanNotSuspend(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass, Func<bool> condition, string effectName)
    {
        GainCanNotSuspend(targetPermanent, effectDuration, activateClass?.EffectSourceCard, condition, effectName);
        await Task.CompletedTask;
    }
}
