// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_051.cs — a Digimon.
// 1:1 mirror of the original BT3_051.
//   [On Play] Reveal the top 3 cards of your deck. Add 1 level 5 and 1 level 6 Digimon card among them to
//   your hand. Trash the remaining cards.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && Owner.LibraryCards.Count >= 1, ORDER=-1,
//   ISOPTIONAL=false. ActivateCoroutine = SimplifiedRevealDeckTopCardsAndSelect(revealCount:3, conditions:
//   [level 5 Digimon -> AddHand maxCount:1, level 6 Digimon -> AddHand maxCount:1], remainingCardsPlace:
//   Trash, mutualConditions:true — a card picked for one condition cannot also be picked for the other).
// Headless mirror: CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect — the headless
// SimplifiedRevealAndSelectEffect ALREADY tracks picks across conditions within one reveal pass (a `picked`
// set filters candidates for every subsequent condition, BT3_008 precedent), so AS-IS's mutualConditions
// disjointness is the primitive's default behaviour — no separate flag needed. The [On Play] path
// (PlayCardAction) resolves this card's own OnEnterFieldAnyone effects directly (subject = this card), so
// CanTriggerOnPlay/IsExistOnBattleArea are structurally satisfied; the "library >= 1" precondition is covered
// by the primitive's own no-op-if-empty reveal.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_051 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectLevel5Digimon(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                return candidate.IsDigimon && candidate.HasLevel && candidate.Level == 5;
            }

            bool CanSelectLevel6Digimon(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                return candidate.IsDigimon && candidate.HasLevel && candidate.Level == 6;
            }

            cardEffects.Add(CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(
                card: card,
                revealCount: 3,
                conditions: new[]
                {
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: CanSelectLevel5Digimon,
                        message: "Select 1 level 5 Digimon card.",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: CanSelectLevel6Digimon,
                        message: "Select 1 level 6 Digimon card.",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),
                },
                remainingTo: RevealDestination.Trash,
                description: "[On Play] Reveal the top 3 cards of your deck. Add 1 level 5 and 1 level 6 Digimon card among them to your hand. Trash the remaining cards."));
        }

        return cardEffects;
    }
}
