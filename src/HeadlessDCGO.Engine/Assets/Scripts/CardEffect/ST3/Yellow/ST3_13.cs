// 1:1 mirror of the original ST3_13 (ST3/Yellow) — an Option.
//   [Main]     1 of your Digimon gets +3000 DP for the turn.  -> SelectAndBuffDpEffect (owner Digimon, +3000)
//   [Security] All of your Digimon and Security Digimon get +5000 DP for the turn. Then add this card to its
//              owner's hand.  -> PlayerScopeBuffDpEffect + PlayerScopeBuffSecurityDpEffect + AddThisCardToHandEffect
// Resolved imperatively until the interactive activation flow is wired.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST3_13 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: 3000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Main] 1 of your Digimon gets +3000 DP for the turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlayerScopeBuffDpEffect(
                card, changeValue: 5000, duration: EffectDuration.UntilEachTurnEnd,
                description: "[Security] All of your Digimon get +5000 DP for the turn."));
            cardEffects.Add(CardEffectFactory.PlayerScopeBuffSecurityDpEffect(
                card, changeValue: 5000, duration: EffectDuration.UntilEachTurnEnd,
                description: "[Security] All of your Security Digimon get +5000 DP for the turn."));
            cardEffects.Add(new AddThisCardToHandEffect(card, "Then add this card to its owner's hand."));
        }

        return cardEffects;
    }
}
