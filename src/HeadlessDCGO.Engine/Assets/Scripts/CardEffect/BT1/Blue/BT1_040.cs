// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_040.cs (WereGarurumon)
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_040 (BT1/Blue).
//   [When Attacking] Gain 3 memory. At end of turn lose 3 memory.
// AS-IS gains 3 THEN registers the "-3 at OnEndTurn" as a delayed player effect via
// `card.Owner.UntilEachTurnEndEffects.Add(GetCardEffect)` — the exact AS-IS list-add this batch's
// `CardEffectCommons.AddEffectToPlayer(effectDuration, card, cardEffect, timing)` bridges (its doc comment
// cites the SAME AS-IS source, GiveEffectToPermanentOrPlayer.cs:57), so the call translates 1:1 to that
// bridge (same idiom BT1_021 already established for this exact "gain now + EoT reversal" shape).
// `GManager.instance.GetComponent<Effects>().CreateBuffEffect(...)` (AS-IS Effects.cs:1433) is pure UI/VFX/SE
// (GameObject instantiate + AudioClip, no state) — stripped, per the standard UI-elision convention.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

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

                // AS-IS `card.Owner.UntilEachTurnEndEffects.Add(GetCardEffect)` -> the bridged
                // AddEffectToPlayer(UntilEachTurnEnd, card, cardEffect, timing) call (see file header).
                CardEffectCommons.AddEffectToPlayer(EffectDuration.UntilEachTurnEnd, card, CardEffectFactory.EoTLose3Memory(card), EffectTiming.OnEndTurn);

                // AS-IS `GManager.instance.GetComponent<Effects>().CreateBuffEffect(card.PermanentOfThisCard())`
                // — pure VFX/SE (Effects.cs:1433), stripped (see file header).
            }
        }

        return cardEffects;
    }
}