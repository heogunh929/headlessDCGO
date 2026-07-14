// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_107.cs — an Option.
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Main] branch; the
// [Security] branch already reuses [Main] via AddActivateMainOptionSecurityEffect (unchanged).
//   [Main] Trigger <Recovery +1 (Deck)>. (Place the top card of your deck on top of your security stack.)
// AS-IS: ActivateClass on OptionSkill, CanUseCondition = CanTriggerOptionMainEffect, CanActivateCondition = null,
//   ORDER=-1, ISOPTIONAL=false. ActivateCoroutine = new IRecovery(card.Owner, 1, activateClass).Recovery().
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; IRecovery ctor -> mirror
//   (EngineContext, HeadlessPlayerId, count, cause) shape.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_107 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Trigger <Recovery +1 (Deck)>. (Place the top card of your deck on top of your security stack.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new IRecovery(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Recovery();
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card,
                cardEffects: ref cardEffects,
                effectName: $"Recovery +1 (Deck)");
        }

        return cardEffects;
    }
}
