// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_044.cs
//   [When Attacking] Play 1 level 4 or lower digivolution card under this card as another Digimon without
//   paying its memory cost.
// AS-IS: ActivateClass on EffectTiming.OnAllyAttack (BT1_044.cs:15-19).
//   CanUseCondition (:46-49) = CanTriggerOnAttack(hashtable, card).
//   CanActivateCondition (:51-62) = IsExistOnBattleArea(card) &&
//     card.PermanentOfThisCard().DigivolutionCards.Count(CanSelectCardCondition) >= 1.
//   CanSelectCardCondition (:27-44) = IsDigimon -> CanPlayAsNewPermanent(payCost:false) -> Level <= 4 ->
//     HasLevel — over THIS card's own stack under-cards ONLY (:55/:89 customRootCardList:
//     selectedPermanent.DigivolutionCards where selectedPermanent = card.PermanentOfThisCard()).
//   ActivateCoroutine: SelectCardEffect(canNoSelect: () => false, maxCount: min(1, matching)), then
//     PlayPermanentCards(payCost:false, root: DigivolutionCards, activateETB:true). ORDER=-1, ISOPTIONAL=false.
// Headless mirror: uniform ActivatedEffect(canUse = CanTriggerOnAttack, canActivate = on-battle-area +
//   >= 1 matching own-stack under-card) whose body is ActivatedPlayFromUnderEffect scoped selfStackOnly
//   (AS-IS card.PermanentOfThisCard().DigivolutionCards) with canTarget = the AS-IS CanSelectCardCondition
//   (NOT the default all-owner-Digimon 2-level scope, and NOT the unfiltered "any Digimon under-card" gate).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_044 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            // AS-IS CanSelectCardCondition (BT1_044.cs:27-44), same predicate order.
            bool CanSelectCardCondition(CardSource cardSource) =>
                cardSource.IsDigimon
                && CardEffectCommons.CanPlayAsNewPermanent(cardSource, payCost: false, cardEffect: null)
                && cardSource.Level <= 4
                && cardSource.HasLevel;

            const string desc = "[When Attacking] Play 1 level 4 or lower digivolution card under this card as another Digimon without paying its memory cost.";
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                // AS-IS CanUseCondition (BT1_044.cs:46-49).
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                // AS-IS CanActivateCondition (BT1_044.cs:51-62): on the battle area AND this card's OWN
                // permanent holds >= 1 under-card passing CanSelectCardCondition.
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card)
                    && card.PermanentOfThisCard().DigivolutionCards
                        .Count(under => CanSelectCardCondition(new CardSource(card.Context, under.InstanceId, card.Owner))) >= 1,
                body: new ActivatedPlayFromUnderEffect(
                    card, desc,
                    canTarget: CanSelectCardCondition,   // AS-IS CanSelectCardCondition (replaces the default cardType filter)
                    isOptional: false,                   // AS-IS canNoSelect: () => false (mandatory pick)
                    selfStackOnly: true),                // AS-IS card.PermanentOfThisCard().DigivolutionCards (:55/:89)
                maxCountPerTurn: null, isOptional: false, description: desc));
        }

        return cardEffects;
    }
}
