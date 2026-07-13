// Source: DCGO/Assets/Scripts/CardEffect/BT2/Purple/BT2_073.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_073 (BT2/Purple).
//   [Your Turn][Once Per Turn] When one of your other Digimon is deleted, gain 1 memory.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.AddMemoryTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure.
// AS-IS structure kept verbatim: inline ActivateClass, priority=1 (not -1), SetIsInheritedEffect(true),
// SetHashString("Memory+1_BT2_073") (the AS-IS [Once Per Turn] hash-dedup key).
// Substrate translations: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `Func<Permanent,bool> PermanentCondition` (`permanent != card.PermanentOfThisCard()`, "one of your
// OTHER Digimon") -> the established Permanent-vs-PermanentView identity idiom
// (`permanent.InstanceId != card.PermanentOfThisCard().TopInstanceId`, see BT2_002.cs);
// `CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable, PermanentCondition)` -> the real AS-IS-signature
// Hashtable+Func&lt;Permanent,bool&gt; overload (CardEffectCommons/CanUseEffects/OnDeletion.cs); AS-IS
// `card.Owner.AddMemory(1, activateClass)` -> the `HeadlessPlayerId.AddMemory` extension (bridge W4/PRIM).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT2_073 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("Memory+1_BT2_073");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn][Once Per Turn] When one of your other Digimon is deleted, gain 1 memory.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.InstanceId != card.PermanentOfThisCard().TopInstanceId)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable, PermanentCondition))
                    {
                        if (CardEffectCommons.IsOwnerTurn(card))
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
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return cardEffects;
    }
}
