// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [OnDestroyedAnyone] returns a
// self-static <Ascension> (CardEffectFactory.AscensionSelfEffect) — the AS-IS printed shape (isOptional=false
// quirk). Used by the C-Del 3a POST-cluster witness to prove that a printed [Ascension] fires through the
// AS-IS OnDestroyedAnyone cut-in window (its internal "place in security?" yes/no prompt), single-fire (the
// invented DeletionReplacementGate/Timing Ascension half is retired). No real card has the number
// "TfxAscension", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxAscension : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.AscensionSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        return cardEffects;
    }
}
