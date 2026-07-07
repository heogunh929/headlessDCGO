// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_078.cs
// 1:1 mirror of the original BT1_078 (BT1/Green).
//   [When Attacking] Reveal 3 cards from the top of your deck. You can digivolve this card into 1 level 6
//   green Digimon card among them without paying its memory cost. Place the remaining cards at the bottom
//   of your deck in any order.
// AS-IS: ActivateClass on OnAllyAttack, CanUseCondition = CanTriggerOnAttack, CanActivateCondition =
//   IsExistOnBattleArea && Owner.LibraryCards.Count >= 1, ORDER=-1, ISOPTIONAL=false. ActivateCoroutine =
//   SimplifiedRevealDeckTopCardsAndSelect(revealCount:3, ONE SimplifiedSelectCardCondition mode:Custom
//   [level==6, HasCardColor(Green), CanPlayCardTargetFrame(this card's own PermanentFrame, payCost:false),
//   HasLevel], maxCount:1, remainingCardsPlace:DeckBottom, canNoSelect:true) THEN, if a card was Custom-
//   selected, PlayCardClass(selectedCard, payCost:false, targetPermanent: card.PermanentOfThisCard(),
//   root:Library, activateETB:true) — digivolve the reveal-selected library card onto THIS card's own
//   battle-area permanent for free.
//
// Headless mirror: RevealSelectThenFreeDigivolveSelfEffect (an IActivatedCardEffect resolved via the
//   activation flow / ActivatedEffectResolver). It reveals the top 3 library cards, opens an optional
//   (canNoSelect) pick of 1 matching card, sends the remaining revealed cards to the deck bottom, then
//   free-digivolves the selected card onto this card via FreeDigivolveHelpers.DigivolveFreeAsync (the same
//   costless single-target digivolve mechanic Blast/Arts use), registering the new top's effects. The
//   candidate gate mirrors CanPlayCardTargetFrame via DigivolveAction.TryGetEvolutionCost (digivolution
//   legality + requirement) plus the ContinuousRestrictionGate.EvaluateDigivolve (CanNotEvolve) check.
//   [When Attacking] fires at OnAllyAttack, subject-scoped to this attacker (the AS-IS CanTriggerOnAttack
//   gate), and the effect self-guards on library>=1 (the AS-IS CanActivate).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_078 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            // AS-IS CanSelectCardCondition: level==6 && HasCardColor(Green) && IsExistOnBattleArea(this) &&
            // CanPlayCardTargetFrame(this card's own permanent, payCost:false) && HasLevel.
            bool CanSelect(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner);
                return candidate.Level == 6
                    && candidate.HasCardColor("Green")
                    && candidate.HasLevel
                    && CardEffectCommons.IsExistOnBattleArea(card)
                    && DigivolveAction.TryGetEvolutionCost(card.Context, id, card.InstanceId, out _, out _)
                    && !ContinuousRestrictionGate.EvaluateDigivolve(card.Context, card.InstanceId).IsRestricted;
            }

            cardEffects.Add(new RevealSelectThenFreeDigivolveSelfEffect(
                card,
                revealCount: 3,
                canSelect: CanSelect,
                description: "[When Attacking] Reveal 3 cards from the top of your deck. You can digivolve this card into 1 level 6 green Digimon card among them without paying its memory cost. Place the remaining cards at the bottom of your deck in any order."));
        }

        return cardEffects;
    }
}
