// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS ST16_14
// OnDiscardHand block (ST16/Purple) — the F1-Tier1 OnDiscardHand witness.
//   [All Turns] "When one of your effects trashes a card in your hand, by suspending this Tamer, gain 1 memory."
// AS-IS: ActivateClass on OnDiscardHand, ORDER=-1 (uncapped; the suspend cost is the natural limiter),
// ISOPTIONAL=true ("by suspending" is a you-may cost). CanUseCondition = IsExistOnBattleArea &&
// CanTriggerOnTrashHand(SkillCondition, cardCondition): SkillCondition(ICardEffect) = cardEffect != null &&
// cardEffect.EffectSourceCard != null && cardEffect.EffectSourceCard.Owner == card.Owner (SELF effect);
// cardCondition(CardSource) = cardSource.Owner == card.Owner (a card in YOUR hand). CanActivateCondition =
// IsExistOnBattleArea && CanActivateSuspendCostEffect. ActivateCoroutine = SuspendPermanentsClass(self).Tap()
// THEN card.Owner.AddMemory(1).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; AS-IS `new SuspendPermanentsClass(
// new List<Permanent>{ card.PermanentOfThisCard() }, CardEffectHashtable(activateClass)).Tap()` -> the mirror
// ctor `(List<Permanent>, HeadlessEntityId? causeEffectSourceId, bool isBlock).Tap()` (BT1_088 idiom);
// `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)`; `card.Owner.AddMemory(1,
// activateClass)` -> the mirror HeadlessPlayerId extension.
//
// The AS-IS OnStartTurn (SetMemoryTo3TamerEffect) and SecuritySkill (PlaySelfTamerSecurityEffect) blocks are
// intentionally OMITTED (this witness exercises only the OnDiscardHand bridge — same scoping as before).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST16.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST16_14 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDiscardHand)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] When one of your effects trashes a card in your hand, by suspending this Tamer, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    bool SkillCondition(ICardEffect cardEffect)
                    {
                        if (cardEffect != null)
                        {
                            if (cardEffect.EffectSourceCard != null)
                            {
                                if (cardEffect.EffectSourceCard.Owner == card.Owner)
                                {
                                    return true;
                                }
                            }
                        }

                        return false;
                    }

                    if (CardEffectCommons.CanTriggerOnTrashHand(hashtable, SkillCondition, cardSource => cardSource.Owner == card.Owner))
                    {
                        return true;
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

                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return cardEffects;
    }
}
