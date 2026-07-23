// Source: DCGO/Assets/Scripts/CardEffect/BT7/Black/BT7_058.cs (167 lines) — TRUE AS-IS-verbatim port.
// ① AS-IS 앵커 regions:
//    * When Attacking :14-137 (timing == OnAllyAttack) — trash all digivolution cards of 1 of your [DeadlyAxemon]
//      and place it at the BOTTOM of THIS Digimon's digivolution cards (the live-top re-parent via
//      IPlacePermanentToDigivolutionCards, EX-115), then digivolve THIS into a [DarkKnightmon] from hand w/o cost.
//    * Inherit (ESS) :139-163 (timing == None) — +1 S.Attack while owner turn & top contains Knightmon/Bagramon.
//
// ② live-top re-parent (MIG4-DETACH-LIVE-TOP witness): line :115's
//    `new IPlacePermanentToDigivolutionCards([[selectedPermanent, card.PermanentOfThisCard()]], toTop:false,
//    activateClass).PlacePermanentToDigivolutionCards()` folds ANOTHER permanent's whole live top (the DeadlyAxemon)
//    under this Digimon as a bottom digivolution source. The ported primitive (CardController.cs:2199-2382) performs
//    RemoveField(source) BEFORE the per-card AddDigivolutionCardsBottom→DetachEmbeddedSourceOrLinkAsync, so at detach
//    time the source top is already off the battle area and PermanentOfThisCard() is empty → the detach guard early-
//    returns (no STOP) and the top attaches under the host 1:1. Witnessed by LATENT-Close.Witness (re-parent tests).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return ContinuousController.instance.StartCoroutine(X)` → `await X`.
//    * `card.PermanentOfThisCard()` (used where AS-IS needs a Permanent) → `ICardEffect.ResolvePermanentOfThisCard(card)`
//      (the mirror Permanent; established idiom BT9_031/BT18_065). The IPlace host arg + DigivolveIntoHandOrTrashCard
//      targetPermanent both take this.
//    * `permanent != card.PermanentOfThisCard()` → `permanent.InstanceId != card.PermanentOfThisCard().TopInstanceId`
//      (permanent id == top id; Permanent is a per-access view — BT9_031 idiom).
//    * `CanSelectPermanentCondition`(Func<Permanent,bool>) drives HasMatchConditionPermanent,
//      MatchConditionPermanentCount, and SelectPermanentEffect.SetUp's canonical Func<Permanent,bool> overload
//      directly (no id adapter).
//    * `selectedPermanent.TopCard == null` (source dissolved after the fold) → `!IsPermanentExistsOnBattleArea(
//      selectedPermanent)` — the mirror Permanent.TopCard is never null (a view over its id); the AS-IS
//      "TopCard == null after RemoveField" state maps to "the source id is no longer a battle-area top".
//    * `card.PermanentOfThisCard().DigivolutionCards.Contains(topCard)` → `.DigivolutionCards.Any(c => c.InstanceId
//      == topCard.InstanceId)` (CardSource views — no reference identity).
//    * `TrashDigivolutionCardsFromTopOrBottom` returns Task<int> in the mirror — awaited, count ignored (AS-IS void
//      coroutine). `DigivolveIntoHandOrTrashCard(..., activateClass:, successProcess: null)` = AS-IS-signature bridge
//      (DigivolveAndTrashBridge.cs:34).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT7.Black;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT7_058 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Place a Digimon in digivolution cards to digivolve", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] You may trash all of the digivolution cards of 1 of your [DeadlyAxemon] and place it at the bottom of this Digimon's digivolution cards to digivolve this Digimon into a [DarkKnightmon] in your hand without paying its memory cost.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.InstanceId != card.PermanentOfThisCard().TopInstanceId)
                    {
                        if (permanent.TopCard.CardNames.Contains("DeadlyAxemon"))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.CardNames.Contains("DarkKnightmon");
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (!ICardEffect.ResolvePermanentOfThisCard(card).IsToken)
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    Permanent thisCardPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        Permanent selectedPermanent = null;

                        int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectPermanentCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: maxCount,
                            canNoSelect: true,
                            canEndNotMax: false,
                            selectPermanentCoroutine: SelectPermanentCoroutine,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to place in digivolution cards.", "The opponent is selecting 1 Digimon to place in digivolution cards.");

                        await selectPermanentEffect.Activate();

                        Task SelectPermanentCoroutine(Permanent permanent)
                        {
                            selectedPermanent = permanent;

                            return Task.CompletedTask;
                        }

                        if (selectedPermanent != null)
                        {
                            CardSource topCard = selectedPermanent.TopCard;

                            if (selectedPermanent.DigivolutionCards.Count((cardSource) => !cardSource.CanNotTrashFromDigivolutionCards(activateClass)) >= 1)
                            {
                                await CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom(targetPermanent: selectedPermanent, trashCount: selectedPermanent.DigivolutionCards.Count, isFromTop: true, activateClass: activateClass);
                            }

                            await new IPlacePermanentToDigivolutionCards(new List<Permanent[]>() { new Permanent[] { selectedPermanent, ICardEffect.ResolvePermanentOfThisCard(card) } }, false, activateClass).PlacePermanentToDigivolutionCards();

                            if (!CardEffectCommons.IsPermanentExistsOnBattleArea(selectedPermanent) && CardEffectCommons.IsExistOnBattleArea(card))
                            {
                                if (ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Any(c => c.InstanceId == topCard.InstanceId) || topCard.IsToken)
                                {
                                    await CardEffectCommons.DigivolveIntoHandOrTrashCard(
                                        targetPermanent: thisCardPermanent,
                                        cardCondition: CanSelectCardCondition,
                                        payCost: false,
                                        reduceCostTuple: null,
                                        fixedCostTuple: null,
                                        ignoreDigivolutionRequirementFixedCost: -1,
                                        isHand: true,
                                        activateClass: activateClass,
                                        successProcess: null);
                                }
                            }
                        }
                    }
                }
            }
        }

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (ICardEffect.ResolvePermanentOfThisCard(card).TopCard.ContainsCardName("Knightmon"))
                        {
                            return true;
                        }

                        if (ICardEffect.ResolvePermanentOfThisCard(card).TopCard.ContainsCardName("Bagramon"))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: true, card: card, condition: Condition));
        }

        return cardEffects;
    }
}
