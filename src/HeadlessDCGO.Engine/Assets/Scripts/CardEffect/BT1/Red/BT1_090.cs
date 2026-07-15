// Source: DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_090.cs — an Option (single timing).
//   [Main] Gain 2 memory. At end of turn, lose 2 memory.
// AS-IS (BT1_090.cs): ONE ActivateClass on OptionSkill — CanUseCondition = CanTriggerOptionMainEffect (:26-29),
// canActivate = null, ORDER=-1, ISOPTIONAL=false. The ActivateCoroutine (:31-59) gains +2 THEN builds a NESTED
// ActivateClass "Memory -2" (CanUse/CanActivate always-true, body = AddMemory(-2)) and registers it at player
// scope via CardEffectCommons.AddEffectToPlayer(UntilEachTurnEnd, card, activateClass1, OnEndTurn) — a one-shot
// -2 that fires at the end of THIS turn only.
//
// R3-C2b-2 RE-PORT (old-model ActivatedEffect + MemoryGainThenScheduledReversalBody -> AS-IS 1:1 ActivateClass):
// the pre-R3 STOP (R6P-EOT-PLAYER-EFFECTLIST) is resolved — since the R3-C2 window flip, AutoProcessing.GetSkillInfos
// enumerates player.EffectList(OnEndTurn), so the bucket-stored "Memory -2" ActivateClass fires through the live
// end-of-turn window and the per-duration bucket clear resets it. Substrate: IEnumerator->async Task,
// StartCoroutine->await; AS-IS `card.Owner` (Player) -> mirror HeadlessPlayerId AddMemory extension.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT1_090 : CEntity_Effect
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
                return "[Main] Gain 2 memory. At end of turn, lose 2 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(2, activateClass);

                ActivateClass activateClass1 = new ActivateClass();
                activateClass1.SetUpICardEffect("Memory -2", CanUseCondition1, card);
                activateClass1.SetUpActivateClass(CanActivateCondition1, ActivateCoroutine1, -1, false, EffectDiscription1());
                CardEffectCommons.AddEffectToPlayer(effectDuration: EffectDuration.UntilEachTurnEnd, card: card, cardEffect: activateClass1, timing: EffectTiming.OnEndTurn);

                string EffectDiscription1()
                {
                    return "Lose 2 memory.";
                }

                bool CanUseCondition1(Hashtable hashtable)
                {
                    return true;
                }

                bool CanActivateCondition1(Hashtable hashtable)
                {
                    return true;
                }

                async Task ActivateCoroutine1(Hashtable _hashtable1)
                {
                    await card.Owner.AddMemory(-2, activateClass1);
                }
            }
        }

        return cardEffects;
    }
}
