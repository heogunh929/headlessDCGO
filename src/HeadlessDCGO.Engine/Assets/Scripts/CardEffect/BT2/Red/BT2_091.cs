// Source: DCGO/Assets/Scripts/CardEffect/BT2/Red/BT2_091.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_091 (BT2/Red, an Option).
//   [Main]     Delete 1 of your opponent's Digimon with 4000 DP or less.
//   [Security] (use the Main effect, custom description)
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndDestroyEffect(...)` call (an invented
// helper — explicitly prohibited/retired) with the literal AS-IS inline `new ActivateClass()` structure +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` (Mode.Destroy) selection pattern (bridge W4).
// `CardEffectCommons.AddActivateMainOptionSecurityEffect` on SecuritySkill IS the real AS-IS call (verbatim).
// Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` (`permanent.DP <= card.Owner.
// MaxDP_DeleteEffect(4000, activateClass)`) -> the established entity-id predicate idiom
// (`CardEffectCommons.CurrentDp(card, id) <= CardEffectCommons.MaxDpDeleteThreshold(card, baseThreshold: 4000)`).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Red;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_091 : CEntity_Effect
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
                return "[Main] Delete 1 of your opponent's Digimon with 4000 DP or less.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                {
                    if (CardEffectCommons.CurrentDp(card, id) <= CardEffectCommons.MaxDpDeleteThreshold(card, baseThreshold: 4000))
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
                        mode: SelectPermanentEffect.Mode.Destroy,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Delete 1 Digimon with 4000 DP or less");
        }

        return cardEffects;
    }
}
