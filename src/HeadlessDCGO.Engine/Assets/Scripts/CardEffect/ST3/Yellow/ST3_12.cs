// 1:1 mirror of the original ST3_12 (ST3/Yellow) — a Tamer.
//   [All Turns] During your opponent's turn, your Security Digimon get +2000 DP.
//     -> ChangeSecurityDigimonCardDPStaticEffect (continuous, owner Security-zone Digimon, opponent-turn condition)
//   [Security] Play this Tamer.  -> PlaySelfTamerSecurityEffect (security-skill flow, Wave 3 — deferred)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST3_12 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool CardCondition(CardSource cardSource)
            {
                return cardSource.Owner == card.Owner;
            }

            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOpponentTurn(card);
            }

            cardEffects.Add(CardEffectFactory.ChangeSecurityDigimonCardDPStaticEffect(
                cardCondition: CardCondition,
                changeValue: 2000,
                isInheritedEffect: false,
                card: card,
                condition: Condition,
                effectName: "Your Security Digimon gains DP +2000"));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
