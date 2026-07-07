namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST4_04 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.SelfDpBuffTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                changeValue: 2000,
                duration: EffectDuration.UntilEachTurnEnd,
                card: card,
                condition: null,
                description: "[When Attacking] If you attack an opponent's Digimon, this Digimon gets +2000 DP for the turn.",
                triggerGate: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card) && card.Context.AttackController.Current.TargetId.HasValue));
        }

        return cardEffects;
    }
}
