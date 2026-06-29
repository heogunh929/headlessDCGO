// Source: Assets/Scripts/CardEffect/ST1/Red/ST1_13.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, ST1 timed-buff wave). Option.
//
// 1:1 mirror of the original ST1_13:
//   [Main]     1 of your Digimon gets +3000 DP for the turn.                 -> SelectAndBuffDpEffect
//   [Security] All your Digimon gain <Security Attack +1> until your next     -> PlayerScopeBuffSAttackEffect
//              turn's end.
// NOTE: the original selects via SelectPermanentEffect + coroutine ChangeDigimonDP / uses
// ChangeDigimonSAttackPlayerEffect. The headless registers a duration-tagged continuous modifier (on the
// chosen target / player-scope) that the gate folds in and EffectDurationExpiry removes. Interactive
// Option/Security activation is resolved imperatively for now (recipe §5).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST1_13 : CEntity_Effect
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
            cardEffects.Add(CardEffectFactory.PlayerScopeBuffSAttackEffect(
                card: card,
                changeValue: 1,
                duration: EffectDuration.UntilOwnerTurnEnd,
                description: "[Security] All of your Digimon gain <Security Attack +1> until the end of your next turn."));
        }

        return cardEffects;
    }
}
