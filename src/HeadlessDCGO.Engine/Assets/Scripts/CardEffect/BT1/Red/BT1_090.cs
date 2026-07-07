// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_090.cs — an Option (single timing).
//   [Main] Gain 2 memory. At end of turn, lose 2 memory.
// 1:1 mirror of the AS-IS BT1_090 [Main] (OptionSkill): ActivateClass(CanUseCondition = CanTriggerOptionMainEffect,
//   ORDER=-1, ISOPTIONAL=false). ActivateCoroutine: card.Owner.AddMemory(+2) immediately, THEN a nested
//   ActivateClass "Memory -2" registered via CardEffectCommons.AddEffectToPlayer(UntilEachTurnEnd, card,
//   timing: OnEndTurn) — a one-shot -2 that fires at the end of THIS turn only.
// Headless mirror: uniform ActivatedEffect + MemoryGainThenScheduledReversalBody(2) — stages the immediate +2
//   AddMemory mutation, then registers a fire-once TriggeredMemoryEffect(-2, OnEndTurn) via AddEffectToPlayer
//   (DelayedOneShot semantics), so the boost is this-turn-only, not a permanent gain.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class BT1_090 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
                canActivate: null,
                body: new MemoryGainThenScheduledReversalBody(amount: 2),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[Main] Gain 2 memory. At end of turn, lose 2 memory."));
        }

        return cardEffects;
    }
}
