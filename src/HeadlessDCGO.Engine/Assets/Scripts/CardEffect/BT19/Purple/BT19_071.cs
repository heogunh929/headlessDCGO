// Source: DCGO/Assets/Scripts/CardEffect/BT19/Purple/BT19_071.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [All Turns][Once Per Turn]
// OnDiscardLibrary branch — the F1-Tier1 OnDiscardLibrary witness.
//   [All Turns][Once Per Turn] When effects trash cards from your deck, delete 1 of your opponent's level 5 or
//   lower Digimon. — AS-IS `new ActivateClass()` + SetUpActivateClass(..., 1, false, ...) (ORDER 1 = once per turn,
//   mandatory) + SetHashString("DeleteDigimon_BT19_071") (BT19_071.cs:102-167). CanUse = IsExistOnBattleAreaDigimon
//   && CanTriggerWhenDiscardLibrary(cardSource.Owner == card.Owner). CanActivate = IsExistOnBattleAreaDigimon &&
//   HasMatchConditionPermanent(opponent battle-area Digimon, level <= 5). Body = SelectPermanentEffect(Mode.Destroy,
//   maxCount Min(1,count)).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await;
// `GManager.instance.GetComponent<SelectPermanentEffect>()` + full AS-IS SetUp (bridge W4, BT9_062 idiom); AS-IS
// `Func<Permanent,bool> CanSelectPermanentCondition` wrapped to the entity-id predicate via `PermanentOf(id)`.
//
// The AS-IS [On Play] and [When Digivolving] blocks (trash top 2 of deck, then gain <Blocker>) remain deliberately
// OMITTED — this witness exercises only the OnDiscardLibrary bridge (same scoping as the prior pass).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Purple;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT19_071 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDiscardLibrary)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 of your opponent's level 5 or lower Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("DeleteDigimon_BT19_071");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns][Once Per Turn] When effects trashes cards from your deck, delete 1 of your opponent's level 5 or lower Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerWhenDiscardLibrary(hashtable, cardSource => cardSource.Owner == card.Owner))
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
                    if (permanent.TopCard.HasLevel && permanent.Level <= 5)
                    {
                        return true;
                    }
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

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) && CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition);
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
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Destroy,
                    cardEffect: activateClass);

                await selectPermanentEffect.Activate();
            }
        }

        return cardEffects;
    }
}
