// Source: Assets/Scripts/CardEffect/ST1/Red/ST1_16.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, ST1 activated wave). Option.
//
// 1:1 mirror of the original ST1_16:
//   [Main]     Delete 1 of your opponent's Digimon.        -> SelectAndDestroyEffect (OptionSkill)
//   [Security] (use the Main effect)                        -> AddActivateMainOptionSecurityEffect
// NOTE: the original builds an inline ActivateClass + SelectPermanentEffect; the headless uses the
// SelectAndDestroyEffect helper (select -> Delete mutation). The interactive Option/Security ACTIVATION
// flow (resolving these with a live choice provider) is not yet wired — these effects are resolved
// imperatively for now (see docs/audit/card_porting_recipe.md §5). The predicate is the headless
// entity-id form of the original Permanent predicate.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST1_16 : CEntity_Effect
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

            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[Main] Delete 1 of your opponent's Digimon."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Delete 1 Digimon");
        }

        return cardEffects;
    }
}
