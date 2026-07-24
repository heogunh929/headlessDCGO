// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_064.cs
// 1:1 mirror of the original BT3_064 (single branch, inherited).
//   [When Attacking] If this Digimon is level 7, trigger <De-Digivolve 1> on 1 of your opponent's Digimon.
//   -> SelectAndDeDigivolveEffect (OnAllyAttack, opponent Digimon, de-digivolve count 1). CanUseCondition =
//      CanTriggerOnAttack (the OnAllyAttack timing itself); CanActivateCondition = at least 1 matching
//      opponent Digimon exists AND self is level 7 (folded into an outer gate around the Add, matching the
//      EX8_074 idiom for an extra CanActivateCondition beyond plain target selection).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_064 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            bool CanActivate()
            {
                HeadlessEntityId topId = card.PermanentOfThisCard().TopInstanceId;
                if (topId.IsEmpty)
                {
                    return false;
                }

                var permanent = new Permanent(card.Context, topId, card.Owner);
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition) >= 1
                    && permanent.Level == 7
                    && permanent.TopCard.HasLevel;
            }

            if (CanActivate())
            {
                cardEffects.Add(CardEffectFactory.SelectAndDeDigivolveEffect(
                    card: card,
                    canTarget: CanSelectPermanentCondition,
                    maxCount: 1,
                    count: 1,
                    canEndNotMax: false,
                    description: "[When Attacking] If this Digimon is level 7, trigger <De-Digivolve 1> on 1 of your opponent's Digimon. (Trash a card from the top of one of your opponent's Digimon. If it has no digivolution cards, or becomes a level 3 Digimon, you can't trash any more cards.)"));
            }
        }

        return cardEffects;
    }
}
