// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotReturnToHand.cs
// (EFFECT-MODEL REBUILD / bridge W2, Group A) AS-IS-signature `Task` overload; delegates to the verified
// substrate `GainCanNotReturnToHand` (CardEffectCommons.cs:3288). `cardEffectCondition` is adapted via the
// shared RD-W2-1 adapter (docs/audit/rebuild_bridge_w2_notes.md; defined alongside
// GiveEffectToPermanent/CanNotBeDeletedByEffect.cs's `AdaptCardEffectCondition`).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainCanNotReturnToHand(...)</c>
    /// (GiveEffect/GiveEffectToPermanent/CanNotReturnToHand.cs:10) — AS-IS-signature overload; delegates to the
    /// verified substrate implementation. See RD-W2-1 (CanNotBeDeletedByEffect.cs) for the
    /// <paramref name="cardEffectCondition"/> adaptation.</summary>
    public static async Task GainCanNotReturnToHand(Permanent targetPermanent, Func<ICardEffect, bool> cardEffectCondition, EffectDuration effectDuration, ICardEffect activateClass, string effectName)
    {
        GainCanNotReturnToHand(targetPermanent, AdaptCardEffectCondition(cardEffectCondition), effectDuration, activateClass?.EffectSourceCard, effectName);
        await Task.CompletedTask;
    }
}
