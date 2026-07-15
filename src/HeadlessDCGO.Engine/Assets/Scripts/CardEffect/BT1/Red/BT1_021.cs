// Source: DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_021.cs — a Digimon.
//   [When Attacking] Gain 3 memory. At end of turn lose 3 memory.
// AS-IS (BT1_021.cs): ONE ActivateClass on OnAllyAttack — CanUseCondition = CanTriggerOnAttack (:22-25),
// CanActivateCondition = IsExistOnBattleArea (:27-30), ORDER=-1, ISOPTIONAL=false. The ActivateCoroutine
// (:32-49) gains +3 THEN registers the "-3 at end of turn" reversal by ADDING a deferred selector to the
// owner's UntilEachTurnEnd bucket (card.Owner.UntilEachTurnEndEffects.Add(GetCardEffect) returning
// CardEffectFactory.EoTLose3Memory(card) at OnEndTurn) — so the loss exists ONLY per activation (attack twice
// = two -3 entries; never attack = no loss).
//
// R3-C2b-2 RE-PORT (old-model ActivatedEffect + MemoryGainThenScheduledReversalBody -> AS-IS 1:1 ActivateClass):
// the pre-R3 STOP (R6P-EOT-PLAYER-EFFECTLIST) is resolved — since the R3-C2 window flip, AutoProcessing.GetSkillInfos
// enumerates player.EffectList(OnEndTurn), so the bucket-stored EoTLose3Memory ActivateClass fires through the live
// end-of-turn window and the per-duration bucket clear resets it. Substrate: IEnumerator->async Task,
// StartCoroutine->await; AS-IS `card.Owner` (Player) -> mirror HeadlessPlayerId AddMemory extension for the +3, and
// `new Player(card.Context, card.Owner)` for the UntilEachTurnEndEffects bucket. The AS-IS
// `GManager.instance.GetComponent<Effects>().CreateBuffEffect(...)` (Effects.cs:1433) is pure VFX/SE — stripped.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_021 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +3", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Gain 3 memory. At end of turn lose 3 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(3, activateClass);

                new Player(card.Context, card.Owner).UntilEachTurnEndEffects.Add(GetCardEffect);

                ICardEffect GetCardEffect(EffectTiming _timing)
                {
                    if (_timing == EffectTiming.OnEndTurn)
                    {
                        return CardEffectFactory.EoTLose3Memory(card);
                    }

                    return null!;
                }

                // AS-IS `GManager.instance.GetComponent<Effects>().CreateBuffEffect(card.PermanentOfThisCard())`
                // — pure VFX/SE (Effects.cs:1433), stripped.
            }
        }

        return cardEffects;
    }
}
