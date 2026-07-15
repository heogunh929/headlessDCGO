// TEST FIXTURE (not a real card). Sibling of TfxWouldBeDeleted with a DISTINCT EffectName
// ("TfxWouldBeDeletedSurviveB") so a two-effect PRE cut-in (one TfxWouldBeDeleted + one TfxWouldBeDeletedB in a
// SINGLE Destroy batch) exercises the MultipleSkills order-choice with TWO DISTINCT keywords — the RD-3C2BP-01
// witness. Both are MANDATORY, gate-invisible, window-form survival replacements (see TfxWouldBeDeleted's audit).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxWouldBeDeletedB : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("TfxWouldBeDeletedSurviveB", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false,
                "[When this Digimon would be deleted] It is not deleted. (test fixture B)");
            activateClass.SetIsInheritedEffect(false);
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent) &&
                       CardEffectCommons.CanTriggerWhenPermanentRemoveField(hashtable, permanent => permanent == targetPermanent);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsPermanentExistsOnBattleArea(targetPermanent);
            }

            Task ActivateCoroutine(Hashtable hashtable)
            {
                if (targetPermanent != null)
                {
                    targetPermanent.willBeRemoveField = false;
                }

                return Task.CompletedTask;
            }
        }

        return cardEffects;
    }
}
