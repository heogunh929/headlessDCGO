// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_056.cs
// R5-A re-port (RD-R6-01 / RD-P8-01 resolved): old-model ActivatedEffect + ActivatedSelectAndPlayFromZonesEffect ->
// new-model ActivateClass now that the mirror SelectHandEffect exists (Script/SelectHandEffect.cs). The prior
// mirror pooled Hand ∪ Trash into one combined select ("area prompt = UI sugar"); AS-IS instead asks a REAL
// area bool (from hand / from trash) then runs the zone-specific component — restored verbatim.
//   [On Play] You may play 1 [Tinkermon] from your hand or recycle bin without paying its memory cost.
// AS-IS structure kept verbatim (BT1_056.cs:17-174): inline ActivateClass, SetUpActivateClass(..., -1, true, ...).
// The ActivateCoroutine's area bool = GManager.instance.userSelectionManager (bridge W5, BT1_111 idiom); the
// from-hand branch = SelectHandEffect(Mode.Custom) (bridge W4); the from-trash branch = SelectCardEffect(Root.Trash);
// then CardEffectCommons.PlayPermanentCards(root: fromHand ? Hand : Trash, payCost:false).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `card.Owner.HandCards` ->
// new Player(ctx, owner).HandCards; SelectCardCoroutine (IEnumerator, yield null) -> Task.CompletedTask.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_056 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [Tinkermon] from hand or trash", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] You may play 1 [Tinkermon] from your hand or recycle bin without paying its memory cost.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.CardNames.Contains("Tinkermon"))
                {
                    if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).HandCards.Count >= 1)
                    {
                        return true;
                    }

                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                bool canSelectHand = new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 1;
                bool canSelectTrash = CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition);

                if (canSelectHand || canSelectTrash)
                {
                    if (canSelectHand && canSelectTrash)
                    {
                        List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                        {
                            new SelectionElement<bool>(message: $"From hand", value : true, spriteIndex: 0),
                            new SelectionElement<bool>(message: $"From trash", value : false, spriteIndex: 1),
                        };

                        string selectPlayerMessage = "From which area do you play a card?";
                        string notSelectPlayerMessage = "The opponent is choosing from which area to play a card.";

                        GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);
                    }

                    else
                    {
                        GManager.instance.userSelectionManager.SetBool(canSelectHand);
                    }

                    await GManager.instance.userSelectionManager.WaitForEndSelect();

                    bool fromHand = GManager.instance.userSelectionManager.SelectedBoolValue;

                    List<CardSource> selectedCards = new List<CardSource>();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        return Task.CompletedTask;
                    }

                    if (fromHand)
                    {
                        int maxCount = 1;

                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount,
                            canNoSelect: true,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            mode: SelectHandEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectHandEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                        selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                        await selectHandEffect.Activate();
                    }

                    else
                    {
                        int maxCount = 1;

                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 card to play.",
                            maxCount: maxCount,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                        selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                        await selectCardEffect.Activate();
                    }

                    SelectCardEffect.Root root = SelectCardEffect.Root.Hand;

                    if (!fromHand)
                    {
                        root = SelectCardEffect.Root.Trash;
                    }

                    await CardEffectCommons.PlayPermanentCards(
                        cardSources: selectedCards,
                        activateClass: activateClass,
                        payCost: false,
                        isTapped: false,
                        root: root,
                        activateETB: true);
                }
            }
        }

        return cardEffects;
    }
}
