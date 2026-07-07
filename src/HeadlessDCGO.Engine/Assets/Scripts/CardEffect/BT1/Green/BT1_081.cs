// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_081.cs
// 1:1 mirror of the original BT1_081 (BT1/Green).
//   [Piercing] (self, static)
//   -> OnDetermineDoSecurityCheck: CardEffectFactory.PierceSelfEffect (same shape as BT1_022 / BT1_026 /
//      ST4_13 / ST7_10 — this timing is a query-time keyword, not a trigger emission).
//   [End of Attack][Twice Per Turn] You can unsuspend this Digimon by decreasing your memory by 3.
//   -> STOP (see below).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_081 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        // STOP: [End of Attack][Twice Per Turn] "You can unsuspend this Digimon by decreasing your memory
        // by 3." (AS-IS: ActivateClass on OnEndAttack, CanUseCondition = CanTriggerOnAttack, CanActivateCondition
        // = IsExistOnBattleArea && Owner.MaxMemoryCost >= 3 [i.e. MemoryController.CanPay(3)], ORDER=2,
        // ISOPTIONAL=true, ActivateCoroutine = card.Owner.AddMemory(-3) THEN IUnsuspendPermanents(self)).
        // No catalog factory / uniform-ActivatedEffect IEffectBody covers "pay an N-memory cost, then apply a
        // self mutation": the existing bodies only cover the reverse (SuspendSelfAndGainMemoryBody: suspend-self
        // cost -> gain memory) or a hand-trash cost -> self mutation (SelectTrashHandThenSelfMutationBody, used
        // by BT1_039). Grepped ActivatedEffect.cs (all IEffectBody impls) and PRIMITIVE-CATALOG.md (Unsuspend*/
        // Memory* entries) twice — no "memory-cost then follow-up mutation" body/factory exists. Per the
        // primitive-gap rule this needs a new IEffectBody (e.g. a MemoryThenSelfMutationBody mirroring
        // SuspendSelfAndGainMemoryBody but with the cost/effect order reversed) — engine-side primitive
        // development, deferred to the strong-model primitive pass. — 강모델
        // if (timing == EffectTiming.OnEndAttack) { ... }

        return cardEffects;
    }
}
