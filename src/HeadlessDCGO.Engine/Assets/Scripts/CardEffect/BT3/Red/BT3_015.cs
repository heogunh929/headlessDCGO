namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_015 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = [];

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectCardCondition(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner, card.Owner);
                return candidate.IsDigimon && candidate.Level == 7 && candidate.HasTrait("Virus") && candidate.HasLevel;
            }

            cardEffects.Add(CardEffectFactory.SelectAndAddToHandFromZoneEffect(
                card,
                ChoiceZone.Trash,
                CanSelectCardCondition,
                maxCount: 1,
                canEndNotMax: true,
                description: "[When Digivolving] You may return 1 level 7 Digimon card with [Virus] in its attribute from your trash to your hand."));
        }

        return cardEffects;
    }
}
