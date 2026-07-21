// TEST FIXTURE (not a real card). A CAPPED ([Once Per Turn]) activated effect whose interactive body is
// SKIPPABLE ("you MAY trash up to 1 hand card"), modelled as an AS-IS REFUND card — one of the ~38 whose body
// explicitly runs `if (!executed) RemoveUse()` (AD1_024-style / BT25_039 ESS `else activateClass.RemoveUse()`).
// When the agent SKIPS the selection the body does nothing and the registered per-turn use is refunded.
// NOTE the refund is PER-CARD OPT-IN: the AS-IS default (a card without RemoveUse, e.g. BT2_078) keeps the use
// consumed on a skipped selection — see TfxOncePerTurnOptionalTrashNoRefund for that default.
// (uniform-사멸 flip) Re-written from the retired invented uniform `ActivatedEffect`(refundWhenNotExecuted:true) to
// the literal AS-IS inline `new ActivateClass()` idiom: the cap rides CEntity_EffectController
// (register-before-body via OnProcessCallbuck), and the refund is the AS-IS card-authored coroutine tail
// `if (nothing selected) activateClass.RemoveUse()` -> RemoveUseEffectThisTurn (CEntityUseCycle staged-refund
// aware). Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOncePerTurnOptionalTrash : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may trash up to 1 card in your hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, 1, false, EffectDescription());
            effects.Add(activateClass);

            string EffectDescription()
            {
                return "[Once Per Turn] You may trash up to 1 card in your hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();

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
                    afterSelectCardCoroutine: AfterSelectCardCoroutine,
                    mode: SelectHandEffect.Mode.Discard,
                    cardEffect: activateClass);

                selectHandEffect.SetUpCustomMessage("you may trash up to 1 hand card", "The opponent is selecting up to 1 card to trash.");

                await selectHandEffect.Activate();

                Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                {
                    selectedCards = cardSources;
                    return Task.CompletedTask;
                }

                // AS-IS per-card opt-in refund: the body did nothing -> return the registered use
                // (`if (!executed) RemoveUse()`, BT25_039 ESS idiom).
                if (selectedCards.Count == 0)
                {
                    activateClass.RemoveUse();
                }
            }
        }

        return effects;
    }
}
