// 1:1 mirror of the original BT3_039 (BT3/Yellow).
//   [When Digivolving] 1 of your opponent's Digimon gains <Security Attack -2> (checks 2 fewer security
//              cards) until the end of your opponent's next turn.
//              -> SelectAndBuffSAttackEffect (opponent Digimon, -2, UntilOpponentTurnEnd)
//   [When Attacking] If you have 3 or fewer security cards, you may play 1 yellow level 3 Digimon card from
//              your hand without paying its memory cost.
//              -> SelectAndPlayFromZoneEffect (Hand, cost-free); the security<=3 gate and the
//              Digimon/Yellow/Level-3/CanPlayAsNewPermanent target predicate are folded into canTarget —
//              when the gate fails there are zero candidates, mirroring the AS-IS CanActivateCondition gate.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_039 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffSAttackEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: -2,
                duration: EffectDuration.UntilOpponentTurnEnd,
                description: "[When Digivolving] 1 of your opponent's Digimon gains <Security Attack -2> (This Digimon checks 2 fewer security cards) until the end of your opponent's next turn."));
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectCardCondition(HeadlessEntityId id)
            {
                if (CardEffectCommons.SecurityCount(card) > 3)
                {
                    return false;
                }

                CardSource cardSource = new CardSource(card.Context, id, card.Owner, card.Owner);
                return cardSource.IsDigimon
                    && cardSource.HasCardColor("Yellow")
                    && cardSource.Level == 3
                    && cardSource.HasLevel
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource, payCost: false, cardEffect: null);
            }

            // AS-IS canNoSelect:true (the owner may decline entirely) maps to canEndNotMax:true on this
            // primitive — it has no separate canNoSelect knob (minCount = canEndNotMax ? 0 : max; canSkip =
            // canEndNotMax), so canEndNotMax:true reproduces "0 selections allowed" for maxCount:1.
            cardEffects.Add(CardEffectFactory.SelectAndPlayFromZoneEffect(
                card: card,
                fromZone: ChoiceZone.Hand,
                canTarget: CanSelectCardCondition,
                maxCount: 1,
                canEndNotMax: true,
                description: "[When Attacking] If you have 3 or fewer security cards, you may play 1 yellow level 3 Digimon card from your hand without paying its memory cost."));
        }

        return cardEffects;
    }
}
