// TEST FIXTURE (not a real card). An UNCAPPED [All Turns] OnLoseSecurity reactor: whenever a card is removed
// from its OWNER'S security stack, the owner gains 1 memory — with NO once-per-turn cap. This is the witness the
// real BT15_037 / BT24_018 cannot be (both are [Once Per Turn], so their cap ALONE collapses N fires to one,
// hiding whether the F1-M1 P1-1 security-loss batch-collapse is doing anything). Uncapped, the observable splits:
//   * an effect trashing N security cards in ONE batch (one shared security-loss id) gains +1 IFF the collapse
//     fires the reactor once — +N if the bridge fired per CardMoved.
//   * an attack security CHECK of N cards (per-card, unstamped reveals, each in its own per-iteration window)
//     gains +N — proving the collapse does NOT wrongly merge the per-card check path.
// Self-scope (player == card.Owner), mirroring BT15_037's OnLoseSecurity player gate. Inert in actual play.
//
// R6-C CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass): the Hashtable-overload
// playerCondition is a mirror `Player`; `player == card.Owner` -> `player.PlayerId == card.Owner` (BT8_057 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnLoseSecurityCounter : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnLoseSecurity)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "[All Turns] When a card is removed from your security stack, gain 1 memory (uncapped).";

            // AS-IS CanUseCondition mirror: IsExistOnBattleArea && CanTriggerWhenLoseSecurity(player == card.Owner).
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerWhenLoseSecurity(hashtable, player => player.PlayerId == card.Owner);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return effects;
    }
}
