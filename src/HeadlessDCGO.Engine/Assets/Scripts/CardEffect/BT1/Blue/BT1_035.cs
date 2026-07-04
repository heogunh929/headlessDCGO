using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Effects;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// GENERATED FROM porting/data/ir/BT1.Blue/BT1_035.json — DO NOT EDIT (pipeline-v3 codegen).
public sealed class BT1_035 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            cardEffects.Add(CardEffectFactory.AddMemoryTriggerEffect(
                timing: EffectTiming.OnDestroyedAnyone,
                amount: 2,
                isInheritedEffect: false,
                card: card,
                condition: null,
                description: "[On Deletion] Gain 2 memory."));
        }
        return cardEffects;
    }
}
