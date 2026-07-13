// Source: DCGO/Assets/Scripts/CardEffect/BT2/Purple/BT2_110.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_110 (BT2/Purple, an Option).
//   [Main]     Delete 1 of your opponent's unsuspended Digimon.
//   [Security] (use the Main effect, custom description)
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndDestroyEffect(...)` call (an invented
// helper — explicitly prohibited/retired) with the literal AS-IS inline `new ActivateClass()` structure +
// `GManager.instance.GetComponent<SelectPermanentEffect>()` (Mode.Destroy) selection pattern (bridge W4).
// `CardEffectCommons.AddActivateMainOptionSecurityEffect` on SecuritySkill IS the real AS-IS call (verbatim).
// Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` (`!permanent.IsSuspended`) -> the established
// entity-id predicate idiom (`!CardEffectCommons.IsSuspended(card, id)`).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Purple;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_110 : CEntity_Effect
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
                return "[Main] Delete 1 of your opponent's unsuspended Digimon.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                {
                    if (!CardEffectCommons.IsSuspended(card, id))
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
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Delete 1 unsuspended Digimon");
        }

        return cardEffects;
    }
}
