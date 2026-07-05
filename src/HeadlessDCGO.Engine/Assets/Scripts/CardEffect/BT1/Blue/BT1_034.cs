namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_034 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsOwnerTurn(card);
            }

            cardEffects.Add(CardEffectFactory.CanNotBeBlockedStaticSelfEffect(
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}