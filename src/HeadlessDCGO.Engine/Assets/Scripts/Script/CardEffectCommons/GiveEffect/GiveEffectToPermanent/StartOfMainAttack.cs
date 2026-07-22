// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/StartOfMainAttack.cs
// (SKEL-Exhaust) 1:1 mirror rehoused from the monolith CardEffectCommons.cs into the AS-IS mirrored path.
// Same partial class. Substrate translation of the AS-IS coroutine grant: AS-IS stores an ActivateClass in
// targetPermanent.UntilOwnerTurnEndEffects keyed to EffectTiming.OnStartMainPhase; the mirror registers the
// equivalent duration-tagged (UntilOwnerTurnEnd) EffectBinding on the OnStartMainPhase trigger whose effect
// (StartOfMainAttackEffect) opens the mandatory attack offer. The AS-IS CreateDebuffEffect animation is UI-only
// and stripped. The firing WINDOW itself (auto-firing OnStartMainPhase effects at main start) is the engine's
// existing OnStartMainPhase trigger delivery, so no gap remains (RD-3A-01 resolved).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Generic;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>StartOfMainAttack</c> (GiveEffect/StartOfMainAttack.cs:5, verbatim): until the
    /// owner's turn end, at the start of the owner's main phase this Digimon MUST attack (the offer cannot
    /// be declined; player or any Digimon). Registered as a duration-tagged trigger binding whose effect
    /// opens the attack offer.</summary>
    public static void StartOfMainAttack(Permanent? targetPermanent, CardSource sourceCard)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty)
        {
            return;
        }

        EngineContext context = sourceCard.Context;
        HeadlessEntityId attackerId = targetPermanent.InstanceId;
        var effectContext = new EffectContext(
            sourceCard.Controller, sourceCard.Owner, attackerId,
            triggerEntityId: null, targetEntityIds: new[] { attackerId },
            values: new Dictionary<string, object?>(StringComparer.Ordinal));
        // The binding request id MUST equal the effect body's Definition.EffectId
        // (StartOfMainAttackEffect.Definition = "start-of-main-attack:{attackerId}"), else EffectBinding rejects
        // the pair. (The pre-relocation monolith copy carried a mismatched id — inert there because it had 0 live
        // callers; corrected here so the grant is actually registrable. Digest-safe: still 0 live callers.)
        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(
                new HeadlessEntityId($"start-of-main-attack:{attackerId.Value}"),
                sourceCard.Controller, Headless.Effects.TriggerTimings.OnStartMainPhase, effectContext),
            keywords: null, EffectQueryRole.None, queryScopes: null,
            effect: new StartOfMainAttackEffect(context, attackerId),
            duration: EffectDuration.UntilOwnerTurnEnd));
    }
}
