// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_029.cs
// 1:1 headless mirror. Original: [On Play] (OnEnterFieldAnyone, CanTriggerOnPlay) -> <Draw 1>.
// The [On Play] activated effect is resolved when the card is played (PlayCardAction resolves the just-played
// card's OnEnterFieldAnyone activated effects through the activation flow). The AS-IS CanActivateCondition
// (library >= 1) is subsumed by DrawCardsEffect drawing only what the deck holds.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_029 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            cardEffects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
        }
        return cardEffects;
    }
}
