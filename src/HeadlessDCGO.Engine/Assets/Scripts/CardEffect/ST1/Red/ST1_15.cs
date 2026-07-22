// Source: DCGO/Assets/Scripts/CardEffect/ST1/Red/ST1_15.cs
// TRUE AS-IS-verbatim re-port (ST1/Red batch). 1:1 mirror of the original ST1_15 (Option).
//   [Main]     Delete up to 2 of your opponent's Digimon with 4000 DP or less.
//   [Security] (use the Main effect)
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndDestroyEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` select flow (bridge W4 — see BT1_017.cs), Mode
// = Destroy. The `[Security]` block already calls the REAL AS-IS `CardEffectCommons.
// AddActivateMainOptionSecurityEffect(...)` (verified against multiple AS-IS cards, e.g. BT2_091), so it is
// left untouched.
// AS-IS structure kept verbatim: `SetUpActivateClass(null, ...)` (CanActivateCondition IS null), `canNoSelect:
// false, canEndNotMax: true`, the `CanEndSelectCondition` local guarding against an empty selection list.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `CanSelectPermanentCondition(Permanent permanent)` -> the established `Func<HeadlessEntityId,bool>`
// idiom (see BT1_017.cs); AS-IS `permanent.DP <= card.Owner.MaxDP_DeleteEffect(4000, activateClass)` (a
// raise-able threshold) -> `CardEffectCommons.CurrentDp(card, id) <= CardEffectCommons.MaxDpDeleteThreshold(
// card, baseThreshold: 4000)` (established mirror of the same raise-able-cap semantics, over the id idiom).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST1_15 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Delete up to 2 of your opponent's Digimon with 4000 DP or less.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                {
                    if (CardEffectCommons.CurrentDp(card, id) <= new Player(card.Context, card.Owner).MaxDP_DeleteEffect(4000, activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    int maxCount = Math.Min(2, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: CanEndSelectCondition,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: true,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();

                    bool CanEndSelectCondition(List<Permanent> permanents)
                    {
                        if (CardEffectCommons.HasNoElement(permanents))
                        {
                            return false;
                        }

                        return true;
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: $"Delete up to 2 Digimon with 4000 DP or less");
        }

        return cardEffects;
    }
}
