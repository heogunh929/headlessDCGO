// Source: DCGO/Assets/Scripts/CardEffect/ST1/Red/ST1_08.cs
// TRUE AS-IS-verbatim re-port (ST1/Red batch). 1:1 mirror of the original ST1_08.
//   [When Digivolving] 1 of your Digimon gets +3000 DP for the turn.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndBuffDpEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS structure: gated by `CanTriggerWhenDigivolving`,
// inline `new ActivateClass()` + `GManager.instance.GetComponent<SelectPermanentEffect>()` select flow
// (bridge W4 — see BT1_017.cs). Registration timing = the mirror WhenDigivolving dispatch key (AS-IS uses
// OnEnterFieldAnyone; see the DISPATCH REMAP note at the timing check below).
// AS-IS structure kept verbatim: inline ActivateClass, nested select + local ActivateCoroutine.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `CanSelectPermanentCondition(Permanent permanent)` kept on the canonical `Func<Permanent,bool>` shape
// (id-flip 3b — SelectPermanentEffect.SetUp's canTargetCondition takes the Permanent predicate directly);
// AS-IS `CardEffectCommons.HasMatchConditionPermanent(CanSelectPermanentCondition)` /
// `MatchConditionPermanentCount(CanSelectPermanentCondition)` (global scan, no CardSource arg in AS-IS) ->
// mirror's `(card, condition)` overloads (same substrate adaptation already established across the ported
// corpus, e.g. BT1_017/BT1_104).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST1_08 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // DISPATCH REMAP (batch-3): AS-IS registers under EffectTiming.OnEnterFieldAnyone (the shared
        // enter-field timing, discriminated by the CanTriggerWhenDigivolving hashtable gate). The mirror
        // engine dispatches [When Digivolving] activated effects on the DEDICATED WhenDigivolving key
        // (DigivolveAction.cs ResolveAsync(..., EffectTiming.WhenDigivolving)) and plain plays on
        // OnEnterFieldAnyone — registering under the literal AS-IS key would silently never fire on digivolve
        // (batch-2 BT1_025/BT1_062 established this exact remap; the AS-IS gate below is kept verbatim).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Your 1 Digimon gains DP +3000", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] 1 of your Digimon gets +3000 DP for the turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
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
                        "Select 1 Digimon that will get DP +3000.",
                        "The opponent is selecting 1 Digimon that will get DP +3000.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonDP(
                            targetPermanent: permanent,
                            changeValue: 3000,
                            effectDuration: EffectDuration.UntilEachTurnEnd,
                            activateClass: activateClass);
                    }
                }
            }
        }

        return cardEffects;
    }
}
