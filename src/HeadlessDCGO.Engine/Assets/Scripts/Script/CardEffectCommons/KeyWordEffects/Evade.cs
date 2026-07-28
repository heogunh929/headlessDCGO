// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Evade.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainEvade` (CardEffectCommons.cs:3437). AS-IS siblings CanTriggerEvade/CanActivateEvade/EvadeProcess
// (same file) are not in the bridge map's 91-helper intersection and are left for a later batch.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public static partial class CardEffectCommons
{
    /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.GainEvade</c> (KeyWordEffects/Evade.cs:53),
    /// 1:1: build the <see cref="CardEffectFactory.EvadeEffect"/> <c>ActivateClass</c> and store it in the target
    /// permanent's <c>WhenPermanentWouldBeDeleted</c> duration bucket via <see cref="AddEffectToPermanent"/> — so the
    /// PRE cut-in deletion window (sink / battle / security) collects it via GetSkillInfos and resolves
    /// <see cref="EvadeProcess"/> (the retired DeletionReplacementGate firing-half already expects the bucket effect,
    /// DeletionReplacementGate.cs:76). Replaces the invented <c>GainKeywordToPermanent</c> funnel (a
    /// <c>ContinuousKeywordGate.Evade</c> registry marker the retired gate no longer reads — the granted keyword was
    /// inert). ADAPTATION: the AS-IS terminal <c>CreateBuffEffect</c> VFX is dropped.</summary>
    public static async Task GainEvade(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
    {
        if (targetPermanent == null) return;
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
        if (activateClass == null) return;
        if (activateClass.EffectSourceCard == null) return;

        CardSource card = activateClass.EffectSourceCard;

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

        ActivateClass evade = CardEffectFactory.EvadeEffect(
            targetPermanent: targetPermanent, isInheritedEffect: false, condition: CanUseCondition,
            rootCardEffect: activateClass, targetPermanent.TopCard);

        AddEffectToPermanent(
            targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
            cardEffect: evade, timing: EffectTiming.WhenPermanentWouldBeDeleted);

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
    /// prevent its deletion, then cancel the pending deletion (<c>willBeRemoveField = false</c>).
    /// <c>SuspendPermanentsClass</c>'s mirror ctor takes (permanents, causeEffectSourceId, isBlock) in place of
    /// the AS-IS (permanents, hashtable) shape — <c>isBlock: false</c> (AS-IS's hashtable carried no IsBlock key
    /// here). (C-Del 3c-1) the AS-IS trailing <c>willBeRemoveField = false</c> is now RESTORED — survival is owned
    /// by the AS-IS PRE cut-in window (the sink opens it, 3b), not the retired DeletionReplacementGate: clearing
    /// the flag is what the sweep's survivor-fix reads to spare this Digimon. <c>HideDeleteEffect()</c> = UI
    /// (stripped, established convention).</summary>
    public static async Task EvadeProcess(Permanent targetPermanent, ICardEffect activateClass, CancellationToken cancellationToken = default)
    {
        if (!IsPermanentExistsOnBattleArea(targetPermanent))
        {
            return;
        }

        await new SuspendPermanentsClass(
            new List<Permanent> { targetPermanent }, activateClass, isBlock: false)
            .Tap(cancellationToken).ConfigureAwait(false);

        targetPermanent.willBeRemoveField = false;
    }
}
