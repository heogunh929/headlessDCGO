// 1:1 mirror of the original BT1_074 (BT1/Green).
//   [When Digivolving] Reveal 3 cards from the top of your deck. Add 1 level 5 or higher Digimon card
//   among them to your hand. Place the remaining cards at the bottom of your deck in any order.
//   -> SimplifiedRevealDeckTopCardsAndSelect (reveal 3, select Lv5+ Digimon -> Hand, rest -> DeckBottom)
// Declared under WhenDigivolving (the DigivolveAction-wired timing that resolves activated selects via
// ActivatedEffectResolver), NOT OnEnterFieldAnyone — the bridge excludes OnEnterFieldAnyone, so an
// activated select/reveal there never fires live. Matches ST1_08/ST4_10/BT1_025's WhenDigivolving idiom.
// AS-IS CanUseCondition (CanTriggerWhenDigivolving) is subsumed by the DigivolveAction dispatch itself
// (only invoked for the card that is actually digivolving); AS-IS CanActivateCondition
// (IsExistOnBattleArea + LibraryCards.Count >= 1) is subsumed by the reveal effect's own no-op-if-empty
// behavior (SimplifiedRevealAndSelectEffect.ResolveAsync returns early when nothing is revealed) — same
// fold as ST4_10's dropped gate.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_074 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelectCardCondition(HeadlessEntityId id)
            {
                var revealed = new CardSource(card.Context, id, card.Controller, card.Owner);
                return revealed.IsDigimon && revealed.Level >= 5 && revealed.HasLevel;
            }

            cardEffects.Add(CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(
                card: card,
                revealCount: 3,
                conditions: new[]
                {
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: CanSelectCardCondition,
                        message: "Select 1 level 5 or higher Digimon card.",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),
                },
                remainingTo: RevealDestination.DeckBottom,
                description: "[When Digivolving] Reveal 3 cards from the top of your deck. Add 1 level 5 or higher Digimon card among them to your hand. Place the remaining cards at the bottom of your deck in any order."));
        }

        return cardEffects;
    }
}
