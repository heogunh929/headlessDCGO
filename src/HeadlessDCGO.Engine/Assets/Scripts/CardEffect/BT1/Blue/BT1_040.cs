// Source: Assets/Scripts/CardEffect/BT1/BT1_040.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, BT1 trigger wave).
//
// 1:1 mirror of the original BT1_040 (WereGarurumon):
//   [When Attacking] Gain 3 memory.             -> AddMemoryTriggerEffect (OnAllyAttack, amount: +3)
//   At end of turn lose 3 memory.               -> EoTLose3Memory (registered from OnAllyAttack, fires EoT)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_040 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                amount: 3,
                isInheritedEffect: false,
                card: card,
                condition: null,
                description: "[When Attacking] Gain 3 memory. At end of turn lose 3 memory."));
            cardEffects.Add(CardEffectFactory.EoTLose3Memory(card));
        }

        return cardEffects;
    }
}