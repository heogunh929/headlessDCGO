// TEST FIXTURE (not a real card). [Main] (OptionSkill) hatches the controller's top digi-egg into the
// breeding area (CanHatch-gated) — mirrors the original HatchDigiEggClass. Used by tests/BT-PRE-A4 (G9-018).
// Inert in actual play (no real card numbered "TfxHatch").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxHatch : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(new HatchDigiEggEffect(card, "Hatch 1 digi-egg."));
        }

        return cardEffects;
    }
}
