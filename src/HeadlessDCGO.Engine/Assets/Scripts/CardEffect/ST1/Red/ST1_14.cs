// Source: Assets/Scripts/CardEffect/ST1/Red/ST1_14.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, ST1 timed-buff wave). Option.
//
// 1:1 mirror of the original ST1_14:
//   [Main]     All of your Security Digimon get +7000 DP until the end of your opponent's next turn.
//   [Security] All of your Security Digimon get +7000 DP for the turn.
// Both -> PlayerScopeBuffSecurityDpEffect (a player-scope DP buff scoped to the owner's Security-zone
// Digimon, duration-tagged). The original uses ChangeSecurityDigimonCardDPPlayerEffect; the headless
// registers a zone-scoped player-scope continuous binding (resolved imperatively for now, recipe §5).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class ST1_14 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(CardEffectFactory.PlayerScopeBuffSecurityDpEffect(
                card: card,
                changeValue: 7000,
                duration: EffectDuration.UntilOpponentTurnEnd,
                description: "[Main] All of your Security Digimon get +7000 DP until the end of your opponent's next turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlayerScopeBuffSecurityDpEffect(
                card: card,
                changeValue: 7000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Security] All of your Security Digimon get +7000 DP for the turn."));
        }

        return cardEffects;
    }
}
