// 1:1 mirror of the original ST4_03 (ST4/Green) — a Digimon.
//   [On Play] Reveal the top card of your deck. If it's a green Digimon card, add it to your hand.
//             Otherwise, place it at the bottom of your deck.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && Owner.LibraryCards.Count >= 1, ORDER=-1, ISOPTIONAL=false,
//   ActivateCoroutine = RevealDeckTopCardsAndProcessForAll(revealCount:1, SimplifiedSelectCardConditionClass
//   (green Digimon -> Mode.AddHand), remainingCardsPlace: DeckBottom).
//   Headless mirror: CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect (AS-IS
//   SimplifiedRevealDeckTopCardsAndSelect) — the [On Play] play path (PlayCardAction) resolves this card's own
//   OnEnterFieldAnyone effects directly (subject = this card), so CanTriggerOnPlay / IsExistOnBattleArea are
//   structurally satisfied; the "library >= 1" precondition is covered by the primitive's own no-op-if-empty
//   reveal (SimplifiedRevealAndSelectEffect.ResolveAsync: `if (revealed.Count == 0) return;`).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_03 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool IsGreenDigimon(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                return candidate.IsDigimon && candidate.HasCardColor("Green");
            }

            var condition = new SimplifiedSelectCardConditionClass(
                canTargetCondition: IsGreenDigimon,
                message: "",
                selectedTo: RevealDestination.Hand,
                maxCount: -1);

            cardEffects.Add(CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(
                card,
                revealCount: 1,
                conditions: new[] { condition },
                remainingTo: RevealDestination.DeckBottom,
                description: "[On Play] Reveal the top card of your deck. If it's a green Digimon card, add it to your hand. Otherwise, place it at the bottom of your deck."));
        }

        return cardEffects;
    }
}
