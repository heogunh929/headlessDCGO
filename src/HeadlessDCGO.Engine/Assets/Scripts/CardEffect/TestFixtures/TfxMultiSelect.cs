// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [Main] runs TWO sequential
// select-and-destroy blocks inside ONE uniform ActivateClass coroutine (the ST1_16 OptionSkill idiom), so
// activating it requires the agent to make TWO choices in one activation. Used by
// tests/G12-002.MultiChoiceActivationE2E to exercise the multi-choice deferred resume loop on the real
// (pump) play path. No real card has the number "TfxMultiSelect", so this is inert in actual play.
// (4b B6: re-expressed from two plain resolver effects to the uniform ActivateClass so the AS-IS
// UseOptionClass OptionSkill loop — which activates ActivateICardEffect members only — drives it.)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxMultiSelect : CEntity_Effect
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
                return "[Main] Delete 1 of your opponent's Digimon. Then delete 1 more of your opponent's Digimon.";
            }

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                // First select-and-destroy (suspends for the agent's FIRST choice).
                await SelectAndDestroyOnceAsync().ConfigureAwait(false);
                // Second select-and-destroy (suspends again for the SECOND choice; commit-once semantics
                // are exercised by the two-round deferred resume across this same coroutine).
                await SelectAndDestroyOnceAsync().ConfigureAwait(false);

                async Task SelectAndDestroyOnceAsync()
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

                        await selectPermanentEffect.Activate().ConfigureAwait(false);
                    }
                }
            }
        }

        return cardEffects;
    }
}
