namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_010 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnDestroyedAnyone,
                amount: 1,
                isInheritedEffect: false,
                card: card,
                condition: () => CardEffectCommons.IsOwnerTurn(card),
                description: "[On Deletion] If it's your turn, gain 1 memory."));
        }

        return cardEffects;
    }
}