// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [OnDestroyedAnyone] returns a
// self-static <Fortitude> (CardEffectFactory.FortitudeSelfEffect) — the AS-IS printed shape. Used by the
// C-Del 3a POST-cluster witness to prove that a printed [Fortitude] fires through the AS-IS OnDestroyedAnyone
// cut-in window (collect-before-removal), single-fire (the invented DeletionReplacementGate replay is retired).
// No real card has the number "TfxFortitude", so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxFortitude : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.FortitudeSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        return cardEffects;
    }
}
