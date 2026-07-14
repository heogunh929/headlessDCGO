// Source: DCGO/Assets/Scripts/CardEffect/BT2/Purple/BT2_080.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the original
// BT2_080 (BT2/Purple).
//   [Retaliation] (OnDestroyedAnyone) -> RetaliationSelfEffect (already new-model, untouched).
//   [On Play] You may play up to 2 level 4 or lower purple Digimon cards from your trash without paying their
//     memory costs. Any [On Play] effects on Digimon played with this effect don't activate.
// This RESOLVES the previous pass's `activateETB:false` STOP: the AS-IS-verbatim
// `GManager.instance.GetComponent<SelectCardEffect>()` (Mode.Custom, Root.Trash) + `CardEffectCommons.
// PlayPermanentCards(..., activateETB:false)` bridge (PlayCardsBridge.cs, same as BT1_044) carries the
// suppress-[On Play] flag through 1:1. AS-IS structure kept verbatim: inline `new ActivateClass()`, ORDER=-1,
// ISOPTIONAL=true (canNoSelect:() => true, canEndNotMax:true), the nested CanEndSelectCondition/
// SelectCardCoroutine accumulator.
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `card.Owner.TrashCards` (AS-IS live
// Player member) -> `new Player(card.Context, card.Owner).TrashCards` (established BT1_081 idiom; mirror
// `CardSource.Owner` is a bare HeadlessPlayerId); `HasCardColor(CardColor.Purple)` -> the string overload
// `HasCardColor("Purple")`; `GManager.instance.GetComponent<SelectCardEffect>()`/`SelectCardEffect.Mode/Root`
// = bridge W4 verbatim (established BT1_044 idiom).
// design item RD-R6-04 (FRAME-MODEL, inert): AS-IS caps maxCount by the owner's EMPTY battle-area frame count
// (`fieldCardFrames.Count(f => f.IsEmptyFrame() && f.IsBattleAreaFrame())`). The mirror has no frame/slot model
// (zones are unbounded lists — the documented MIG5-FRAME-MODEL gap), so the empty-frame reduction has no mirror
// surface and never fires; the AS-IS `Math.Min(2, TrashCards matching)` cap is kept and the frame reduction is
// omitted as inert substrate (NOT a game-logic simplification — the mirror imposes no frame limit to reduce by).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Purple;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT2_080 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.RetaliationSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play Digimon from trash", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] You may play up to 2 level 4 or lower purple Digimon cards from your trash without paying their memory costs. Any [On Play] effects on Digimon played with this effect don't activate. ";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.HasCardColor("Purple"))
                    {
                        if (cardSource.Level <= 4)
                        {
                            if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                            {
                                if (cardSource.HasLevel)
                                {
                                    return true;
                                }
                            }
                        }
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
                    if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition))
                {
                    List<CardSource> selectedCards = new List<CardSource>();

                    // AS-IS :77-82 — `Math.Min(2, TrashCards matching)` then reduce by empty battle-area frames.
                    // The frame reduction is inert in the mirror (RD-R6-04, no frame model).
                    int maxCount = Math.Min(2, new Player(card.Context, card.Owner).TrashCards.Count(CanSelectCardCondition));

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                                canTargetCondition: CanSelectCardCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: CanEndSelectCondition,
                                canNoSelect: () => true,
                                selectCardCoroutine: SelectCardCoroutine,
                                afterSelectCardCoroutine: null,
                                message: "Select cards to play.",
                                maxCount: maxCount,
                                canEndNotMax: true,
                                isShowOpponent: true,
                                mode: SelectCardEffect.Mode.Custom,
                                root: SelectCardEffect.Root.Trash,
                                customRootCardList: null,
                                canLookReverseCard: true,
                                selectPlayer: card.Owner,
                                cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select cards to play.", "The opponent is selecting cards to play.");
                    selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                    await selectCardEffect.Activate();

                    bool CanEndSelectCondition(List<CardSource> cardSources)
                    {
                        if (CardEffectCommons.HasNoElement(cardSources))
                        {
                            return false;
                        }

                        return true;
                    }

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

        return cardEffects;
    }
}
