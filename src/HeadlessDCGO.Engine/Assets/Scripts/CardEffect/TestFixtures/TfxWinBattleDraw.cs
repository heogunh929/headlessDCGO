// TEST FIXTURE (not a real card). "[End of Battle] if this card's permanent won the battle, draw 1" — a
// TRIGGERED ACTIVATED effect at OnEndBattle gated by CanTriggerWhenWinBattle. Exercises the EVENT-BROADCAST
// bridge for OnEndBattle: the reacting card must read the driving event's "event.winnerIds" metadata, which
// only reaches it if OnEndBattle broadcasts the driving event (was previously a boundary timing that
// discarded it). Same shape/gap-class as ST4_11.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): the ctx-form CanTriggerWhenWinBattle
// "this card's permanent won" maps to the Hashtable overload winnerCondition = permanent.cardSources.Contains(card);
// the `DrawBody(1)` body becomes the AS-IS `new DrawClass(...).Draw()` coroutine idiom (BT1_046).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxWinBattleDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEndBattle)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "[End of Battle] if this card's permanent won the battle, draw 1.";

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.CanTriggerWhenWinBattle(hashtable, permanent => permanent.cardSources.Contains(card));

            bool CanActivateCondition(Hashtable hashtable) => true;

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Draw();
            }
        }

        return effects;
    }
}
