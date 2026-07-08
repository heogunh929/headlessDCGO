// Source: Assets/Scripts/CardEffect/BT2/BT2_026.cs
// Decision: PARTIAL PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, BT2 wave).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_026 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: HasMatchConditionOwnersPermanent inner predicate requires (1) color check via HeadlessEntityId
        // (no documented CardEffectCommons.HasCardColor(card, id, color) or equivalent TopCard color accessor by id)
        // and (2) Tamer-type check via HeadlessEntityId (no documented CardEffectCommons.IsBattleAreaTamer(card, id)).
        // Cannot faithfully express the JammingSelfStaticEffect condition; unconditional registration would expand the guard.
        if (timing == EffectTiming.None)
        {
        }

        return cardEffects;
    }
}