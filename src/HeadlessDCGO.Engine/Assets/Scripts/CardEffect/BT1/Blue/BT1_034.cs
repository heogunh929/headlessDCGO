namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_034 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsOwnerTurn(card);
            }

            bool DefenderCondition(Permanent defender)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleArea(defender, card))
                {
                    if (defender.HasNoDigivolutionCards)
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.CanNotBeBlockedStaticSelfEffect(
                defenderCondition: DefenderCondition,
                isInheritedEffect: true,
                card: card,
                condition: Condition,
                effectName: "Can't be Blocked by a Digimon with no digivolution cards"));
        }

        return cardEffects;
    }
}