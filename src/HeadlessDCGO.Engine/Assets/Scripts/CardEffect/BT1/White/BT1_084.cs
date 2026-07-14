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
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; AS-IS `Func<Permanent,bool>` ->
//   `Func<HeadlessEntityId,bool>` idiom (IsOpponentBattleAreaDigimon); `card.Owner.Enemy` -> `new Player(
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

            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
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
        // digivolution cards to your hand." Already ported (branch 2) as SelectDigivolutionSourceToHandThenSelf
        // FollowUpEffect — NOT an ActivatedEffect, so out of the P8/R6-A ActivateClass conversion scope; kept verbatim.
        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectCardCondition(CardSource cardSource) =>
                cardSource.IsDigimon && cardSource.Level == 6 && cardSource.HasLevel;

            cardEffects.Add(new SelectDigivolutionSourceToHandThenSelfFollowUpEffect(
                card,
                canSelect: CanSelectCardCondition,
                isOptional: true,
                onSelected: sink => CardEffectCommons.UnsuspendSelf(sink, card),
                description: "[When Attacking] You can unsuspend this Digimon by returning 1 of this Digimon's level 6 digivolution cards to your hand."));
        }

        return cardEffects;
    }
}
