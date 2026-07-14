// TEST FIXTURE (not a real card). "When one of your opponent's Digimon's digivolution cards is trashed,
// draw 1" — a TRIGGERED ACTIVATED effect at OnDigivolutionCardDiscarded, gated like the AS-IS BT2_085
// (CanTriggerOnTrashDigivolutionCard + opponent-host permanentCondition). Exercises the EVENT-BROADCAST
// bridge: the listener is NOT the event subject (the host Digimon is), so it only fires if the bridge
// scans the field and threads the driving event (subject + discardedCardIds) into the gate.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): 1:1 with BT2_085's gate shape
// (CanTriggerOnTrashDigivolutionCard(permanentCondition, cardEffect => true, cardSource => true)); the `DrawBody(1)`
// body becomes the AS-IS `new DrawClass(...).Draw()` coroutine idiom (BT1_046).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxDigiTrashDraw : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDigivolutionCardDiscarded)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "When one of your opponent's Digimon's digivolution cards is trashed, draw 1.";

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.CanTriggerOnTrashDigivolutionCard(
                    hashtable,
                    permanent => CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card),
                    cardEffect => true,
                    cardSource => true);

            bool CanActivateCondition(Hashtable hashtable) => true;

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Draw();
            }
        }

        return effects;
    }
}
