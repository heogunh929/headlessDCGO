// Source: Assets/Scripts/CardEffect/BT2/BT2_091.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect. Option.
//
// 1:1 mirror of the original BT2_091:
//   [Main]     Delete 1 of your opponent's Digimon with 4000 DP or less.  -> SelectAndDestroyEffect
//   [Security] (use the Main effect)                                       -> AddActivateMainOptionSecurityEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_091 : CEntity_Effect
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
                    if (CardEffectCommons.CurrentDp(card, id) <= CardEffectCommons.MaxDpDeleteThreshold(card, baseThreshold: 4000))
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[Main] Delete 1 of your opponent's Digimon with 4000 DP or less."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Delete 1 Digimon with 4000 DP or less");
        }

        return cardEffects;
    }
}