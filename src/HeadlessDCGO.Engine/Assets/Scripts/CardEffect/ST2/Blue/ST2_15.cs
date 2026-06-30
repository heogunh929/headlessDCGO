// 1:1 mirror of the original ST2_15 (ST2/Blue) — an Option.
//   [Main] Choose a Digimon digivolution card placed under 1 of your Digimon and play it as another
//          Digimon without paying its memory cost.  -> ActivatedPlayFromUnderEffect (G10-007)
//   [Security] (use the Main effect)                -> AddActivateMainOptionSecurityEffect
// The play-from-under move (a Digimon under-card leaves its host and becomes a fresh battle-area Digimon
// cost-free) is realized by the PlayDigivolutionAsDigimon mutation, auto-registering the new permanent.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST2_15 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(new ActivatedPlayFromUnderEffect(card,
                "[Main] Choose a Digimon digivolution card placed under 1 of your Digimon and play it as another Digimon without paying its memory cost."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Play a digivolution card as another Digimon");
        }

        return cardEffects;
    }
}
