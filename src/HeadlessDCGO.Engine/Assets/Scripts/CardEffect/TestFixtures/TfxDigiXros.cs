// TEST FIXTURE (not a real card). A dispatch-discoverable CEntity_Effect that declares a DigiXros recipe
// via CardEffectFactory.DigiXrosEffectFromNames — used by tests/G9-049 to verify the on-demand recipe
// registration in SpecialPlayAction.GetLegalActions (a hand card's special play becomes discoverable even
// though its declaration only runs when considered). No real card has the number "TfxDigiXros", so this is
// inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxDigiXros : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
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
