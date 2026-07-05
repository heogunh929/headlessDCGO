// Source: Assets/Scripts/CardEffect/BT1/BT1_026.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, BT1 wave 1).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_026 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        return cardEffects;
    }
}