// Source: Assets/Scripts/CardEffect/ST1/Red/ST1_06.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, ST1 trigger wave).
//
// 1:1 mirror of the original ST1_06:
//   [All Turns] <Blocker>                       -> BlockerSelfStaticEffect (continuous)
//   [When Attacking] Lose 2 memory.             -> AddMemoryTriggerEffect (OnAllyAttack)
// NOTE: the original builds an inline ActivateClass + coroutine for the memory trigger; the headless uses
// the AddMemoryTriggerEffect helper (coroutine -> emitted AddMemory mutation). 1:1 is relaxed for trigger
// plumbing (see docs/audit/card_porting_recipe.md §5).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST1_06 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                amount: -2,
                isInheritedEffect: false,
                card: card,
                condition: null,
                description: "[When Attacking] Lose 2 memory."));
        }

        return cardEffects;
    }
}
