// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_082.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the original
// BT1_082 (BT1/Green).
//   [Opponent's Turn] When an opponent's Digimon attacks a player, if this Digimon is suspended, spend 1 of
//   your opponent's Digimon.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local
// functions (registered on EffectTiming.OnAllyAttack — the AS-IS timing key for this hook, discriminated by
// the CanTriggerOnPermanentAttack gate over the ATTACKING permanent being an opponent's permanent). Substrate
// translations only: IEnumerator->Task, StartCoroutine->await; AS-IS `permanent.HasBlocker` -> the self-static
// keyword gate (negated); `GManager.instance.attackProcess.DefendingPermanent == null` (the attack targets the
// player directly) -> `card.Context.AttackController.Current.IsDirectAttack` (established idiom, BT1_001
// sibling's opposite-phrasing check); "if this Digimon is suspended" -> `CardEffectCommons.IsSuspended(card,
// ICardEffect.ResolvePermanentOfThisCard(card).TopInstanceId)` (the BT1_053 convention);
// `GManager.instance.GetComponent<SelectPermanentEffect>()` -> bridge W4.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_082 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Suspend 1 Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Opponent's Turn] When an opponent's Digimon attacks a player, if this Digimon is suspended, spend 1 of your opponent's Digimon.";
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

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsOpponentPermanent(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOpponentTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, PermanentCondition))
                        {
                            if (card.Context.AttackController.Current.IsDirectAttack)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        if (CardEffectCommons.IsSuspended(card, ICardEffect.ResolvePermanentOfThisCard(card).InstanceId))
                        {
                            return true;
                        }
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
