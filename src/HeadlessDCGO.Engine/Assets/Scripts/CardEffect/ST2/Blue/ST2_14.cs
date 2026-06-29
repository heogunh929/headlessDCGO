// 1:1 mirror of the original ST2_14 (ST2/Blue) — an Option.
//   [Main]     Choose 1 of your opponent's Digimon with no digivolution cards. That Digimon can't attack
//              or block until the end of your opponent's next turn.  -> SelectAndRestrictEffect (UntilOpponentTurnEnd)
//   [Security] Same, until the end of your next turn.                -> SelectAndRestrictEffect (UntilOwnerTurnEnd)
// Resolved imperatively (BuildRequest -> answer -> ApplyRestriction); each registers a duration-tagged
// can't-attack + can't-block restriction binding on the chosen target.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_14 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        bool CanSelectPermanentCondition(HeadlessEntityId id)
        {
            return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                && CardEffectCommons.HasNoDigivolutionCards(card, id);
        }

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(CardEffectFactory.SelectAndRestrictEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                duration: EffectDuration.UntilOpponentTurnEnd,
                cannotAttack: true,
                cannotBlock: true,
                description: "[Main] Choose 1 of your opponent's Digimon with no digivolution cards. That Digimon can't attack or block until the end of your opponent's next turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.SelectAndRestrictEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                duration: EffectDuration.UntilOwnerTurnEnd,
                cannotAttack: true,
                cannotBlock: true,
                description: "[Security] Choose 1 of your opponent's Digimon with no digivolution cards. That Digimon can't attack or block until the end of your next turn."));
        }

        return cardEffects;
    }
}
