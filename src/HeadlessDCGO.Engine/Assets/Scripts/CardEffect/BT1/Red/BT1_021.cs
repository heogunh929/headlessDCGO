namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_021 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnAllyAttack,
                amount: 3,
                isInheritedEffect: false,
                card: card,
                condition: null,
                description: "[When Attacking] Gain 3 memory. At end of turn lose 3 memory."));
        }

        if (timing == EffectTiming.OnEndTurn)
        {
            cardEffects.Add(CardEffectFactory.EoTLose3Memory(card));
        }

        return cardEffects;
    }
}