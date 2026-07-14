// Source: DCGO/Assets/Scripts/CardEffect/BT2/Blue/BT2_085.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the original
// BT2_085 (BT2/Blue Tamer).
//   [Your Turn] When one of your opponent's digivolution cards is trashed, you may suspend this Tamer to gain
//   1 memory.
//   [Security] Play this Tamer.  -> PlaySelfTamerSecurityEffect (already new-model, untouched).
// AS-IS structure kept verbatim: inline `new ActivateClass()` + local functions. CanUseCondition =
// IsExistOnBattleArea && IsOwnerTurn && CanTriggerOnTrashDigivolutionCard(hashtable, PermanentCondition,
// cardEffect => true, cardSource => true); CanActivateCondition = CanActivateSuspendCostEffect; ORDER=-1,
// ISOPTIONAL=true; ActivateCoroutine = SuspendPermanentsClass(self).Tap() THEN card.Owner.AddMemory(1).
// Substrate translations only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)`; `new SuspendPermanentsClass(
// {self}, CardEffectHashtable(activateClass))` -> the mirror carrier ctor `(List<Permanent>, HeadlessEntityId?
// cause, bool isBlock)` (cause = `activateClass.EffectSourceCard?.InstanceId`, isBlock=false — not a block-tap);
// `card.Owner.AddMemory(1, activateClass)` -> the mirror HeadlessPlayerId extension (established BT1_081 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT2_085 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDigivolutionCardDiscarded)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When one of your opponent's digivolution cards is trashed, you may suspend this Tamer to gain 1 memory.";
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
                        if (CardEffectCommons.CanTriggerOnTrashDigivolutionCard(hashtable, PermanentCondition, cardEffect => true, cardSource => true))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanActivateSuspendCostEffect(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new SuspendPermanentsClass(new List<Permanent>() { selectedPermanent }, activateClass, false).Tap();

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
