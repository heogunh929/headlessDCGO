// TEST FIXTURE. Effect-driven DNA Digivolution: DNA-digivolve INTO a hand card ("INTO") by fusing a battle-area
// permanent ("PERM") with a hand material ("MAT") (AS-IS DNADigivolveWithHandOrTrashCardIntoHandOrTrash).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class TfxDnaFromHand : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OptionSkill)
        {
            effects.Add(CardEffectFactory.DnaDigivolveFromHandOrTrashEffect(
                card,
                intoCondition: cs => cs.CardNumber == "INTO",
                permanentCondition: cs => cs.CardNumber == "PERM",
                materialCondition: cs => cs.CardNumber == "MAT",
                intoFromHand: true, materialFromHand: true));
        }
        return effects;
    }
}
