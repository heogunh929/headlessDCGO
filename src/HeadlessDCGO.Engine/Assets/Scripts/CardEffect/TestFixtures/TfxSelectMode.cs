// TEST FIXTURE (not a real card). [Main] (OptionSkill) offers a mode menu (AS-IS UserSelectionManager
// SetBool/IntSelection): "Draw 1" / "Draw 3", plus a conditional "Draw 5" available only when the card's
// "extraMode" metadata flag is set (mirrors the original conditional selectionElements.Add). Each branch is an
// existing DrawEffect. Used by tests/PRIM-P0 (mode-choice primitive). Inert in actual play (no real card
// numbered "TfxSelectMode").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxSelectMode : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool extraMode =
                card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? record) &&
                record is not null &&
                record.Metadata.TryGetValue("extraMode", out object? raw) && raw is bool b && b;

            effects.Add(CardEffectFactory.SelectModeEffect(
                card,
                "Choose one effect to activate.",
                new ModeChoiceEffect.Mode("Draw 1 card.", IsAvailable: null, CardEffectFactory.DrawCardsEffect(card, 1)),
                new ModeChoiceEffect.Mode("Draw 3 cards.", IsAvailable: null, CardEffectFactory.DrawCardsEffect(card, 3)),
                new ModeChoiceEffect.Mode("Draw 5 cards.", IsAvailable: () => extraMode, CardEffectFactory.DrawCardsEffect(card, 5))));
        }

        return effects;
    }
}
