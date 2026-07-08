namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_012 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.SelfDpBuffTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                changeValue: 4000,
                duration: EffectDuration.UntilEachTurnEnd,
                card: card,
                condition: null,
                description: "[When Attacking] When this Digimon attacks a player, it gets +4000 DP for the turn.",
                triggerGate: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card)));
        }

        return cardEffects;
    }
}