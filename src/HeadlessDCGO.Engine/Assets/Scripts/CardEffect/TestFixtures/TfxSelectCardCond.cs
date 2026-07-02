// TEST FIXTURE (not a real card). [Main] uses the fuller SelectCardConditionClass descriptor in the same
// reveal-select mechanism (reveal top 3, add 1 matching Tamer to hand, rest to deck bottom) via
// SimplifiedRevealAndSelectEffect + SelectCardConditionClass.ToSimplified(). Used by tests/PRIM-W2 (G9-029).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxSelectCardCond : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool IsTamer(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) && inst is not null
                && card.Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
                && def.IsCardType("Tamer");

            var condition = new SelectCardConditionClass(
                canTargetCondition: IsTamer,
                canTargetConditionByPreSelectedList: null,
                canEndSelectCondition: null,
                canNoSelect: true,
                message: "Select 1 Tamer card.",
                maxCount: 1,
                canEndNotMax: false,
                selectedTo: RevealDestination.Hand);

            cardEffects.Add(new SimplifiedRevealAndSelectEffect(
                card,
                revealCount: 3,
                conditions: new[] { condition.ToSimplified() },
                remainingTo: RevealDestination.DeckBottom,
                description: "Reveal 3, add 1 Tamer to hand, rest to deck bottom (SelectCardConditionClass)."));
        }

        return cardEffects;
    }
}
