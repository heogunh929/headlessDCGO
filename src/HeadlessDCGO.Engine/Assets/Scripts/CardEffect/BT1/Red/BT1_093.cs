using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_093 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            // STOP: CardEffectCommons.HasMatchConditionPermanent is not in the catalog — 강모델
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            // STOP: CardEffectCommons.AddThisCardToHand is not in the catalog — 강모델
        }

        return cardEffects;
    }
}