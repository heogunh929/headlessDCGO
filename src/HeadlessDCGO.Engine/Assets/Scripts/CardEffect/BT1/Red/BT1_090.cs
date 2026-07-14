// R6-P CUTOVER STOP (design item RD-R6-02 member gap RESOLVED; residual = R6P-EOT-PLAYER-EFFECTLIST): kept in
// old-model ActivatedEffect. The AS-IS ActivateCoroutine registers a NESTED new-model ActivateClass ("Memory -2") at
// player scope via CardEffectCommons.AddEffectToPlayer(UntilEachTurnEnd, ..., OnEndTurn). The player MEMBER gap is now
// closed: the mirror Player has the AS-IS effect-list buckets (Player.UntilEachTurnEndEffects, RD-R6-02) and a 5-param
// AddEffectToPlayer overload that STORES a new-model ICardEffect into the bucket with no ToBinding. BUT the live
// OnEndTurn window (WindowResolverWiring.CollectUnifiedSeed / the activated bridge) collects from the registry + card
// zone scan and does NOT yet enumerate player.EffectList — so a bucket-stored OnEndTurn reactor would be INERT (the
// -2 reversal never fires). Wiring player.EffectList into the OnEndTurn collect is R3 trigger-window rehousing
// (design item R6P-EOT-PLAYER-EFFECTLIST). Until then this card keeps its WORKING ActivatedEffect +
// MemoryGainThenScheduledReversalBody (registers a live DelayedOneShot binding the window DOES collect); re-porting to
// AddEffectToPlayer list-storage now would REGRESS the EoT loss to a no-op.
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
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
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
