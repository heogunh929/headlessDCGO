// Source: DCGO/Assets/Scripts/CardEffect/ST2/Blue/ST2_15.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS ST2_15
// (ST2/Blue Option). ([Security] AddActivateMainOptionSecurityEffect branch was already new-model, untouched.)
//   [Main] Choose a Digimon digivolution card placed under 1 of your Digimon and play it as another Digimon
//   without paying its memory cost.
// AS-IS: ActivateClass on OptionSkill, ORDER=-1, ISOPTIONAL=false, CanUseCondition = CanTriggerOptionMainEffect.
// ActivateCoroutine (guarded by HasMatchConditionPermanent): SelectPermanentEffect (Mode.Custom, select 1 of
// owner's Digimon that has >=1 playable digivolution card) -> SelectCardEffect (Mode.Custom, Root.Custom over
// that permanent's DigivolutionCards) -> PlayPermanentCards(payCost:false, Root.DigivolutionCards, ETB).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; select flow via bridge W4
// (`GManager.instance.GetComponent<SelectPermanentEffect/SelectCardEffect>()`, BT9_109/BT1_044 idiom); AS-IS
// `Func<Permanent,bool> CanSelectPermanentCondition = IsPermanentExistsOnOwnerBattleAreaDigimon(permanent,card)
// && permanent.DigivolutionCards.Count(CanSelectCardCondition) >= 1` kept verbatim on the canonical
// `Func<Permanent,bool>` shape (id-flip 3b — SelectPermanentEffect.SetUp takes it directly, no id round-trip
// needed); `Permanent.DigivolutionCards` -> `.ToList()` for the
// `List<CardSource>? customRootCardList` param; `Mathf`->`Math`; SelectCardEffect Root/Mode enum members kept
// verbatim; `CardEffectCommons.PlayPermanentCards(...)` = the AS-IS-signature bridge overload (BT1_044 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_15 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Choose a Digimon digivolution card placed under 1 of your Digimon and play it as another Digimon without paying its memory cost.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DigivolutionCards.Count(CanSelectCardCondition) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
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
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    Permanent? selectedPermanent = null;

                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that has digivolution cards.", "The opponent is selecting 1 Digimon that has digivolution cards.");

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;

                        await Task.CompletedTask;
                    }

                    if (selectedPermanent != null)
                    {
                        List<CardSource> selectedCards = new List<CardSource>();

                        if (selectedPermanent.DigivolutionCards.Count(CanSelectCardCondition) >= 1)
                        {
                            maxCount = Math.Min(1, selectedPermanent.DigivolutionCards.Count(CanSelectCardCondition));

                            SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                            selectCardEffect.SetUp(
                                        canTargetCondition: CanSelectCardCondition,
                                        canTargetCondition_ByPreSelecetedList: null,
                                        canEndSelectCondition: null,
                                        canNoSelect: () => false,
                                        selectCardCoroutine: SelectCardCoroutine,
                                        afterSelectCardCoroutine: null,
                                        message: "Select 1 digivolution card to play.",
                                        maxCount: maxCount,
                                        canEndNotMax: false,
                                        isShowOpponent: true,
                                        mode: SelectCardEffect.Mode.Custom,
                                        root: SelectCardEffect.Root.Custom,
                                        customRootCardList: selectedPermanent.DigivolutionCards.ToList(),
                                        canLookReverseCard: true,
                                        selectPlayer: card.Owner,
                                        cardEffect: activateClass);

                            selectCardEffect.SetUpCustomMessage("Select 1 digivolution card to play.", "The opponent is selecting 1 digivolution card to play.");
                            selectCardEffect.SetUpCustomMessage_ShowCard("Played Card");

                            await selectCardEffect.Activate();

                            async Task SelectCardCoroutine(CardSource cardSource)
                            {
                                selectedCards.Add(cardSource);

                                await Task.CompletedTask;
                            }
                        }

                        await CardEffectCommons.PlayPermanentCards(
                            cardSources: selectedCards,
                            activateClass: activateClass,
                            payCost: false,
                            isTapped: false,
                            root: SelectCardEffect.Root.DigivolutionCards,
                            activateETB: true);
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Play a digivolution card as another Digimon");
        }

        return cardEffects;
    }
}
