// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByBattle.cs
// (SKEL-Exhaust) 1:1 mirror rehoused from the monolith CardEffectCommons.cs (:47) into the AS-IS mirrored path.
// The census listed this as UNPORTED (§3) but the symbol WAS present in the monolith — this relocation makes
// the mirror structurally 1:1 (the player-scope sibling already lives at GiveEffectToPlayer/). Same partial
// class. Substrate translation of the AS-IS coroutine grant: synchronous (returns bool); the AS-IS
// CreateBuffEffect animation is UI-only and stripped.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public static partial class CardEffectCommons
{
    /// <summary>(AD1-G) 1:1 mirror of AS-IS <c>CardEffectCommons.GainCanNotBeDeletedByBattle</c>
    /// (GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByBattle.cs:11-54): grant the TARGET permanent a
    /// timed battle-deletion immunity. Registers a duration-tagged, card-TARGETED restriction binding
    /// (consumed by <see cref="Headless.Runtime.BattleDeletionGate"/>): the flag + the caller's 4-arg battle
    /// predicate stored verbatim + a LIVE condition (target still on the battle area && !CanNotBeAffected — the
    /// AS-IS <c>CanUseCondition</c>). (RD-J-01) The grant is UNCONDITIONAL — AS-IS has no grant-time immunity
    /// refusal; an immune target still receives the (inert) grant and it activates once immunity lifts.
    /// Synchronous (all ported Gain-commons are; the AS-IS coroutine only drove UI). Returns true when the grant
    /// registered.</summary>
    public static bool GainCanNotBeDeletedByBattle(
        Permanent targetPermanent,
        Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition,
        EffectDuration effectDuration,
        ICardEffect activateClass,
        string effectName)
    {
        // (R3-W3c-1) AS-IS 1:1 signature: the causing effect is the live `ICardEffect activateClass`
        // (GiveEffectToPermanent/CanNotBeDeletedByBattle.cs:11), NOT a bare source card. AS-IS :15-18 guards.
        if (targetPermanent is null)
        {
            return false;
        }

        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            return false;   // AS-IS :15-16.
        }

        CardSource sourceCard = activateClass.EffectSourceCard;   // AS-IS :18 `CardSource card = activateClass.EffectSourceCard`.
        EngineContext context = sourceCard.Context;
        HeadlessEntityId targetId = targetPermanent.InstanceId;
        var zones = (IZoneStateReader)context.ZoneMover;
        if (targetId.IsEmpty || !zones.GetCards(targetPermanent.OwnerId, ChoiceZone.BattleArea).Contains(targetId))
        {
            return false;   // AS-IS :14 IsPermanentExistsOnBattleArea guard.
        }

        // (RD-J-01) AS-IS grants UNCONDITIONALLY — there is NO grant-time immunity guard. The earlier
        // "(R3-W3c-1) AS-IS grant-time guard (AS-IS :26)" note was a MISREAD: AS-IS :26 is the read-time
        // CanUseCondition check (kept below); AS-IS AddEffectToPermanent runs regardless, with CanNotBeAffected
        // otherwise only gating the dropped CreateBuffEffect UI visual. Removed so a temporarily-immune target
        // still receives the inert grant, which activates once immunity lifts.

        // (R3-W3c-2) AS-IS 1:1 (…/CanNotBeDeletedByBattle.cs:36-47): build the CanNotBeDestroyedByBattleClass via
        // the factory (carrying the 4-arg battle predicate + the PermanentCondition `permanent == targetPermanent`
        // + the live CanUseCondition) and store it in the target's duration bucket via AddEffectToPermanent(None).
        bool PermanentCondition(Permanent permanent) => permanent == targetPermanent;

        bool CanUseCondition()
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (!targetPermanent.TopCard.CanNotBeAffected(activateClass))
                {
                    return true;
                }
            }

            return false;
        }

        CardEffects.CanNotBeDestroyedByBattleClass canNotBeDestroyedByBattleClass = CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(
            canNotBeDestroyedByBattleCondition: canNotBeDestroyedByBattleCondition!,
            permanentCondition: PermanentCondition,
            isInheritedEffect: false,
            card: sourceCard,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPermanent(
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: sourceCard,
            cardEffect: canNotBeDestroyedByBattleClass,
            timing: EffectTiming.None);
        return true;
    }
}
