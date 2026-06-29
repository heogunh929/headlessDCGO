// 1:1 mirror of the original ST2_13 (ST2/Blue) — an Option.
//   [Main] Gain 1 memory.       -> GainMemoryActivatedEffect (OptionSkill, +1)
//   [Security] Gain 2 memory.   -> GainMemoryActivatedEffect (SecuritySkill, +2)
// Resolved imperatively until the interactive activation flow is wired.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST2_13 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(CardEffectFactory.GainMemoryActivatedEffect(card, amount: 1, description: "[Main] Gain 1 memory."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.GainMemoryActivatedEffect(card, amount: 2, description: "[Security] Gain 2 memory."));
        }

        return cardEffects;
    }
}
