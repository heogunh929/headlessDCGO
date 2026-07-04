using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// GENERATED FROM porting/data/ir/BT1.Blue/BT1_031.json — DO NOT EDIT (pipeline-v3 codegen).
public sealed class BT1_031 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }
        return cardEffects;
    }
}
