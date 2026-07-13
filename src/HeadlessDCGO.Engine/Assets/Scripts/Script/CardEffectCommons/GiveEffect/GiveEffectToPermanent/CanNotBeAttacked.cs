// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeAttacked.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainCanNotBeAttacked` (CardEffectCommons.cs:3082).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotBeAttacked(...)</c> (GiveEffect/GiveEffectToPermanent/CanNotBeAttacked.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCanNotBeAttacked(Permanent targetPermanent, Func<Permanent, bool> attackerCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        GainCanNotBeAttacked(targetPermanent, attackerCondition, effectDuration, activateClass?.EffectSourceCard, effectName);
        await Task.CompletedTask;
    }
}
