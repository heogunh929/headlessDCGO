// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/ChangeLinkMax.cs
// (SKEL-Exhaust) 1:1 mirror of the AS-IS ChangeDigimonLinkMax grant (both overloads). Latent (0 callers).
// Substrate translation: coroutine IEnumerator -> async Task; the AS-IS CreateBuffEffect/CreateDebuffEffect
// animation is UI-only and stripped (it was the coroutine's only yield); the activateAnimation flag is kept
// for signature parity but no longer drives anything.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>ChangeDigimonLinkMax</c> (GiveEffect/GiveEffectToPermanent/ChangeLinkMax.cs): grant the
    /// target Digimon a link-max delta for <paramref name="effectDuration"/>. An immune target (CanNotBeAffected)
    /// is refused via the live CanUseCondition.</summary>
    public static async Task ChangeDigimonLinkMax(Permanent targetPermanent, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)
    {
        if (targetPermanent is null ||
            !IsPermanentExistsOnBattleArea(targetPermanent) ||
            changeValue == 0 ||
            activateClass is null ||
            activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

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

        var changeLinkMaxClass = CardEffectFactory.ChangeTargetLinkMaxStaticEffect(
            targetPermanent: targetPermanent,
            changeValue: changeValue,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition);

        AddEffectToPermanent(
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: changeLinkMaxClass,
            timing: EffectTiming.None);

        // AS-IS CreateBuffEffect/CreateDebuffEffect animation stripped (UI-only).
        await Task.CompletedTask;
    }

    /// <summary>AS-IS <c>ChangeDigimonLinkMax</c> animation-flagged + hashstring overload
    /// (GiveEffect/GiveEffectToPermanent/ChangeLinkMax.cs).</summary>
    public static async Task ChangeDigimonLinkMax(Permanent targetPermanent, int changeValue, EffectDuration effectDuration, ICardEffect activateClass, bool activateAnimation, string? hashstring = null)
    {
        if (targetPermanent is null ||
            !IsPermanentExistsOnBattleArea(targetPermanent) ||
            changeValue == 0 ||
            activateClass is null ||
            activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

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

        var changeLinkMaxClass = CardEffectFactory.ChangeTargetLinkMaxStaticEffect(
            targetPermanent: targetPermanent,
            changeValue: changeValue,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            hashstring: hashstring);

        AddEffectToPermanent(
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: card,
            cardEffect: changeLinkMaxClass,
            timing: EffectTiming.None);

        // AS-IS gated CreateBuffEffect/CreateDebuffEffect on activateAnimation; animation stripped (UI-only).
        _ = activateAnimation;
        await Task.CompletedTask;
    }
}
