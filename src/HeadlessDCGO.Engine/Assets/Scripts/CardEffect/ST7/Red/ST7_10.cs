// Source: Assets/Scripts/CardEffect/ST7/Red/ST7_10.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1 vertical slice).
//
// 1:1 mirror of the original DCGO ST7_10 (DCGO/Assets/Scripts/CardEffect/ST7/Red/ST7_10.cs):
//   [All Turns] <Security Attack +1>            -> ChangeSelfSAttackStaticEffect (continuous, main)
//   [When Attacking] <Piercing>                 -> PierceSelfEffect (OnDetermineDoSecurityCheck)
// Both effects are unconditional (condition: null) and main (isInheritedEffect: false), so they map
// directly onto the existing continuous-modifier / keyword gates with no conditional gating needed.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST7.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST7_10 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        return cardEffects;
    }
}
