// TEST FIXTURE (not a real card). Two OptionSkill activated effects: one [Main]-tagged (gains 1 memory) and one
// NOT [Main]-tagged (draws 1 card). Used by tests/FAILa-13 to prove ActivateMainOfOptionSide / the [Security]
// [Main]-reuse resolve ONLY the [Main] effect (AS-IS OptionMainEffect discriminator), not every OptionSkill
// effect. Inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxMainDisc : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            effects.Add(new ActivatedEffect(card, EffectTiming.OptionSkill, canUse: null, canActivate: null,
                body: new MemoryBody(1), maxCountPerTurn: null, isOptional: false, description: "[Main] Gain 1 memory."));
            effects.Add(new ActivatedEffect(card, EffectTiming.OptionSkill, canUse: null, canActivate: null,
                body: new DrawBody(1), maxCountPerTurn: null, isOptional: false, description: "Draw 1 card."));
        }
        return effects;
    }
}
