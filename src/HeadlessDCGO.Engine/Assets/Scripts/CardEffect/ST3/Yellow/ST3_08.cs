// Source: DCGO/Assets/Scripts/CardEffect/ST3/Yellow/ST3_08.cs
// TRUE AS-IS-verbatim re-port (ST3 Yellow batch). 1:1 mirror of the original ST3_08 (ST3/Yellow).
//   [When Attacking] 1 of your opponent's Digimon gets -1000 DP for the turn.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndBuffDpEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure, matching
// the established `SelectPermanentEffect` selection pattern (BT1_017/023/092/094 pattern).
// AS-IS structure kept verbatim: inline ActivateClass, SetIsInheritedEffect(true).
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// the AS-IS `Func<Permanent,bool>` CanSelectPermanentCondition is kept on the canonical `Func<Permanent,bool>`
// shape (id-flip 3b — `HasMatchConditionPermanent`/`MatchConditionPermanentCount`, and
// `SelectPermanentEffect.SetUp`'s `canTargetCondition`, all take this shape directly).
// AS-IS `GManager.instance.GetComponent<SelectPermanentEffect>()` + full `SetUp(...)` + `.Activate()` resolves
// via the bridge-W4 GManager.GetComponent<T>()/SelectPermanentEffect support.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST3_08 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DP -1000", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] 1 of your opponent's Digimon gets -1000 DP for the turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
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
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
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
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage(
                        "Select 1 Digimon that will get DP -1000.",
                        "The opponent is selecting 1 Digimon that will get DP -1000.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonDP(
                            targetPermanent: permanent,
                            changeValue: -1000,
                            effectDuration: EffectDuration.UntilEachTurnEnd,
                            activateClass: activateClass);
                    }
                }
            }
        }

        return cardEffects;
    }
}
