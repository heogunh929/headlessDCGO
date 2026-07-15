// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_040.cs (WereGarurumon)
// TRUE AS-IS-verbatim re-port. 1:1 mirror of the original BT1_040 (BT1/Blue) — identical shape to BT1_021.
//   [When Attacking] Gain 3 memory. At end of turn lose 3 memory.
// AS-IS gains 3 THEN registers the "-3 at OnEndTurn" reversal by ADDING a deferred selector to the owner's
// UntilEachTurnEnd bucket (`card.Owner.UntilEachTurnEndEffects.Add(GetCardEffect)` returning
// CardEffectFactory.EoTLose3Memory(card) at OnEndTurn) — per-activation loss (attack twice = two -3 entries).
// R3-C2b-2: since the R3-C2 window flip, AutoProcessing.GetSkillInfos enumerates player.EffectList(OnEndTurn),
// so the bucket-stored EoTLose3Memory ActivateClass fires through the live end-of-turn window. Substrate:
// IEnumerator->async Task, StartCoroutine->await; AS-IS `card.Owner` (Player) -> mirror HeadlessPlayerId AddMemory
// extension for the +3, and `new Player(card.Context, card.Owner)` for the UntilEachTurnEndEffects bucket.
// `GManager.instance.GetComponent<Effects>().CreateBuffEffect(...)` (AS-IS Effects.cs:1433) is pure UI/VFX/SE — stripped.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_040 : CEntity_Effect
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
                // — pure VFX/SE (Effects.cs:1433), stripped (see file header).
            }
        }

        return cardEffects;
    }
}