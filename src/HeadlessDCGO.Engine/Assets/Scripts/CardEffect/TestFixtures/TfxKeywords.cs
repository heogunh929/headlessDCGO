// TEST FIXTURE (not a real card). A CEntity_Effect whose [All Turns] continuous effects grant the three
// self-static keywords (Blocker / Jamming / Piercing) at once, registered the same way a real card's
// keywords are (SelfKeywordEffect -> EffectRegistry binding, NO metadata flag). Used by tests/GR-005 to
// verify ContinuousKeywordGate bridges those registry bindings to the keyword consumers. No real card has
// the number "TfxKeywords", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxKeywords : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
            cardEffects.Add(CardEffectFactory.JammingSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        return cardEffects;
    }
}
