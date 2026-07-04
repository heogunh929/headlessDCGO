using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_030 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            // STOP: ActivateClass — 강모델
            // STOP: AddMemoryTriggerEffect — 강모델
            // STOP: CanActivateOnDeletion — 강모델
            // STOP: CanTriggerOnDeletion — 강모델
            // STOP: MemoryForPlayer — 강모델
        }

        return cardEffects;
    }
}
