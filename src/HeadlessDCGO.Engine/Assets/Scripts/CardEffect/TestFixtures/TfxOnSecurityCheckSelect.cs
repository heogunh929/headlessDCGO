// TEST FIXTURE (not a real card). An OnSecurityCheck WINDOW reactor whose body is a select-and-destroy — so when a
// security card is checked, this reactor's OnSecurityCheck window needs the agent to pick a target, suspending the
// window (a pending-choice exception) mid-security-loop. Used by tests/P1r-SecCheckInteractive to exercise the P1-3
// (RD-C2-SECCHECK-INTERACTIVE) park: SecurityResolver catches the suspend and parks the remaining check via the
// deferred-security-check machinery, so the game pauses on the choice instead of the attack aborting with an
// uncaught throw. New-model ActivateClass shape modelled on BT9_062 (select-and-destroy trigger reactor). No real
// card has the number "TfxOnSecurityCheckSelect", so this is inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class TfxOnSecurityCheckSelect : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnSecurityCheck)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 opponent Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription() =>
                "[On Security Check] Delete 1 of your opponent's Digimon.";

            bool CanSelectPermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            // Test-fixture gate: fire whenever this reactor is a battle-area Digimon (a permissive OnSecurityCheck
            // gate — real cards would use a print-specific CanTrigger; the fixture only needs to open the window).
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                && CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition);

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
