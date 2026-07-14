// Source: DCGO/Assets/Scripts/CardEffect/BT13/Blue/BT13_023.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass).
//   * <Evade> (WhenPermanentWouldBeDeleted) -> CardEffectFactory.EvadeSelfEffect(isInheritedEffect:false)
//     (unchanged keyword factory; the PRE would-be-deleted window offers "suspend to survive").
//   * [When Attacking] Trash the digivolution card at the bottom of 1 of your opponent's Digimon. — AS-IS `new
//     ActivateClass()` + SetIsInheritedEffect(true) + SetUpActivateClass(..., -1, false, ...) (uncapped, mandatory)
//     (BT13_023.cs:20-88). CanUse = CanTriggerOnAttack. CanActivate = IsExistOnBattleArea &&
//     HasMatchConditionPermanent(opponent battle-area Digimon). Body = SelectPermanentEffect(Mode.Custom, maxCount
//     Min(1,count)) -> TrashDigivolutionCardsFromTopOrBottom(pick, 1, isFromTop:false).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await;
// `GManager.instance.GetComponent<SelectPermanentEffect>()` + full AS-IS SetUp (bridge W4, BT9_062 idiom); AS-IS
// `Func<Permanent,bool> CanSelectPermanentCondition` wrapped to the entity-id predicate via `PermanentOf(id)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT13.Blue;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT13_023 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.EvadeSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash 1 digivolution card", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Trash the digivolution card at the bottom of 1 of your opponent's Digimon.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    return true;
                }

                return false;
            }

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            bool CanSelectPermanentById(HeadlessEntityId id)
            {
                Permanent? permanent = PermanentOf(id);
                return permanent is not null && CanSelectPermanentCondition(permanent);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
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
                int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentById));

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentById,
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

        return cardEffects;
    }
}
