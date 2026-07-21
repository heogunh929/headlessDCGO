// TEST FIXTURE (not a real card). A CAPPED ([Once Per Turn]) activated effect with an INTERACTIVE body:
// "[Once Per Turn] select 1 card in your hand and trash it, then unsuspend this." at OnEnterFieldAnyone. Exercises
// B-1 (P1-3): the interactive body suspends mid-choice (DeferredChoicePendingException); the register-before-body
// staged use must survive the suspend so the resumed re-invocation's CanTrigger cap re-check does NOT read its own
// in-flight use as capped-out (CEntityUseCycle replay cursor), and the cap commits exactly once on completion.
// (uniform-사멸 flip) Re-written from the retired invented uniform `ActivatedEffect` +
// `SelectTrashHandThenSelfMutationBody` to the literal AS-IS inline `new ActivateClass()` idiom the printed-card
// corpus uses: SetUpActivateClass(maxCountPerTurn: 1) carries the [Once Per Turn] cap on the AS-IS
// CEntity_EffectController path (isOverMaxCountPerTurn / RegisterUseEffectThisTurn), the hand-select is the AS-IS
// SelectHandEffect(Mode.Discard) component flow, and the self-unsuspend is IUnsuspendPermanents (AS-IS
// UnTapPermanents idiom). Assertion surface pinned by tests/B1 + tests/B3 is unchanged. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOncePerTurnInteractiveTrash : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Select 1 card in your hand and trash it, then unsuspend this", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, 1, false, EffectDescription());
            effects.Add(activateClass);

            string EffectDescription()
            {
                return "[Once Per Turn] Select 1 card in your hand and trash it, then unsuspend this.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                selectHandEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: static _ => true,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    isShowOpponent: false,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: null,
                    mode: SelectHandEffect.Mode.Discard,
                    cardEffect: activateClass);

                selectHandEffect.SetUpCustomMessage("select 1 card in your hand to trash", "The opponent is selecting 1 card to trash.");

                await selectHandEffect.Activate();

                await new IUnsuspendPermanents(
                    new List<Permanent> { ICardEffect.ResolvePermanentOfThisCard(card) }, activateClass).Unsuspend();
            }
        }

        return effects;
    }
}
