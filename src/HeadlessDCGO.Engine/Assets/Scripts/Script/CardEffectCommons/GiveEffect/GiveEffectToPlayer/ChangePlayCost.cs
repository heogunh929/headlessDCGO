// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangePlayCost.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `ChangePlayCostPlayerEffect` (CardEffectCommons.cs:3372).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.ChangePlayCostPlayerEffect(...)</c> (GiveEffect/GiveEffectToPlayer/ChangePlayCost.cs:11) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task ChangePlayCostPlayerEffect(Func<Permanent, bool> permanentCondition, int changeValue, bool setFixedCost, EffectDuration effectDuration, ICardEffect activateClass)
    {
        ChangePlayCostPlayerEffect(permanentCondition, changeValue, setFixedCost, effectDuration, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }
}
