// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_088.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Main] branch (the
// [Security] PlaySelfTamerSecurityEffect factory branch was already new-model and is untouched).
//   [Main] If you have a level 5 or higher green Digimon in play, you can suspend this Tamer to reveal the
//   top card of your deck. If that card is a Digimon card, add it to your hand. Otherwise place it at the
//   bottom of your deck.
// AS-IS: an ActivateClass on EffectTiming.OnDeclaration -- CanUseCondition = IsExistOnBattleArea(card) &&
// CanActivateSuspendCostEffect(card) && HasMatchConditionOwnersPermanent(IsDigimon && green && Level >= 5 &&
// HasLevel); CanActivateCondition = null; ORDER=-1 (uncapped), ISOPTIONAL=false. ActivateCoroutine =
// SuspendPermanentsClass(self).Tap() THEN RevealDeckTopCardsAndProcessForAll(revealCount:1, Digimon ->
// Mode.AddHand (maxCount -1), remaining -> DeckBottom).
// AS-IS structure kept verbatim: inline `new ActivateClass()` + local functions. Substrate translations only:
// IEnumerator->Task, StartCoroutine->await; `permanent.TopCard.CardColors.Contains(Green)` ->
// `permanent.TopCard.HasCardColor("Green")`; `new SuspendPermanentsClass(List<Permanent>, Hashtable).Tap()`
// -> the mirror ctor `(List<Permanent>, HeadlessEntityId? causeEffectSourceId, bool isBlock)`;
// `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)`;
// `CardEffectCommons.RevealDeckTopCardsAndProcessForAll(...)` / `new SimplifiedSelectCardConditionClass(...)`
// -> the AS-IS-signature bridge overloads (RevealLibrary.cs bridge W3), same call shape as gold.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_088 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Reveal the top card of deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] If you have a level 5 or higher green Digimon in play, you can suspend this Tamer to reveal the top card of your deck. If that card is a Digimon card, add it to your hand. Otherwise place it at the bottom of your deck.";
            }

            bool CardCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanActivateSuspendCostEffect(card))
                    {
                        if (CardEffectCommons.HasMatchConditionOwnersPermanent(card, permanent => permanent.IsDigimon && permanent.TopCard.HasCardColor("Green") && permanent.Level >= 5 && permanent.TopCard.HasLevel))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new SuspendPermanentsClass(
                    new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) },
                    activateClass,
                    isBlock: false).Tap();

                await CardEffectCommons.RevealDeckTopCardsAndProcessForAll(
                    revealCount: 1,
                    simplifiedSelectCardCondition:
                    new SimplifiedSelectCardConditionClass(
                            canTargetCondition: CardCondition,
                            message: "",
                            mode: SelectCardEffect.Mode.AddHand,
                            maxCount: -1,
                            selectCardCoroutine: null),
                    remainingCardsPlace: RemainingCardsPlace.DeckBottom,
                    activateClass: activateClass);
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
