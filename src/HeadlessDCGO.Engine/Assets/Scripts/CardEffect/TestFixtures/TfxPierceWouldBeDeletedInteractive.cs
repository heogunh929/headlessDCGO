// TEST FIXTURE (not a real card). Composes two ALREADY-PORTED model pieces onto one permanent so a single
// registered card can drive the "Piercing follow-up security check parks/resumes through the PRE would-be-deleted
// window" witness (C5-SecurityPreWindow.PiercingDeferAndResume):
//   [Piercing] (self, static) -> OnDetermineDoSecurityCheck: CardEffectFactory.PierceSelfEffect (identical to the
//     real BT1_022 piercing branch).
//   [When this Digimon would be deleted] You may prevent it. -> WhenPermanentWouldBeDeleted: an OPTIONAL,
//     NON-KEYWORD-named, window-form ActivateClass that cancels the deletion (willBeRemoveField=false) — identical
//     in shape to TfxWouldBeDeletedInteractive.
//
// No engine logic is invented: both branches reuse existing factories/shapes. DELIBERATELY GATE-INVISIBLE (same
// audit as the other TestFixtures): the survival effect name is not a recognised replacement keyword and it is a
// new-model ActivateClass with no EffectRegistry binding, so HasPreOption is FALSE and the PRE cut-in window is the
// sole firing path. No real card has this number, so it is inert in actual play.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxPierceWouldBeDeletedInteractive : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("TfxPierceWouldBeDeletedInteractive", CanUseCondition, card);
            // isOptional:true — a "you may" replacement, so the cut-in drain surfaces an agent choice and PAUSES.
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true,
                "[When this Digimon would be deleted] You may prevent it. (test fixture)");
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
