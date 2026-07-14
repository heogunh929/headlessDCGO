// TEST FIXTURE (not a real card). The OPPONENT-scope mirror of TfxOnAddCounter: UNCAPPED [All Turns] "card added
// to zone" reactors that gain the OWNER 1 memory when the OPPONENT'S hand / security grows (player != owner) — the
// AS-IS "when a card is added to your OPPONENT'S hand/security" player-scope form. Used to prove the F1-Tier1 OnAdd
// activated bridge fires POSITIVELY on an opponent-scope gate (the existing suite only had opponent-scope negatives
// against a SELF-scope reactor). NO once-per-turn cap and NO suspend cost so the observable is the raw fire count.
//   * OnAddHand (opponent-scope, cause REQUIRED) — fires +1 when an EFFECT adds cards to the opponent's hand.
//   * OnAddSecurity (opponent-scope, NO cause) — fires +1 PER card added to the opponent's security (per-card).
// Inert in actual play (no such card exists).
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): `MemoryBody(1)` ->
// `card.Owner.AddMemory(1, activateClass)`; the Hashtable-overload playerCondition is a mirror `Player` so
// `player != owner` -> `player.PlayerId != card.Owner`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnAddCounterOpp : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAddHand)
        {
            // AS-IS CanTriggerWhenAddHand player-scope OPPONENT form: subject owner != card.Owner, effect-driven
            // (cause != null — the AS-IS cardEffect != null idiom).
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenAddHand(hashtable,
                    player => player.PlayerId != card.Owner, cardEffect => cardEffect != null);
            AddMemoryReactor(effects, card, CanUseCondition,
                "[All Turns] When an effect adds cards to your OPPONENT'S hand, gain 1 memory (uncapped).");
        }

        if (timing == EffectTiming.OnAddSecurity)
        {
            // AS-IS CanTriggerWhenAddSecurity player-scope OPPONENT form: subject owner != card.Owner. Per-card.
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenAddSecurity(hashtable, player => player.PlayerId != card.Owner);
            AddMemoryReactor(effects, card, CanUseCondition,
                "[All Turns] When a card is added to your OPPONENT'S security, gain 1 memory (uncapped).");
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
