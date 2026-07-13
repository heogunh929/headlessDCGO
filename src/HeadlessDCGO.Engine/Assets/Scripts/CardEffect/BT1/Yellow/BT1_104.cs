// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_104.cs (an Option, single branch)
// AS-IS (OptionSkill only — no [Security] branch in the original):
//   [Main] All of your Digimon gain the following effect for the turn: "[When Attacking] 1 of your
//   opponent's Digimon gets -2000 DP for the turn."
//   CanUseCondition = CanTriggerOptionMainEffect(hashtable, card) [ported: exists, CardEffectCommons.
//     CanTriggerOptionMainEffect]. CanActivateCondition = null (ORDER=-1, ISOPTIONAL=false — mandatory,
//     no once-per-turn cap).
//   ActivateCoroutine:
//     (1) for each of the owner's CURRENT battle-area permanents (PermanentCondition =
//         IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) [ported: exists]) — play a purely
//         cosmetic CreateBuffEffect (VFX only, no state change; correctly out of scope for headless).
//     (2) build an AddSkillClass ("DP -2000", CanUseCondition1 = always true, CardSourceCondition =
//         PermanentCondition(cardSource.PermanentOfThisCard()) && cardSource == the permanent's TopCard —
//         i.e. ANY of the owner's current-and-future battle-area Digimon top cards, no further narrowing)
//         whose GetEffects, when queried at EffectTiming.OnAllyAttack for a matching cardSource, splices a
//         NEW ActivateClass ("DP -2000", ORDER=-1, ISOPTIONAL=false) onto that cardSource:
//           CanUseCondition2 = CardSourceCondition(cardSource) && GManager.instance.attackProcess.
//             AttackingPermanent == cardSource.PermanentOfThisCard() (only fires for the ACTUAL attacker).
//           CanActivateCondition1 = IsExistOnBattleArea(cardSource) && the opponent has >=1 matching
//             Digimon (CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon).
//           ActivateCoroutine1 = SelectPermanentEffect(selectPlayer: cardSource.Owner, mode: Custom,
//             maxCount = Min(1, matching count), canNoSelect:false, canEndNotMax:false) — an INTERACTIVE
//             pick of exactly 1 opponent Digimon — THEN CardEffectCommons.ChangeDigimonDP(selected,
//             changeValue: -2000, effectDuration: UntilEachTurnEnd, activateClass1) [ported: exists,
//             CardEffectCommons.ChangeDigimonDP].
//         This whole AddSkillClass is registered via CardEffectCommons.AddEffectToPlayer(effectDuration:
//         UntilEachTurnEnd, card, cardEffect: addSkillClass, timing: EffectTiming.None) — i.e. the ABILITY
//         GRANT ITSELF expires at the end of the current turn (not permanent), and can fire on EVERY
//         qualifying attack this turn (not a single one-shot).
//
// STOP (genuine compound primitive gap — grepped 2x+ per rule 4, not a per-card shortcut):
//
// Piece 1 — the granted [When Attacking] ability body itself ("interactively select 1 opponent Digimon,
// then apply -2000 DP to it for the turn") needs a ToBinding-registrable (non-ActivatedEffect-flow)
// trigger effect, because CardEffectFactory.GrantTriggeredEffectToScopedSet(card, scopePlayer,
// nestedTriggeredEffect) — the existing (PRIM-P0 AddSkill) mirror of AS-IS AddSkillClass-splices-a-
// TRIGGERED-effect-onto-a-live-set (docs/audit/porting_translation_cheatsheet.md:92,
// tests/PRIM-P0.TriggerGrantSetSplice.Tests) — calls `nestedTriggeredEffect.ToBinding(...)` directly (see
// PlayerScopeTriggerGrantEffect.ToBinding, CardPortingFramework.cs:1685), which requires the nested effect
// to be one of the ICardEffect+IHeadlessCardEffect "Triggered*" family (TriggeredMemoryEffect /
// TriggeredGainMemoryEffect / TriggeredSetMemoryEffect / TriggeredUnsuspendSelfEffect /
// TriggeredSelfDpBuffEffect / RecoverTriggerEffect — all grepped, CardPortingFramework.cs) resolved
// synchronously through CardEffectSchedulerResolver, NOT the interactive uniform ActivatedEffect (whose
// SelectBody / IEffectBody catalog in ActivatedEffect.cs supports the interactive select this card needs,
// but whose own ToBinding explicitly `throw new NotSupportedException(...)` — "resolved via the activation
// flow, not registered" — so an ActivatedEffect/SelectBody CANNOT be used as the nested effect here). Every
// existing Triggered* class in that ToBinding-compatible family is a FIXED, non-interactive mutation
// (memory/DP/unsuspend on a statically-known target); none supports "interactively select an opponent
// permanent, then debuff THAT selection". No factory composes "trigger-grant splice + interactive select +
// DP mutation on the selection".
//
// Piece 2 — even setting Piece 1 aside, the GRANT itself must expire at end of turn (AS-IS
// AddEffectToPlayer(effectDuration: UntilEachTurnEnd, ...)), but the headless AddEffectToPlayer mirror
// (CardPortingFramework.cs:8498) is documented and implemented as a ONE-SHOT "fires once at `timing` then
// is cleared" primitive (`_ = effectDuration; _ = timing;` then unconditionally
// `[AutoProcessingTriggerCollector.DelayedOneShotKey] = true`) — it is NOT a general "register this
// continuous/repeatable trigger-grant with an expiring duration" mechanism (BT1_090's STOP note documents
// this same one-shot-vs-continuous mismatch for a different card). GrantContinuousBody (ActivatedEffect.cs)
// is the one body that registers an arbitrary nested ICardEffect from Apply(), but it always registers with
// `duration: null` (permanent) via `_continuous.ToBinding(id)`, and every Triggered* class's own ToBinding
// also hardcodes `duration: null` — so even a permanently-registered grant could not be made to
// self-expire at turn end through any existing path. Registering this as a PERMANENT grant instead of a
// this-turn-only one would materially strengthen the card (fidelity-over-coverage forbids this).
//
// No composed primitive exists for "temporary (until-turn-end), repeatable, live-set trigger-grant whose
// body is an interactive select + target-locked debuff." Per rule 4 this is a primitive gap requiring new
// engine-layer IEffectBody/Triggered* work, out of scope for a single-card porting pass. No cardEffects
// registered (OptionSkill only timing the AS-IS declares). — 강모델
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_104 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [Main] "All of your Digimon gain the following effect for the turn: '[When Attacking] 1 of
        // your opponent's Digimon gets -2000 DP for the turn.'" (OptionSkill) — needs a "temporary
        // (until-turn-end), live-set trigger-grant with an interactive select + DP-debuff body" that does
        // not exist yet (see file header, both pieces).
        // if (timing == EffectTiming.OptionSkill) { ... }

        return cardEffects;
    }
}
