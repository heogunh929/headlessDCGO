// Source: Assets/Scripts/CardEffect/BT1/Green/BT1_007.cs
// 1:1 mirror of the original BT1_007 (BT1/Green) — a DigiEgg's inherited effect.
//   [When Attacking] If you've digivolved this turn, this Digimon gets +1000 DP for the turn.
//   -> SelfDpBuffTriggerEffect(OnAllyAttack, +1000, UntilEachTurnEnd,
//        triggerGate = CanTriggerOnAttack, condition = IsExistOnBattleArea && DigivolveCountThisTurn >= 1).
// AS-IS CanActivateCondition gates on `card.Owner.DigivolveCount_ThisTurn >= 1` — now backed by the
// PlayerTurnCounters service (DigivolveAction increments on each digivolve, reset at turn start), read via
// CardEffectCommons.DigivolveCountThisTurn.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT1_007 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.SelfDpBuffTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                changeValue: 1000,
                duration: EffectDuration.UntilEachTurnEnd,
                card: card,
                condition: () => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.DigivolveCountThisTurn(card) >= 1,
                description: "[When Attacking] If you've digivolved this turn, this Digimon gets +1000 DP for the turn.",
                triggerGate: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card)));
        }

        return cardEffects;
    }
}
