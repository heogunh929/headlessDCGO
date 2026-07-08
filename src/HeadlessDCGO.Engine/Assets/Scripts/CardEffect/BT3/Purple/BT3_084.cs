// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_084.cs
// 1:1 mirror of the original BT3_084 (BT3/Purple).
//   [On Play] Reveal the top 3 cards of your deck. Add 1 Option card among them to your hand. Trash the
//   remaining cards.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && library >= 1, ActivateCoroutine =
//   CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect(revealCount:3, [AddHand pass over IsOption,
//   maxCount:1], remainingCardsPlace: Trash).
//   -> CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(card, revealCount:3,
//      [SimplifiedSelectCardConditionClass(IsOption, "...", RevealDestination.Hand, maxCount:1)],
//      remainingTo: RevealDestination.Trash, description). The [On Play] play path (PlayCardAction)
//      resolves this card's own OnEnterFieldAnyone effects directly (subject = this card), so
//      CanTriggerOnPlay / IsExistOnBattleArea are structurally satisfied; the library>=1 gate is covered
//      by the reveal primitive's own available-card fold (same pattern as BT1_011's dropped trash-count
//      gate).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_084 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectCardCondition(HeadlessEntityId id) =>
                new CardSource(card.Context, id, card.Owner, card.Owner).IsOption;

            cardEffects.Add(CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(
                card,
                revealCount: 3,
                conditions: new[]
                {
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: CanSelectCardCondition,
                        message: "Select 1 Option card.",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),
                },
                remainingTo: RevealDestination.Trash,
                description: "[On Play] Reveal the top 3 cards of your deck. Add 1 Option card among them to your hand. Trash the remaining cards."));
        }

        return cardEffects;
    }
}
