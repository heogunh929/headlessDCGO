// Source: Assets/Scripts/CardEffect/BT2/BT2_055.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect.
//
// 1:1 mirror of the original BT2_055: inherited Reboot static effect, no condition.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_055 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(isInheritedEffect: true, card: card, condition: null));
        }

        return cardEffects;
    }
}