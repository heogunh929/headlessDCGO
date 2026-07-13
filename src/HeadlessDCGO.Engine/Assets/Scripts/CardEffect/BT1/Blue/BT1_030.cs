// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_030.cs
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_030 (BT1/Blue).
//   [On Deletion] Gain 1 memory.
// AS-IS structure kept verbatim: inline ActivateClass, SetIsInheritedEffect(true).
// UNRESOLVED (kept verbatim, logged to docs/audit/rebuild_p5_cards_missing.md): AS-IS `card.Owner.
// CanAddMemory(activateClass)` — the mirror bridge only adds a `HeadlessPlayerId.AddMemory` extension
// (bridge W4), not `CanAddMemory` (that stays a `Player`-instance-only member, no bare-id extension).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_030 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Deletion] Gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanActivateOnDeletion(hashtable, card))
                {
                    // UNRESOLVED (see file header): AS-IS `card.Owner.CanAddMemory(activateClass)` — no mirror
                    // bridge for this exact member on the bare-id `Owner`. Kept verbatim.
                    if (card.Owner.CanAddMemory(activateClass))
                    {
                        return true;
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