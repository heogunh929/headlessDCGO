// Source: Assets/Scripts/CardEffect/BT3/Red/BT3_015.cs (a Red Digimon, mixed timings)
// AS-IS has two branches:
//   [Security] This Digimon gets Piercing. (OnDetermineDoSecurityCheck) -> PierceSelfEffect(isInheritedEffect: false).
//   [When Digivolving] You may return 1 level 7 Digimon card with [Virus] in its attribute from your trash
//   to your hand. -> ActivateClass on OnEnterFieldAnyone gated by CanTriggerWhenDigivolving; optional
//   (canNoSelect:true) select 1 own trash card (level 7, Virus attribute), mode SelectCardEffect.Mode.AddHand.
//
// STOP (genuine primitive gap, [When Digivolving] branch only): no headless activated factory selects a
// card OUT OF THE TRASH ZONE and routes it to hand (grepped CardPortingFramework.cs's IActivatedCardEffect
// catalog: ActivatedSelectEffect only targets battle-area permanents; ActivatedSelectAndPlayEffect plays a
// zone card onto the battle area, not to hand; ActivatedSelectAndDeDigivolveEffect/
// ActivatedSelectTrashDigivolutionEffect only remove digivolution sources; SimplifiedRevealAndSelectEffect /
// RevealMultiSelectEffect operate on the REVEALED-from-deck pool, not the trash zone directly). Composing a
// "select 1 of the owner's trash cards matching a condition, optional, to hand" activated body is a new
// primitive, out of scope for a single-card porting pass. No cardEffects registered for WhenDigivolving.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_015 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        // STOP: [When Digivolving] "You may return 1 level 7 Digimon card with [Virus] in its attribute
        // from your trash to your hand." — needs a select-from-trash-to-hand activated body that does not
        // exist yet (see file header).
        // if (timing == EffectTiming.WhenDigivolving) { ... }

        return cardEffects;
    }
}
