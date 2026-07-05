namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_001 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.SelfDpBuffTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                changeValue: 1000,
                duration: EffectDuration.UntilEachTurnEnd,
                card: card,
                condition: null,
                description: "[When Attacking] If you attack an opponent's Digimon, this Digimon gets +1000 DP for the turn.",
                triggerGate: ctx => CardEffectCommons.CanTriggerOnPermanentAttack(ctx, card, null)));
        }

        return cardEffects;
    }
}