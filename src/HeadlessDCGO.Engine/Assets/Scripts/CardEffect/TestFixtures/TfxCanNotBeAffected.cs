// TEST FIXTURE (not a real card). Returns a general effect-immunity kind-class (AS-IS CanNotAffectedClass —
// "Isn't affected by your opponent's effects") at EffectTiming.None, protecting exactly this card's permanent
// (permanentCondition null => the AS-IS self-only grant). The skillCondition gates on the CAUSING effect: only an
// OPPONENT effect is blocked (CardEffectCommons.IsOpponentEffect), so the witness can exercise the source-relativity
// (opponent cause = immune, own/ally cause = not immune). Consumed by the AS-IS-literal live scan
// CardSource.CanNotBeAffected(ICardEffect) (R1-e), which the B군 P0-1 rehoming now reaches from the mutation sink
// (general-immunity :527, return-sources :1935, trash-sources :1959) and the CardEffectCommons continuous-grant
// cores. Used by tests/Ba-P0-1 and tests/G3.5-C15. Inert in play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxCanNotBeAffected : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            effects.Add(CardEffectFactory.CanNotAffectedStaticEffect(
                permanentCondition: null,
                skillCondition: effect => CardEffectCommons.IsOpponentEffect(effect?.EffectSourceCard, card),
                isInheritedEffect: false, card, condition: null));
        }
        return effects;
    }
}
