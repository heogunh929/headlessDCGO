// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeSAttack.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `ChangeDigimonSAttackPlayerEffect` (CardEffectCommons.cs:3353). The AS-IS sibling
// `InvertDigimonSAttackPlayerEffect` (same file) is not in the bridge map's 91-helper intersection and is
// left for a later batch.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.ChangeDigimonSAttackPlayerEffect(...)</c> (GiveEffect/GiveEffectToPlayer/ChangeSAttack.cs:10) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task ChangeDigimonSAttackPlayerEffect(Func<Permanent, bool> permanentCondition, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)
    {
        ChangeDigimonSAttackPlayerEffect(permanentCondition, changeValue, effectDuration, activateClass?.EffectSourceCard, activateClass);
        await Task.CompletedTask;
    }
}
