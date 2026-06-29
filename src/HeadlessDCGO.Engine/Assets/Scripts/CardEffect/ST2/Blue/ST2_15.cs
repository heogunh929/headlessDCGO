// 1:1 mirror of the original ST2_15 (ST2/Blue) — an Option.
//   [Main] Choose a Digimon digivolution card placed under 1 of your Digimon and play it as another
//          Digimon without paying its memory cost.  -> play-from-under flow (not yet wired)
//   [Security] (use the Main effect)                -> AddActivateMainOptionSecurityEffect
// The "play a card from under a permanent as a fresh Digimon without paying cost" path is a dedicated
// special-play flow that is not yet ported (docs/audit/card_porting_recipe.md §5). Represented as a
// DeferredCardEffect for 1:1 source fidelity; it compiles and is skipped by auto-registration.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST2_15 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(new DeferredCardEffect(
                "[Main] Play a Digimon digivolution card from under 1 of your Digimon as another Digimon without paying its cost (play-from-under flow, Wave 3)."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Play a digivolution card as another Digimon");
        }

        return cardEffects;
    }
}
