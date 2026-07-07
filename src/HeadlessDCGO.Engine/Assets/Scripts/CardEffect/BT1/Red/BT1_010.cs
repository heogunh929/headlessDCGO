// 1:1 mirror of the original BT1_010 (BT1/Red) — a Tamer.
//   [On Play] Reveal 5 cards from the top of your deck. Add 1 Tamer card among them to your hand. Place
//             the remaining cards at the bottom of your deck in any order.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && Owner.LibraryCards.Count >= 1, ORDER=-1, ISOPTIONAL=false,
//   ActivateCoroutine = SimplifiedRevealDeckTopCardsAndSelect(revealCount:5, SimplifiedSelectCardConditionClass
//   (IsTamer -> Mode.AddHand, maxCount:1), remainingCardsPlace: DeckBottom).
//   Headless mirror: CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect (AS-IS
//   SimplifiedRevealDeckTopCardsAndSelect) — the [On Play] play path (PlayCardAction) resolves this card's own
//   OnEnterFieldAnyone effects directly (subject = this card), so CanTriggerOnPlay / IsExistOnBattleArea are
//   structurally satisfied; the "library >= 1" precondition is covered by the primitive's own no-op-if-empty
//   reveal (SimplifiedRevealAndSelectEffect.ResolveAsync: `if (revealed.Count == 0) return;`) — same fold as
//   ST4_03/ST4_10's dropped IsExistOnBattleArea/LibraryCards gate.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_010 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectCardCondition(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                return candidate.IsTamer;
            }

            var condition = new SimplifiedSelectCardConditionClass(
                canTargetCondition: CanSelectCardCondition,
                message: "Select 1 Tamer card.",
                selectedTo: RevealDestination.Hand,
                maxCount: 1);

            cardEffects.Add(CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(
                card,
                revealCount: 5,
                conditions: new[] { condition },
                remainingTo: RevealDestination.DeckBottom,
                description: "[On Play] Reveal 5 cards from the top of your deck. Add 1 Tamer card among them to your hand. Place the remaining cards at the bottom of your deck in any order."));
        }

        return cardEffects;
    }
}
