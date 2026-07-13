// 1:1 mirror of the original BT1_076 (BT1/Green).
//   [When Attacking] If your opponent has 2 or more suspended Digimon, gain 1 memory.
//   -> AddMemoryTriggerEffect (OnAllyAttack, +1, condition: opponent suspended-Digimon count >= 2;
//      same shape as ST3_05 "If you have 4 or more security cards, gain 1 memory" — a triggerGate +
//      condition ActivateClass mirrored onto TriggeredMemoryEffect. AS-IS also checked
//      card.Owner.CanAddMemory(activateClass) before returning true; MatchStateMutationSink.ApplyMemory
//      already gates a positive AddMemory mutation against the CannotAddMemory restriction (PRIM-P0 B.O.6,
//      the AS-IS Player.CanAddMemory mirror), so that check is redundant here — same precedent as ST3_05.)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_076 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.MatchConditionOpponentsPermanentCount(card, permanent => permanent.IsDigimon && permanent.IsSuspended) >= 2;
            }

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                amount: 1,
                isInheritedEffect: true,
                card: card,
                condition: Condition,
                description: "[When Attacking] If your opponent has 2 or more suspended Digimon, gain 1 memory.",
                triggerGate: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card)));
        }

        return cardEffects;
    }
}
