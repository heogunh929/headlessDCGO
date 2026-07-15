// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [OnDestroyedAnyone] returns a
// self <Save> (CardEffectFactory.SaveEffect) — the AS-IS printed shape. Used by the C-Del 3a POST-cluster
// witness to prove that a printed [Save] fires through the AS-IS OnDestroyedAnyone cut-in window, single-fire
// (the invented DeletionReplacementGate/Timing Save half is retired). No real card has the number "TfxSave",
// so this is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxSave : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.SaveEffect(card));
        }

        return cardEffects;
    }
}
