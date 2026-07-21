// 1:1 mirror of the original BT1_087 (Yellow Tamer, mixed):
//   [Start of Your Turn] Set your memory to 3 (if 2 or less).      -> SetMemoryTo3TamerEffect
//   [On Play] Look at your security stack, then reveal 1 card in it and add it to your hand. If that card
//             is yellow, <Recovery +1 (Deck)>. (Place the top card of your deck on top of your security
//             stack.) Then shuffle your security stack.
//   [Security] Play this Tamer.                                     -> PlaySelfTamerSecurityEffect
//
// [On Play] AS-IS: ActivateClass on OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea && Owner.SecurityCards.Count >= 1, ORDER=-1, ISOPTIONAL=false.
//   ActivateCoroutine: SelectCardEffect(root:Security, mode:AddHand, maxCount:Min(1,count), canNoSelect:()=>
//   false) with an AfterSelect step: if the selected card is yellow, IRecovery(owner,1).Recovery(); then
//   ContinuousController shuffle + Owner.SecurityCards = RandomUtility.ShuffledDeckCards(SecurityCards).
//   Headless mirror: SecuritySelectToHandColorRecoveryShuffleEffect — mandatory select 1 security card -> hand,
//   color-gated <Recovery +1 (Deck)> keyed off the SPECIFIC selected card, then a deterministic security
//   shuffle (ShuffleSecurity sink mutation / IZoneMover.ShuffleSecurityAsync). The three steps stage on the
//   sink so they flush in AS-IS order (the recovered card is shuffled in). Self-guards on security>=1 (CanActivate).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_087 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            // (이연③-d) Re-ported the invented SecuritySelectToHandColorRecoveryShuffleEffect -> the literal AS-IS
            // inline ActivateClass: SelectCardEffect(root: Security, mode: AddHand) reveals+adds the picked security
            // card to hand, an afterSelect captures it, a color-gated IRecovery(owner,1) fires if it is yellow,
            // then the security stack is shuffled. Substrate translations: IEnumerator->Task, StartCoroutine->await;
            // `card.Owner.SecurityCards.Count` -> `CardEffectCommons.SecurityCount(card)`; `new IRecovery(card.Owner,
            // 1, activateClass)` -> `new IRecovery(card.Context, card.Owner, 1, card.InstanceId)` (ST3_09 idiom);
            // `card.Owner.SecurityCards = RandomUtility.ShuffledDeckCards(...)` -> `IZoneMover.ShuffleSecurityAsync`
            // (the mirror's security-shuffle substrate). The AS-IS afterSelect `IReduceSecurity` is OMITTED: on the
            // mirror the AddHand-from-Security mutation (ReturnToHand) already REMOVES the card from the stack, so
            // the AS-IS copy-then-IReduceSecurity pair collapses to the single AddHand here (a second IReduceSecurity
            // would wrongly trash an extra security card).
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Add 1 card from security to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Look at your security stack, then reveal 1 card in it and add it to your hand. If that card is yellow, <Recovery +1 (Deck)>. (Place the top card of your deck on top of your security stack.) Then shuffle your security stack.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return true;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.SecurityCount(card) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.SecurityCount(card) >= 1)
                {
                    int maxCount = Math.Min(1, CardEffectCommons.SecurityCount(card));

                    CardSource? selectedCard = null;

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => false,
                        selectCardCoroutine: null,
                        afterSelectCardCoroutine: AfterSelectCardCoroutine,
                        message: "Select 1 card to add to your hand.",
                        maxCount: maxCount,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.AddHand,
                        root: SelectCardEffect.Root.Security,
                        customRootCardList: null,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    await selectCardEffect.Activate();

                    Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                    {
                        foreach (CardSource cardSource in cardSources)
                        {
                            selectedCard = cardSource;
                        }

                        return Task.CompletedTask;
                    }

                    if (selectedCard != null)
                    {
                        if (selectedCard.HasCardColor("Yellow"))
                        {
                            await new IRecovery(card.Context, card.Owner, 1, card.InstanceId).Recovery();
                        }
                    }

                    await card.Context.ZoneMover.ShuffleSecurityAsync(card.Owner);
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
