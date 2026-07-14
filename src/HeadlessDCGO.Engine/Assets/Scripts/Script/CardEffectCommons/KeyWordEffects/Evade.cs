// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Evade.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainEvade` (CardEffectCommons.cs:3437). AS-IS siblings CanTriggerEvade/CanActivateEvade/EvadeProcess
// (same file) are not in the bridge map's 91-helper intersection and are left for a later batch.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public static partial class CardEffectCommons
{
    /// <summary>(BRIDGE) AS-IS <c>CardEffectCommons.GainEvade(...)</c> (KeyWordEffects/Evade.cs:53) — AS-IS-signature overload; delegates to the verified substrate implementation.</summary>
    public static async Task GainEvade(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
    {
        GainEvade(targetPermanent, effectDuration, activateClass?.EffectSourceCard);
        await Task.CompletedTask;
    }

    /// <summary>(P6 cluster2) AS-IS <c>CanTriggerEvade</c> (KeyWordEffects/Evade.cs:9, verbatim).</summary>
    public static bool CanTriggerEvade(Hashtable hashtable, Permanent targetPermanent) =>
        IsPermanentExistsOnBattleArea(targetPermanent) &&
        CanTriggerWhenPermanentRemoveField(hashtable, permanent => permanent == targetPermanent);

    /// <summary>(P6 cluster2) AS-IS <c>CanActivateEvade</c> (KeyWordEffects/Evade.cs:24, verbatim).</summary>
    public static bool CanActivateEvade(Permanent targetPermanent) =>
        IsPermanentExistsOnBattleArea(targetPermanent) && CanActivatePermanentSuspendCostEffect(targetPermanent);

    /// <summary>(P6 cluster2) AS-IS <c>EvadeProcess</c> (KeyWordEffects/Evade.cs:39): suspend this Digimon to
    /// prevent its deletion. <c>SuspendPermanentsClass</c>'s mirror ctor takes (permanents, causeEffectSourceId,
    /// isBlock) in place of the AS-IS (permanents, hashtable) shape — <c>isBlock: false</c> (AS-IS's hashtable
    /// carried no IsBlock key here). AS-IS's trailing <c>willBeRemoveField = false; HideDeleteEffect();</c>
    /// (cancelling the pending-deletion coroutine race + its VFX) has no mirror field — the mirror's actual
    /// live "does Evade save this permanent" behaviour is owned entirely by
    /// <see cref="Headless.Runtime.DeletionReplacementGate"/> (its own header cites this exact method as its
    /// AS-IS behavioural model), so this old-model ActivateClass path performs only the real state mutation
    /// (the suspend) — omitted the same way <see cref="BarrierProcess"/> omits ShowDeleteEffect/HideDeleteEffect.</summary>
    public static async Task EvadeProcess(Permanent targetPermanent, ICardEffect activateClass, CancellationToken cancellationToken = default)
    {
        if (!IsPermanentExistsOnBattleArea(targetPermanent))
        {
            return;
        }

        await new SuspendPermanentsClass(
            new List<Permanent> { targetPermanent }, activateClass, isBlock: false)
            .Tap(cancellationToken).ConfigureAwait(false);
    }
}
