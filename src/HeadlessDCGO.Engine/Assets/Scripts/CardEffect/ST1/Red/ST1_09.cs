// Source: Assets/Scripts/CardEffect/ST1/Red/ST1_09.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, ST1 trigger wave).
//
// 1:1 mirror of the original ST1_09 (inherited):
//   [Your Turn] When this Digimon is blocked, gain 3 memory.  -> AddMemoryTriggerEffect (OnBlockAnyone)
// NOTE: the original builds an inline ActivateClass + coroutine; the headless uses the
// AddMemoryTriggerEffect helper (coroutine -> emitted AddMemory mutation). The [Your Turn] condition is a
// read-time predicate.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST1_09 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnBlockAnyone)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsOwnerTurn(card))
                {
                    return true;
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnBlockAnyone,
                amount: 3,
                isInheritedEffect: true,
                card: card,
                condition: Condition,
                description: "[Your Turn] When this Digimon is blocked, gain 3 memory."));
        }

        return cardEffects;
    }
}
