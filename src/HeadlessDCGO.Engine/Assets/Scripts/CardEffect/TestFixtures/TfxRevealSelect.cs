// TEST FIXTURE (not a real card). [Main] (OptionSkill) reveals the top 3 library cards, lets you add 1
// matching Tamer to hand, and sends the rest to the deck bottom — mirrors the original
// SimplifiedRevealDeckTopCardsAndSelect + SimplifiedSelectCardConditionClass (Mode.AddHand). Used by
// tests/BT-PRE-A2 (G9-016). Inert in actual play (no real card numbered "TfxRevealSelect").

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxRevealSelect : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool IsTamer(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) && inst is not null
                && card.Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
                && string.Equals(def.CardType, "Tamer", StringComparison.OrdinalIgnoreCase);

            var condition = new SimplifiedSelectCardConditionClass(
                canTargetCondition: IsTamer,
                message: "Select 1 Tamer card.",
                selectedTo: RevealDestination.Hand,
                maxCount: 1);

            cardEffects.Add(new SimplifiedRevealAndSelectEffect(
                card,
                revealCount: 3,
                conditions: new[] { condition },
                remainingTo: RevealDestination.DeckBottom,
                description: "Reveal 3, add 1 Tamer to hand, rest to deck bottom."));
        }

        return cardEffects;
    }
}
