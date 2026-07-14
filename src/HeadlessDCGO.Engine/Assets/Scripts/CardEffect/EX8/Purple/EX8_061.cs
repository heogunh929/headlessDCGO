// Source: DCGO/Assets/Scripts/CardEffect/EX8/Purple/EX8_061.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the two trash-play ActivateClasses.
// Unchanged verbatim factory mirrors:
//   * Alternate Digivolution (None) -> AddSelfDigivolutionRequirementStaticEffect(IsLevel4 && [DS], cost 3).
//   * <Scapegoat> (WhenPermanentWouldBeDeleted) -> ScapegoatSelfEffect(false).
// [When Attacking][Once Per Turn] — AS-IS `new ActivateClass()` + SetHashString("PlayDigimon_EX8_061") +
//   SetUpActivateClass(..., 1, true, ...) (ORDER 1, isOptional true) (EX8_061.cs:49-135). CanUse =
//   IsExistOnBattleAreaDigimon && CanTriggerOnAttack. CanActivate = IsExistOnBattleAreaDigimon &&
//   HasMatchConditionOwnersCardInTrash(DigimonToPlay) && MemoryForPlayer >= 1. Body = SelectCardEffect(root:Trash,
//   Mode.Custom, maxCount:1, canNoSelect) -> PlayPermanentCards(payCost:false, root:Trash, activateETB:true).
// [On Deletion] — AS-IS `new ActivateClass()` + SetIsInheritedEffect(true) + SetUpActivateClass(..., -1, true, ...)
//   (uncapped, isOptional true) (EX8_061.cs:139-232). CanUse = CanTriggerOnDeletion. CanActivate =
//   CanActivateOnDeletion && HasMatchConditionOwnersCardInTrash(HasCorrectTrait). Body = same select-and-play.
// Substrate translations only: IEnumerator->Task, StartCoroutine->await;
// `GManager.instance.GetComponent<SelectCardEffect>()` + full AS-IS SetUp; `card.Owner.MemoryForPlayer` ->
// `CardEffectCommons.MemoryForPlayer(card)` (the mirror player-relative memory read). NOTE: AS-IS
// `CanPlayAsNewPermanent(cardSource, false, activateClass, SelectCardEffect.Root.Trash)` (the 4-arg root overload)
// has no mirror surface — the mirror exposes only `CanPlayAsNewPermanent(cardSource, payCost, cardEffect, ...)`
// (CardEffectCommons.cs:3648). Under payCost:false the play-zone (root) drives only the cost path, which is moot,
// and the actual play zone is pinned by the SelectCardEffect root + PlayPermanentCards root; the pre-filter's root
// arg is therefore redundant (established EX8_061 mirror translation), so the 3-arg form is used for both branches.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Purple;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX8_061 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.IsLevel(4) && targetPermanent.TopCard.EqualsTraits("DS");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 3,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null));
        }
        #endregion

        #region Scapegoat
        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            string EffectDiscription()
            {
                return "<Scapegoat> (When this Digimon would be deleted other than by your own effects, by deleting 1 of your other Digimon, prevent that deletion.)";
            }

            cardEffects.Add(CardEffectFactory.ScapegoatSelfEffect(isInheritedEffect: false, card: card, condition: null, effectName: "<Scapegoat>", effectDiscription: EffectDiscription()));
        }
        #endregion

        #region When Attacking
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may play 1 level 4 or lower Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDescription());
            activateClass.SetHashString("PlayDigimon_EX8_061");
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[When Attacking] [Once Per Turn] If you have 1 or more memory, you may play 1 level 4 or lower Digimon card with the [DS], [Mollusk], or [Crustacean] trait from your trash without paying the cost.";
            }

            bool DigimonToPlay(CardSource cardSource)
            {
                return CardEffectCommons.CanPlayAsNewPermanent(cardSource, false, activateClass) &&
                       cardSource.IsDigimon &&
                       cardSource.HasLevel &&
                       cardSource.Level <= 4 &&
                       (cardSource.EqualsTraits("DS") || cardSource.EqualsTraits("Mollusk") || cardSource.EqualsTraits("Crustacean"));
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                    return CardEffectCommons.CanTriggerOnAttack(hashtable, card);

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, DigimonToPlay) &&
                       CardEffectCommons.MemoryForPlayer(card) >= 1;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();
                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                    canTargetCondition: DigimonToPlay,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: SelectCardCoroutine,
                    message: "Select 1 level 4 or lower Digimon card to play.",
                    maxCount: 1,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.Trash,
                    customRootCardList: null,
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                selectCardEffect.SetUpCustomMessage(
                    "Select 1 level 4 or lower Digimon card to play.",
                    "The opponent is selecting 1 level 4 or lower Digimon card to play.");
                selectCardEffect.SetUpCustomMessage_ShowCard("Play Card");

                await selectCardEffect.Activate();

                async Task SelectCardCoroutine(List<CardSource> sources)
                {
                    selectedCards = sources;
                    await Task.CompletedTask;
                }

                if (selectedCards.Count > 0)
                {
                    await CardEffectCommons.PlayPermanentCards(
                        cardSources: selectedCards,
                        activateClass: activateClass,
                        payCost: false,
                        isTapped: false,
                        root: SelectCardEffect.Root.Trash,
                        activateETB: true);
                }
            }
        }
        #endregion

        #region On Deletion - ESS
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 level 4 or lower Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDescription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[On Deletion] You may play 1 level 4 or lower Digimon card with the [DS], [Mollusk], or [Crustacean] trait from your trash without paying the cost.";
            }

            bool HasCorrectTrait(CardSource cardSource)
            {
                if (cardSource.EqualsTraits("DS") || cardSource.EqualsTraits("Mollusk") || cardSource.EqualsTraits("Crustacean"))
                {
                    if (cardSource.HasLevel && cardSource.Level <= 4)
                    {
                        if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanActivateOnDeletion(hashtable, card) &&
                       CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, HasCorrectTrait);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();

                SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                selectCardEffect.SetUp(
                    canTargetCondition: HasCorrectTrait,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    canNoSelect: () => true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    message: "Select 1 level 4 or lower Digimon card to play.",
                    maxCount: 1,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    mode: SelectCardEffect.Mode.Custom,
                    root: SelectCardEffect.Root.Trash,
                    customRootCardList: null,
                    canLookReverseCard: true,
                    selectPlayer: card.Owner,
                    cardEffect: activateClass);

                selectCardEffect.SetUpCustomMessage(
                    "Select 1 level 4 or lower Digimon card to play.",
                    "The opponent is selecting 1 level 4 or lower Digimon card to play.");
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
                    activateETB: true);
            }
        }
        #endregion

        return cardEffects;
    }
}
