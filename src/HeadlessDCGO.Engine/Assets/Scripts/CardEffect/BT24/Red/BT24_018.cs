// Source: DCGO/Assets/Scripts/CardEffect/BT24/Red/BT24_018.cs — "Styracomon".
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [All Turns][Once Per Turn]
// OnLoseSecurity branch — the F1-M1 activated-bridge witness.
//   [All Turns][Once Per Turn] When your opponent's security stack is removed from, you may delete 1 of their
//   Digimon. — AS-IS `new ActivateClass()` + SetUpActivateClass(..., 1, true, ...) (ORDER 1 = once per turn,
//   isOptional true = "you may") + SetHashString("BT24_18_AT_Sec_Removed") (BT24_018.cs:204-255). CanUse =
//   IsExistOnBattleAreaDigimon && CanTriggerWhenLoseSecurity(player == card.Owner.Enemy). CanActivate =
//   IsExistOnBattleAreaDigimon && HasMatchConditionPermanent(opponent battle-area Digimon). Body =
//   SelectPermanentEffect(Mode.Destroy, maxCount Min(1,count)).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `player => player == card.Owner.Enemy` ->
// `player.PlayerId == CardEffectCommons.OpponentOf(card)` (the Hashtable-overload playerCondition is a mirror
// Player; card.Owner.Enemy = the opponent seat); `GManager.instance.GetComponent<SelectPermanentEffect>()` + full
// AS-IS SetUp (bridge W4); `Func<Permanent,bool> CanSelectPermanentCondition` passed directly.
//
// STOP / design item RD-R6-06: the AS-IS [When Digivolving] branch (BT24_018.cs:75-196: trash 1 opponent security
// via the security-break UI + optional self-unsuspend) and the [All Turns] WhenRemoveField prevention
// (BT24_018.cs:260-351, Tier-3 PRE leave-hook, unopened until F-1 M3) remain OMITTED. The first needs the
// security-break `Effects.BreakSecurityEffect/DestroySecurityEffect` UI carriers (no headless surface — invention
// forbidden); the second is a not-yet-opened bridge tier. Orthogonal to the OnLoseSecurity bridge under test.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT24.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT24_018 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnLoseSecurity)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 of your opponent's Digimon?", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetHashString("BT24_18_AT_Sec_Removed");
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[All Turns] [Once Per Turn] When your opponent's security stack is removed from, you may delete 1 of their Digimon.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerWhenLoseSecurity(hashtable, player => player.PlayerId == CardEffectCommons.OpponentOf(card));
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                    CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition);
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

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
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 digimon to send to delete.", "Your opponent is selecting 1 digimon to delete.");

                    await selectPermanentEffect.Activate();
                }
            }
        }

        return cardEffects;
    }
}
