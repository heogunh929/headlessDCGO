// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanentOrPlayer.cs
// AS-IS 1:1 mirror of the `partial class CardEffectCommons` half that houses AddEffectToPermanent (the AS-IS file
// also houses AddEffectToPlayer; that overload pair currently lives in the mirror CardEffectCommons.cs — R6-P /
// batch-C scope — and is intentionally left there for now, see task W3 point 4).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    #region Add effects to 1 target permanent

    /// <summary>AS-IS <c>AddEffectToPermanent(targetPermanent, effectDuration, card, cardEffect, timing)</c>
    /// (GiveEffect/GiveEffectToPermanentOrPlayer.cs:11-51) — register an effect on the target permanent with a
    /// duration. AS-IS stores a deferred <see cref="GetCardEffectByEffectTiming"/> selector into the target's
    /// duration bucket (owner-relative swap for the two turn-end durations), which <see cref="Permanent.EffectList_Added"/>
    /// then surfaces. RD-P6C3-C1 RESOLVED: a NEW-model effect (an <c>ActivateClass</c>; it has no <c>ToBinding</c>
    /// method) takes this AS-IS 1:1 bucket path.
    ///
    /// TRANSITIONAL SPLIT (retires in batch C): an OLD-model effect (one that still exposes <c>ToBinding(string)</c>)
    /// keeps the pre-R3 registry-lowering substrate path unchanged — the live legacy gates read the registry, so an
    /// old-model grant must stay there until batch C retires the registry. The two effect populations are DISJOINT
    /// (keyed by effect model: <see cref="LegacyBindingBridge.TryToBinding"/> true = old / false = new); no consumer
    /// reads "registry OR bucket" for the same effect. There is no live old-model caller of this method in the mirror
    /// today (the sole caller, BT1_112, passes a new-model ActivateClass), so the registry branch is currently
    /// inert but preserved verbatim.</summary>
    public static void AddEffectToPermanent(
        Permanent? targetPermanent, EffectDuration effectDuration, CardSource card, ICardEffect cardEffect, EffectTiming timing)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardEffect);
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty)
        {
            return;
        }

        // TRANSITIONAL (batch C): OLD-model effects lower to a registry binding (LegacyBindingBridge left the
        // ICardEffect contract's ToBinding); keep that substrate path so the live legacy gates keep reading them.
        if (LegacyBindingBridge.TryToBinding(
                cardEffect,
                $"{card.InstanceId.Value}:addEffect:{targetPermanent.InstanceId.Value}:{Guid.NewGuid():N}",
                out EffectBinding? binding) && binding is not null)
        {
            var retargeted = new EffectContext(
                binding.Request.Context.SourcePlayerId,
                binding.Request.Context.OwnerPlayerId,
                binding.Request.Context.SourceEntityId,
                binding.Request.Context.TriggerEntityId,
                targetEntityIds: new[] { targetPermanent.InstanceId },
                values: binding.Request.Context.Values);
            card.Context.EffectRegistry.Register(new EffectBinding(
                new EffectRequest(binding.Request.EffectId, binding.Request.ControllerId, binding.Request.Timing, retargeted),
                binding.Keywords, binding.QueryRoles, binding.QueryScopes, binding.Effect, effectDuration));
            return;
        }

        // AS-IS 1:1 (GiveEffectToPermanentOrPlayer.cs:13-50) — NEW-model effect: store the deferred selector in the
        // target permanent's duration bucket.
        Func<EffectTiming, ICardEffect> getCardEffect = GetCardEffectByEffectTiming(timing: timing, cardEffect: cardEffect);

        switch (effectDuration)
        {
            case EffectDuration.UntilOpponentTurnEnd:
                if (IsOwnerPermanent(targetPermanent, card))
                {
                    targetPermanent.UntilOpponentTurnEndEffects.Add(getCardEffect);
                }
                else
                {
                    targetPermanent.UntilOwnerTurnEndEffects.Add(getCardEffect);
                }
                break;

            case EffectDuration.UntilOwnerTurnEnd:
                if (IsOwnerPermanent(targetPermanent, card))
                {
                    targetPermanent.UntilOwnerTurnEndEffects.Add(getCardEffect);
                }
                else
                {
                    targetPermanent.UntilOpponentTurnEndEffects.Add(getCardEffect);
                }
                break;

            case EffectDuration.UntilEachTurnEnd:
                targetPermanent.UntilEachTurnEndEffects.Add(getCardEffect);
                break;

            case EffectDuration.UntilEndAttack:
                targetPermanent.UntilEndAttackEffects.Add(getCardEffect);
                break;

            case EffectDuration.UntilNextUntap:
                targetPermanent.UntilNextUntapEffects.Add(getCardEffect);
                break;
        }
    }

    #endregion
}
