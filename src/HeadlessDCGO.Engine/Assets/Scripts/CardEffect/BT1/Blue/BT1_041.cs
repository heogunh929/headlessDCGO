// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_041.cs
// 1:1 headless mirror.
//   [On Play] Trigger <Draw 2>.                          -> DrawCardsEffect (OnEnterFieldAnyone, 2)
//   [When Attacking] If opponent has a Digimon with no    -> AddMemoryTriggerEffect (OnAllyAttack, +1,
//     digivolution cards in play, gain 1 memory.             condition: opponent no-source Digimon; self-scoped)
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_041 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            cardEffects.Add(CardEffectFactory.DrawCardsEffect(card, 2));
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool Condition() => CardEffectCommons.HasMatchConditionOpponentsPermanent(
                card, id => CardEffectCommons.IsBattleAreaDigimon(card, id) && CardEffectCommons.HasNoDigivolutionCards(card, id));

            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                amount: 1,
                isInheritedEffect: false,
                card: card,
                condition: Condition,
                description: "[When Attacking] If your opponent has a Digimon with no digivolution cards in play, gain 1 memory.",
                triggerGate: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card)));
        }

        return cardEffects;
    }
}
