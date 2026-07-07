// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_090.cs
// 1:1 mirror of the original BT1_090 (BT1/Red) — an Option.
//   [Main] Gain 2 memory. At end of turn, lose 2 memory.
//   -> STOP (see below).
// AS-IS (OptionSkill): ActivateClass(CanUseCondition = CanTriggerOptionMainEffect, ORDER=-1, ISOPTIONAL=false).
// ActivateCoroutine: `yield return card.Owner.AddMemory(2, activateClass)` (immediate +2), THEN builds a SECOND
// nested ActivateClass ("Memory -2", CanUseCondition/CanActivateCondition both `true`, ActivateCoroutine =
// `card.Owner.AddMemory(-2, activateClass1)`) and registers it via
// `CardEffectCommons.AddEffectToPlayer(effectDuration: UntilEachTurnEnd, card, cardEffect: activateClass1,
// timing: OnEndTurn)` — i.e. the +2 now is paired with a one-shot -2 that fires at the end of THIS turn only.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_090 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "Gain 2 memory. At end of turn, lose 2 memory." (OptionSkill).
        // Pieces confirmed to exist individually:
        //   - The immediate "+2 memory" half alone is coverable: CardEffectFactory.GainMemoryActivatedEffect
        //     (ActivatedMemoryEffect) is the exact ST2_13 [Main] "Gain N memory" shape.
        //   - The deferred half's underlying facility exists too: CardEffectCommons.AddEffectToPlayer(
        //     effectDuration, card, nestedEffect, timing) is documented (docs/porting/PRIMITIVE-CATALOG.md:191)
        //     as a fire-once player-scope delayed effect — its own worked example is
        //     `TriggeredGainMemoryEffect(card, EffectTiming.OnEndTurn, amount: -2)`, i.e. literally this card's
        //     follow-up.
        // But there is no IEffectBody / factory that composes BOTH inside a single OptionSkill activation:
        // "apply an immediate MemoryBody(+2) mutation, THEN ALSO call AddEffectToPlayer to register the nested
        // one-shot -2 trigger" (mirroring the AS-IS ActivateCoroutine's two-step body). The one body that
        // registers a nested ICardEffect from within Apply() — GrantContinuousBody — calls
        // `EffectRegistry.Register(_continuous.ToBinding(id))` directly with whatever duration ToBinding sets;
        // TriggeredGainMemoryEffect.ToBinding always sets `duration: null` with NO DelayedOneShotKey stamp, so
        // routing the "lose 2 memory" trigger through GrantContinuousBody would register it as a PERMANENT
        // OnEndTurn trigger (firing every future turn end) instead of the AS-IS one-shot (this turn only) —
        // a materially different (and much worse) effect, not a faithful mirror. Only AddEffectToPlayer's
        // specific DelayedOneShotKey + fire-then-clear bookkeeping gives the correct one-shot semantics, and no
        // existing body invokes AddEffectToPlayer.
        // Grepped ActivatedEffect.cs (every IEffectBody impl) and docs/porting/PRIMITIVE-CATALOG.md (Memory*
        // entries + the AddEffectToPlayer entry) twice — confirmed gap, not a naming miss.
        // Registering only the "+2 memory" half (dropping the reversal) would turn a temporary, this-turn-only
        // memory boost into a permanent +2 gain — not a faithful partial mirror (fidelity-over-coverage) — so
        // the whole timing is left unregistered pending a new IEffectBody (e.g. a
        // "MemoryThenScheduleReversalBody" wrapping MemoryBody + AddEffectToPlayer, mirroring how
        // SuspendSelfAndGainMemoryBody already composes a cost step with an effect step) — engine-side primitive
        // development, deferred to the strong-model primitive pass. — 강모델
        // if (timing == EffectTiming.OptionSkill) { ... }

        return cardEffects;
    }
}
