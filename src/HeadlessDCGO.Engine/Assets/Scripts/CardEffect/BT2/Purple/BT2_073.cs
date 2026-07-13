// 1:1 mirror of BT2_073.
//   [Your Turn][Once Per Turn] When one of your other Digimon is deleted, gain 1 memory.
//   -> AddMemoryTriggerEffect (OnDestroyedAnyone, +1)
// Gates: [Your Turn] (condition) + own-Digimon-deleted-not-self (CanTriggerOnPermanentDeleted)
// + [Once Per Turn] (maxCountPerTurn=1 + hash).
// No IsDPZeroDelete gate (AS-IS does not require 0-DP delete).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_073 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);
            }

            bool TriggerGate(CardEffectResolveContext ctx)
            {
                return CardEffectCommons.CanTriggerOnPermanentDeleted(card, ctx,
                    id => CardEffectCommons.IsOwnerBattleAreaDigimon(card, id)
                       && id.Value != card.InstanceId.Value);
            }

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnDestroyedAnyone,
                amount: 1,
                isInheritedEffect: true,
                card: card,
                condition: Condition,
                description: "[Your Turn][Once Per Turn] When one of your other Digimon is deleted, gain 1 memory.",
                triggerGate: TriggerGate,
                maxCountPerTurn: 1,
                hash: "Memory+1_BT2_073",
                isOptional: false));
        }

        return cardEffects;
    }
}