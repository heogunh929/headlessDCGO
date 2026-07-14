// Source: DCGO/Assets/Scripts/CardEffect/ST4/Green/ST4_14.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [Your Turn] branch (the
// [Security] PlaySelfTamerSecurityEffect factory branch was already new-model and is untouched). 1:1 mirror
// of the AS-IS ST4_14 (ST4/Green Tamer).
//   [Your Turn] When one of your opponent's Digimon becomes suspended, you may suspend this Tamer to gain 1
//   memory.
// AS-IS: ActivateClass on OnTappedAnyone, ORDER=-1 (uncapped), ISOPTIONAL=true ("you may"). CanUseCondition =
// IsExistOnBattleArea && IsOwnerTurn && CanTriggerWhenPermanentSuspends(PermanentCondition =
// IsPermanentExistsOnOpponentBattleAreaDigimon). CanActivateCondition = IsExistOnBattleArea &&
// CanActivateSuspendCostEffect. ActivateCoroutine = SuspendPermanentsClass(self).Tap() THEN
// card.Owner.AddMemory(1).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; AS-IS `new SuspendPermanentsClass(
// new List<Permanent>{ card.PermanentOfThisCard() }, CardEffectHashtable(activateClass)).Tap()` -> the mirror
// ctor `(List<Permanent>, HeadlessEntityId? causeEffectSourceId, bool isBlock).Tap()` (established BT1_088
// idiom); `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)`;
// `card.Owner.AddMemory(1, activateClass)` -> the mirror HeadlessPlayerId extension (BT1_081/BT9_109 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST4_14 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnTappedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When one of your opponent's Digimon becomes suspended, you may suspend this Tamer to gain 1 memory.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerWhenPermanentSuspends(hashtable, PermanentCondition))
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

                await card.Owner.AddMemory(1, activateClass);
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
