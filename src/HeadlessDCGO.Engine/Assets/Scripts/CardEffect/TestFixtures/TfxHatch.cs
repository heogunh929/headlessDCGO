// TEST FIXTURE (not a real card). [Main] (OptionSkill) hatches the controller's top digi-egg into the
// breeding area (CanHatch-gated) — mirrors the original HatchDigiEggClass. Used by tests/BT-PRE-A4 (G9-018).
// Inert in actual play (no real card numbered "TfxHatch").
//
// (이연③-b RE-TARGET) re-pointed old-model `HatchDigiEggEffect` (retired) → the AS-IS hatch path driven
// inline through a new-model `ActivateClass` (BT1_089 [Main] hatch idiom): the empty-breeding +
// available-egg CanHatch guard, then `ZoneMover.HatchDigitamaAsync`. Same behaviour, resolved via
// ActivatedEffectResolver's ActivateICardEffect case (no bespoke resolver switch).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxHatch : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Hatch 1 digi-egg.", CanUseConditionTrue, card);
            activateClass.SetUpActivateClass(null, ActivateHatchCoroutine, -1, false, "Hatch 1 digi-egg.");
            cardEffects.Add(activateClass);

            // AS-IS canUse: null -> always true (BT1_089 idiom uses a real guard; this bare fixture is open).
            bool CanUseConditionTrue(Hashtable hashtable) => true;

            async Task ActivateHatchCoroutine(Hashtable _hashtable)
            {
                EngineContext context = card.Context;
                if (context.ZoneMover is not IZoneStateReader zones)
                {
                    return;
                }

                HeadlessPlayerId player = card.Owner;
                // AS-IS `if (card.Owner.CanHatch)` — an empty breeding area AND an available digi-egg. The raw
                // HatchDigitamaAsync only checks for an available egg, so the empty-breeding rule is re-checked
                // here (also keeping this re-run safe). Verbatim from BT1_089's [Main] hatch branch.
                if (zones.GetCards(player, ChoiceZone.BreedingArea).Count == 0
                    && zones.GetCards(player, ChoiceZone.DigitamaLibrary).Count > 0)
                {
                    await context.ZoneMover.HatchDigitamaAsync(player);
                }
            }
        }

        return cardEffects;
    }
}
