// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_081.cs
// 1:1 mirror of the original BT1_081 (BT1/Green).
//   [Piercing] (self, static)
//   -> OnDetermineDoSecurityCheck: CardEffectFactory.PierceSelfEffect (same shape as BT1_022 / BT1_026 /
//      ST4_13 / ST7_10 — this timing is a query-time keyword, not a trigger emission).
//   [End of Attack][Twice Per Turn] You can unsuspend this Digimon by decreasing your memory by 3.
//   -> STOP (see below).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

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

        // [End of Attack][Twice Per Turn] "You can unsuspend this Digimon by decreasing your memory by 3."
        // AS-IS: ActivateClass on OnEndAttack, CanUseCondition = CanTriggerOnAttack, CanActivateCondition =
        // IsExistOnBattleArea && MemoryController.CanPay(3), ORDER=2 ([Twice Per Turn] -> maxCountPerTurn:2),
        // ISOPTIONAL=true, ActivateCoroutine = card.Owner.AddMemory(-3) THEN IUnsuspendPermanents(self).Unsuspend().
        // Headless mirror: uniform ActivatedEffect + MemoryCostThenUnsuspendSelfBody (the reverse-order sibling of
        // SuspendSelfAndGainMemoryBody: pay -3 memory as a cost, then unsuspend this card's own permanent).
        if (timing == EffectTiming.OnEndAttack)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEndAttack,
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card) && card.Context.MemoryController.CanPay(3),
                body: new MemoryCostThenUnsuspendSelfBody(memoryCost: 3),
                maxCountPerTurn: 2,
                isOptional: true,
                description: "[End of Attack][Twice Per Turn] You can unsuspend this Digimon by decreasing your memory by 3."));
        }

        return cardEffects;
    }
}
