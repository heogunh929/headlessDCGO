// Source: Assets/Scripts/CardEffect/BT2/BT2_058.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect.
//
// 1:1 mirror of the original BT2_058: Blocker (static, non-inherited),
// and CanNotAttack on owner's turn (static, non-inherited).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_058 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                return CardEffectCommons.IsOwnerTurn(card);
            }

            cardEffects.Add(CardEffectFactory.CanNotAttackSelfStaticEffect(
                defenderCondition: null,
                isInheritedEffect: false,
                card: card,
                condition: Condition,
                effectName: "Can't Attack"));
        }

        return cardEffects;
    }
}