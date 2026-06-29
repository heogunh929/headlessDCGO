// Source: Assets/Scripts/CardEffect/ST1/Red/ST1_15.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, ST1 activated wave). Option.
//
// 1:1 mirror of the original ST1_15:
//   [Main]     Delete up to 2 of your opponent's Digimon with 4000 DP or less.  -> SelectAndDestroyEffect
//   [Security] (use the Main effect)                                            -> AddActivateMainOptionSecurityEffect
// NOTE: select -> Delete via SelectAndDestroyEffect (interactive activation flow not yet wired; resolved
// imperatively). The original threshold uses card.Owner.MaxDP_DeleteEffect(4000); the headless uses a
// flat 4000 (delete-threshold raising effects are a later refinement).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST1_15 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                {
                    if (CardEffectCommons.CurrentDp(card, id) <= 4000)
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 2,
                canEndNotMax: true,
                description: "[Main] Delete up to 2 of your opponent's Digimon with 4000 DP or less."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Delete up to 2 Digimon with 4000 DP or less");
        }

        return cardEffects;
    }
}
