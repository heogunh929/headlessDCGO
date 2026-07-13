// TEST FIXTURE (not a real card, CardType Option). Its [Main] (OptionSkill) draws 2 cards. Played as a nested
// effect by TfxPlayOption to exercise PlayOptionCardEffect (Build Order 5). Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxOptionDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            effects.Add(CardEffectFactory.DrawCardsEffect(card, 2));
        }
        return effects;
    }
}
