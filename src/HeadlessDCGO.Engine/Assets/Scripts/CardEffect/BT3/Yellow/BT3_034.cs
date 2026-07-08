// STOP: AS-IS BT3_034 [On Play] looks at the top card of the owner's OWN Security stack and may add that
// exact card to hand (SelectCardEffect.Mode.AddHand, root: Security, customRootCardList: {topCard},
// canNoSelect: true), then on a successful add: ReduceSecurity (the removed-from-security bookkeping) +
// Draw 1. No existing headless primitive reveals/selects from the SECURITY zone — the only reveal/select
// primitives (SimplifiedRevealAndSelectEffect / RevealDeckTopCardsAndSelect / SimplifiedSelectCardConditionClass)
// operate over the LIBRARY (deck) zone, not Security, and there is no "select from Security, optionally
// add to hand, then reduce security + draw" declarative effect. Approximating via a library-reveal
// primitive would target the wrong zone (a genuine fidelity break), so this stays STOP.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_034 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [On Play] look at top security card, may add to hand to trigger <Draw 1> — no Security-zone
        // reveal/select primitive exists (see file header).

        return cardEffects;
    }
}
