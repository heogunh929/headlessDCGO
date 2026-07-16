// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Iceclad.cs
// (G-clean-2 grant rehousing) AS-IS-signature `Task` overloads: the [Iceclad] grant, AS-IS 1:1. Kept in the
// flat `...Script.CardEffectCommons` namespace so these are genuine overloads of the same partial
// `CardEffectCommons` type every ported card calls (established convention).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public static partial class CardEffectCommons
{
    /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.GainIceclad</c> (KeyWordEffects/Iceclad.cs:10),
    /// 1:1: build the target-locked <see cref="CardEffectFactory.IcecladStaticEffect"/> and store it in the target
    /// permanent's <c>None</c> duration bucket via <see cref="AddEffectToPermanent"/> — read by
    /// <see cref="Permanent.HasIceclad"/>'s <c>EffectList(None)</c> <c>IIcecladEffect</c> scan. Replaces the invented
    /// <c>GainKeywordToPermanent</c> funnel. ADAPTATION: the AS-IS terminal <c>CreateBuffEffect</c> VFX is
    /// dropped.</summary>
    public static async Task GainIceclad(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
    {
        if (targetPermanent == null) return;
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
        if (activateClass == null) return;
        if (activateClass.EffectSourceCard == null) return;

        CardSource card = activateClass.EffectSourceCard;

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

        IcecladClass iceclad = CardEffectFactory.IcecladStaticEffect(
            permanentCondition: PermanentCondition, isInheritedEffect: false, card: card, condition: CanUseCondition);

        AddEffectToPermanent(
            targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
            cardEffect: iceclad, timing: EffectTiming.None);

        await Task.CompletedTask;
    }

    /// <summary>(G-clean-2 grant rehousing) AS-IS <c>CardEffectCommons.GainIcecladPlayerEffect</c>
    /// (KeyWordEffects/Iceclad.cs:46), 1:1: a PLAYER-scope Iceclad grant stored in the owning player's <c>None</c>
    /// bucket via <see cref="AddEffectToPlayer"/>. ADAPTATION: the AS-IS per-permanent VFX loop is dropped.</summary>
    public static async Task GainIcecladPlayerEffect(Func<Permanent, bool> permanentCondition, EffectDuration effectDuration, ICardEffect activateClass)
    {
        if (activateClass == null) return;
        if (activateClass.EffectSourceCard == null) return;

        CardSource card = activateClass.EffectSourceCard;

        bool PermanentCondition(Permanent permanent)
        {
            if (IsPermanentExistsOnBattleArea(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(activateClass))
                {
                    if (permanentCondition == null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanUseCondition() => true;

        IcecladClass iceclad = CardEffectFactory.IcecladStaticEffect(
            permanentCondition: PermanentCondition, isInheritedEffect: false, card: card, condition: CanUseCondition);

        AddEffectToPlayer(effectDuration: effectDuration, card: card, cardEffect: iceclad, timing: EffectTiming.None);

        await Task.CompletedTask;
    }
}
