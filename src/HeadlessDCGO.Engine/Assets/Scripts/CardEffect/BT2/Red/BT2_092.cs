// 1:1 mirror of BT2_092 — an Option.
//   [Main] Up to 2 of your Digimon gain <Security Attack +1> for the turn.
//          -> SelectAndBuffSAttackEffect (own Digimon, +1, UntilEachTurnEnd, maxCount 2)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_092 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffSAttackEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 2,
                changeValue: +1,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Main] Up to 2 of your Digimon gain <Security Attack +1> (This Digimon checks 1 additional security card) for the turn."));
        }

        return cardEffects;
    }
}