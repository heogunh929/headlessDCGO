// STOP: [On Play] “Place 1 [Durandamon] or [BryweLudramon] from your hand on top of this card's digivolution cards to gain 3 memory”
// requires a top-insert-to-own-digivolution-card operation in an interactive flow, which is not represented in
// the currently available CardEffectFactory catalog.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_019 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = [];

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(
                changeValue: 1,
                isInheritedEffect: false,
                card: card,
                condition: null));

            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        return cardEffects;
    }
}
