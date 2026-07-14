// Source: DCGO/Assets/Scripts/CardEffect/ST2/Blue/ST2_11.cs
// TRUE AS-IS-verbatim re-port (batch: ST2 Blue). 1:1 mirror of the original ST2_11.
//   [When Attacking][Once Per Turn] Unsuspend this Digimon.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.UnsuspendSelfTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` + `IUnsuspendPermanents`
// structure (identical shape to BT1_115.cs's attack-half / BT2_002.cs's sibling pattern).
// AS-IS structure kept verbatim: inline ActivateClass, priority=1 + SetHashString("Unsuspend_ST2_11") (the
// "Once Per Turn" enforcement — no explicit CanActivateCondition count check in AS-IS besides IsExistOnBattleArea).
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `card.PermanentOfThisCard()` (used as a Permanent) -> `ICardEffect.ResolvePermanentOfThisCard(card)`;
// AS-IS `new IUnsuspendPermanents(list, activateClass)` (ICardEffect 2nd arg) -> mirror ctor takes
// `HeadlessEntityId?` -> `activateClass.EffectSourceCard?.InstanceId` (established idiom, see BT1_115.cs).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST2_11 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetHashString("Unsuspend_ST2_11");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking][Once Per Turn] Unsuspend this Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
            }
        }

        return cardEffects;
    }
}
