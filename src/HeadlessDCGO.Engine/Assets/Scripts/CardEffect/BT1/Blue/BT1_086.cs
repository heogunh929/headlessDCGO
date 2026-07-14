// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_086.cs (a Blue Tamer, mixed timings)
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Your Turn] branch (the
// [Start of Your Turn] SetMemoryTo3TamerEffect and [Security] PlaySelfTamerSecurityEffect factory branches were
// already new-model and are untouched).
//   [Your Turn] When you play a blue Digimon, you can suspend this Tamer to trash the bottom digivolution
//   card of 1 of your opponent's Digimon.
// AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone. CanUseCondition = IsExistOnBattleArea(card) &&
// IsOwnerTurn(card) && CanTriggerOnPermanentPlay(hashtable, PermanentCondition), where PermanentCondition
// (permanent) = IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) &&
// permanent.TopCard.CardColors.Contains(Blue). CanActivateCondition = IsExistOnBattleArea(card) &&
// CanActivateSuspendCostEffect(card). ORDER=-1 (no once-per-turn cap), ISOPTIONAL=true ("you can").
// ActivateCoroutine: (1) cost -- SuspendPermanentsClass(this Tamer's permanent).Tap(); (2) select -- maxCount =
// Math.Min(1, MatchConditionPermanentCount(CanSelectPermanentCondition)) where CanSelectPermanentCondition
// (permanent) = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) &&
// permanent.DigivolutionCards.Count(c => !c.CanNotTrashFromDigivolutionCards(activateClass)) >= 1;
// SelectPermanentEffect.SetUp(selectPlayer: owner, maxCount, canNoSelect:false, canEndNotMax:false,
// mode: Custom); (3) effect -- for the selected permanent, TrashDigivolutionCardsFromTopOrBottom(trashCount:1,
// isFromTop:false).
// AS-IS structure kept verbatim: inline `new ActivateClass()` + local functions (including the cost-then-select
// two-step ActivateCoroutine). Substrate translations only: IEnumerator->Task, StartCoroutine->await;
// `permanent.TopCard.CardColors.Contains(Blue)` -> `permanent.TopCard.HasCardColor("Blue")`;
// `cardSource.CanNotTrashFromDigivolutionCards(activateClass)` -> the mirror `(HeadlessEntityId?)` overload,
// fed `activateClass.EffectSourceCard.InstanceId` (established causeId idiom, SelectPermanentEffect.cs);
// `new SuspendPermanentsClass(List<Permanent>, Hashtable).Tap()` -> the mirror ctor
// `(List<Permanent>, HeadlessEntityId? causeEffectSourceId, bool isBlock)` (same idiom the AS-IS-verbatim
// SelectPermanentEffect.Mode.Tap batch uses internally); `card.PermanentOfThisCard()` ->
// `ICardEffect.ResolvePermanentOfThisCard(card)`; `GManager.instance.GetComponent<SelectPermanentEffect>()`
// -> bridge W4.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_086 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnStartTurn)
        {
            cardEffects.Add(CardEffectFactory.SetMemoryTo3TamerEffect(card));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash 1 digivolution card", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When you play a blue Digimon, you can suspend this Tamer to trash the bottom digivolution card of 1 of your opponent's Digimon.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.HasCardColor("Blue"))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.DigivolutionCards.Count(cardSource => !cardSource.CanNotTrashFromDigivolutionCards(activateClass)) >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            // Id-shape adapter for the W4-bridged call sites (SetUp canTargetCondition takes
            // Func<HeadlessEntityId,bool>): resolve the mirror Permanent for the candidate id (BT9_109 idiom)
            // and evaluate the VERBATIM AS-IS predicate above.
            bool CanSelectPermanentConditionById(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                && CanSelectPermanentCondition(new Permanent(card.Context, id, rec.OwnerId));

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnPermanentPlay(hashtable, PermanentCondition))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanActivateSuspendCostEffect(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new SuspendPermanentsClass(
                    new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) },
                    activateClass.EffectSourceCard?.InstanceId,
                    isBlock: false).Tap();

                int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, (Func<Permanent, bool>)CanSelectPermanentCondition));

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentConditionById,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: maxCount,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: SelectPermanentCoroutine,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will trash digivolution cards.", "The opponent is selecting 1 Digimon that will trash digivolution cards.");

                await selectPermanentEffect.Activate();

                async Task SelectPermanentCoroutine(Permanent permanent)
                {
                    Permanent selectedPermanent = permanent;

                    await CardEffectCommons.TrashDigivolutionCardsFromTopOrBottom(targetPermanent: selectedPermanent, trashCount: 1, isFromTop: false, activateClass: activateClass);
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
