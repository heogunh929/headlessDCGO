// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangeCardDP.cs
// (J-3) 1:1 mirror of AS-IS CardEffectCommons.ChangeSecurityDigimonCardDPPlayerEffect
// (…/GiveEffectToPlayer/ChangeCardDP.cs:10-40): the OWNING PLAYER gains a timed "security Digimon gains ±DP"
// effect. Builds the AS-IS kind-class via CardEffectFactory.ChangeSecurityDigimonCardDPStaticEffect (forces
// IsUpDown()==true; the factory's CardCondition gates on attackProcess.SecurityDigimon == cardSource AND the
// caller predicate), then stores it in the owning player's duration bucket via
// AddEffectToPlayer(timing: EffectTiming.None). Read LIVE by the AS-IS-literal IChangeCardDPEffect scan
// (NewModelContinuousScan.FoldCardDp over player.EffectList(None), AS-IS CardSource.CardDP), which
// SecurityResolver.TryReadSecurityDigimonDp already unions — producer-only (the legacy securityCardDpDelta
// registry-fold arm is excised). AS-IS body has NO CanNotBeAffected/immunity guard, so no cause is threaded
// (the factory takes only `card`); both the AS-IS-signature `Task` overload and the CardSource-only substrate
// overload (CardEffectCommons.cs) share the same Impl. AS-IS coroutine drove no UI beyond the grant.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>ChangeSecurityDigimonCardDPPlayerEffect</c>
    /// (GiveEffectToPlayer/ChangeCardDP.cs:10) — the AS-IS-signature overload: null-guards the live
    /// <paramref name="activateClass"/> / its <c>EffectSourceCard</c>, then lands the AS-IS 1:1 body against that
    /// source card. <paramref name="cardCondition"/> narrows WHICH security cards receive the DP delta.</summary>
    public static async Task ChangeSecurityDigimonCardDPPlayerEffect(Func<CardSource, bool> cardCondition, int changeValue, EffectDuration effectDuration, ICardEffect activateClass)
    {
        // AS-IS :12-13 guards (activateClass / EffectSourceCard null).
        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            await Task.CompletedTask;
            return;
        }

        ChangeSecurityDigimonCardDPPlayerEffectImpl(cardCondition, changeValue, effectDuration, card: activateClass.EffectSourceCard);
        await Task.CompletedTask;
    }

    /// <summary>AS-IS 1:1 body shared by the <c>ICardEffect</c> overload (above) and the CardSource-only substrate
    /// overload (CardEffectCommons.cs). Mirrors AS-IS ChangeSecurityDigimonCardDPPlayerEffect :14-39. There is no
    /// <c>CanNotBeAffected</c> guard in the AS-IS body, so no cause is threaded — the factory takes only
    /// <paramref name="card"/>.</summary>
    private static bool ChangeSecurityDigimonCardDPPlayerEffectImpl(
        Func<CardSource, bool>? cardCondition, int changeValue, EffectDuration effectDuration, CardSource? card)
    {
        if (card is null || changeValue == 0)   // AS-IS :14 (changeValue==0) + source-card null guard
        {
            return false;
        }

        bool isUpValue = changeValue > 0;   // AS-IS :16-17
        string effectName = isUpValue ? $"Security Digimon gains DP +{changeValue}" : $"Security Digimon gains DP {changeValue}";

        bool Condition()   // AS-IS :21-24
        {
            return true;
        }

        bool CardCondition(CardSource cardSource)   // AS-IS :26-29
        {
            return cardCondition == null || cardCondition(cardSource);
        }

        CardEffects.ChangeCardDPClass changeDPClass = CardEffectFactory.ChangeSecurityDigimonCardDPStaticEffect(   // AS-IS :31-37
            cardCondition: CardCondition,
            changeValue: changeValue,
            isInheritedEffect: false,
            card: card,
            condition: Condition,
            effectName: effectName);

        AddEffectToPlayer(effectDuration: effectDuration, card: card, cardEffect: changeDPClass, timing: EffectTiming.None);   // AS-IS :39
        return true;
    }
}
