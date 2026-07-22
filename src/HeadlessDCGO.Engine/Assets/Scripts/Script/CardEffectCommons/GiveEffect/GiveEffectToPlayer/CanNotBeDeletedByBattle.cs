// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotBeDeletedByBattle.cs
// (J-4) 1:1 mirror of AS-IS CardEffectCommons.GainCanNotBeDeletedPlayerEffect (…/GiveEffectToPlayer/
// CanNotBeDeletedByBattle.cs:10-56): the OWNING PLAYER gains a timed "its permanents can't be deleted in battle"
// restriction. Builds the AS-IS kind-class via CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect where the
// PermanentCondition folds on-battle-area + !TopCard.CanNotBeAffected(cause) + the caller's predicate, and the
// caller's 4-arg battle predicate rides `canNotBeDestroyedByBattleCondition`; CanUseCondition = true. Stores it in
// the owning player's duration bucket via AddEffectToPlayer(timing: EffectTiming.None). Read LIVE by
// Permanent.CanBeDestroyedByBattle (player arm) over player.EffectList(None) — the registry joint arm goes silent.
// AS-IS coroutine only drove the per-permanent CreateBuffEffect UI visual (dropped). The public AS-IS-signature
// `Task` overload threads the LIVE `activateClass` as the CanNotBeAffected cause; the CardSource-only substrate
// overload (CardEffectCommons.cs) collapses the cause to BareCauseEffect.For(sourceCard).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>GainCanNotBeDeletedPlayerEffect</c> (GiveEffectToPlayer/CanNotBeDeletedByBattle.cs:10)
    /// — the AS-IS-signature overload: threads the LIVE <paramref name="activateClass"/> as the
    /// <c>CanNotBeAffected</c> cause folded into the PermanentCondition.</summary>
    public static async Task GainCanNotBeDeletedPlayerEffect(
        Func<Permanent, bool> permanentCondition,
        Func<Permanent, Permanent, Permanent, CardSource, bool> canNotBeDestroyedByBattleCondition,
        EffectDuration effectDuration,
        ICardEffect activateClass,
        string effectName)
    {
        // AS-IS :12-13 guards (activateClass / EffectSourceCard null).
        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        GainCanNotBeDeletedPlayerEffectImpl(
            permanentCondition, canNotBeDestroyedByBattleCondition, effectDuration,
            card: activateClass.EffectSourceCard, cause: activateClass, effectName);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs). Mirrors AS-IS GainCanNotBeDeletedPlayerEffect :10-56.</summary>
    private static bool GainCanNotBeDeletedPlayerEffectImpl(
        Func<Permanent, bool>? permanentCondition,
        Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition,
        EffectDuration effectDuration,
        CardSource? card,
        ICardEffect? cause,
        string effectName)
    {
        if (card is null || cause is null) return false;   // AS-IS :12-13

        bool PermanentCondition(Permanent attacker)   // AS-IS :17-31
        {
            if (IsPermanentExistsOnBattleArea(attacker))
            {
                if (!attacker.TopCard.CanNotBeAffected(cause))
                {
                    if (permanentCondition is null || permanentCondition(attacker))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanUseCondition() => true;   // AS-IS :33-36

        CardEffects.CanNotBeDestroyedByBattleClass canNotBeDestroyedByBattleClass = CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(  // AS-IS :38-44
            canNotBeDestroyedByBattleCondition: canNotBeDestroyedByBattleCondition!,
            permanentCondition: PermanentCondition,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPlayer(  // AS-IS :46
            effectDuration: effectDuration,
            card: card,
            cardEffect: canNotBeDestroyedByBattleClass,
            timing: EffectTiming.None);

        // AS-IS :48-54 iterated PermanentsForTurnPlayer running CreateBuffEffect (UI visual) — dropped headless.
        return true;
    }
}
