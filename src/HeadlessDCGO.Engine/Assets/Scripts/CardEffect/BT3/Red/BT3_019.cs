// Source: Assets/Scripts/CardEffect/BT3/Red/BT3_019.cs (a Red Digimon, mixed timings)
// AS-IS has three branches:
//   [Continuous] +1 security attack (unconditional). -> ChangeSelfSAttackStaticEffect(changeValue: 1,
//     isInheritedEffect: false, condition: null) (timing None).
//   [Continuous] This Digimon gets Reboot (unconditional). -> RebootSelfStaticEffect(isInheritedEffect:
//     false, condition: null) (timing None).
//   [When Digivolving] You may place 1 [Durandamon] or [BryweLudramon] from your hand on top of this card's
//     digivolution cards to gain 3 memory. -> ActivateClass on OnEnterFieldAnyone gated by
//     CanTriggerWhenDigivolving; optional (canNoSelect:true) select 1 own hand card (name match), mode
//     SelectHandEffect.Mode.Custom, then AddDigivolutionCardsTop(selectedCards) + AddMemory(3).
//
// STOP (genuine primitive gap, [When Digivolving] branch only): no headless activated factory selects a
// card FROM HAND and places it on top of THIS card's own digivolution stack (grepped
// CardPortingFramework.cs's IActivatedCardEffect catalog: no "AddDigivolutionCardsTop"/select-from-hand-
// onto-own-stack primitive exists; SelectAndDeDigivolveEffect only removes sources, SelectAndPlayFromZoneEffect
// plays a card to the battle area as its own new permanent, not onto an existing stack). The dependent memory
// gain (GainMemoryActivatedEffect) also needs to be conditional on the selection actually happening
// (canNoSelect), which the same missing hook would carry. Composing this "select from hand, add to own
// digivolution stack, gain memory" activated body is a new primitive, out of scope for a single-card
// porting pass. No cardEffects registered for WhenDigivolving.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_019 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        // STOP: [When Digivolving] "You may place 1 [Durandamon] or [BryweLudramon] from your hand on top
        // of this card's digivolution cards to gain 3 memory." — needs a select-from-hand-onto-own-stack
        // activated body that does not exist yet (see file header).
        // if (timing == EffectTiming.WhenDigivolving) { ... }

        return cardEffects;
    }
}
