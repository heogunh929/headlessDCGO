// Source: DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_114.cs
// TRUE AS-IS-verbatim re-port (P5, bridge-complete pass).
// 1:1 mirror of the original BT1_114:
//   [All Turns] Security Attack +2.        -> CardEffectFactory.ChangeSelfSAttackStaticEffect (AS-IS factory call, unchanged)
//   [When Attacking] Lose 5 memory.        -> inline `new ActivateClass()` (AS-IS structure, this pass)
//   [Your Turn] DP+3000.                   -> CardEffectFactory.ChangeSelfDPStaticEffect (AS-IS factory call, unchanged)
//
// [When Attacking] AS-IS body (BT1_114.cs): ActivateClass on EffectTiming.OnAllyAttack, CanUseCondition =
// CanTriggerOnAttack(hashtable, card), CanActivateCondition = IsExistOnBattleArea(card), ORDER=-1,
// ISOPTIONAL=false, ActivateCoroutine = `yield return
// ContinuousController.instance.StartCoroutine(card.Owner.AddMemory(-5, activateClass))`.
//
// UNRESOLVED MEMBER (kept verbatim, not simplified/faked): AS-IS `card.Owner` is the AS-IS `Player` object,
// which exposes an instance `AddMemory(int, ICardEffect)` coroutine (Player.cs:1082). The mirror `CardSource.Owner`
// is a bare `HeadlessPlayerId` (no `Player` handle at the card-effect call site), and the mirror `Player` class
// (Assets/Scripts/Script/Player.cs, MIG5) does not expose `AddMemory` at all (design item MIG5-CANADDMEMORY only
// covers `CanAddMemory`) — there is no CardEffectCommons.* bridge for this AS-IS `Player` instance method (the W1-W3
// bridge batches only covered `CardEffectCommons.*` mutation coroutines, not `Player.*` ones). Per the task's
// no-fallback rule, the call is kept in AS-IS shape (translated only by the standard IEnumerator->Task /
// StartCoroutine->await substrate rules) rather than substituted with the old-model `AddMemoryTriggerEffect`
// factory/`MemoryBody` route. Logged: docs/audit/rebuild_p5_cards_missing.md.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_114 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(
                changeValue: 2,
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory -5", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Lose 5 memory.";
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
                // UNRESOLVED (see file header): AS-IS `card.Owner.AddMemory(-5, activateClass)` — no mirror
                // Player.AddMemory bridge exists. Kept verbatim (not routed through the old-model
                // AddMemoryTriggerEffect/MemoryBody). Logged to docs/audit/rebuild_p5_cards_missing.md.
                await card.Owner.AddMemory(-5, activateClass);
            }
        }

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(
                changeValue: 3000,
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}
