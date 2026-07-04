using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT18.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT18_034 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsCardName("Cupimon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 5,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null)
            );
        }

        // Start of Your Main Phase
        // STOP: OnStartMainPhase — 강모델 (not supported in headless)
        // This branch uses unsupported timing, so we'll leave it unimplemented for now

        // On Play
        // STOP: OnEnterFieldAnyone — 강모델 (not supported in headless)
        // This branch uses unsupported timing, so we'll leave it unimplemented for now

        // End of Your Turn
        // STOP: OnEndTurn — 강모델 (not supported in headless)
        // This branch uses unsupported timing, so we'll leave it unimplemented for now

        return cardEffects;
    }
}
