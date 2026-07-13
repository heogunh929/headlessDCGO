// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByBattle.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainCanNotBeDeletedByBattle` (CardEffectCommons.cs:47).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotBeDeletedByBattle(...)</c> (GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByBattle.cs:11) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainCanNotBeDeletedByBattle(Permanent targetPermanent, Func<Permanent, Permanent, Permanent, CardSource, bool> canNotBeDestroyedByBattleCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        GainCanNotBeDeletedByBattle(targetPermanent, canNotBeDestroyedByBattleCondition, effectDuration, activateClass?.EffectSourceCard, effectName);
        await Task.CompletedTask;
    }
}
