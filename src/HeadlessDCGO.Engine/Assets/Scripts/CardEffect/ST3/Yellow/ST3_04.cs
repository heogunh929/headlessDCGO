// 1:1 mirror of the original ST3_04 (ST3/Yellow).
//   [Your Turn][Once Per Turn] When an opponent's Digimon is deleted by dropping to 0 DP, gain 1 memory.
//   -> AddMemoryTriggerEffect (OnDestroyedAnyone, +1)
// Gates restored (G10-002): [Your Turn] (condition) + opponent-Digimon-deleted (CanTriggerOnPermanentDeleted)
// + 0-DP delete (IsDPZeroDelete) + [Once Per Turn] (maxCountPerTurn=1 + SetHashString).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST3_04 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
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
                return CardEffectCommons.CanTriggerOnPermanentDeleted(card, ctx, id => CardEffectCommons.IsOpponentOwnedDigimon(card, id))
                    && CardEffectCommons.IsDPZeroDelete(card, ctx);
            }

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnDestroyedAnyone,
                amount: 1,
                isInheritedEffect: true,
                card: card,
                condition: Condition,
                description: "[Your Turn][Once Per Turn] When an opponent's Digimon is deleted by dropping to 0 DP, gain 1 memory.",
                triggerGate: TriggerGate,
                maxCountPerTurn: 1,
                hash: "Memory+1_ST3_04"));
        }

        return cardEffects;
    }
}
