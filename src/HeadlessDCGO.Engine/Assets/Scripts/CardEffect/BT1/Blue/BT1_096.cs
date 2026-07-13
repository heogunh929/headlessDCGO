// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_096.cs
// 1:1 headless mirror (Option).
//   [Main] 1 of your Digimon gets +3000 DP for the turn. -> SelectAndBuffDpEffect (OptionSkill, owner Digimon,
//                                                            maxCount 1, +3000, UntilEachTurnEnd)
//   [Security] Trigger <Draw 1>. Then add this card to    -> DrawCardsEffect (1) + AddThisCardToHandEffect
//     its owner's hand.                                       (SecuritySkill, in order)
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_096 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanTarget(HeadlessEntityId id) => CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanTarget,
                maxCount: 1,
                changeValue: 3000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Main] 1 of your Digimon gets +3000 DP for the turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.DrawCardsEffect(card, 1));
            cardEffects.Add(CardEffectFactory.AddThisCardToHandEffect(card));
        }

        return cardEffects;
    }
}
