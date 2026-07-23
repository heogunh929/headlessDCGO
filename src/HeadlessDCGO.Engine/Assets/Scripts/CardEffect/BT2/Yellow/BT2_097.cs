// Source: DCGO/Assets/Scripts/CardEffect/BT2/Yellow/BT2_097.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_097 (BT2/Yellow, an Option).
//   [Main] 3 of your opponent's level 3 Digimon get -4000 DP for the turn.
//   [Security] Activate this card's [Main] effect. (AS-IS SecuritySkill block, :85-88 —
//   AddActivateMainOptionSecurityEffect.)
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.SelectAndBuffDpEffect(...)` with the literal AS-IS
// inline `new ActivateClass()` + `GManager.instance.GetComponent<SelectPermanentEffect>()` (Mode.Custom, W4)
// structure. Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->
// `await X`. The AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` is kept VERBATIM (all four AS-IS
// conjuncts, incl. `permanent.TopCard.HasLevel` and `permanent.CanSelectBySkill(activateClass)` — the latter
// via the mirror `Permanent.CanSelectBySkill` (batch-3, same RestrictionScan the select's internal gate uses,
// preserving AS-IS's double evaluation)); `MatchConditionPermanentCount`, `HasMatchConditionPermanent`, and
// `SetUp` canTargetCondition all take the Permanent-shape predicate (Func<Permanent,bool>) directly.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Yellow;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_097 : CEntity_Effect
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
                return "[Main] 3 of your opponent's level 3 Digimon get -4000 DP for the turn.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.Level == 3)
                    {
                        if (permanent.TopCard.HasLevel)
                        {
                            if (permanent.CanSelectBySkill(activateClass))
                            {
                                return true;
                            }
                        }
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
                    int maxCount = Math.Min(3, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

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

                    selectPermanentEffect.SetUpCustomMessage("Select Digimon to DP -4000.", "The opponent is selecting Digimon to DP -4000.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.ChangeDigimonDP(targetPermanent: permanent, changeValue: -4000, effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: $"Opponent's 3 Digimon get DP -4000");
        }

        return cardEffects;
    }
}
