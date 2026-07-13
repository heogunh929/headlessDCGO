// Source: Assets/Scripts/CardEffect/BT2/Yellow/BT2_040.cs
// [On Deletion] Place this card on top of your security stack face down.
// STOP: "place self (Digimon, from trash) into top of security" body primitive absent;
//   AS-IS calls CardObjectController.AddSecurityCard(card) but no confirmed headless ICardEffectBody or
//   CardEffectFactory covers placing this card (self, from trash) into security;
//   PlaceSelfDelayOptionSecurityEffect is for the Delay-Option mechanic (option card), not a Digimon placing itself;
//   additionally card.Owner.CanAddSecurity() canActivate guard has no known headless commons predicate.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_040 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            // STOP: body primitive absent — AS-IS: CardObjectController.AddSecurityCard(card) (place self from trash to top of security); no confirmed headless ICardEffectBody for this. card.Owner.CanAddSecurity() canActivate guard also has no confirmed headless predicate.
        }

        return cardEffects;
    }
}