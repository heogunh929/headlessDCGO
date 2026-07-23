// TEST FIXTURE (not a real card). Dispatch-discoverable CEntity_Effect whose [Security] returns a
// select-and-destroy effect, so resolving it as a revealed security card requires the agent to make a
// choice. Used by tests/G12-004.SecuritySkillDeferredE2E to exercise the [Security] deferred resume path
// (SecurityResolver suspends -> DeferredActivations -> ResolveChoice resumes). No real card has the number
// "TfxSecuritySelect", so this is inert in actual play.
// (R6-Da'-1) Re-written from the retired invented helper `CardEffectFactory.SelectAndDestroyEffect(...)`
// to the literal AS-IS inline `new ActivateClass()` + `GManager.instance.GetComponent<SelectPermanentEffect>()`
// (Mode.Destroy) idiom — [Security] block structure per the printed-card precedent ST2_14.cs (SecuritySkill
// timing gate `CanTriggerSecurityEffect` + `SetIsSecurityEffect(true)`), select flow per ST1_16.cs/BT2_110.cs.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxSecuritySelect : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 opponent Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Delete 1 of your opponent's Digimon.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
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

        return cardEffects;
    }
}
