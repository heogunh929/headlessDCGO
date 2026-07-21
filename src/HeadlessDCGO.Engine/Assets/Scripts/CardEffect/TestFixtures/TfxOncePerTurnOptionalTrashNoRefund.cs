// TEST FIXTURE (not a real card). The AS-IS DEFAULT counterpart of TfxOncePerTurnOptionalTrash: a CAPPED
// ([Once Per Turn]) activated effect with a SKIPPABLE interactive body and NO RemoveUse — like the
// ~1,170 [Once Per Turn] cards that never refund (witness BT2_078: canNoSelect:true, no RemoveUse — selecting
// nothing still spends the use). Skipping the selection keeps the per-turn use CONSUMED (register-before-body,
// no refund), so the effect cannot re-fire this turn.
// (uniform-사멸 flip) Re-written from the retired invented uniform `ActivatedEffect` to the literal AS-IS inline
// `new ActivateClass()` idiom: identical to TfxOncePerTurnOptionalTrash minus the coroutine-tail RemoveUse —
// the AS-IS default. Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOncePerTurnOptionalTrashNoRefund : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may trash up to 1 card in your hand (no refund)", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, 1, false, EffectDescription());
            effects.Add(activateClass);

            string EffectDescription()
            {
                return "[Once Per Turn] You may trash up to 1 card in your hand (no refund).";
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
                    canNoSelect: true,
                    canEndNotMax: false,
                    isShowOpponent: false,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: null,
                    mode: SelectHandEffect.Mode.Discard,
                    cardEffect: activateClass);

                selectHandEffect.SetUpCustomMessage("you may trash up to 1 hand card", "The opponent is selecting up to 1 card to trash.");

                await selectHandEffect.Activate();

                // AS-IS default: NO RemoveUse — a skipped selection keeps the registered use consumed.
            }
        }

        return effects;
    }
}
