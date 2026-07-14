// TEST FIXTURE (not a real card). An UNCAPPED [All Turns] discard reactor: whenever a card the OWNER controls is
// trashed from the owner's hand / security / deck BY AN EFFECT, the owner gains 1 memory — with NO once-per-turn
// cap. This is the witness the real cards (ST16_14 / BT19_071, both effectively single-fire — suspend cost /
// [Once Per Turn]) cannot be: uncapped, the observable splits the F1-Tier1 batch-collapse behaviour —
//   * an effect discarding N cards in ONE batch (one shared discard/security-loss id) gains +1 IFF the collapse
//     fires the reactor once (+N if the bridge fired per CardMoved).
//   * two INDEPENDENT discard batches (distinct ids) in one drain gain +2 (the id distinguishes them).
//   * a NON-effect security loss (attack security-CHECK reveal — no cause id) gains +0 for OnDiscardSecurity
//     (CardEffect!=null gate), while still gaining for OnLoseSecurity (a different reactor). Inert in actual play.
//
// One class serves all three discard timings; each block mirrors its AS-IS CanUse gate (OnTrashHand.cs /
// WhenDiscardSecurity.cs / WhenDiscardLibrary.cs) with a self-owner card any-match.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): `MemoryBody(1)` ->
// `card.Owner.AddMemory(1, activateClass)`; the hand/security gates keep their AS-IS "CardEffect != null" idiom
// (the Hashtable overloads enforce it internally — `cardEffect => true` mirrors the old `cardEffectSourceCondition: null`).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxDiscardCounter : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDiscardHand)
        {
            // AS-IS CanTriggerOnTrashHand: CardEffect != null (effect-driven) + a discarded card in YOUR hand.
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerOnTrashHand(hashtable, cardEffect => true, cardSource => cardSource.Owner == card.Owner);
            AddMemoryReactor(effects, card, CanUseCondition,
                "[All Turns] When an effect trashes a card from your hand, gain 1 memory (uncapped).");
        }

        if (timing == EffectTiming.OnDiscardSecurity)
        {
            // AS-IS CanTriggerOnTrashSecurity: CardEffect != null (effect-driven) + a discarded card in YOUR security.
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerOnTrashSecurity(hashtable, cardEffect => true, cardSource => cardSource.Owner == card.Owner);
            AddMemoryReactor(effects, card, CanUseCondition,
                "[All Turns] When an effect trashes a card from your security, gain 1 memory (uncapped).");
        }

        if (timing == EffectTiming.OnDiscardLibrary)
        {
            // AS-IS CanTriggerWhenDiscardLibrary: NO CardEffect check — just a discarded card from YOUR deck.
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenDiscardLibrary(hashtable, cardSource => cardSource.Owner == card.Owner);
            AddMemoryReactor(effects, card, CanUseCondition,
                "[All Turns] When an effect trashes a card from your deck, gain 1 memory (uncapped).");
        }

        return effects;
    }

    private static void AddMemoryReactor(
        List<ICardEffect> effects, CardSource card, Func<Hashtable, bool> canUseCondition, string description)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Memory +1", canUseCondition, card);
        activateClass.SetUpActivateClass(
            hashtable => CardEffectCommons.IsExistOnBattleArea(card),
            _hashtable => card.Owner.AddMemory(1, activateClass),
            -1, false, description);
        effects.Add(activateClass);
    }
}
