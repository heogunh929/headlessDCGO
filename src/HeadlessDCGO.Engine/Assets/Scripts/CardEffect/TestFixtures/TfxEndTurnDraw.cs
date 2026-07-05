// TEST FIXTURE. "[End of Your Turn] draw 1" — a TRIGGERED ACTIVATED effect at the OnEndTurn BOUNDARY timing
// (no card subject). Exercises the v2 bridge (scan all battle cards at boundary timings). Gates to the owner's
// turn so it only fires on the controller's end-of-turn.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxEndTurnDraw : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        // "[End of YOUR Turn]" — only on the owner's turn.
        if (timing == EffectTiming.OnEndTurn && CardEffectCommons.IsOwnerTurn(card))
        {
            effects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
        }
        return effects;
    }
}
