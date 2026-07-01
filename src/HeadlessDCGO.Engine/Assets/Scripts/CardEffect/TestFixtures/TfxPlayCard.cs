// TEST FIXTURE (not a real card). [Main] (OptionSkill) plays the controller's top library card onto the
// battle area at no cost — mirrors the original PlayCardClass simple case (payCost:false, root:Library).
// Used by tests/BT-PRE-A5 (G9-019). Inert in actual play (no real card numbered "TfxPlayCard").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxPlayCard : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill && card.Context.ZoneMover is IZoneStateReader zones)
        {
            IReadOnlyList<HeadlessEntityId> library = zones.GetCards(card.Owner, ChoiceZone.Library);
            if (library.Count > 0)
            {
                cardEffects.Add(new PlayCardEffect(card, library[0], ChoiceZone.Library, "Play the top library card cost-free."));
            }
        }

        return cardEffects;
    }
}
