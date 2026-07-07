// Source: Assets/Scripts/CardEffect/BT3/Red/BT3_098.cs
// Decision: PORT
// Category: CardEffect
// Priority: HIGH
// Migration: Port per-card effect source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red
// AS-IS: [Main]  Delete 1 of your opponent's Digimon with 13000 DP or more.
//        [Security] Add this card to its owner's hand.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_098 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = [];

        if (timing == EffectTiming.OptionSkill)
        {
            const string description = "[Main] Delete 1 of your opponent's Digimon with 13000 DP or more.";

            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                && CardEffectCommons.CurrentDp(card, id) >= 13000;

            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: description));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(
                card: card,
                cardEffects: ref cardEffects,
                effectName: "Delete Digimon with 13000 DP or more");
        }

        return cardEffects;
    }
}
