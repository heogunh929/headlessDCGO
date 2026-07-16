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

        // (R3-W3b) AS-IS 1:1 (GiveEffectToPermanentOrPlayer.cs:13-50): store the deferred selector in the target
        // permanent's duration bucket. The pre-R3 transitional LegacyBindingBridge registry-lowering branch that used
        // to precede this (for OLD-model effects still exposing ToBinding) is retired: every live caller passes a
        // NEW-model ActivateClass (no ToBinding), so the registry branch was inert. The bucket path is the sole path,
        // matching AS-IS exactly.
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

    #region Add effects to 1 target player

    /// <summary>AS-IS <c>AddEffectToPlayer(effectDuration, card, cardEffect, timing, getCardEffect)</c>
    /// (GiveEffect/GiveEffectToPermanentOrPlayer.cs:57-89) — register an effect at the owning PLAYER's scope with a
    /// duration. AS-IS stores a deferred <see cref="GetCardEffectByEffectTiming"/> selector into the player's
    /// duration bucket; <see cref="Player.EffectList"/> then surfaces it, and (since the R3-C2 window flip) the live
    /// <c>AutoProcessing.GetSkillInfos</c> enumerates <c>player.EffectList(timing)</c> — so a bucket-stored reactor
    /// (e.g. a <c>[When Attacking] … at end of turn lose N memory</c> reversal) fires through the live window at its
    /// timing, then the per-duration bucket clear (HeadlessEndTurnCleanupFlow, AS-IS <c>= new List&lt;…&gt;()</c>)
    /// resets it. The stored value is the EFFECT OBJECT (a new-model <c>ActivateClass</c> needs no <c>ToBinding</c>);
    /// AS-IS the bucket is a LIST, so a second activation the same turn ADDS a second delegate (BT1_021 attacking
    /// twice loses 6 at end of turn). RD-P6C3-C1 RESOLVED (R3-C2b-2): the pre-R3 registry-lowering path is retired —
    /// every live caller passes a new-model effect (EoTLose3Memory→ActivateClass, BT1_090 nested "Memory -2"
    /// ActivateClass, BT1_104 AddSkillClass), so there is no old-model producer and the bucket path is the sole path.
    /// AS-IS 1:1 (the <c>getCardEffect ??=</c> default and the per-duration switch, including the
    /// <c>UntilOwnerActivePhase</c> Enemy redirect).</summary>
    public static void AddEffectToPlayer(
        EffectDuration effectDuration, CardSource card, ICardEffect cardEffect, EffectTiming timing,
        Func<EffectTiming, ICardEffect>? getCardEffect = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardEffect);

        Player player = new Player(card.Context, card.Owner);

        getCardEffect ??= GetCardEffectByEffectTiming(timing: timing, cardEffect: cardEffect);

        switch (effectDuration)
        {
            case EffectDuration.UntilOpponentTurnEnd:
                player.UntilOpponentTurnEndEffects.Add(getCardEffect);
                break;

            case EffectDuration.UntilOwnerTurnEnd:
                player.UntilOwnerTurnEndEffects.Add(getCardEffect);
                break;

            case EffectDuration.UntilEachTurnEnd:
                player.UntilEachTurnEndEffects.Add(getCardEffect);
                break;

            case EffectDuration.UntilEndBattle:
                player.UntilEndBattleEffects.Add(getCardEffect);
                break;

            case EffectDuration.UntilOwnerActivePhase:
                player.Enemy!.UntilOwnerActivePhaseEffects.Add(getCardEffect);
                break;

            case EffectDuration.UntilCalculateFixedCost:
                player.UntilCalculateFixedCostEffect.Add(getCardEffect);
                break;
        }
    }

    #endregion
}
