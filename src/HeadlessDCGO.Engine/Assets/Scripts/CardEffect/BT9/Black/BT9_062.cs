// Source: DCGO/Assets/Scripts/CardEffect/BT9/Black/BT9_062.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [End of Attack] INHERITED
// (digivolution-source) branch.
//   [End of Attack] If this Digimon has [Alphamon] in its name, delete 1 of your opponent's Digimon with a play
//   cost of 5 or less.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpActivateClass(..., -1, false, ...) (uncapped,
// mandatory) + SetIsInheritedEffect(true) (BT9_062.cs:16-90). Substrate translations only: IEnumerator->Task,
// StartCoroutine->await; `GManager.instance.GetComponent<SelectPermanentEffect>()` + full AS-IS SetUp(Mode.Destroy)
// (bridge W4, established BT2_092 idiom); AS-IS `Func<Permanent,bool> CanSelectPermanentCondition` supplied to
// SetUp/MatchConditionPermanentCount as the entity-id predicate (`PermanentOf(id)` reconstruction, established
// BT9_021 idiom); `card.PermanentOfThisCard().TopCard.ContainsCardName("Alphamon")` (the HOST top card, an
// inherited source resolves PermanentOfThisCard to its host) -> `ICardEffect.ResolvePermanentOfThisCard(card)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Black;

using System;
using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT9_062 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEndAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 Digimon with 5 or less Cost", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[End of Attack] If this Digimon has [Alphamon] in its name, delete 1 of your opponent's Digimon with a play cost of 5 or less.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.GetCostItself <= 5)
                    {
                        if (permanent.TopCard.HasPlayCost)
                        {
                            return true;
                        }
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
                        if (ICardEffect.ResolvePermanentOfThisCard(card).TopCard.ContainsCardName("Alphamon"))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
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
        }

        return cardEffects;
    }
}
