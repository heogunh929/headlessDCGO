namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_025 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffSAttackEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: 1,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[When Digivolving] This Digimon gains <Security Attack +1> for the turn."));
        }

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.JammingSelfStaticEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        return cardEffects;
    }
}