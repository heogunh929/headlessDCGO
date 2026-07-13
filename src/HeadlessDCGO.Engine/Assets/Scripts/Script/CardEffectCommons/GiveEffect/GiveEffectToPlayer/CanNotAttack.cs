// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotAttack.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainCanNotAttackPlayerEffect` (CardEffectCommons.cs:3244).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotAttackPlayerEffect(...)</c> (GiveEffect/GiveEffectToPlayer/CanNotAttack.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCanNotAttackPlayerEffect(Func<Permanent, bool> attackerCondition, Func<Permanent, bool> defenderCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        GainCanNotAttackPlayerEffect(attackerCondition, defenderCondition, effectDuration, activateClass?.EffectSourceCard, effectName);
        await Task.CompletedTask;
    }
}
