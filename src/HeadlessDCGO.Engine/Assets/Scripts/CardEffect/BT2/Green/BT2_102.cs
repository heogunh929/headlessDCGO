namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_102 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        bool CanSelect(HeadlessEntityId id) =>
            CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
            && CardEffectCommons.IsSuspended(card, id);

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(CardEffectFactory.SelectAndReturnToDeckEffect(
                card: card,
                canTarget: CanSelect,
                maxCount: 1,
                toTop: false,
                canEndNotMax: false,
                description: "[Main] Return 1 of your opponent's suspended Digimon to the bottom of their deck. (Trash all of the digivolution cards of that Digimon.)"));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.SelectAndReturnToDeckEffect(
                card: card,
                canTarget: CanSelect,
                maxCount: 1,
                toTop: false,
                canEndNotMax: false,
                description: "Return 1 Digimon to deck"));
        }

        return cardEffects;
    }
}