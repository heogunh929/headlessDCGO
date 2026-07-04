// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [On Play]
// (OnEnterFieldAnyone) yields a DrawEffect. Used by tests/G9-016 to prove a played card's OWN
// [On Play] activated effect fires through PlayCardAction. Inert in actual play (no real card "TfxOnPlayDraw").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxOnPlayDraw : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            cardEffects.Add(new DrawEffect(card, drawCount: 2, "[On Play] Draw 2 cards."));
        }
        return cardEffects;
    }
}
