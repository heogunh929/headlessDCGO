// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotUnsuspend.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overloads; delegate to the verified substrate
// `GainCantUnsuspendNextActivePhase` (CardEffectCommons.cs:3121) / `GainCantUnsuspendUntilOpponentTurnEnd`
// (CardEffectCommons.cs:3114) / `GainCanNotUnsuspend` (CardEffectCommons.cs:3107).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCantUnsuspendNextActivePhase(...)</c> (GiveEffect/GiveEffectToPermanent/CanNotUnsuspend.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCantUnsuspendNextActivePhase(Permanent targetPermanent, ICardEffect activateClass)
    {
        GainCantUnsuspendNextActivePhase(targetPermanent, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCantUnsuspendUntilOpponentTurnEnd(...)</c> (GiveEffect/GiveEffectToPermanent/CanNotUnsuspend.cs:45) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCantUnsuspendUntilOpponentTurnEnd(Permanent targetPermanent, ICardEffect activateClass)
    {
        GainCantUnsuspendUntilOpponentTurnEnd(targetPermanent, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }

    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotUnsuspend(...)</c> (GiveEffect/GiveEffectToPermanent/CanNotUnsuspend.cs:69) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCanNotUnsuspend(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass, Func<bool> condition, string effectName)
    {
        GainCanNotUnsuspend(targetPermanent, effectDuration, activateClass?.EffectSourceCard, condition, effectName);
        await Task.CompletedTask;
    }
}
