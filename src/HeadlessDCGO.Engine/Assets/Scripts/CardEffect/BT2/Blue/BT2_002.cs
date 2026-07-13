// 1:1 mirror of the original BT2_002.
//   [Your Turn][Once Per Turn] When this Digimon becomes unsuspended during your main phase,
//   it gets +1000 DP for the turn.  -> SelfDpBuffTriggerEffect (OnUnTappedAnyone, +1000, UntilEachTurnEnd)
// Gates: [Your Turn] (condition: IsExistOnBattleArea + IsOwnerTurn)
//        + self-unsuspend (CanTriggerWhenPermanentUnsuspends, permanentCondition=null → self)
//        + [Once Per Turn] (maxCountPerTurn=1 + hash).
// Main-phase check dropped: per cheatsheet §4, unsuspend timing already forces main phase implicitly.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_002 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnUnTappedAnyone)
        {
            bool Condition()
            {
                return CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);
            }

            bool TriggerGate(CardEffectResolveContext ctx)
            {
                return CardEffectCommons.CanTriggerWhenPermanentUnsuspends(ctx, card, null);
            }

            cardEffects.Add(CardEffectFactory.SelfDpBuffTriggerEffect(
                timing: EffectTiming.OnUnTappedAnyone,
                changeValue: 1000,
                duration: EffectDuration.UntilEachTurnEnd,
                card: card,
                condition: Condition,
                description: "[Your Turn][Once Per Turn] When this Digimon becomes unsuspended during your main phase, it gets +1000 DP for the turn.",
                triggerGate: TriggerGate,
                maxCountPerTurn: 1,
                hash: "DP+1000_BT2_002"));
        }

        return cardEffects;
    }
}