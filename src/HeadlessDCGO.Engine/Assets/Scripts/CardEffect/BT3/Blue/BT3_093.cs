// Source: Assets/Scripts/CardEffect/BT3/Blue/BT3_093.cs — a Tamer, mixed timings.
//   [Start of Your Turn] If you have 2 or less memory, set your memory to 3. -> SetMemoryTo3TamerEffect
//     (OnStartTurn), verbatim (same factory match as BT1_086).
//   [On Play] Reveal the top 3 cards of your deck. Add 1 blue and 1 green Digimon card among them to your
//     hand. Place the remaining cards at the bottom of your deck in any order. AS-IS: ActivateClass on
//     OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay, CanActivateCondition = IsExistOnBattleArea &&
//     LibraryCards.Count>=1, ActivateCoroutine = SimplifiedRevealDeckTopCardsAndSelect(revealCount:3,
//     [blue-Digimon -> AddHand maxCount 1, green-Digimon -> AddHand maxCount 1], remainingCardsPlace:
//     DeckBottom, mutualConditions:true).
//   Headless mirror: CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect with two
//     SimplifiedSelectCardConditionClass passes (Blue Digimon -> Hand maxCount 1; Green Digimon -> Hand
//     maxCount 1), remainingTo: DeckBottom. SimplifiedRevealAndSelectEffect's resolve loop ALWAYS excludes
//     already-picked cards from later passes (CardPortingFramework.cs:3048-3057, `picked` HashSet) — i.e. it
//     unconditionally implements the AS-IS mutualConditions:true behaviour, so no separate parameter is
//     needed (same idiom as BT1_074/BT1_048, declared under OnEnterFieldAnyone since this is [On Play], not
//     [When Digivolving] — PlayCardAction resolves this card's own OnEnterFieldAnyone effects directly).
//   [Security] Play this Tamer. -> PlaySelfTamerSecurityEffect (security-skill flow, mirrors ST1_12/ST2_12/
//     ST3_12/ST4_14/BT1_086).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_093 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool IsBlueDigimon(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                return candidate.IsDigimon && candidate.HasCardColor("Blue");
            }

            bool IsGreenDigimon(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                return candidate.IsDigimon && candidate.HasCardColor("Green");
            }

            cardEffects.Add(CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect(
                card: card,
                revealCount: 3,
                conditions: new[]
                {
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: IsBlueDigimon,
                        message: "Select 1 blue Digimon card.",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),
                    new SimplifiedSelectCardConditionClass(
                        canTargetCondition: IsGreenDigimon,
                        message: "Select 1 green Digimon card.",
                        selectedTo: RevealDestination.Hand,
                        maxCount: 1),
                },
                remainingTo: RevealDestination.DeckBottom,
                description: "[On Play] Reveal the top 3 cards of your deck. Add 1 blue and 1 green Digimon card among them to your hand. Place the remaining cards at the bottom of your deck in any order."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
