// TEST FIXTURE (not a real card). [Main] (OptionSkill) offers a mode menu (AS-IS UserSelectionManager
// SetBool/IntSelection): "Draw 1" / "Draw 3", plus a conditional "Draw 5" available only when the card's
// "extraMode" metadata flag is set (mirrors the original conditional selectionElements.Add). Used by tests/PRIM-P0
// (mode-choice primitive). Inert in actual play (no real card numbered "TfxSelectMode").
// (이연③-h) Each mode branch re-written from the retired invented `CardEffectFactory.DrawCardsEffect` to the AS-IS
// `new DrawClass(...).Draw()` coroutine idiom (BT1_046), wrapped in an inline ActivateClass — the branch is
// dispatched recursively by the resolver's ActivateICardEffect case.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxSelectMode : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool extraMode =
                card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? record) &&
                record is not null &&
                record.Metadata.TryGetValue("extraMode", out object? raw) && raw is bool b && b;

            // Inline AS-IS draw ActivateClass (BT1_046 idiom): draw `count` via new DrawClass(...).Draw().
            ActivateClass DrawMode(int count)
            {
                ActivateClass activateClass = new ActivateClass();
                activateClass.SetUpICardEffect($"Draw {count}", _ => true, card);
                activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, $"Draw {count} card(s).");
                return activateClass;

                async Task ActivateCoroutine(Hashtable _hashtable)
                {
                    await new DrawClass(card.Context, card.Owner, count, activateClass).Draw();
                }
            }

            effects.Add(CardEffectFactory.SelectModeEffect(
                card,
                "Choose one effect to activate.",
                new ModeChoiceEffect.Mode("Draw 1 card.", IsAvailable: null, DrawMode(1)),
                new ModeChoiceEffect.Mode("Draw 3 cards.", IsAvailable: null, DrawMode(3)),
                new ModeChoiceEffect.Mode("Draw 5 cards.", IsAvailable: () => extraMode, DrawMode(5))));
        }

        return effects;
    }
}
