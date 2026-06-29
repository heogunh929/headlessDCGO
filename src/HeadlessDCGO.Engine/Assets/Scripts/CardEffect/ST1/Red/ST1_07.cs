// Source: Assets/Scripts/CardEffect/ST1/Red/ST1_07.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, ST1 wave 1).
//
// 1:1 mirror of the original ST1_07: inherited <Security Attack +1>.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST1_07 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: true, card: card, condition: null));
        }

        return cardEffects;
    }
}
