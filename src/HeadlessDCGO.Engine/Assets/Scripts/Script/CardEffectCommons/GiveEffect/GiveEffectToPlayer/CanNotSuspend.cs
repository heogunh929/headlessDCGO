// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotSuspend.cs
// (J-4) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotSuspendPlayerEffect (…/GiveEffectToPlayer/CanNotSuspend.cs
// :10-67): the OWNING PLAYER gains a timed "its permanents can't suspend" restriction. Builds the AS-IS kind-class
// via CardEffectFactory.CantSuspendStaticEffect where the PermanentCondition folds on-battle-area +
// !TopCard.CanNotBeAffected(cause) + the caller's predicate (inner _PermanentCondition) AND, when
// isOnlyActivePhase, narrows to the turn player's own permanents; CanUseCondition = `!isOnlyActivePhase || phase ==
// Active`. Stores it in the owning player's duration bucket via AddEffectToPlayer(timing: EffectTiming.None). Read
// LIVE by Permanent.CanSuspend (player arm) over player.EffectList(None), consumed by CanActivateSuspendCostEffect
// — the registry joint arm goes silent. AS-IS coroutine only drove the per-permanent CreateDebuffEffect UI visual
// (dropped). The public AS-IS-signature `Task` overload threads the LIVE `activateClass` as the CanNotBeAffected
// cause; the CardSource-only substrate overload (CardEffectCommons.cs) collapses the cause to BareCauseEffect.For(sourceCard).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.State;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotSuspendPlayerEffect</c> (GiveEffectToPlayer/CanNotSuspend.cs:10)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause folded into the PermanentCondition.</summary>
    public static async Task GainCanNotSuspendPlayerEffect(
        Func<Permanent, bool> permanentCondition,
        EffectDuration effectDuration,
        ICardEffect activateClass,
        bool isOnlyActivePhase,
        string effectName)
    {
        // AS-IS :12-13 guards (activateClass / EffectSourceCard null).
        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        GainCanNotSuspendPlayerEffectImpl(
            permanentCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, isOnlyActivePhase, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs). Mirrors AS-IS GainCanNotSuspendPlayerEffect :10-67.</summary>
    private static bool GainCanNotSuspendPlayerEffectImpl(
        Func<Permanent, bool>? permanentCondition,
        EffectDuration effectDuration,
        CardSource? card,
        ICardEffect? cause,
        bool isOnlyActivePhase,
        string effectName)
    {
        if (card is null || cause is null) return false;   // AS-IS :12-13

        bool _PermanentCondition(Permanent permanent)   // AS-IS :17-31
        {
            if (IsPermanentExistsOnBattleArea(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(cause))
                {
                    if (permanentCondition is null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool PermanentCondition(Permanent permanent)   // AS-IS :33-44
        {
            if (_PermanentCondition(permanent))
            {
                // AS-IS :37 `gameContext.TurnPlayer == permanent.TopCard.Owner` — mirror TurnPlayer is a Player and
                //   TopCard.Owner is a HeadlessPlayerId, so compare the live TurnController turn-player id to the
                //   permanent's owner id (same idiom as GainCanNotUnsuspendPlayerEffect). ADAPTATION (substrate translation).
                if (!isOnlyActivePhase || card.Context.TurnController.Current.TurnPlayerId == permanent.OwnerId)
                {
                    return true;
                }
            }

            return false;
        }

        bool CanUseCondition()   // AS-IS :46-49
        {
            return !isOnlyActivePhase || new GameContext(card.Context).TurnPhase == GameContext.phase.Active;
        }

        CardEffects.CanNotSuspendClass canNotSuspendClass = CardEffectFactory.CantSuspendStaticEffect(  // AS-IS :51-56
            permanentCondition: PermanentCondition,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPlayer(  // AS-IS :58
            effectDuration: effectDuration,
            card: card,
            cardEffect: canNotSuspendClass,
            timing: EffectTiming.None);

        // AS-IS :60-66 iterated PermanentsForTurnPlayer running CreateDebuffEffect (UI visual) — dropped headless.
        return true;
    }
}
