namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_012 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnBlockAnyone)
        {
            cardEffects.Add(CardEffectFactory.SelfDpBuffTriggerEffect(
                timing: EffectTiming.OnBlockAnyone,
                changeValue: 2000,
                duration: EffectDuration.UntilEachTurnEnd,
                card: card,
                condition: () => CardEffectCommons.IsOwnerTurn(card),
                description: "[Your Turn] When this Digimon is blocked, it gets +2000 DP.",
                triggerGate: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card)));
        }

        return cardEffects;
    }
}