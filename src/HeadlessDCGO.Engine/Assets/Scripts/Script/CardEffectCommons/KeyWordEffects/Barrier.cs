// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Barrier.cs
// (EFFECT-MODEL REBUILD / bridge W1) AS-IS-signature `Task` overload; delegates to the verified substrate
// `GainBarrier` (CardEffectCommons.cs:3461).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.GainBarrier</c> (KeyWordEffects/Barrier.cs:65),
    /// 1:1: build the <see cref="CardEffectFactory.BarrierEffect"/> <c>ActivateClass</c> (card = <c>TopCard</c>, AS-IS)
    /// and store it in the target permanent's <c>WhenPermanentWouldBeDeleted</c> duration bucket via
    /// <see cref="AddEffectToPermanent"/> — so the PRE cut-in deletion window collects it via GetSkillInfos and
    /// resolves <see cref="BarrierProcess"/> (the retired DeletionReplacementGate firing-half already expects the
    /// bucket effect, DeletionReplacementGate.cs:76). Replaces the invented <c>GainKeywordToPermanent</c> funnel (a
    /// <c>ContinuousKeywordGate.Barrier</c> registry marker the retired gate no longer reads). ADAPTATION: the AS-IS
    /// terminal <c>CreateBuffEffect</c> VFX is dropped.</summary>
    public static async Task GainBarrier(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
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

        ActivateClass barrier = CardEffectFactory.BarrierEffect(
            targetPermanent: targetPermanent,
            isInheritedEffect: false,
            condition: CanUseCondition,
            rootCardEffect: activateClass,
            card: targetPermanent.TopCard);

        AddEffectToPermanent(
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: barrier,
            timing: EffectTiming.WhenPermanentWouldBeDeleted);

        await Task.CompletedTask;
    }

    /// <summary>(P6 cluster2) AS-IS <c>CanActivateBarrier</c> (KeyWordEffects/Barrier.cs:10, verbatim).</summary>
    public static bool CanActivateBarrier(Permanent permanent) =>
        IsPermanentExistsOnBattleArea(permanent) && new Player(permanent.TopCard.Context, permanent.TopCard.Owner).SecurityCards.Count >= 1;

    /// <summary>(P6 cluster2) AS-IS <c>BarrierProcess</c> (KeyWordEffects/Barrier.cs:25): trash the top security
    /// card to prevent this Digimon's deletion, then cancel the pending deletion (<c>willBeRemoveField = false</c>).
    /// ShowDeleteEffect/HideDeleteEffect/add-log = UI (stripped, established convention). <c>IDestroySecurity</c>'s
    /// mirror ctor takes (context, playerId, count, causeEffectSourceId, fromTop) in place of the AS-IS (player,
    /// count, cardEffect, fromTop) shape. (C-Del 3c-1) the AS-IS trailing <c>willBeRemoveField = false</c> is now
    /// RESTORED — survival is owned by the AS-IS PRE cut-in window, not the retired DeletionReplacementGate.</summary>
    public static async Task BarrierProcess(Permanent permanent, ICardEffect activateClass, CancellationToken cancellationToken = default)
    {
        if (permanent is null || permanent.TopCard is null)
        {
            return;
        }

        CardSource topCard = permanent.TopCard;
        if (new Player(topCard.Context, topCard.Owner).SecurityCards.Count < 1)
        {
            return;
        }

        await new IDestroySecurity(
            topCard.Context, topCard.Owner, destroySecurityCount: 1,
            cardEffect: activateClass, fromTop: true)
            .DestroySecurity(cancellationToken).ConfigureAwait(false);

        permanent.willBeRemoveField = false;
    }
}
