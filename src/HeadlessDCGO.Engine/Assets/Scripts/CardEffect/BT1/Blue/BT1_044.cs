// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_044.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the original
// BT1_044 (BT1/Blue).
//   [When Attacking] Play 1 level 4 or lower digivolution card under this card as another Digimon without
//   paying its memory cost.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local
// functions; CanSelectCardCondition/CanUseCondition/CanActivateCondition/ActivateCoroutine all 1:1, including
// the nested SelectCardCoroutine accumulator and the customRootCardList = this card's OWN
// `PermanentOfThisCard().DigivolutionCards` (top-most-stack-only scope — NOT a general Digimon scan).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await;
// `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)`;
// `GManager.instance.GetComponent<SelectCardEffect>()` -> bridge W4 (established BT9_109/BT1_017 idiom);
// `SelectCardEffect.Root.DigivolutionCards`/`SelectCardEffect.Mode.Custom` kept verbatim (both real enum
// members on the mirror SelectCardEffect); `Permanent.DigivolutionCards` (IReadOnlyList<CardSource>) ->
// `.ToList()` for the `List<CardSource>? customRootCardList` parameter shape;
// `CardEffectCommons.PlayPermanentCards(...)` -> the AS-IS-signature bridge overload (PlayCardsBridge.cs,
// same call shape as gold, already used by other re-ported cards).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_044 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 digivolution card", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Play 1 level 4 or lower digivolution card under this card as another Digimon without paying its memory cost.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                    {
                        if (cardSource.Level <= 4)
                        {
                            if (cardSource.HasLevel)
                            {
                                return true;
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
                    if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(CanSelectCardCondition) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                    int maxCount = Math.Min(1, selectedPermanent.DigivolutionCards.Count(CanSelectCardCondition));

                    List<CardSource> selectedCards = new List<CardSource>();

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

                    await selectCardEffect.Activate();

                    async Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);

                        await Task.CompletedTask;
                    }

                    await CardEffectCommons.PlayPermanentCards(cardSources: selectedCards, activateClass: activateClass, payCost: false, isTapped: false, root: SelectCardEffect.Root.DigivolutionCards, activateETB: true);
                }
            }
        }

        return cardEffects;
    }
}
