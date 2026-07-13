namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST5;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST5_11 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(
                isInheritedEffect: true,
                card: card,
                condition: null));
        }

        return cardEffects;
    }
}