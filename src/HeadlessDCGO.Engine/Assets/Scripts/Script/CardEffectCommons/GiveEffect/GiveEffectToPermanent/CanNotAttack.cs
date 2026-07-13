// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotAttack.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainCanNotAttack` (CardEffectCommons.cs:3067).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotAttack(...)</c> (GiveEffect/GiveEffectToPermanent/CanNotAttack.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCanNotAttack(Permanent targetPermanent, Func<Permanent, bool> defenderCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        GainCanNotAttack(targetPermanent, defenderCondition, effectDuration, activateClass?.EffectSourceCard, effectName);
        await Task.CompletedTask;
    }
}
