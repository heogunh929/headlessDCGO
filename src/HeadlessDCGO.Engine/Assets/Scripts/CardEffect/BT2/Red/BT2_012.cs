// Source: DCGO/Assets/Scripts/CardEffect/BT2/Red/BT2_012.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_012 (BT2/Red).
//   [When Attacking] When this Digimon attacks a player, it gets +4000 DP for the turn.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelfDpBuffTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure.
// FIDELITY NOTE: the previous pass's factory call collapsed CanUseCondition down to just
// `CanTriggerOnAttack` via its `triggerGate` parameter, SILENTLY DROPPING the AS-IS
// `GManager.instance.attackProcess.DefendingPermanent == null` gate (the actual "attacks A PLAYER, not a
// blocker" condition this card's text requires) — restored verbatim below.
// Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// `card.PermanentOfThisCard()` used as the `Permanent` argument to `ChangeDigimonDP` ->
// `ICardEffect.ResolvePermanentOfThisCard(card)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT2_012 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DP +4000", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] When this Digimon attacks a player, it gets +4000 DP for the turn";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanTriggerOnAttack(hashtable, card))
                {
                    if (GManager.instance.attackProcess.DefendingPermanent == null)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    await CardEffectCommons.ChangeDigimonDP(targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card), changeValue: 4000, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                }
            }
        }

        return cardEffects;
    }
}
