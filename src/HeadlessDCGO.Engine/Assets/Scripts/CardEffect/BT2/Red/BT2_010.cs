// Source: DCGO/Assets/Scripts/CardEffect/BT2/Red/BT2_010.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_010.
//   [On Deletion] If it's your turn, gain 1 memory.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.AddMemoryTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure.
// AS-IS structure kept verbatim: no SetIsInheritedEffect call (AS-IS omits it here, unlike BT1_030's sibling),
// no SetHashString. Substrate translation only: IEnumerator->Task, `ContinuousController.instance.
// StartCoroutine(X)`->`await X`. AS-IS `card.Owner.CanAddMemory(activateClass)` now resolves against the
// `HeadlessPlayerId.CanAddMemory` extension added this batch (docs/audit/rebuild_p5_cards_missing.md).
// NOTE: this file lives under BT2/Red, matching the mirror's pre-existing (correct) location — the erroneous
// Blue-namespace declaration this file previously had (a leftover from the invented-model pass) is corrected
// to the AS-IS-matching namespace below.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT2_010 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Deletion] If it's your turn, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnTrash(card))
                {
                    if (card.Owner.CanAddMemory(activateClass))
                    {
                        if (CardEffectCommons.IsOwnerTurn(card))
                        {
                            return true;
                        }
                    }
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
