// TEST FIXTURE (not a real card). Its [Main] (OptionSkill) plays an Option card from the owner's HAND as a
// nested effect (AS-IS PlayOptionCards). Used by tests/PRIM-P0 (Build Order 5). Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;

public sealed class TfxPlayOption : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            effects.Add(CardEffectFactory.PlayOptionCardEffect(card, ChoiceZone.Hand, _ => true, maxCount: 1, canEndNotMax: false, "Play 1 Option from your hand."));
        }
        return effects;
    }
}
