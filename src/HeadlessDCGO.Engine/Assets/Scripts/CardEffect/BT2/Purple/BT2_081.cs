// Source: DCGO/Assets/Scripts/CardEffect/BT2/Purple/BT2_081.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the original
// BT2_081 (BT2/Purple).
//   [When Attacking] You may play 1 purple level 3 Digimon card from your trash without paying its memory cost.
//   Any [On Play] effects on Digimon played with this effect don't activate.
// This RESOLVES the previous pass's `activateETB:false` STOP: the AS-IS-verbatim
// `GManager.instance.GetComponent<SelectCardEffect>()` (Mode.Custom, Root.Trash) + `CardEffectCommons.
// PlayPermanentCards(..., activateETB:false)` bridge (PlayCardsBridge.cs, same as BT1_044) carries the
// suppress-[On Play] flag through 1:1. AS-IS structure kept verbatim: inline `new ActivateClass()`, ORDER=-1,
// ISOPTIONAL=true (canNoSelect:() => true), the ActivateCoroutine's isExistOnField + GetBattleAreaDigimons().
// Contains(self) + HasMatchConditionOwnersCardInTrash triple re-guard.
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `isExistOnField(card)` = inherited
// CEntity_Effect static (unqualified, as AS-IS — established ST1_09 idiom); `card.Owner.GetBattleAreaDigimons()`
// / `card.Owner.TrashCards` (AS-IS live Player members) -> `HeadlessPlayerId.GetBattleAreaDigimons()` extension
// and `new Player(card.Context, card.Owner).TrashCards` (established ST1_09/BT1_081 idioms; mirror
// `CardSource.Owner` is a bare HeadlessPlayerId); `card.PermanentOfThisCard()` (as `Permanent`) ->
// `ICardEffect.ResolvePermanentOfThisCard(card)`; `HasCardColor(CardColor.Purple)` -> the string overload
// `HasCardColor("Purple")` (the mirror `HasCardColor` takes a string); `GManager.instance.GetComponent<
// SelectCardEffect>()`/`SelectCardEffect.Mode/Root` = bridge W4 verbatim (established BT1_044 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Purple;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT2_081 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 Digimon from trash", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] You may play 1 purple level 3 Digimon card from your trash without paying its memory cost. Any [On Play] effects on Digimon played with this effect don't activate. ";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource != null)
                {
                    if (cardSource.IsDigimon)
                    {
                        if (cardSource.Owner == card.Owner)
                        {
                            if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                            {
                                if (cardSource.HasCardColor("Purple"))
                                {
                                    if (cardSource.Level == 3)
                                    {
                                        if (cardSource.HasLevel)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (isExistOnField(card))
                {
                    if (card.Owner.GetBattleAreaDigimons().Contains(ICardEffect.ResolvePermanentOfThisCard(card)))
                    {
                        if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, (cardSource) => CanSelectCardCondition(cardSource)))
                        {
                            int maxCount = Math.Min(1, new Player(card.Context, card.Owner).TrashCards.Count((cardSource) => CanSelectCardCondition(cardSource)));

                            List<CardSource> selectedCards = new List<CardSource>();

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

                            async Task SelectCardCoroutine(CardSource cardSource)
                            {
                                selectedCards.Add(cardSource);

                                await Task.CompletedTask;
                            }

                            await CardEffectCommons.PlayPermanentCards(
                                cardSources: selectedCards,
                                activateClass: activateClass,
                                payCost: false,
                                isTapped: false,
                                root: SelectCardEffect.Root.Trash,
                                activateETB: false);
                        }
                    }
                }
            }
        }

        return cardEffects;
    }
}
