// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [Main] (OptionSkill) yields a
// DrawEffect — mirrors the original DrawClass usage ("draw N"). Used by tests/BT-PRE-A1 (G9-015). Inert in
// actual play (no real card numbered "TfxDraw").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxDraw : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(new DrawEffect(card, drawCount: 2, "Draw 2 cards."));
        }

        return cardEffects;
    }
}
