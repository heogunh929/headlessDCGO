// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_035.cs
// Decision: STOP
// Category: CardEffect
// Priority: HIGH
// Migration: Port per-card effect source
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue
// STOP: ActivateClass — 강모델
// STOP: AddMemory — 강모델
// STOP: CanActivateOnDeletion — 강모델
// STOP: CanTriggerOnDeletion — 강모델
// STOP: MemoryForPlayer — 강모델

using System.Collections.Generic;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

public sealed class BT1_035 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: OnDestroyedAnyone — 강모델
        // This card uses ActivateClass and coroutines which are not supported in headless
        // The original card has complex logic that cannot be ported as-is

        return cardEffects;
    }
}