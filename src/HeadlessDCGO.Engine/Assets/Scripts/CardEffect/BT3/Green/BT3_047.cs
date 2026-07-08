// Source: Assets/Scripts/CardEffect/BT3/Green/BT3_047.cs — a Digimon.
// 1:1 mirror of the original BT3_047.
//   [On Deletion] Reveal the top 3 cards of your deck. Add 1 level 4 or 5 Digimon card among them to your
//   hand. Place the remaining cards at the bottom of your deck in any order.
//   AS-IS: ActivateClass on EffectTiming.OnDestroyedAnyone, CanUseCondition = CanTriggerOnDeletion,
//   CanActivateCondition = CanActivateOnDeletion(hashtable, card) && Owner.LibraryCards.Count >= 1, ORDER=-1,
//   ISOPTIONAL=false. ActivateCoroutine = SimplifiedRevealDeckTopCardsAndSelect(revealCount:3, conditions:[level
//   4 or 5 Digimon -> AddHand, maxCount:1], remainingCardsPlace: DeckBottom).
// Headless mirror: CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect (AS-IS
// SimplifiedRevealDeckTopCardsAndSelect, verbatim) registered under OnDestroyedAnyone — the [On Deletion]
// bridge resolves this card's own deletion trigger directly (subject = this card), so CanTriggerOnDeletion is
// structurally satisfied (same fold as the [On Play]/OnEnterFieldAnyone convention, e.g. BT1_048/BT1_055);
// the "library >= 1" precondition is covered by the primitive's own no-op-if-empty reveal
// (SimplifiedRevealAndSelectEffect.ResolveAsync: `if (revealed.Count == 0) return;`).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_047 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            bool CanSelectLevel4Or5Digimon(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                return candidate.IsDigimon && candidate.HasLevel && (candidate.Level == 4 || candidate.Level == 5);
            }

            cardEffects.Add(CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(
                card: card,
                revealCount: 3,
                conditions: new[]
                {
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: CanSelectLevel4Or5Digimon,
                        message: "Select 1 level 4 or 5 Digimon card.",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),
                },
                remainingTo: RevealDestination.DeckBottom,
                description: "[On Deletion] Reveal the top 3 cards of your deck. Add 1 level 4 or 5 Digimon card among them to your hand. Place the remaining cards at the bottom of your deck in any order."));
        }

        return cardEffects;
    }
}
