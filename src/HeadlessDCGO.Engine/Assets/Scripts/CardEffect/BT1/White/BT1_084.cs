// Source: DCGO/Assets/Scripts/CardEffect/BT1/White/BT1_084.cs (a White Digimon, two branches)
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [When Digivolving]
// delete branch (branch 1); the [When Attacking] return-and-unsuspend branch (branch 2) is already ported as
// SelectDigivolutionSourceToHandThenSelfFollowUpEffect (not an ActivatedEffect) and is UNCHANGED.
//   [When Digivolving] Choose 1 of your opponent's Digimon. Delete all of your opponent's Digimon that share a
//   name with it.
// AS-IS branch 1: ActivateClass declared under OnEnterFieldAnyone but CanUseCondition = CanTriggerWhenDigivolving
//   -> registered under the mirror WhenDigivolving key (BT1_074/ST1_08/BT1_017 dispatch-remap idiom); the gate
//   itself is verbatim. CanActivateCondition = IsExistOnBattleArea && HasMatchConditionPermanent(CanSelect),
//   CanSelect = IsPermanentExistsOnOpponentBattleAreaDigimon. ORDER=-1, ISOPTIONAL=false. ActivateCoroutine:
//   maxCount = Min(1, MatchConditionPermanentCount); SelectPermanentEffect.SetUp(mode: Custom, canNoSelect:false,
//   canEndNotMax:false, selectPermanentCoroutine) picks 1 reference; SelectPermanentCoroutine derives
//   destroyTargetPermanents = card.Owner.Enemy.GetBattleAreaDigimons().Filter(p => p.TopCard.HasSameCardName(
//   reference.TopCard)) and deletes them via new DestroyPermanentsClass(targets, hashtable).Destroy() (reflexive).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; AS-IS `Func<Permanent,bool>` kept
//   verbatim on the canonical shape (id-flip 3b); `card.Owner.Enemy` -> `new Player(
//   card.Context, card.Owner).Enemy!` (established Player mirror route); GManager.GetComponent -> bridge W4.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.White;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_084 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Choose 1 of your opponent's Digimon. Delete all of your opponent's Digimon that share a name with it.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
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

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon.", "The opponent is selecting 1 Digimon.");

                await selectPermanentEffect.Activate();

                async Task SelectPermanentCoroutine(Permanent permanent)
                {
                    List<Permanent> destroyTargetPermanents = new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons().Filter((permanent1) => permanent1.TopCard.HasSameCardName(permanent.TopCard));
                    await new DestroyPermanentsClass(destroyTargetPermanents, CardEffectCommons.CardEffectHashtable(activateClass)).Destroy();
                }
            }
        }

        // [When Attacking] "You can unsuspend this Digimon by returning 1 of this Digimon's level 6
        // digivolution cards to your hand." (이연③-d) Re-ported the invented
        // SelectDigivolutionSourceToHandThenSelfFollowUpEffect -> the literal AS-IS inline ActivateClass:
        // SelectCardEffect(mode: AddHand, root: Custom over this permanent's DigivolutionCards, canNoSelect:false,
        // maxCount 1) THEN `new IUnsuspendPermanents(self).Unsuspend()` — both live mirror components (BT9_043 /
        // BT9_081 idiom). Substrate: IEnumerator->Task, StartCoroutine->await; `card.PermanentOfThisCard()` ->
        // `ICardEffect.ResolvePermanentOfThisCard(card)` (returns the mirror Permanent whose DigivolutionCards is
        // the List<CardSource> customRootCardList / IUnsuspendPermanents target).
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return 1 digivolution card to unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] You can unsuspend this Digimon by returning 1 of this Digimon's level 6 digivolution cards to your hand.";
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (cardSource.Level == 6)
                    {
                        if (cardSource.HasLevel)
                        {
                            return true;
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
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                if (selectedPermanent.DigivolutionCards.Count(CanSelectCardCondition) >= 1)
                {
                    int maxCount = Math.Min(1, selectedPermanent.DigivolutionCards.Count(CanSelectCardCondition));

                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                                canTargetCondition: CanSelectCardCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                canNoSelect: () => false,
                                selectCardCoroutine: null,
                                afterSelectCardCoroutine: null,
                                message: "Select 1 digivolution card to add to your hand.",
                                maxCount: maxCount,
                                canEndNotMax: false,
                                isShowOpponent: true,
                                mode: SelectCardEffect.Mode.AddHand,
                                root: SelectCardEffect.Root.Custom,
                                customRootCardList: selectedPermanent.DigivolutionCards.ToList(),
                                canLookReverseCard: true,
                                selectPlayer: card.Owner,
                                cardEffect: activateClass);

                    await selectCardEffect.Activate();

                    await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
                }
            }
        }

        return cardEffects;
    }
}
