// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotBeDeletedByBattle.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainCanNotBeDeletedPlayerEffect` (CardEffectCommons.cs:3271).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotBeDeletedPlayerEffect(...)</c> (GiveEffect/GiveEffectToPlayer/CanNotBeDeletedByBattle.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCanNotBeDeletedPlayerEffect(Func<Permanent, bool> permanentCondition, Func<Permanent, Permanent, Permanent, CardSource, bool> canNotBeDestroyedByBattleCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        GainCanNotBeDeletedPlayerEffect(permanentCondition, canNotBeDestroyedByBattleCondition, effectDuration, activateClass?.EffectSourceCard, effectName);
        await Task.CompletedTask;
    }
}
