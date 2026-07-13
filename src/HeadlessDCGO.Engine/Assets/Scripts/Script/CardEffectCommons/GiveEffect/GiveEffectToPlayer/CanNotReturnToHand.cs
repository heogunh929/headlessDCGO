// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotReturnToHand.cs
// (EFFECT-MODEL REBUILD / bridge W2, Group A) AS-IS-signature `Task` overload; delegates to the verified
// substrate `GainCanNotReturnToHandPlayerEffect` (CardEffectCommons.cs:3304). `cardEffectCondition` is adapted
// via the shared RD-W2-1 adapter (docs/audit/rebuild_bridge_w2_notes.md; defined alongside
// GiveEffectToPermanent/CanNotBeDeletedByEffect.cs's `AdaptCardEffectCondition`).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotReturnToHandPlayerEffect(...)</c>
    /// (GiveEffect/GiveEffectToPlayer/CanNotReturnToHand.cs:10) — AS-IS-signature overload; delegates to the
    /// verified substrate implementation. See RD-W2-1 (GiveEffectToPermanent/CanNotBeDeletedByEffect.cs) for
    /// the <paramref name="cardEffectCondition"/> adaptation.</summary>
    public static async Task GainCanNotReturnToHandPlayerEffect(Func<Permanent, bool> permanentCondition, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        GainCanNotReturnToHandPlayerEffect(permanentCondition, AdaptCardEffectCondition(cardEffectCondition), effectDuration, activateClass?.EffectSourceCard, effectName);
        await Task.CompletedTask;
    }
}
