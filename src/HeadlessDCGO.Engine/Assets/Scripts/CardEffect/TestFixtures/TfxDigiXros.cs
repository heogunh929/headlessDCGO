// TEST FIXTURE (not a real card). A dispatch-discoverable CEntity_Effect that declares a DigiXros recipe
// the AS-IS way, via CardEffectFactory.DigiXrosEffectFromNames — which returns an AddDigiXrosConditionClass
// into the card's OWN effect list (AS-IS CardEffectFactory.cs:784), read back through
// CardSource.DigiXrosConditionOf() and consumed by SelectDigiXrosClass on the ordinary PlayCard entry.
// (SpecialPlay re-migration: the former note here described the deleted SpecialPlayAction.GetLegalActions
// on-demand registration — that invented driver and its side registry are gone.) No real card has the
// number "TfxDigiXros", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxDigiXros : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // [DigiXros] fuse "MatA" + "MatB" under this card.
        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(CardEffectFactory.DigiXrosEffectFromNames(card, 0, null, "MatA", "MatB"));
        }

        return cardEffects;
    }
}
