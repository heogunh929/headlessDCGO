// TEST FIXTURE (not a real card). UNCAPPED [All Turns] "card added to zone" reactors that gain the owner 1 memory
// with NO once-per-turn cap and NO suspend cost — so the observable directly splits the F1-Tier1 OnAdd batch model
// that the real single-fire witnesses (BT8_090 / BT15_083, both suspend-cost) would mask:
//   * OnAddHand (self-scope, cause REQUIRED) — an effect adding N cards to hand in ONE batch (one shared add-hand
//     id) gains +1 IFF the collapse fires the reactor once (+N if per-CardMoved); two INDEPENDENT hand-add batches
//     gain +2; a NON-effect add (no cause id — a turn/mulligan draw) gains +0 (the CardEffect!=null gate).
//     Player-scope: adding to the OPPONENT'S hand gains +0 (player != owner).
//   * OnAddSecurity (self-scope, NO cause) — AS-IS fires PER SINGLE card (per IAddSecurity), so N cards added to
//     security gain +N (NO batch collapse). Player-scope: adding to the OPPONENT'S security gains +0.
// Inert in actual play (no such card exists).
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): `MemoryBody(1)` ->
// `card.Owner.AddMemory(1, activateClass)`. The self OnAddHand cause-required form is the AS-IS
// CanTriggerWhenAddHand(player == owner, cardEffect != null) — behaviour-identical to the old CanTriggerOnHandAdded;
// the Hashtable-overload playerCondition is a mirror `Player` so `player == owner` -> `player.PlayerId == card.Owner`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnAddCounter : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAddHand)
        {
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenAddHand(hashtable,
                    player => player.PlayerId == card.Owner, cardEffect => cardEffect != null);
            AddMemoryReactor(effects, card, CanUseCondition,
                "[All Turns] When an effect adds cards to your hand, gain 1 memory (uncapped).");
        }

        if (timing == EffectTiming.OnAddSecurity)
        {
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenAddSecurity(hashtable, player => player.PlayerId == card.Owner);
            AddMemoryReactor(effects, card, CanUseCondition,
                "[All Turns] When a card is added to your security, gain 1 memory (uncapped).");
        }

        return effects;
    }

    // Builds an UNCAPPED memory-gain ActivateClass whose body is the AS-IS AddMemory coroutine; CanActivate = the
    // shared IsExistOnBattleArea gate (verbatim from the old fixture — no CanAddMemory gate added).
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
