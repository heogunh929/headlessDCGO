// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_079.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the original
// BT1_079 (BT1/Green).
//   [When Attacking] Suspend 1 of your opponent's Digimon without <Blocker>.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local
// functions, SetIsInheritedEffect(true) (AS-IS BT1_079.cs:19). Substrate translations only: IEnumerator->Task,
// StartCoroutine->await; the AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` is expressed verbatim
// on the canonical Func<Permanent,bool> shape (id-flip 3b); AS-IS `permanent.HasBlocker` -> (R1-c) the rehoused
// getter, negated for "without <Blocker>"; `GManager.instance.GetComponent<SelectPermanentEffect>()` -> bridge W4.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_079 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Suspend 1 Digimon without Blocker", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Suspend 1 of your opponent's Digimon without <Blocker>.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (!permanent.HasBlocker)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: maxCount,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Tap,
                    cardEffect: activateClass);

                await selectPermanentEffect.Activate();
            }
        }

        return cardEffects;
    }
}
