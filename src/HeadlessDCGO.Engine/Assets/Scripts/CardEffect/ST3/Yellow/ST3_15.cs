// 1:1 mirror of the original ST3_15 (ST3/Yellow) — an Option.
//   [Main]     1 of your opponent's Digimon gains <Security Attack -3> until the end of your opponent's next
//              turn.  -> SelectAndBuffSAttackEffect (opponent Digimon, -3, UntilOpponentTurnEnd)
//   [Security] All of your opponent's Digimon gain <Security Attack -1> for the turn.
//              -> OpponentScopeBuffSAttackEffect (-1, UntilEachTurnEnd, scoped to the opponent)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST3_15 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffSAttackEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: -3,
                duration: EffectDuration.UntilOpponentTurnEnd,
                description: "[Main] 1 of your opponent's Digimon gains <Security Attack -3> until the end of your opponent's next turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.OpponentScopeBuffSAttackEffect(
                card: card,
                changeValue: -1,
                duration: EffectDuration.UntilEachTurnEnd,
                opponentId: CardEffectCommons.OpponentOf(card),
                description: "[Security] All of your opponent's Digimon gain <Security Attack -1> for the turn."));
        }

        return cardEffects;
    }
}
