// 1:1 mirror of the original ST3_01 (ST3/Yellow).
//   [Your Turn][Once Per Turn] When an opponent's Digimon is deleted by dropping to 0 DP, this Digimon
//   gets +1000 DP for the turn.  -> SelfDpBuffTriggerEffect (OnDestroyedAnyone, +1000, UntilEachTurnEnd)
// Gates restored (G10-002): [Your Turn] (condition) + opponent-Digimon-deleted (CanTriggerOnPermanentDeleted)
// + 0-DP delete (IsDPZeroDelete, the DPZero marker) + [Once Per Turn] (maxCountPerTurn=1 + SetHashString).
// The trigger gate reads the deleted subject from the resolve context's TriggerEntityId.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST3_01 : CEntity_Effect
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

            cardEffects.Add(CardEffectFactory.SelfDpBuffTriggerEffect(
                timing: EffectTiming.OnDestroyedAnyone,
                changeValue: 1000,
                duration: EffectDuration.UntilEachTurnEnd,
                card: card,
                condition: Condition,
                description: "[Your Turn][Once Per Turn] When an opponent's Digimon is deleted by dropping to 0 DP, this Digimon gets +1000 DP for the turn.",
                triggerGate: TriggerGate,
                maxCountPerTurn: 1,
                hash: "DP+1000_ST3_01"));
        }

        return cardEffects;
    }
}
