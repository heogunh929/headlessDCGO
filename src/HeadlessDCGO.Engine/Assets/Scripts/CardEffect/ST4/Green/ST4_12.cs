// Source: DCGO/Assets/Scripts/CardEffect/ST4/Green/ST4_12.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original ST4_12 (ST4/Green).
//   [When Digivolving] Choose 1 of your opponent's Digimon. That Digimon can't attack or block until the
//   end of their next turn.
// AS-IS declares this under EffectTiming.OnEnterFieldAnyone (gated by CanTriggerWhenDigivolving) — the
// established headless bridge dispatch key for [When Digivolving] activated-select effects is
// EffectTiming.WhenDigivolving instead (see BT1_025.cs precedent / ST4_10.cs this batch).
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndRestrictEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` + `SetUp(...)` (Mode.Custom) +
// `SelectPermanentCoroutine` structure (see BT1_043.cs for the identically-shaped Mode.Custom precedent).
// AS-IS structure kept verbatim, INCLUDING the body-head re-guard (isExistOnField + GetBattleAreaDigimons()
// .Contains(PermanentOfThisCard()) + HasMatchConditionPermanent) that duplicates CanActivateCondition — not
// simplified/dropped, per the no-simplification rule.
// Substrate translation only: IEnumerator->Task; `yield return ContinuousController.instance.StartCoroutine(X)`
// -> `await X`; the AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` is kept Permanent-shaped as the
// local `PermanentCondition(Permanent)` fed directly to HasMatchConditionPermanent/MatchConditionPermanentCount
// AND SelectPermanentEffect.SetUp's canTargetCondition (id-flip 3b canonical overload — no id-shape sibling
// needed); AS-IS
// `card.Owner.GetBattleAreaDigimons().Contains(card.PermanentOfThisCard())` -> `new Player(card.Context,
// card.Owner).GetBattleAreaDigimons()` (the established bare-HeadlessPlayerId -> Player instantiation idiom,
// see BT1_104.cs/BT1_110.cs) + `.Any(permanent => permanent.InstanceId == card.PermanentOfThisCard()
// .TopInstanceId)` (the established Permanent-vs-PermanentView identity idiom applied to `.Contains`).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_12 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Can't Attack or Block", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] 1 of your opponent's Digimon can't attack or block until the end of their next turn.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (isExistOnField(card))
                {
                    if (new Player(card.Context, card.Owner).GetBattleAreaDigimons()
                        .Any(permanent => permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId))
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition))
                        {
                            int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, PermanentCondition));

                            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                            selectPermanentEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: PermanentCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: maxCount,
                                canNoSelect: false,
                                canEndNotMax: false,
                                selectPermanentCoroutine: SelectPermanentCoroutine,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.Custom,
                                cardEffect: activateClass);

                            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get effects.", "The opponent is selecting 1 Digimon that will get effects.");

                            await selectPermanentEffect.Activate();

                            async Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                await CardEffectCommons.GainCanNotAttack(
                                    targetPermanent: permanent,
                                    defenderCondition: null,
                                    effectDuration: EffectDuration.UntilOpponentTurnEnd,
                                    activateClass: activateClass,
                                    effectName: "Can't Attack");

                                await CardEffectCommons.GainCanNotBlock(
                                    targetPermanent: permanent,
                                    attackerCondition: null,
                                    effectDuration: EffectDuration.UntilOpponentTurnEnd,
                                    activateClass: activateClass,
                                    effectName: "Can't Block");
                            }
                        }
                    }
                }
            }
        }

        return cardEffects;
    }
}
