// TEST FIXTURE (not a real card). "[Your Turn] when one of your OPPONENT'S Digimon is suspended, gain 1
// memory" — a TRIGGERED ACTIVATED effect at OnTappedAnyone gated by CanTriggerWhenPermanentSuspends over an
// opponent-Digimon predicate. Exercises the EVENT-BROADCAST bridge for OnTappedAnyone: the reacting card is a
// DIFFERENT card than the suspended subject, so it only fires if OnTappedAnyone broadcasts (was previously
// subject-scoped). Same cross-card gap-class as ST4_14 (fixture gains memory, no suspend cost, to isolate the
// bridge-delivery check).
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): 1:1 with ST4_14's gate shape;
// `MemoryBody(1)` -> `card.Owner.AddMemory(1, activateClass)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOppSuspendMemory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnTappedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "[Your Turn] when an opponent's Digimon is suspended, gain 1 memory.";

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.CanTriggerWhenPermanentSuspends(
                    hashtable, permanent => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card));

            bool CanActivateCondition(Hashtable hashtable) => true;

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return effects;
    }
}
